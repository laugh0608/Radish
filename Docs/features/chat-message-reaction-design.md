# F4-D 聊天消息 Reaction

> **状态**：F4-D-A / B / C / D 已完成，专题关闭
>
> **复核日期**：2026-07-19
>
> **正式产品入口**：`/messages`；WebOS `/desktop` 复用同一 Chat App

## 摘要

- 聊天 Reaction 是会话内的轻量回应，不是论坛点赞、评论、通知或新消息；首批不产生通知中心事件、不推进未读游标。
- 不把现有 Main 库 `Reaction` 直接扩成 `TargetType=ChatMessage`。Chat 消息位于 Chat 库且受成员、私聊状态、阻断、撤回和租户边界约束，Reaction 必须与消息共库并通过专属 Service / Repository 维护。
- 新增 `ChatMessageReaction`、`ChatMessageReactionOperation` 与 `ChannelMessage.ReactionRevision`。客户端提交目标状态和唯一 operation ID，重复请求只回放，不使用会因重试而反转的 Toggle。
- 读取要求 `CanView`；写入要求独立 `CanReact`。公开 / 公告频道可回应，普通私有频道仅成员可回应；一对一私聊只有已接受、未阻断且对端可用时可回应。
- Hub 广播带 revision 的完整聚合快照；客户端只接受更高 revision，避免并发和乱序事件把新状态覆盖成旧状态。广播中的 `VoIsReacted` 属于发起者视角，其他客户端只消费聚合字段并保留本账号已知选择；本人 HTTP 回包可在相同 revision 下校正选择态。
- 复用现有 Unicode / sticker 资源、`ReactionSummaryVo` 与共享 Reaction 展示组件，但不复用公开论坛的匿名 API、目标校验和通知语义。

## 一、用户价值与路径

消息 Reaction 解决“表达收到、赞同、感谢或情绪回应，但不值得再发一条消息”的高频需求：

1. 用户可以直接点击消息下方已有回应，设置或取消自己的同类回应。
2. 用户可以从更多入口选择 Unicode emoji 或已启用 sticker；同一用户可同时保留多种回应，沿用每目标最多 10 种的上限。
3. 所有可见端实时收到同一条消息的完整聚合状态，不依赖本地计数加减。
4. 消息撤回后不展示也不能新增回应；历史回应随消息一起失去读取入口。
5. Reaction 不改变频道最后消息、未读、搜索文本、消息排序或通知角标。

PC 在消息操作区提供回应入口并在气泡下方展示聚合；mobile 使用轻触已有回应与明确“回应”按钮，不依赖 hover 或长按。自己发送的消息也允许回应，不引入特殊例外。

## 二、现状审计与裁决

### 2.1 可复用资产

- Main 库 `Reaction` 已有 Unicode / sticker 规范化、软删除、聚合 VO、10 种上限和共享 `ReactionBar`。
- Chat 已有 `ChannelMessage`、统一 `ChatChannelAccessService`、消息撤回、`GetHistory / GetMessageWindow`、SignalR 频道组、LongId 字符串和 WebOS 共用组件。
- Sticker / StickerGroup 由 Main 库管理，附件 URL 通过 `IAttachmentUrlResolver` 派生；Chat 只保存必要的 sticker attachment 快照。

### 2.2 不能直接复用的边界

现有 `ReactionController` 的读取允许匿名，目标只支持公开 Post / Comment，实体和事务位于 Main 库，也没有 Chat 成员 ACL、私聊状态或撤回检查。直接增加 `ChatMessage` target 会造成：

- 私聊消息 ID 可被匿名或非成员探测；
- Main Reaction 与 Chat 消息撤回无法同库收口；
- 论坛 Toggle 在认证刷新或网络重试时可能把期望状态反转；
- 通用 Reaction 通知与聊天轻回应语义混淆；
- Hub 乱序时缺少版本，客户端可能回退到旧计数。

因此 F4-D 只复用展示契约和表情资源，不复用 Main Reaction 的持久化、Controller 或目标校验。

## 三、数据模型

### 3.1 `ChatMessageReaction`

位于 Chat 库：

| 字段 | 说明 |
| --- | --- |
| `Id` | Snowflake 主键 |
| `TenantId / ChannelId / MessageId` | 消息与频道归属；服务端不接受客户端推导租户 |
| `UserId / UserName` | 回应者及展示快照 |
| `EmojiType / EmojiValue` | `unicode` 或 `sticker`，沿用现有规范 |
| `StickerAttachmentId` | sticker 当前附件快照；Unicode 为空 |
| `IsDeleted / DeletedAt / DeletedBy` | 取消回应使用软删除 |
| 审计字段 | 创建和修改人、时间、ID |

唯一约束：`(TenantId, MessageId, UserId, EmojiType, EmojiValue)`。查询索引按 `(TenantId, MessageId, IsDeleted)` 和 `(TenantId, ChannelId, MessageId)` 建立。

### 3.2 `ChatMessageReactionOperation`

位于 Chat 库，记录有限期幂等事实：

- `(TenantId, UserId, ClientOperationId)` 唯一；
- 保存 `ChannelId / MessageId / EmojiType / EmojiValue / IsActive` 请求指纹；
- 保存 `ResultRevision` 和创建时间；
- 同 operation ID 同指纹只回放，不再写入；同 ID 异指纹返回 `409 Chat.ReactionIdempotencyConflict`。

operation ID 长度 `8-100`，由客户端每次明确动作生成。记录保留 30 天；清理由受控小批次维护入口承担，不在普通读取请求中全表扫描。

### 3.3 消息 revision

`ChannelMessage.ReactionRevision` 默认为 0。Reaction 实际状态发生变化时在同一 Chat 事务内原子加一；无变化请求和幂等回放不增加 revision。

消息正文、搜索文本、未读、最后消息和创建时间均不因 Reaction 修改。

## 四、权限与状态

### 4.1 读取

Reaction 汇总读取必须登录，并且目标消息：

- 属于请求 `channelId`；
- 未撤回；
- 当前用户对频道 `CanView=true`；
- 租户与公共租户可见性符合现有 Chat 规则。

批量读取最多 100 条消息；任一目标不属于频道、已撤回或不可读时，整批返回目标不可用，不部分泄露存在性。

### 4.2 写入

增加统一派生能力 `CanReact`：

- Public：可读即可回应；
- Announcement：可读即可回应，不要求具备发公告权限；
- 普通 Private：有效成员可回应；
- Direct：仅 `Accepted`、未阻断且对端可用时可回应；Pending / Declined / Blocked 均不可写，即使历史仍可读。

管理员不能穿透私聊成员边界。消息作者、频道管理员和普通成员使用同一 Reaction 规则，不增加后台代操作。

## 五、写入、并发与撤回

`SetReaction` 采用目标状态：

1. 在事务内验证消息仍存在且未撤回。
2. 检查 operation ledger；命中同指纹则回放当前权威聚合。
3. 对 `(message, user, emoji)` 设置 `IsActive=true/false`，不存在则新增、已删除则恢复、已存在则软删除。
4. 只有实际变化才原子递增 `ReactionRevision`。
5. 写入 operation 事实，提交后查询完整汇总。
6. Controller 仅在实际变化时向 `channel:{tenantId}:{channelId}` 广播 `MessageReactionsChanged`。

SQLite 使用进程级短写锁配合事务；PostgreSQL 依赖唯一约束、行级更新和事务。唯一冲突转换为稳定 409，不吞异常或重复 Toggle。

消息撤回应在 Chat 库事务内将消息标记撤回、清空 `SearchText`，并软删除该消息全部活跃 Reaction。撤回广播继续以 `MessageRecalled` 为主，客户端收到后同步移除 Reaction；不额外制造逐条通知。

## 六、API 与实时契约

### 6.1 批量读取

`POST /api/v1/ChannelMessageReaction/GetStates`

请求：`ChannelId`、`MessageIds`（1-100）。

返回 `ChatMessageReactionStateVo[]`：

- `VoMessageId`
- `VoRevision`
- `VoItems: ReactionSummaryVo[]`

无回应的可见消息仍返回 revision 和空 items，便于前端建立完整状态。

### 6.2 设置状态

`POST /api/v1/ChannelMessageReaction/Set`

请求：`ChannelId / MessageId / EmojiType / EmojiValue / IsActive / ClientOperationId`。

返回 `ChatMessageReactionMutationVo`：

- `VoState`
- `VoChanged`
- `VoReplayed`

### 6.3 稳定错误

| HTTP | Code / MessageKey | 场景 |
| --- | --- | --- |
| 400 | `Chat.ReactionInvalidArgument` | ID、emoji、operation ID 或批量大小无效 |
| 403 | `Chat.ReactionNotAllowed` | 可读但当前状态不允许回应 |
| 404 | `Chat.ReactionTargetUnavailable` | 频道、消息、租户或 ACL 不可用 |
| 409 | `Chat.ReactionIdempotencyConflict` | operation ID 异指纹复用 |
| 409 | `Chat.ReactionConcurrentConflict` | 唯一 / 并发冲突重试后仍失败 |
| 400 | `Chat.ReactionLimitExceeded` | 单用户在同消息超过 10 种活跃回应 |
| 404 | `Chat.ReactionStickerUnavailable` | sticker 不存在、禁用或不可用于当前租户 |

错误使用真实 HTTP 状态、稳定 Code / MessageKey 和 TraceId，不把 SQL 或异常原文返回页面。

### 6.4 Hub

`MessageReactionsChanged` 载荷就是完整 `ChatMessageReactionStateVo`。客户端以 `VoMessageId + VoRevision` 合并：新 revision 替换，旧或相等 revision 忽略；重连后由当前消息批量读取追平，不维护增量计数协议。

## 七、前端与交互

- `@radish/http` 提供专用请求 / VO 契约；`radish.client` 统一 API 层生成 operation ID。
- Chat Store 按消息 ID 保存 reaction state，不把它混入搜索结果、草稿或跨账号持久化；账号 reset 清空。
- 历史和消息窗口加载后按当前可见 message IDs 批量补状态；新增消息以 revision 0 开始，Hub / 后续读取更新。
- 复用共享 `ReactionBar` 的展示、emoji/sticker 选择与可访问性能力；Chat 包装层负责 ACL disabled、紧凑布局、错误文案和 operation ID。
- PC / mobile 都有明确按钮；键盘可聚焦、Enter / Space 可操作、picker 可 Esc 关闭，状态使用礼貌播报。
- 撤回消息不渲染 Reaction；发送失败的本地临时消息和引用快照不允许回应。

## 八、通知、搜索与多端边界

- 首批 Reaction 不产生 `NotificationType`、Message inbox、Toast 或系统通知；Reaction 是当前会话内实时反馈，避免低价值复访噪声。
- Reaction 不进入 `SearchText`，不影响搜索摘要、cursor 或命中排序。
- `/messages` 是正式入口；WebOS 复用同组件、API 和 Hub。Flutter、Tauri、PWA 与移动系统通知不扩展。
- Console 不提供跨用户 Reaction 浏览或代操作；举报和治理仍只处理原消息。

## 九、实施批次

### F4-D-A：现状审计与专题设计（已完成）

- 交叉审计通用 Reaction、Chat 数据 / ACL / Hub、通知、共享 UI、SQLite / PostgreSQL 和现有文档。
- 固定专用 Chat 持久化、目标状态幂等、revision、权限、通知停止线和 A-D 验收口径。

### F4-D-B：服务端权威契约（已完成）

- 新增实体、Chat ledger migration、Repository 原子写入与 operation ledger。
- 新增 Service、API、Hub payload、`@radish/http` 契约和稳定双语错误。
- 补 SQLite / PostgreSQL migration、ACL、幂等、并发、撤回和 LongId 定向测试。

### F4-D-C：Pencil 与正式 Web（已完成）

- 已更新 PC `P13E` 与 mobile `P27E` Pencil，覆盖消息气泡 Reaction、显式回应入口、Unicode / sticker picker、聚合与 revision 状态。
- 正式 `/messages` 与 WebOS 共用消息气泡 Reaction、picker、Store revision 合并和重连追平；撤回清理、只读权限、加载 / 错误、中英文、键盘、焦点、长聚合与四主题 token 已接入。

### F4-D-D：成组验收（已完成）

- 已使用三个普通账号覆盖公开、公告、普通私有、已接受 Direct、待处理、拒绝、阻断和撤回状态。
- `zh / en × PC / mobile`、多标签、真实离线重连、并发目标状态、10 种上限、Unicode / sticker 和 WebOS 矩阵通过。
- 临时 Reaction、operation、账号、消息、通知、凭据和备份均已清理，六库完整性与 migration verify 通过；详细证据见 [F4-D-D 成组验收记录](/records/f4-d-d-chat-message-reaction-stage-acceptance-2026-07-19)。

## 十、验证矩阵

- Migration：SQLite / PostgreSQL 空库、历史库、重入、checksum、索引、默认 revision 和备份恢复。
- Repository：新增、恢复、取消、无变化、同 operation 回放、异指纹冲突、revision 原子增长和 10 种上限。
- ACL：公开、公告、普通私有成员 / 非成员、Direct accepted / pending / declined / blocked、跨租户、撤回。
- Emoji：Unicode、组合 emoji、sticker、禁用资源、非法格式、附件快照。
- API：LongId 字符串、批量 1 / 100 / 101、稳定状态与错误。
- Hub / Client：乱序 revision、多标签、重连追平、账号切换、撤回移除、PC / mobile 与 WebOS。
- 常规门禁：定向测试、解决方案构建、前端 type-check / lint / test / build、Baseline Quick、repo hygiene 和 `git diff --check`。

## 十一、停止线与风险

本专题不做：

- 论坛 Reaction 重构、点赞模型合并或 Main / Chat 跨库统一表；
- Reaction 用户名单、排行榜、热度推荐、搜索加权或行为分析；
- Reaction 通知、邮件、Web Push、移动系统通知或营销触达；
- 消息置顶、逐条阅读回执、编辑、转发、线程、阅后即焚；
- Flutter / Tauri 专属页面或 Console 私聊浏览；
- 外部缓存计数、Redis counter 或大而全事件平台。

剩余风险：

- Chat 与 Main sticker 资源是跨库只读校验，资源后续禁用时历史 Reaction 仍保留 attachment 快照但不得再新增；缩略图失效应降级为文本标识。
- operation ledger 会增长，必须按 30 天保留期受控清理并保留迁移 / 清理测试；不能在普通请求中无界清理。
- Hub 只保证实时体验，不是持久真值；断线和多实例仍以 HTTP 批量读取和 revision 追平。

## 十二、完成标准

1. Chat Reaction 不通过匿名通用 Reaction API 暴露私聊存在性。
2. 所有读写复用 Chat ACL，Direct 状态不能借 Reaction 绕过阻断或请求边界。
3. 目标状态、operation ledger 和 revision 在 SQLite / PostgreSQL 下幂等且并发正确。
4. 撤回、失效、跨租户和账号切换不保留可见 Reaction 或旧状态。
5. 正式 Web / WebOS 的 PC / mobile、中英文、键盘、主题、多标签和重连矩阵通过。
6. Reaction 不污染未读、最后消息、搜索、通知或正式消息正文。
7. 临时数据清理、数据库完整性、定向验证和文档留痕全部通过。

2026-07-19 的 F4-D-D 已满足以上七项条件，F4-D 正式关闭。下一顺位进入 F4-E-A 聊天消息置顶的现状审计与专题设计；逐条已读和移动系统通知继续分别后置，主动生产证据采集保持最终收尾冻结。
