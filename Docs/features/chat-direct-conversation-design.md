# 正式 Web 一对一私聊与会话管理设计

> 状态：设计完成，待实现
>
> 最后更新：2026-07-18（Asia/Shanghai）
>
> 本文是一对一私聊专题的实现依据。聊天室公共能力继续以 [聊天室系统设计](./chat-system.md) 为准，正式 Web 路由与壳层边界继续以 [纯 Web 私域复访入口设计说明](/frontend/private-web-revisit) 为准。

## 摘要

- 一对一私聊复用现有 `Channel / ChannelMember / ChannelMessage / ChatHub / ChatApp`，不新增平行的 `PrivateMessage` 消息体系。
- 新增 `DirectConversation` 只保存参与者、请求和阻断状态；`Channel.Type=Private` 仍是消息容器，是否为一对一会话只由 `DirectConversation.ChannelId` 判定。
- 陌生人不能直接连续发送消息：非互关用户创建的是待处理请求，发起人只能发送一条纯文本请求；接收人接受后才能正常互发，也可以拒绝或阻断。
- 私聊成员授权必须覆盖列表、详情、历史、发送、撤回、已读、在线成员、Hub 加组、消息定位、举报和附件访问。不能只在页面隐藏入口。
- `/messages` 和公开个人页是正式 Web 唯一新增产品入口；WebOS 只保持兼容，Flutter、Tauri 和公开聊天室不在本专题范围。

## 1. 专题定位

Radish 已具备公开频道消息、未读状态、实时同步、图片、引用回复、撤回、举报和 `/messages` 工作区。本专题把这些能力扩展为可长期使用的一对一会话，解决三个产品问题：

1. 用户在公开内容和关注关系形成后，可以从对方公开主页继续交流。
2. 用户再次登录时，可以从会话列表、未读状态和通知定位继续之前的对话。
3. 陌生人请求、拒绝、阻断、私聊附件和 Console 治理遵循明确的隐私边界。

本专题不是另建即时通信平台，也不重写现有聊天组件。交付目标是让“一对一会话”成为正式 Web 社区关系链中的稳定能力。

## 2. 现有实现与缺口

| 边界 | 当前事实 | 本专题要求 |
| --- | --- | --- |
| 消息存储 | 已有 `ChannelMessage`、引用、图片、撤回、未读游标 | 继续复用，并补持久化发送幂等键 |
| 私聊类型 | `ChannelType.Private` 只是预留值 | 由 `DirectConversation` 明确一对一语义 |
| 频道可见性 | `GetChannelListAsync` 当前返回全部启用频道 | 公开频道按租户可见，私有频道只对有效成员可见 |
| 消息授权 | 历史、窗口、发送、加入和已读当前未校验私有成员 | 所有读写与 Hub 操作统一经过服务端访问策略 |
| 会话分区 | 前端已有 `voConversationKind` 可选字段，但后端未返回 | 后端权威返回 `public / mutual / stranger / group` |
| 发起入口 | 公开个人页只有关注和分享 | 增加“发消息”，登录后幂等创建或恢复会话 |
| 陌生人治理 | 没有请求、拒绝或阻断状态 | 增加请求生命周期和会话级阻断 |
| 发送恢复 | `clientRequestId` 目前只回显 | 持久化并建立唯一约束，相同请求返回原消息 |
| 聊天附件 | Chat 上传默认公开；非公开附件只允许上传者或管理员 | Chat 附件默认不公开，按频道访问权授权，管理员角色不自动穿透私聊 |
| Console | 举报队列可回看聊天目标 | 只允许查看用户主动举报形成的快照，不提供私聊浏览器 |

当前代码中的授权缺口在公开频道阶段没有直接暴露私人内容，但在启用 `Private` 前必须先治理。实现顺序不得先开放入口、再补成员校验。

## 3. 用户路径

### 3.1 从公开个人页发起

1. 当前用户在 `/u/:identifier` 点击“发消息”。
2. 匿名用户保存公开主页和 `message` 意图，完成登录后回到同一用户页继续操作。
3. 前端使用目标用户的稳定 `voUserId` 调用幂等创建接口；URL 不携带目标用户内部信息。
4. 服务端返回既有或新建的 `channelId`，前端进入 `/messages?channelId={id}`。
5. 自己的公开主页不展示“发消息”；目标不存在、已禁用、已删除或不属于当前租户时，保留在个人页并展示结构化错误。

### 3.2 互相关注会话

- 创建时双方互相关注，会话直接进入 `Accepted`。
- 双方均可发送现有支持的文本、图片和引用消息。
- 会话位于“互相关注”分区；关系变化后会话和历史不删除，只根据当前关系进入“陌生人私信”分区。
- 已经接受的会话不因取消关注自动降级为待处理请求，避免关系变更破坏既有对话。

### 3.3 陌生人消息请求

- 创建时双方不是互相关注，会话进入 `Pending`。
- 发起人只能发送一条纯文本请求消息；不能发送图片、重复消息或 `@mention` 通知。
- 接收人在首条消息成功后才看到该会话，归入“陌生人私信”。空的待处理会话不进入接收人的列表。
- 接收人可以接受、拒绝或阻断：
  - 接受后状态变为 `Accepted`，双方可以正常互发；
  - 拒绝后保留请求和历史，发起人不能再次发送；接收人以后仍可主动接受；
  - 阻断后双方都不能发送，只有执行阻断的一方可以解除。
- 待处理请求只产生一条不含正文预览的站内通知，目标仍为 `/messages?channelId=...&messageId=...`。

### 3.4 复访、未读与归档

- 会话列表展示对方当前显示名、头像、最近消息、最近时间、未读数和请求状态，不使用 `Channel.Name` 猜测对方身份。
- `LastReadMessageId` 继续作为服务端权威未读游标；本专题不增加逐条成员头像阅读回执。
- 用户可以归档会话。归档只影响自己的列表，不删除会话、成员和消息。
- `/messages` 提供独立的“已归档”视图，用户可以查看并取消归档；归档不是不可恢复的隐藏操作。
- 对方发来新消息时自动清除接收人的归档状态；用户通过合法深链进入归档会话时仍可查看。
- 对方账号不可用时保留历史和举报入口，名称降级为“用户不可用”，同时禁止继续发送。

## 4. 数据模型

### 4.1 复用原则

一对一私聊继续使用：

- `Channel`：会话容器和最后消息摘要；
- `ChannelMember`：两名参与者、成员授权和已读游标；
- `ChannelMessage`：全部消息内容；
- `ChatReliableOutboxMessage`：请求通知和附件绑定等可靠任务；
- `ChatHub`：实时消息、撤回和未读同步。

不新增 `PrivateMessage`，也不复制发送、撤回、附件、未读和实时协议。旧文档中的 `PrivateConversation + PrivateMessage` 口径由本文替代。

### 4.2 DirectConversation

`DirectConversation` 位于 `Chat` 数据库，只承担一对一会话元数据：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `long` | 雪花主键 |
| `ChannelId` | `long` | 关联 `Channel`，唯一 |
| `ParticipantLowUserId` | `long` | 两名用户中较小的 ID |
| `ParticipantHighUserId` | `long` | 两名用户中较大的 ID |
| `RequestedByUserId` | `long` | 首次发起人 |
| `RequestStatus` | enum | `Pending / Accepted / Declined` |
| `AcceptedAt` | `DateTime?` | 接受时间，统一 UTC |
| `DeclinedAt` | `DateTime?` | 拒绝时间，统一 UTC |
| `BlockedByUserId` | `long?` | 当前阻断人；为空表示未阻断 |
| `BlockedAt` | `DateTime?` | 阻断时间，统一 UTC |
| `TenantId` | `long` | 租户边界 |
| 审计字段 | 现有规范 | 创建、修改信息 |

数据库约束：

- 唯一索引：`(TenantId, ParticipantLowUserId, ParticipantHighUserId)`；
- 唯一索引：`(TenantId, ChannelId)`；
- `ParticipantLowUserId < ParticipantHighUserId`；
- `RequestedByUserId`、`BlockedByUserId` 必须是两名参与者之一；
- `ChannelId` 对应的频道必须为 `ChannelType.Private`。

规范化用户对由服务端完成，客户端不得传 `Low / High` 字段。并发创建依靠 Chat 数据库事务与唯一约束收敛到同一会话；命中唯一冲突后重新读取并返回既有记录。

### 4.3 现有实体调整

`Channel`：

- 不增加对方用户名、头像或关注关系快照；这些展示信息由服务端批量读取当前用户资料和关系后装配。
- 一对一频道使用 `Type=Private`。`Private` 但没有 `DirectConversation` 的频道保留给受邀群组，不能按名称或成员数猜测为私聊。

`ChannelMember`：

- 增加 `ArchivedAt`，只表示该成员归档了会话；不复用 `IsDeleted`。
- 一对一会话始终有且只有两个未删除成员。归档、拒绝或阻断都不删除成员记录。

`ChannelMessage`：

- 增加可空 `ClientRequestId`，长度 `100`；
- 建立 `(TenantId, UserId, ClientRequestId)` 条件唯一语义；历史记录保持 `null`；
- 同一用户重复提交相同 `clientRequestId` 时，如果消息参数一致则返回原消息；参数不一致返回 `409 Conflict`。

### 4.4 迁移与历史数据

- 通过 `RadishSchemaVersion` 增加显式 Chat schema migration，不在 baseline 后依赖 Code First 静默改表。
- SQLite 与 PostgreSQL 都必须覆盖建表、索引、重入、并发创建和 verify。
- 既有 `Channel.Type=Private` 且没有 `DirectConversation` 的记录按私有群组处理，只对有效 `ChannelMember` 可见，不做名称或描述推断。
- 历史 `ChannelMessage.ClientRequestId` 和 `ChannelMember.ArchivedAt` 保持 `null`。
- Main 库中的历史 `BusinessType=Chat` 附件迁移为非公开；访问权改由消息所属频道判断。迁移前由 doctor 报告无法关联到消息的 Chat 附件，不自动删除。

## 5. 服务与授权边界

### 5.1 统一访问策略

在 `Service` 层建立单一聊天访问策略，并由 Controller、Hub 和附件访问共同复用。它至少区分：

- `ViewChannel`
- `ViewHistory`
- `SendMessage`
- `JoinRealtimeGroup`
- `MarkRead`
- `ViewMembers`
- `RecallMessage`
- `ViewAttachment`

规则矩阵：

| 场景 | 读取 | 发送 | 实时加入 | 成员列表 |
| --- | --- | --- | --- | --- |
| `Public` | 当前租户登录用户 | 当前租户登录用户 | 允许 | 允许 |
| `Announcement` | 当前租户登录用户 | 仅既有授权角色 | 允许 | 允许 |
| 私有群组 | 有效成员 | 按成员角色 | 有效成员 | 有效成员 |
| 一对一 `Accepted` | 两名参与者 | 未阻断且目标可用 | 两名参与者 | 两名参与者 |
| 一对一 `Pending` | 发起人；接收人在首条消息后可读 | 发起人仅一条纯文本；接收人接受前不可发送 | 可读方 | 不展示在线成员 |
| 一对一 `Declined` | 两名参与者 | 不允许 | 两名参与者 | 不展示在线成员 |
| 一对一被阻断 | 两名参与者可保留历史和举报 | 不允许 | 两名参与者 | 不展示在线成员 |

所有按 `channelId` 或 `messageId` 的入口先解析频道，再执行同一策略。不存在“列表不可见但猜到 ID 可以读取”的旁路。

### 5.2 撤回与治理

- 一对一消息只允许发送者在现有时间窗口内通过普通聊天接口撤回。
- `Admin / System` 角色不因角色本身获得私聊读取、Hub 加组或撤回权限。
- 参与者可以举报自己有权读取的私聊消息；举报保存必要快照、目标 ID 和时间。
- Console 只在有权限的治理队列中查看用户主动提交的举报快照和处理记录，不提供按用户或会话浏览私聊历史的页面和接口。
- 对举报目标采取治理动作继续走既有受权、确认和审计路径，不复用普通用户撤回接口。

### 5.3 附件隐私

- `BusinessType=Chat` 的新上传附件从创建时即 `IsPublic=false`，避免上传到发送之间出现匿名可访问窗口。
- 附件元数据接口、原图、缩略图和下载入口使用同一访问判定；不能只保护二进制下载。
- Chat 附件的访问者必须能够读取引用它的消息所属频道。公开频道在这里仍指“登录用户频道”，不等于匿名公开资源。
- 普通管理员不能通过现有非公开附件管理员旁路读取私聊附件。
- 消息提交后通过可靠 Outbox 完成附件业务绑定和持久化计费；当前 `TryBindMessageAttachmentAsync` 的吞错方式不得继续作为正式私聊保障。
- 附件绑定暂时延迟时，消息和引用关系仍是访问权依据；任务重放不得生成第二条消息或重复结算。

## 6. API 契约

### 6.1 DirectConversationController

| 方法 | 路由 | 请求 | 返回与作用 |
| --- | --- | --- | --- |
| POST | `/api/v1/DirectConversation/GetOrCreate` | `targetUserId` | 幂等返回会话和当前状态 |
| POST | `/api/v1/DirectConversation/Accept/{channelId}` | 无 | 接收人接受请求 |
| POST | `/api/v1/DirectConversation/Decline/{channelId}` | 无 | 接收人拒绝请求 |
| POST | `/api/v1/DirectConversation/Block/{channelId}` | 无 | 当前参与者阻断会话 |
| POST | `/api/v1/DirectConversation/Unblock/{channelId}` | 无 | 仅阻断人解除 |
| PUT | `/api/v1/DirectConversation/SetArchived/{channelId}` | `archived` | 修改当前成员的归档状态 |

`GetOrCreate` 只创建会话，不代替消息发送。非互关会话在首条请求消息落库前不向接收人显示。

### 6.2 现有接口调整

- `Channel/GetList?view=active|archived`：只返回当前用户可见频道。`active` 返回公开频道和未归档私有会话，`archived` 只返回当前用户已归档的一对一会话；服务端装配会话分区、对方资料、请求状态和可用动作。
- `Channel/GetDetail`：执行访问判定并返回同一会话元数据。
- `ChannelMessage/GetHistory`、`GetMessageWindow`：无权访问统一返回 `404`，不泄露频道或消息是否存在。
- `ChannelMessage/Send`：执行会话状态、阻断、附件、回复来源和持久化幂等检查。
- `ChannelMessage/Recall`：先校验消息所属频道访问权，再校验发送者和时间窗口。
- `ChatHub.JoinChannel / StartTyping / MarkChannelAsRead`：全部执行同一服务端访问策略；未授权时拒绝，不加入组、不更新成员。

### 6.3 ChannelVo 扩展

频道返回继续保留现有字段，并增加：

- `VoConversationKind`：`public / mutual / stranger / group`；
- `VoPeerUserId / VoPeerPublicId / VoPeerDisplayName / VoPeerAvatarUrl`；
- `VoDirectRequestStatus`：`pending / accepted / declined / null`；
- `VoCanSend / VoCanAccept / VoCanDecline / VoCanBlock / VoCanUnblock`；
- `VoIsBlockedByCurrentUser / VoIsArchived`。

所有动作可用性由服务端返回，前端不根据关注文案、频道名称或本地角色自行推导权限。

### 6.4 错误契约

高频失败使用真实 HTTP 状态、稳定 `Code / MessageKey` 和中英文资源，至少覆盖：

- 目标用户不存在、不可用、跨租户或为当前用户；
- 会话不存在或无权访问；
- 请求已拒绝、会话被阻断、尚未接受；
- 待处理请求已经发送过首条消息；
- 私聊请求只允许纯文本；
- `clientRequestId` 同键异参冲突；
- 附件不存在、非当前用户上传、不是图片或无法用于该频道；
- 引用消息不属于当前会话。

## 7. 页面与交互边界

### 7.1 设计源

进入前端实现前，先更新 `Docs/frontend/design-sources/private-web-workflows.pen` 中现有 `P13 - Messages Workspace`，覆盖：

- 公开个人页“发消息”动作；
- PC 会话列表中的待处理请求、接受、拒绝和阻断状态；
- 移动端列表到详情的切换及请求操作；
- 对方不可用、被阻断、归档和发送失败状态。

这是现有页面族的功能扩展，不新增全局导航或新的视觉体系。

### 7.2 正式 Web 页面

公开个人页：

- 非本人主页增加“发消息”；
- 匿名点击进入登录恢复；
- 创建失败保留页面上下文并可重试，不跳转到空白 `/messages`。

`/messages`：

- 继续使用 `channelId / messageId` 字符串参数和既有登录恢复；
- 会话分区由后端字段驱动；
- 提供“当前会话 / 已归档”视图，已归档列表可以打开会话并取消归档；
- 待处理请求在消息区顶部展示明确状态和接受、拒绝、阻断动作；
- 输入区根据 `VoCanSend` 禁用并解释原因；
- 归档动作放在会话上下文菜单，不占主导航；
- PC 保持列表与正文并列，移动端保持列表页与会话详情切换，不把 PC 三栏压缩到手机宽度。

WebOS：

- `/desktop` 继续复用同一 `ChatApp`、API 和权限结果；
- 不为 WebOS 新增独立私聊协议或专属功能入口；
- 正式验收以 `/messages` 为主，`/desktop` 只做阻断级兼容复核。

## 8. 实时、通知与恢复

- `MessageReceived / MessageRecalled / ChannelUnreadChanged` 继续复用，私聊事件只发送到通过访问校验的频道组和用户组。
- 接受、拒绝、阻断、解除和归档新增稳定的会话状态事件，或在事件后统一刷新目标频道；不得依赖刷新整个页面才能生效。
- 待处理请求的首条消息与站内通知请求写入 Chat 源库同一事务和可靠 Outbox；通知正文不复制私聊内容。
- REST 成功但 Hub 回推丢失时，以重新获取频道列表和 `afterMessageId` 补拉恢复。
- 相同 `clientRequestId` 的重试返回同一消息，前端用真实消息替换乐观消息，不生成重复记录。
- 连接断开时保留草稿、待发送图片和失败消息；恢复后由用户重试，不自动重复提交未知结果的非幂等请求。
- 新消息到达归档会话时，服务端清除接收人的 `ArchivedAt` 并推送列表状态变化。

## 9. 停止线

本专题不包含：

- 公开聊天室新页面、匿名聊天或帖子引用进公开聊天室；
- 私有群组、群组邀请和群管理员体系；
- 全局或频道内消息搜索；
- Reaction、消息置顶和逐条成员头像阅读回执；
- 消息编辑、转发、阅后即焚；
- 语音、视频、端到端加密和原生系统推送；
- 管理员私聊浏览、私聊关键词扫描或常态内容审查；
- Flutter、Tauri 专属实现；
- 全局用户拉黑体系。当前阻断只作用于对应一对一会话，后续如扩展为全站关系能力必须另立专题。

## 10. 实施批次

### 批次 A：数据与访问边界

- 新增 `DirectConversation`、Chat ledger migration、唯一约束和 doctor / verify；
- 增加 `ChannelMember.ArchivedAt`、`ChannelMessage.ClientRequestId`；
- 建立统一聊天访问策略，覆盖 REST、Hub 和消息定位；
- 修正 Chat 附件默认可见性、元数据与二进制访问权；
- 补 SQLite / PostgreSQL 迁移、并发建会话和未授权访问测试。

批次 A 完成前不开放公开个人页“发消息”。

### 批次 B：会话生命周期与可靠消息

- 实现幂等创建、接受、拒绝、阻断、解除和归档；
- 实现待处理请求首条消息限制、请求通知和状态推送；
- 实现消息发送持久化幂等、附件可靠绑定和恢复；
- 扩展 `ChannelVo`，消除前端会话分区猜测。

### 批次 C：正式 Web 页面

- 先更新 Pencil 设计源，再实现公开个人页入口；
- 完成 `/messages` 请求状态、动作、分区、归档和移动交互；
- 补齐中英文资源、结构化错误映射和无障碍状态；
- 保持 WebOS 共用组件兼容。

### 批次 D：成组验收

- 执行后端、前端与迁移定向回归；
- 在专题准备验收时，经当轮授权启动服务；
- 使用两个普通账号和一个陌生关系账号覆盖 PC / mobile 会话矩阵；
- 记录临时数据、清理方式、结果和未覆盖风险。

## 11. 验证口径

### 11.1 后端

- 同租户同一用户对顺序互换、并发创建，最终只有一个 `DirectConversation` 和一个频道；
- 跨租户、自聊、目标禁用或删除均被拒绝；
- 私有频道非成员无法列表、详情、历史、窗口、发送、撤回、已读、成员查询或 Hub 加组；
- 陌生请求只能发送一条纯文本，接受后正常互发，拒绝或阻断后禁止发送；
- 相同 `clientRequestId` 同参回放、异参冲突；
- 回复消息和附件必须属于当前频道与当前发送者权限范围；
- Chat 附件匿名、非成员和普通管理员不可读，参与者可读；
- 归档不删除数据，新消息能恢复接收人的列表项；
- 举报只接受参与者可读消息，Console 只返回举报快照；
- SQLite / PostgreSQL migration apply、重入、verify 和唯一约束一致。

### 11.2 前端

- 公开个人页匿名登录回流、本人入口隐藏、失败保留上下文；
- 后端 `VoConversationKind` 驱动四个分区，不保留名称猜测；
- 当前会话与已归档视图分离，归档、取消归档和新消息自动恢复列表状态一致；
- 待处理、已接受、已拒绝、被阻断和用户不可用状态具有明确动作与说明；
- 相同乐观消息在 REST 返回和 Hub 回推后只保留一条；
- `/messages?channelId=&messageId=` 定位、公开个人页来源返回和移动返回保持现有契约；
- PC 与移动端输入区、图片草稿、失败重试和请求操作均不被壳层遮挡。

### 11.3 开发与验收入口

开发中执行：

- Chat Service、Controller、Hub、附件访问和 migration 定向测试；
- `radish.client` 聊天、公开主页、路由和登录恢复测试；
- `npm run type-check --workspace=radish.client`；
- `npm run build --workspace=radish.client`；
- `dotnet build Radish.slnx -c Debug`；
- `git diff --check` 与 changed-file repo hygiene。

专题验收再执行 Gateway 下 PC `1920x1080` 与移动 `390x844 @ DPR 3` 的双账号路径，不把页面可达或自动化测试描述为用户价值证据。

## 12. 完成标准

只有同时满足以下条件，专题才可关闭：

1. 数据模型、迁移、唯一约束和私聊成员 ACL 在 SQLite / PostgreSQL 一致生效。
2. 公开个人页可以幂等发起或恢复会话，互关与陌生请求路径均可完成。
3. `/messages` 能处理请求、正常会话、归档、阻断、未读、消息定位和失败恢复。
4. 消息重试不会重复落库，私聊附件不会匿名公开或被非成员读取。
5. Console 不提供常规私聊浏览，只保留用户举报形成的受权治理快照。
6. 后端、前端、迁移和 PC / mobile 成组验收通过，相关专题文档与当前规划同步。
