# F4-R R1-W01 消息工作区设计前代码事实与能力覆盖门禁

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：只读审计完成；审计发现的举报安全 / LongId、发送重试幂等和历史错误契约阻断已由同日[能力门禁修复](/records/f4-r-r1-w01-messages-capability-gate-implementation-2026-08-08)闭合，撤回能力证据仍需另行裁决
>
> 范围：正式 Web `/messages`、WebOS `/desktop` Chat 复用面、HTTP / SignalR、权限与状态、PC / mobile 结构；未修改 `.pen` 或运行时代码，未启动服务，也不改变 `R1-A01` 第一顺位

## 1. 结论

- 正式 `/messages` 是唯一产品主入口；WebOS 只复用同一 `ChatApp / api/chat.ts / chatStore / chatHub`，没有应迁回正式 Web 的独占聊天能力。
- 会话列表与深链、Direct 请求 / 接受 / 拒绝、统一屏蔽、归档、历史、文本 / 图片 / 引用、搜索、Reaction、Pin、未读、阅读回执、附件 ACL 和实时恢复均已进入正式 Web 主体链路。
- 审计识别的三组代码阻断为 ChatMessage 举报的成员 ACL 与 LongId 链路、失败重试幂等键、History / MessageWindow 的稳定错误契约；它们已由同日获批修复批闭合。
- 撤回动作缺少服务端 `VoCanRecall` 证据，且“频道 Moderator / Owner 可撤回他人消息”的文档口径与当前 System / Admin 后端、仅本人前端不一致；改变权限前必须单独裁决。
- 本次 readiness 只为后续 `R1-W01` 降低返工，不提前制作其画板；Pencil 恢复后仍先返回 `R1-A01`。

## 2. 代表身份与关键状态

正式代表身份建议固定为：

```text
登录普通 User
+ 与可用普通用户的 Accepted 互关 Direct
+ 双方未屏蔽且已有历史
+ 当前会话包含文本、图片、引用、Reaction、一条 Pin 和 Direct 已读边界
+ 侧栏另有未读 / @、陌生请求、既有群与公共频道
```

该身份能在非特权权限下覆盖日常消息主链、共享 Pin、举报、归档、屏蔽和公开主页回访；`Pending` 不作为主画板，避免请求限制遮住正常会话。

必要关键状态只维护区块或交互序列，不复制整页：

| 状态 | 必须表达 | 不得暗示 |
| --- | --- | --- |
| Pending 接收方 | 接受、拒绝、屏蔽；发起方首条仅纯文本 | 已能发送附件、引用或 `@` |
| Declined / Blocked / 对端不可用 | 历史可读、举报可用、输入禁用及原因；仅本人建立的屏蔽可解除 | 屏蔽删除了历史或自动重开请求 |
| Archived | 当前 / 已归档分区与恢复；合法深链仍可读 | 归档是终态或删除 |
| 搜索与定位失败 | PC 右侧搜索、mobile 单列；target / cursor 失效不回显旧正文 | 搜索词进入 URL、持久化或日志 |
| 离线 / 发送失败 | 草稿、失败消息、重试、撤销、复制安全诊断 | 未确认结果可以换幂等键自动重发 |
| Pin / Private 读者详情 | PC 紧凑浮层、mobile 模态 Sheet、焦点恢复 | Revision 是用户可见业务信息 |

## 3. 能力覆盖矩阵

| 能力 | 正式代码事实 | 门禁结论 |
| --- | --- | --- |
| 路由与登录回流 | `/messages?channelId=&messageId=` 使用字符串 ID，通知、公开主页和搜索均可进入 | 已承接 |
| WebOS 复用 | 两个壳层加载同一 `ChatApp`，以独立 owner 共享单例 Hub | 已承接，无历史能力迁移 |
| 会话与 Direct 状态 | active / archived、四类分区和 `VoCan*` 动作由服务端返回 | 已承接 |
| 发送与恢复 | 文本、图片草稿、引用、mention、失败卡片和服务端幂等已存在 | 重试未复用原 key，未通过 |
| 历史与定位 | 前后分页、`GetMessageWindow`、高亮和 Back / Forward 恢复已存在 | 无权 / 不存在错误状态漂移，未通过 |
| 搜索 | 当前 / 全部可读频道、日期、快照 cursor、目标失效和 PC / mobile 恢复 | 已承接 |
| Reaction / Pin | 服务端能力、目标状态、revision、Hub 全量状态和撤回清理 | 已承接 |
| 未读 / 回执 | REST 单调游标、Direct 单一已读边界、Private 受限读者详情 | 已承接 |
| 举报 | UI 有动作，治理侧有快照和案件 | ACL 与 LongId 双重阻断，未通过 |
| 实时与附件 | Hub Join / typing 分别要求 CanView / CanSend，附件按消息所属频道授权 | 已承接 |
| PC / mobile | PC 列表 + 消息 + 按需右上下文；390px 为列表 / 详情单任务流 | 主链已承接，断点和辅助入口有结构债 |

## 4. 设计前代码阻断

### 4.1 ChatMessage 举报未闭合

前端将 `message.voId` 经 `toNumericId` 转为 JavaScript `number`，随后 `ContentReportModal` 会拒绝非安全整数；真实 19 位 Snowflake ID 因而无法提交。共享 Modal 和最终 API 已支持字符串，问题在 Chat 调用链主动破坏 LongId。

后端 `SubmitCaseReportAsync` 在创建举报前仅按 `messageId` 构建 ChatMessage 快照，没有把请求 `tenantId / reporterUserId` 绑定到消息所属频道，也没有调用 `IChatChannelAccessService` 要求当前用户 `CanView`；回执又会返回消息摘要。该路径违反专题“只有参与者可以举报自己有权读取的私聊消息”的硬契约，不能依靠前端隐藏按钮作为权限保护。

候选治理边界：

- 两个举报提交入口共用“提交者可举报目标”校验；ChatMessage 按 `messageId + tenantId` 解析所属频道，再以 reporter 调用访问策略并要求 `CanView`。
- 无权、错误绑定和不存在统一返回不泄露存在性的 `404 / Moderation.TargetUnavailable`；Blocked / Declined 的合法参与者仍可按既有历史读取权举报。
- 只有校验通过后才构建正文快照、查重和写 Report / Case；治理 Evidence / Appeal 复用的通用快照解析不混入普通 reporter 权限。
- 前端全链保留 LongId 字符串，并补超过 `Number.MAX_SAFE_INTEGER` 的回归测试。

### 4.2 失败重试破坏发送幂等

服务端已经用 `(TenantId, UserId, ClientRequestId)`、请求指纹和同参回放闭合模糊结果；首次乐观消息也保存 `voClientRequestId`。但失败卡片点击重试时重新生成 `clientRequestId`。若服务端已提交、客户端只丢失响应，重试会创建第二条消息。

修复必须复用失败消息原有 `voClientRequestId` 和原参数；只有用户明确撤销失败项并重新创作时才能生成新 key。图片、Pending Direct 首条请求和普通消息需共用回归矩阵。

### 4.3 History / MessageWindow 错误契约漂移

专题要求无权或目标不可用统一 `404`。当前 `GetHistory` 对 `CanView=false` 返回成功空数组，`GetMessageWindow` 返回 null 后 Controller 表达为 `400`。内容虽然 fail-closed，但页面会把失权历史误作真实空态，也无法稳定区分无效 deep link。

后续修复应保持统一访问策略，并以稳定 `Code / MessageKey + 404` 收口无权、不存在和错误绑定；不得把成员关系或目标存在性暴露给客户端。

### 4.4 撤回证据与权限待裁决

`ChannelMessageVo` 没有 `VoCanRecall` 或截止证据；前端对所有本人已发送消息持续显示撤回，后端普通用户超过 30 分钟后才拒绝。专题又曾描述 Moderator / Owner 可撤回他人消息，而当前 Controller 只为 System / Admin 传入 elevated 能力，正式 UI 只显示本人动作。

普通代表状态暂只表达“本人且服务端允许时撤回”。是否让频道 Moderator / Owner 撤回他人消息属于权限变化，未经确认不得由 R1-W01 视觉稿或实现暗中扩展。

## 5. 结构债与设计输入

- `ChatApp.tsx` 已达 `1500` 行，`ChatApp.module.css` 为 `1592` 行；后续视觉实现必须先按现有职责拆分，不能继续堆入主文件。
- JavaScript、Messages 壳层以 `720px` 判 compact，Chat 核心 CSS 到 `680px` 才切单列；`681–720px` 会同时出现 mobile 状态和 PC 双栏，后续必须建立单一断点契约。
- 成员栏按既有规范在窄屏退出；现有 API 只有在线成员，不支持完整名册或群管理。若 mobile 保留该辅助能力，只能设计“在线成员”紧凑入口，不得暗增新能力。
- 初次历史和在线成员读取失败目前分别落成“无消息 / 无在线成员”，列表错误也没有显式重试；实现批应区分错误与真实空态。
- 正式 `/messages` 没有与 WebOS `Shell` 对称的显式 Chat Store 账号 reset；需核对登出 / 换号生命周期并补账号边界测试。
- 搜索关闭未稳定恢复触发焦点；mobile Pin 为自制 dialog 而非共享模态 Bottom Sheet，焦点边界与文档承诺不一致。
- `ChatPinnedMessages` 直接展示 revision；这是同步实现细节，不进入正式代表视觉。
- 长期文档中的 5 分钟 MessageGroup、可折叠会话分区、未读 `99+` 和日期快捷范围不是当前实现事实；离线成员也无现有 Presence 契约，Pencil 不得把它们画成已交付能力。

## 6. PC → mobile 响应式输入

PC 保持“会话栏 + 消息主轴 + 按需右上下文”：搜索与在线成员互斥，Pin 作为消息区紧凑入口，不恢复旧摘要仪表盘、WebOS 窗口或 Dock。

Mobile `390 × 844` 不压缩三栏：列表不自动选中会话，详情由 `channelId / messageId` 路由承接；详情与搜索隐藏共享底部导航，Pin、读者详情和确认动作进入 Bottom Sheet，输入区保留安全区和软键盘空间。成员固定栏退出，是否保留在线成员紧凑入口由后续设计明确。

## 7. R2 / R3 边界与停止线

- `R2-W02` 只补 Private 仪表、继续处理队列与辅助状态 rail 在 mobile 的排序 / 折叠，继承 `R1-F01 / R1-W01`；不复制聊天历史、搜索或 Direct 状态机。
- Notifications、Me、Circle、Pet、Private Shop 与 Workbench 继续作为 R3 继承页；只有出现新的实时两级导航或高风险动作才重新升级。
- 本审计本身未修改 `.pen` 或运行时代码，也未启动服务；后续修复已另行获批并按独立记录留痕。
- `R1-A01 → R1-W01 → R1-C01 → R1-C02` 顺位不变，Pencil 可用后立即回到 `R1-A01`。

## 8. 主要证据

- `Docs/features/chat-direct-conversation-design.md`
- `Docs/features/chat-frontend.md`
- `Docs/features/chat-message-search-design.md`
- `Docs/features/chat-message-reaction-design.md`
- `Docs/features/chat-message-pin-design.md`
- `Docs/features/chat-message-read-receipt-design.md`
- `Docs/guide/chat-workspace.md`
- `Frontend/radish.client/src/messages/MessagesApp.tsx`
- `Frontend/radish.client/src/apps/chat/ChatApp.tsx`
- `Frontend/radish.client/src/apps/chat/ChatMessageList.tsx`
- `Frontend/radish.client/src/apps/chat/useChatConversationWorkspace.ts`
- `Frontend/radish.client/src/services/chatHub.ts`
- `Frontend/radish.client/src/stores/chatStore.ts`
- `Radish.Service/ChatService.cs`
- `Radish.Service/ContentModerationService.Cases.cs`
- `Radish.Service/ContentModerationService.Navigation.cs`
