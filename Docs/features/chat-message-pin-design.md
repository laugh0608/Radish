# F4-E 聊天消息置顶

> **状态**：F4-E-A / B 已完成；下一批为 F4-E-C Pencil 与正式 Web
>
> **复核日期**：2026-07-19
>
> **正式产品入口**：`/messages`；WebOS `/desktop` 只复用同一 Chat App

## 摘要

- 消息置顶用于在会话顶部保留少量长期重要消息，服务于公告、规则、协作上下文和一对一约定；它不是收藏、通知、未读或消息排序。
- 旧 Phase 2 草案声称 `Channel.PinnedMessageId` 与 `ChannelMessage.IsPinned / PinnedAt / PinnedBy` 已预留，但真实实体、迁移、VO、API 和 Hub 均不存在这些字段，不能直接按旧草案启用。
- F4-E 采用独立 `ChatMessagePin` 与频道级 `PinRevision`，每频道最多 20 条活跃置顶；不把状态重复写到消息，也不使用会静默覆盖历史上下文的单条指针。
- 写入采用明确目标状态 `IsPinned=true / false`。只有实际变化才在同一 Chat 事务内递增 `PinRevision`；Hub 广播带 revision 的完整置顶快照，断线后由 HTTP 重读追平。
- 读取复用 `CanView`；写入使用独立 `CanPinMessages`。Public / Announcement 由 System / Admin 或显式 Moderator / Owner 管理，普通 Private 只允许本频道 Moderator / Owner，Accepted Direct 允许双方管理；其他 Direct 状态只读。
- 消息撤回时在同一事务内软删除对应活跃置顶并递增频道 revision。置顶不改变正文、搜索、最后消息、未读、排序或通知。

## 一、用户价值与正式路径

置顶解决“重要信息已经发过，但很快被新消息淹没”的问题：

1. 有治理权限的用户可以从消息操作区置顶或取消置顶，不需要复制正文或重发系统消息。
2. 会话顶部显示最近一条置顶与总数；点击打开完整置顶列表，再通过现有消息窗口协议跳到原消息上下文。
3. 最多保留 20 条活跃置顶，避免单条覆盖导致旧规则无提示消失，也避免无界增长。
4. 多账号、多标签和断线恢复始终以服务端完整快照为准，不维护本地增量计数。
5. 消息撤回、频道失效或权限变化后不泄露旧正文，不保留失效操作入口。

PC 使用频道标题下方的紧凑置顶条和侧边 / 浮层列表；mobile 使用紧凑置顶条与 Bottom Sheet。无置顶时完全收起，不占用消息阅读空间。

## 二、现状审计与裁决

### 2.1 可复用资产

- Chat 已有 `Channel / ChannelMember / ChannelMessage / DirectConversation`、统一 `ChatChannelAccessService`、SQLite / PostgreSQL schema ledger、消息撤回事务和 LongId 字符串契约。
- `GetMessageWindow` 与 `useChatMessageNavigation` 已支持跨分页加载、滚动和高亮目标消息，可直接承接置顶跳转。
- `ChatHub` 已有频道组、多标签连接所有权与重连重新订阅；Reaction 已验证“HTTP 权威状态 + revision 完整 Hub 快照”的并发和重连模式。
- `ChannelMember.Role` 已有 `Member / Moderator / Owner`，Announcement 写权限也已按管理角色与 System / Admin 派生。

### 2.2 真实缺口

- 当前 `Channel` 没有置顶指针或 revision，`ChannelMessage` 没有置顶字段，也没有任何置顶 migration。
- 当前 `ChannelVo / ChannelMessageVo`、`IChatService`、Controller、Hub 与 `@radish/http` 没有置顶契约。
- Public 频道普通用户进入时只会成为 Member；旧文档没有解释谁能授予 Moderator，也没有处理普通 Private 的管理员穿透边界。
- 旧草案只允许单条置顶并自动覆盖，缺少容量、并发、撤回、Direct 状态、失权恢复和完整快照语义。

### 2.3 长期裁决

F4-E 不向 `ChannelMessage` 增加 `IsPinned` 等重复字段，也不向 `Channel` 增加单条 `PinnedMessageId`。活跃置顶集合以 `ChatMessagePin` 为唯一真值，频道只保存集合 revision。

选择多条置顶而不是单条覆盖，原因是公告、群组规则与协作上下文本来可能并存；20 条硬上限仍能保证页面和查询边界稳定。个人私有收藏是另一种仅本人可见的数据模型，不混入共享置顶。

## 三、数据模型与迁移

### 3.1 `ChatMessagePin`

位于 Chat 库：

| 字段 | 说明 |
| --- | --- |
| `Id` | Snowflake 主键 |
| `TenantId / ChannelId / MessageId` | 置顶与消息归属；服务端从消息反查并校验，不接受客户端推导租户 |
| `PinnedByUserId / PinnedByName / PinnedAt` | 最近一次置顶操作者与 UTC 时间 |
| `IsDeleted / DeletedAt / DeletedBy / DeletedByUserId` | 取消或撤回时软删除，保留最近一次状态审计 |
| `CreateTime / CreateBy / CreateId / Modify*` | 通用审计字段 |

唯一约束：`(TenantId, ChannelId, MessageId)`。同一消息重新置顶时恢复原行并刷新最近置顶元数据，不新增重复活跃记录。

索引：

- `(TenantId, ChannelId, IsDeleted, PinnedAt DESC)`：权威快照与容量检查；
- `(TenantId, MessageId, IsDeleted)`：撤回联动；
- 唯一 `(TenantId, ChannelId, MessageId)`：并发恢复和重复请求收敛。

### 3.2 `Channel.PinRevision`

`Channel.PinRevision` 为非负 `long`，历史频道迁移时初始化为 0。任何实际新增、恢复、取消或撤回活跃置顶都在同一事务内递增；无变化请求不递增。

revision 属于整个频道置顶集合，不属于单条消息。客户端按 `channelId + revision` 替换完整快照，避免并发和乱序导致已取消置顶重新出现。

### 3.3 Migration

F4-E-B 增加下一条 Chat ledger migration，负责：

- 创建 `ChatMessagePin`、索引和唯一约束；
- 为历史 `Channel` 增加默认值为 0 的 `PinRevision`；
- doctor 报告历史频道数量但不修改消息正文、搜索、未读或最后消息；
- verify 检查表、列、索引、非负 revision 与孤儿活跃置顶；
- 覆盖 SQLite / PostgreSQL 空库、历史库、重入、checksum 和备份恢复。

迁移的 Diagnose / Verify 只能投影本迁移拥有的字段，不得再次耦合未来 schema。

## 四、权限与状态

统一访问结果增加 `CanPinMessages`，不能从页面角色名称自行推断：

| 会话状态 | 读取置顶 | 管理置顶 |
| --- | --- | --- |
| Public | `CanView` | System / Admin 或有效 Moderator / Owner |
| Announcement | `CanView` | System / Admin 或有效 Moderator / Owner |
| 普通 Private | 有效成员 | 仅本频道 Moderator / Owner；System / Admin 若不是成员不得穿透 |
| Direct Accepted | 双方可读 | 未阻断、对端可用时双方均可管理 |
| Direct Pending / Declined / Blocked | 按既有历史读取策略 | 不允许 |
| 频道禁用 / 删除、跨租户、消息撤回 | 不可用 | 不允许 |

Direct 双方管理的是两人共享会话置顶，不是个人收藏。任一方的归档是账号本地列表状态，不改变已接受会话的共享置顶权限；阻断和对端失效立即停止写入。

System / Admin 对 Public / Announcement 具备社区治理权限，但不能借置顶 API 探测或管理自己不属于的普通 Private / Direct。

## 五、写入、并发与容量

`SetPin` 使用目标状态：

1. 校验 `channelId / messageId`、频道访问、`CanPinMessages`、消息归属且未撤回。
2. 在频道级写锁 / 行锁下读取现有 pin 行与活跃数量。
3. `IsPinned=true` 时新增或恢复；已有活跃行则返回无变化。只有新增 / 恢复才检查 20 条上限。
4. `IsPinned=false` 时软删除活跃行；不存在或已删除则返回无变化。
5. 实际变化时原子递增 `Channel.PinRevision`，提交后读取完整权威快照。
6. Controller 只在实际变化时广播 `MessagePinsChanged`。

目标状态天然支持安全重试，不使用 Toggle，也不需要为无副作用回放建立有限期 operation ledger。SQLite 使用进程级短写锁与事务；PostgreSQL 锁定频道行，并依赖唯一约束处理竞争。达到 20 条时返回稳定容量错误，不自动取消最旧置顶。

## 六、撤回与失效恢复

- 消息撤回事务同时软删除对应活跃 `ChatMessagePin` 和全部活跃 Reaction；若置顶发生变化则递增 `Channel.PinRevision`。
- 撤回仍先广播 `MessageRecalled`；若移除了置顶，再广播最新 `MessagePinsChanged` 完整快照。
- 频道删除 / 禁用或成员失权依赖统一 ACL 立即停止读取与写入，不复制旧置顶正文作为 fallback。
- 置顶跳转时若消息刚被撤回或访问发生变化，复用权威消息窗口的目标不可用状态，并触发一次置顶快照刷新。
- 重新启用频道或恢复成员关系不会绕过当前权限；历史未撤回置顶仍由状态表保留并重新可见。

## 七、API、VO 与 Hub 契约

### 7.1 读取状态

`GET /api/v1/ChannelMessagePin/GetState?channelId=...`

返回 `ChatMessagePinStateVo`：

- `VoChannelId`
- `VoRevision`
- `VoItems: ChatMessagePinVo[]`

`ChatMessagePinVo`：

- `VoId / VoMessageId`
- `VoMessage: ChannelMessageVo`
- `VoPinnedByUserId / VoPinnedByName / VoPinnedAt`

items 按 `PinnedAt DESC, Id DESC` 排序，最多 20 条。无置顶也返回 revision 与空集合。

### 7.2 设置目标状态

`POST /api/v1/ChannelMessagePin/Set`

请求：`ChannelId / MessageId / IsPinned`。

返回 `ChatMessagePinMutationVo`：

- `VoState`
- `VoChanged`

### 7.3 稳定错误

| HTTP | Code / MessageKey | 场景 |
| --- | --- | --- |
| 400 | `Chat.PinInvalidArgument` | ID 或请求无效 |
| 403 | `Chat.PinNotAllowed` | 可读但无治理权限或 Direct 状态不允许 |
| 404 | `Chat.PinTargetUnavailable` | 频道、消息、租户或 ACL 不可用 |
| 409 | `Chat.PinConcurrentConflict` | 并发冲突重试后仍失败 |
| 400 | `Chat.PinLimitExceeded` | 频道已有 20 条活跃置顶 |

所有错误保留真实 HTTP 状态、稳定 `Code / MessageKey` 和 TraceId，不回传 SQL 或异常原文。

### 7.4 Hub

`MessagePinsChanged` payload 等于完整 `ChatMessagePinStateVo`，发送到 `channel:{tenantId}:{channelId}`。客户端只接受更高 revision；HTTP 可在相同 revision 下补齐初始状态。重连和重新加入频道后重读 HTTP，不维护增量事件回放。

## 八、正式 Web 与交互

- `ChannelVo` 增加 `VoCanPinMessages`，消息操作区只在该能力为 true 且消息未撤回 / 非临时消息时显示置顶动作。
- 频道顶部 `PinnedMessageBar` 展示最近置顶的发送者、内容摘要与总数；点击最近项直接定位，点击总数打开完整列表。
- PC 使用紧凑面板或抽屉，mobile 使用 Bottom Sheet；完整列表逐项展示置顶人、时间、消息摘要、定位和受权取消操作。
- 置顶文本只取 `ChannelMessageVo` 的当前可见内容；图片使用明确类型摘要，不把附件授权 URL 持久化到 Store。
- Store 按频道保存 state；账号 reset、频道失权和登出清空。Hub / HTTP 按 revision 合并，多标签复用相同权威语义。
- 键盘可聚焦，Enter / Space 可操作，Esc 关闭列表；新增 / 取消成功与失败使用礼貌播报和稳定双语文案。
- 四主题只使用语义 token。长发送者、长正文、20 条列表与 mobile 安全区域必须纳入 Pencil 和实现复核。

## 九、不污染边界

置顶不产生：

- 新 `ChannelMessage`、系统消息或通知中心事件；
- 未读增长、mention、最后消息或频道排序变化；
- `SearchText` 修改、搜索加权或搜索结果副本；
- 邮件、Web Push、移动系统通知或营销触达。

置顶人和时间属于共享会话治理元数据，不进入消息正文、引用快照或公开页面。

## 十、实施批次

### F4-E-A：现状审计与专题设计（已完成）

- 交叉审计真实实体、migration、ACL、Direct 状态、Hub、消息窗口、前端 Store 和旧 Phase 2 文档。
- 固定多条置顶、独立状态表、频道 revision、权限矩阵、目标状态、撤回一致性、UI 边界和 A-D 验收口径。

### F4-E-B：服务端权威契约（已完成）

- 新增实体、Chat ledger migration、Repository 原子设置 / 容量 / revision 与撤回联动。
- 新增访问能力、Service、Controller、Hub payload、`@radish/http` 契约、稳定双语错误和 LongId 测试。
- 覆盖 SQLite / PostgreSQL、ACL、重复目标状态、并发、20 条上限和撤回定向回归。

### F4-E-C：Pencil 与正式 Web

- 先更新 PC / mobile Pencil，再实现置顶条、完整列表、消息动作、定位、加载 / 空态 / 错误和 mobile Bottom Sheet。
- 接入 Store revision、Hub、重连追平、中英文、键盘、焦点、长内容和四主题。

### F4-E-D：成组验收

- 使用至少三个普通账号覆盖 Public、Announcement、普通 Private、Accepted / Pending / Declined / Blocked Direct、撤回与权限变化。
- 执行 `zh / en × PC / mobile`、多标签、真实离线重连、并发目标状态、20 条上限、定位和 WebOS 矩阵。
- 清理临时 pin、账号、频道、消息、通知、凭据和备份，检查六库完整性与 migration verify。

## 十一、验证矩阵

- Migration：SQLite / PostgreSQL 空库、历史库、重入、checksum、索引、默认 revision、孤儿诊断和备份恢复。
- Repository：新增、恢复、取消、无变化、并发、revision 原子增长、20 条上限和撤回联动。
- ACL：Public / Announcement 管理角色、普通 Private 成员角色、Direct 全状态、跨租户、失权和管理员不穿透。
- API：LongId 字符串、稳定状态与错误、目标不属于频道、撤回目标。
- Hub / Client：乱序 revision、多账号、多标签、重连追平、账号切换、撤回移除、PC / mobile 与 WebOS。
- 常规门禁：定向测试、解决方案构建、前端 type-check / lint / test / build、Baseline Quick、repo hygiene 和 `git diff --check`。

## 十二、停止线与完成标准

本专题不做个人收藏、置顶通知、Forum 置顶、逐条已读、消息编辑 / 转发 / 线程、跨频道置顶聚合、Console 私聊浏览、Flutter / Tauri 专属页面或外部缓存计数。

完成必须同时满足：

1. 状态只存在于 Chat 权威表，不通过重复 message flag 或单条频道指针制造分叉。
2. 所有读写复用 Chat ACL，System / Admin 不穿透普通 Private / Direct。
3. 目标状态、20 条上限和 revision 在 SQLite / PostgreSQL 下幂等且并发正确。
4. 撤回、失权、跨租户和账号切换不保留可见旧正文或错误操作能力。
5. 正式 Web / WebOS 的 PC / mobile、中英文、键盘、多标签和重连矩阵通过。
6. 置顶不污染未读、最后消息、搜索、通知、正文或频道排序。
7. 临时数据清理、六库完整性、migration verify、定向验证和文档留痕全部通过。
