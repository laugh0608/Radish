# F4-B 通知中心深化与通知治理

> **状态**：F4-B-A / F4-B-B / F4-B-C / F4-B-D 已完成；F4-B 已关闭
>
> **复核日期**：2026-07-20
>
> **适用主线**：正式 Web `/notifications`；WebOS `/desktop` 仅复用，Flutter 仅做既有 MVP 兼容

## 摘要

F4-B 已把通知中心收口为“社区事件复访与处理入口”，不是第二套私信箱。当前实现使用稳定通知定义、结构化目标、用户通知分组和权威摘要状态：业务事件经可靠 Outbox 进入 Message 库，数据库事务形成事件、接收关系、分组和摘要；HTTP API 返回可本地化的权威列表，SignalR 只发送带 revision 的变更提示。

正式 Web `/notifications`、Workbench、导航角标和 WebOS 复用面均以服务端 summary / revision 为准；偏好、聚合、容量清理、跨标签、断线和 cursor 恢复围绕同一数据真相源运行。F4-G 后 Knowledge 分类已注册 Wiki 协作者邀请与审核结果生产者，并使用 `DocsAuthorDraft` 结构化目标；旧通知 API 暂时保留，删除前需另做消费者和 Flutter 兼容审计。

## 目录

1. [专题定位与目标](#一专题定位与目标)
2. [F4-B-A 现状审计](#二f4-b-a-现状审计)
3. [稳定业务契约](#三稳定业务契约)
4. [数据模型与迁移](#四数据模型与迁移)
5. [服务端写入与查询](#五服务端写入与查询)
6. [HTTP、SignalR 与客户端契约](#六httpsignalr-与客户端契约)
7. [权限、隐私与失败恢复](#七权限隐私与失败恢复)
8. [容量与清理](#八容量与清理)
9. [实施批次](#九实施批次)
10. [验证与成组验收](#十验证与成组验收)
11. [停止线与剩余风险](#十一停止线与剩余风险)
12. [专题完成标准](#十二专题完成标准)

## 一、专题定位与目标

### 1.1 产品定位

通知中心承接以下职责：

- 告知用户与内容、关系、订单、权益、成长、治理和系统有关的离散事件。
- 帮助用户返回仍然有权访问的正式业务目标。
- 提供可靠的未读、分组、已读、删除和偏好状态。
- 在网络恢复或多标签页切换后，以数据库事实追平状态。

通知中心不承接以下职责：

- 不保存一对一私聊正文，不替代 `/messages` 的会话列表和请求处理。
- 不把普通通知扩展成用户之间的站内信或后台广播平台。
- 不因 SignalR 在线就宣称通知已经被用户看到或送达。
- 不为缺少正式页面的业务伪造链接。

### 1.2 本专题目标

1. 用稳定定义替换裸字符串、标题关键字和 `businessType` 猜测。
2. 让偏好在通知入箱前生效，并保证账号安全等强制通知不被错误关闭。
3. 对高频轻互动形成服务端权威聚合，不丢失可靠事件身份。
4. 让分类筛选、未读统计、分页和容量治理不再扫描全部通知分表。
5. 让目标回流使用结构化契约，目的页面继续独立执行 ACL。
6. 让数据库和 HTTP 成为最终真相源，SignalR 只负责加速变化感知。
7. 完成正式 Web 中英文、PC / mobile 页面和双账号真实交互验收。

## 二、F4-B-A 现状审计

### 2.1 已有能力

| 层级 | 当前能力 |
| --- | --- |
| 事件生产 | 点赞、评论、轻回应、关注、私信请求、聊天提及、等级、抽奖、购买成功和权益过期等生产者已接入可靠 Outbox |
| 持久化 | `Notification_YYYYMMDD` 按月分表，`UserNotification` 保存用户已读 / 删除状态，通知与接收关系在 Message 库事务写入 |
| HTTP | 当前具有列表、未读总数、单条 / 全部已读和删除接口 |
| 实时 | `NotificationHub` 支持用户组连接、未读数、新通知、已读和全部已读事件 |
| Web | `/notifications`、Workbench 摘要和 WebOS 通知应用共享 Store；正式 Web 可打开 forum、聊天、用户和订单等部分目标 |
| Flutter | 既有 MVP 可读取最近通知、单条已读和 forum 回流 |

本地只读样本中，Message 库存在 `NotificationSetting`、两个通知月表和 `UserNotification`；设置记录为 `0`，现有两条用户通知均处于 `DeliveryStatus=Created`。该样本只用于核对 schema 和实现事实，不代表生产使用量或用户价值。

### 2.2 生产者审计

| 当前生产者 | 当前类型 | 当前目标信息 | 审计结论 |
| --- | --- | --- | --- |
| 帖子 / 评论点赞可靠任务 | `PostLiked / CommentLiked` | forum `ExtData` | 5 分钟窗口键同时充当业务幂等键，第二个真实点赞可能被当成重复事件丢弃，尚未形成聚合计数 |
| 评论可靠任务 | `CommentReplied` | 帖子或评论 | 同一类型同时表达“评论被回复”和“帖子收到评论”，用户动作语义不稳定 |
| 帖子轻回应 | `PostQuickReplied` | forum `ExtData` | 可聚合，但目前逐条或按缓存窗口去重 |
| 关注 | `Followed` | `User` + 触发者 | 业务键包含新通知 ID，只保证任务自身不重复，不表达业务事件身份 |
| 私信请求 | `DirectMessageRequested` | chat `ExtData` | 目标基本完整；应继续只展示请求摘要，不保存私聊正文 |
| 聊天提及 | `Mentioned` | chat `ExtData` | 未填写 `BusinessType`，正文包含聊天内容预览，长期隐私边界不清 |
| 购买成功 | `PURCHASE_SUCCESS` | `Order` | 使用常量表之外的裸字符串 |
| 权益过期 | `BENEFIT_EXPIRED` | `UserBenefit` | 使用常量表之外的裸字符串 |
| 等级提升 / 抽奖 | `LevelUp / LotteryWon` | 用户或 forum | 已有稳定类型，目标契约仍不统一 |
| 神评 / 沙发奖励 | `GodComment / Sofa` | 当前无通知生产者 | 常量存在但奖励链路只发放资产，不能在 UI 中伪装成已支持通知 |
| 萝卜币 / 系统 / 账号安全 | 对应常量 | 当前无稳定生产者 | 保留定义候选，但必须在真实生产者接入后才对用户显示偏好项 |

### 2.3 数据、服务与实时审计

- `NotificationSetting`、更新 DTO、VO 和映射已经存在，但没有 Service、Controller、HTTP client 或 UI 消费；表缺少“租户 + 用户 + 类型”唯一约束。
- 设置模型暴露 `EnableEmail` 和 `EnableSound`，仓库没有邮件通知或通知声音实现，现状会制造虚假能力。
- `NotificationService` 按类型筛选时先读取所有通知分表中的 ID，再用大 `Contains` 查询 `UserNotification`，无法作为长期分类和容量查询入口。
- `GetUnreadCountDetailAsync` 仍返回 `UnreadCountByType = null`；列表 DTO 的优先级和时间筛选也没有实际应用。
- `NotificationCacheService` 采用读改写增减计数，并在异常时返回 `0`；并发写会丢增量，缓存故障会被伪装成“没有未读”。
- `NotificationDedupService` 依赖缓存且当前没有真实调用者；可靠幂等、业务去重和展示聚合没有统一边界。
- `NotificationRepository.PersistAsync` 能处理全部接收关系已存在，但“部分接收者已存在”时会再次插入通知主记录并触发唯一冲突，剩余接收者可能无法补齐。
- `BusinessKey` 唯一索引只存在于单个月表；可靠重试如果重新生成创建时间，存在跨月落到不同分表的风险。
- `DeliveryStatus / DeliveredAt / RetryCount / LastRetryAt` 没有被实时推送链路更新，却已出现在公共 VO 中，不能代表用户送达状态。
- `NotificationHub.MarkAsRead / MarkAllAsRead` 只向其他连接广播，不写数据库；客户端若误用 Hub 命令，可制造跨端“看似已读、实际未读”。
- Hub 连接和重连只推未读总数，不主动对账列表；`NewNotification` 直接推完整展示对象，无法按每个连接的语言稳定本地化。

### 2.4 正式 Web 与测试审计

- `/notifications` 的分类 chip、行动队列总数和“可打开 / 待确认”指标只来自最近加载的最多 20 条通知，不是完整收件箱统计。
- `notificationActionQueue.ts` 扫描类型、业务类型、标题、正文和 `ExtData` 关键字猜测 `posts / messages / governance / orders` 等范围；用户文案变化会改变分类结果。
- `notificationNavigation.ts` 优先解析结构化 forum / chat 载荷，但仍保留 `businessType` 和通用 `/forum` 回退；弱上下文通知可能被带到错误页面。
- client 删除调用当前使用 `POST /Delete/{id}`，而 Controller 实际暴露 `DELETE /DeleteNotification/{id}`；现有测试没有覆盖该真实 HTTP 契约，删除操作存在明确阻断。
- 正式 Web 和 WebOS 维护一份列表局部状态，SignalR、HTTP 和本地减计数并行，跨端与分页列表只能靠重复刷新修正。
- 当前后端通知服务测试只有 4 个，集中在重复写、刷新缓存和全部已读；缺少 Controller、Hub、偏好、并发计数、部分接收者、迁移和容量测试。
- client 现有测试覆盖目标解析与关键字分类，Flutter 覆盖 forum 目标和单条已读；尚未覆盖服务端分类、聚合、偏好、断线追平和多标签页顺序。
- `notification-center.md` 的实际 HTTP 方法 / 路由与代码不一致；API、前端、实现和实时文档均混合早期方案与当前事实，其中多份已经超过专题文档硬上限。

### 2.5 共同根因

上述问题不是单个页面缺功能，而是五个契约没有分开：

1. **业务事件身份**：什么真实事件只处理一次。
2. **通知定义**：事件属于什么类型、分类、模板、偏好和目标政策。
3. **用户收件箱状态**：某用户有哪些通知组、多少未读、如何分页。
4. **展示聚合**：多个可靠事件如何合并为一个可处理项。
5. **实时加速**：在线连接如何获知数据库已经变化。

F4-B 后续批次必须同时治理这五个边界，不能继续通过标题关键字、缓存增减和客户端补刷新掩盖问题。

## 三、稳定业务契约

### 3.1 通知定义注册表

新增代码级 `NotificationDefinitionRegistry`，每个允许写入的通知类型必须登记以下信息：

- 稳定 `Kind` 与用户分类 `Category`。
- 默认优先级、标题 / 正文模板 key 和模板参数校验。
- 允许的 `TargetKind` 及必填目标字段。
- 是否允许用户关闭站内入箱、是否允许关闭实时预览。
- 是否聚合、聚合窗口和聚合维度。
- 保留策略和隐私摘要策略。

`CreateNotificationAsync` 不再接受未登记类型。业务生产者不能自行决定分类、默认目标回退或渠道能力；新增类型必须先补注册表、契约测试和本文档矩阵。

### 3.2 用户分类

| Category | 用户语义 | 首批类型 |
| --- | --- | --- |
| `Discussion` | 评论、回复、提及和轻回应 | `CommentReplied`、新增 `PostCommented`、`PostQuickReplied` |
| `Reaction` | 点赞等低干扰互动 | `PostLiked`、`CommentLiked` |
| `Message` | 需要进入聊天处理的通知 | 新增 `ChatMentioned`、`DirectMessageRequested` |
| `Relationship` | 关注与关系变化 | `Followed` |
| `Commerce` | 订单、背包和权益 | 新增 `PurchaseSucceeded`、`BenefitExpired` |
| `Growth` | 等级、奖励和萝卜币 | `LevelUp`、`LotteryWon`、`GodComment`、`Sofa`、`CoinBalanceChanged` |
| `Governance` | 举报、审核和申诉 | 首批保留分类，不在没有生产者时显示设置项 |
| `Knowledge` | Docs 与知识协作 | `WikiCollaboratorInvited`、`WikiReviewUpdated` |
| `System` | 公告和账号安全 | `SystemAnnouncement`、`AccountSecurity` |

分类只用于用户筛选、摘要和偏好，不替代具体 `Kind`。客户端不得根据标题、正文或语言重新分类。

### 3.3 类型收口

- `CommentReplied` 只表示回复某条评论；帖子收到顶级评论改用 `PostCommented`。
- 当前 chat `Mentioned` 迁移为 `ChatMentioned`；未来 forum 提及必须使用独立类型，不能再让目标反推语义。
- `PURCHASE_SUCCESS` 和 `BENEFIT_EXPIRED` 分别改为注册表常量 `PurchaseSucceeded`、`BenefitExpired`。
- 未接入生产者的 `GodComment / Sofa / CoinBalanceChanged / SystemAnnouncement / AccountSecurity` 可保留定义，但 API 的可配置分类只返回当前真正支持的类型集合。
- 业务事件幂等键与聚合键彻底分离。点赞事件键至少包含目标、点赞者和该次可靠写身份；聚合键由收件箱服务按目标和时间窗口计算。

### 3.4 结构化目标

对外返回 `NotificationTargetVo`，字段全部使用字符串承载 LongId：

```text
VoKind
VoPostId / VoPostPublicId / VoCommentId
VoChannelId / VoMessageId
VoUserId / VoUserPublicId
VoOrderId / VoBenefitId
VoDocumentSlug / VoDocumentId / VoDraftId / VoGovernanceCaseId
```

首批允许的 `VoKind`：

| TargetKind | 客户端正式目标 |
| --- | --- |
| `ForumPost` | `/forum/post/:publicId-or-id`，可附 `commentId` |
| `ChatConversation` | `/messages?channelId=...&messageId=...` |
| `UserProfile` | `/u/:publicId-or-id` |
| `ShopOrder` | `/shop/order/:orderId` |
| `Inventory` | 已有正式背包 / 权益入口；缺失对象时返回列表而不是伪造详情 |
| `Experience` | 已有个人成长入口 |
| `DocsDocument` | `/docs/:slug` |
| `DocsAuthorDraft` | Wiki 邀请与审核结果；服务端按 Owner / Accepted Editor 重新校验文档与可选草稿目标，失权时返回不可用状态 |
| `GovernanceCase` | 仅在当前用户有权访问正式目标时返回 |
| `None` | 保留通知正文，不渲染链接 |

服务端持久化 `TargetKind + TargetDataJson + TargetSchemaVersion`，并由注册表验证。客户端负责根据 `VoKind` 构造本站 URL，数据库不保存可被开放重定向利用的任意 URL。旧 `ExtData` 只用于历史回填兼容，新生产者停止写导航 JSON。

目标解析失败时必须返回 `None` 和稳定原因，不再按“互动通知统一回 `/forum`”处理。目标页面继续执行自身 ACL；通知存在不等于用户永久拥有目标访问权。

### 3.5 模板、本地化与隐私摘要

- 新通知保存 `TemplateKey + TemplateArgumentsJson`，同时保留标题 / 正文快照作为历史兼容和未知 key 兜底。
- HTTP 按请求语言渲染系统模板；用户昵称、帖子标题等用户内容参数保留原文。切换中英文后，新契约通知应使用当前语言显示。
- SignalR 不推送已经渲染的完整通知，避免同一账号不同语言标签页收到错误语言对象。
- 私信请求不保存消息正文；chat 提及只保存发送者和频道摘要，不持久化聊天正文预览。
- 头像和显示名是展示快照，不作为权限或身份真相；业务目标失效时仍可显示安全摘要，但不得泄露已撤销的私域内容。

## 四、数据模型与迁移

### 4.1 保留的事件层

继续保留 `Notification_YYYYMMDD` 月表，F4-B 不进行分表合并或 Message 分库重构。`Notification` 新增：

- `Category`
- `TemplateKey / TemplateArgumentsJson`
- `TargetKind / TargetDataJson / TargetSchemaVersion`
- `OccurredAtUtc`

`OccurredAtUtc` 必须由源事件在可靠任务创建时确定并随 payload 重放，不能在消费者重试时重新取当前时间。这样同一 `NotificationId / BusinessKey` 始终进入同一月表。历史 `Unspecified DateTime` 按仓库既有时间规范解释为 UTC。

### 4.2 用户事件关系

`UserNotification` 继续表示“某个可靠通知事件属于某用户”，新增 `InboxGroupId / OccurredAtUtc`。关系时间与源事件时间保持一致，用于组内已读截止点和容量清理。唯一约束继续保持：

```text
(TenantId, UserId, NotificationId)
```

`DeliveryStatus / DeliveredAt / RetryCount / LastRetryAt` 从实体、VO 和产品语义中退出。迁移先验证这些列是否仍为默认值；若发现真实非默认数据则停止并报告，不静默丢弃。SQLite 可在表重建时删除，PostgreSQL 可在验证后删除或先保留为未映射兼容列。

### 4.3 `NotificationInboxGroup`

新增不分表的用户收件箱分组表：

```text
Id, TenantId, UserId, GroupKey
Category, Kind
LatestNotificationId
OccurrenceCount, DistinctTriggerCount, UnreadOccurrenceCount
FirstOccurredAtUtc, LastOccurredAtUtc
IsDeleted, DeletedAtUtc, ReadAtUtc
CreateTime, ModifyTime
```

- 非聚合类型也形成独立分组，`GroupKey` 使用事件身份。
- 聚合类型按“租户 + 接收者 + 类型 + 结构化目标 + 固定窗口”生成不可逆 hash，不暴露业务正文。
- `LatestNotificationId` 用于列表摘要，最近 3 个触发者可通过组内关系定向查询。
- `DistinctTriggerCount` 记录点赞聚合中的去重触发者数，不把同一触发者的独立业务事件误展示为多个人。
- 唯一约束：`(TenantId, UserId, GroupKey)`。
- 分页索引：`(TenantId, UserId, IsDeleted, LastOccurredAtUtc, Id)`。
- 分类索引：`(TenantId, UserId, Category, IsDeleted, LastOccurredAtUtc, Id)`。

### 4.4 `NotificationInboxState`

新增每用户一行的权威摘要表：

```text
TenantId, UserId, Revision
UnreadGroupCount, UnreadOccurrenceCount
LastChangedAtUtc
```

- `Revision` 在每次入箱、已读、删除、偏好影响结果变化时事务递增。
- Dock 和主导航角标使用 `UnreadGroupCount`，与用户看到的聚合行数一致；页面可同时展示 `UnreadOccurrenceCount`。
- 列表 cursor 携带 `Revision`。当前 revision 已变化时，服务端返回稳定的 cursor 过期错误，客户端刷新第一页，不在变化中的分页上继续猜合并。
- 该表替代 `NotificationCacheService` 的正确性职责。当前规模下不再为一个可单行读取的摘要引入缓存一致性协议。

### 4.5 通知偏好

沿用物理表 `NotificationSetting`，收口为：

```text
Id, TenantId, UserId, Category
InAppEnabled, RealtimePreviewEnabled
CreateTime, ModifyTime, ModifyBy, ModifyId
```

- 唯一约束：`(TenantId, UserId, Category)`。
- 缺少记录时使用注册表默认值，不为每个用户预创建九行。
- `EnableEmail`、`EnableSound` 和重复表达的 `IsEnabled` 退出模型与 API；邮件和声音在没有真实实现前不展示。
- 分类关闭后，普通类型不创建用户事件关系；强制类型可绕过 `InAppEnabled`，但仍允许关闭实时预览。
- `AccountSecurity` 为强制入箱；其他强制类型必须在注册表显式声明并补测试。

### 4.6 迁移与校验

F4-B-B 使用 ledger migration `20260718_003_notification_inbox_governance`，覆盖 SQLite 与 PostgreSQL：

1. doctor 枚举全部通知月表，检查重复 `NotificationId / BusinessKey`、孤立用户关系和非默认 delivery 字段。
2. 为历史事件回填分类、模板兼容状态、结构化目标和 UTC 时间；无法可靠解析目标时写 `None`，不猜页面。
3. 建立分组与摘要表，把每条历史用户关系映射到独立组；首批不对历史数据追溯聚合，避免改变既有已读语义。
4. 回填 `InboxGroupId`、未读组数和未读事件数，校验事件、关系、组和摘要计数守恒。
5. 迁移现有设置；未知类型或冲突设置由 doctor 报告，不能静默覆盖。
6. 建立唯一索引和查询索引，完成重入、checksum、apply / verify 和备份恢复测试。

## 五、服务端写入与查询

### 5.1 写入事务

新增专属 `INotificationInboxRepository`，Service 不直接使用 Db。单次创建在 Message 库事务内执行：

1. 通过注册表校验类型、模板和目标。
2. 规范接收者并读取分类偏好；过滤关闭普通入箱的用户。
3. 按稳定 `NotificationId / BusinessKey / OccurredAtUtc` 幂等写事件。
4. 对每个尚未存在的用户事件关系计算分组并 upsert；已存在关系不得重复增加组计数。
5. 更新分组的最新事件、事件数、未读数和时间。
6. 原子更新 `NotificationInboxState` 的计数与 revision。
7. 返回真正新增的接收者、受影响分组和最新摘要；提交后再 best-effort 推送变更提示。

部分接收者已经存在时，仓储只补缺失关系，不重新插入已存在事件，也不因唯一冲突跳过剩余接收者。全部接收者被偏好过滤时，可靠任务仍以“已按偏好抑制”成功结束，不创建空通知事件。

### 5.2 聚合规则

首批只聚合高频、低风险且目标一致的类型：

| 类型 | 聚合窗口 | 维度 | 展示 |
| --- | --- | --- | --- |
| `PostLiked` | 6 小时 | 接收者 + 帖子 | 最近触发者 + 去重触发者数 |
| `CommentLiked` | 6 小时 | 接收者 + 评论 | 最近触发者 + 去重触发者数 |
| `PostQuickReplied` | 2 小时 | 接收者 + 帖子 | 最近触发者 + 回应数 |

评论回复、帖子新评论、聊天、关注、订单、权益、成长、治理和系统通知首批不聚合。聚合计数以已写入的用户事件关系为依据；业务事件幂等键不能复用聚合窗口键。点赞需要按触发者去重展示，同一用户取消后重新点赞的业务语义由点赞事件身份和关系状态测试明确，不能简单累加缓存。

聚合窗口采用以 UTC 时间片计算的确定性固定窗口；同一可靠事件重放始终落入同一分组，不因消费延迟或重试时钟改变边界。跨窗口事件形成新分组，页面不以滚动窗口猜测合并。

### 5.3 列表、摘要和已读

- 列表先查询 `NotificationInboxGroup`，再按最新通知 ID 定向读取月表，不按类型扫描全部分表。
- cursor 使用 `Revision + LastOccurredAtUtc + GroupId`。revision 不一致时要求刷新，避免组上浮导致漏项或重复。
- 分类、只看未读和时间范围在仓储查询中完成；总数和分类计数来自组 / 摘要查询，不使用当前 20 条前端切片。
- 标记组已读在事务内更新该组未读关系、组计数和摘要；并发到达的新事件必须保留未读。
- 全部已读可按全部或单个分类执行，并返回新的权威摘要。
- 删除是用户分组软删除；组内未读关系同步退出未读计数，但原始业务事件不物理删除。
- 所有时间写入使用 `TimeProvider` UTC，不再新增 `DateTime.Now / DateTime.UtcNow` 直取。

## 六、HTTP、SignalR 与客户端契约

### 6.1 HTTP API

Controller 统一使用 `AuthorizationPolicies.Client`，所有对象只允许当前租户、当前用户访问。新契约：

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/v1/Notification/GetInbox` | 按 category / onlyUnread / cursor 获取权威分组列表 |
| `GET` | `/api/v1/Notification/GetInboxSummary` | 获取 revision、未读组 / 事件数和分类计数 |
| `PUT` | `/api/v1/Notification/MarkInboxGroupsAsRead` | 批量标记当前用户分组已读 |
| `PUT` | `/api/v1/Notification/MarkAllAsRead` | 全部或按分类已读 |
| `DELETE` | `/api/v1/Notification/DeleteInboxGroup/{groupId}` | 删除当前用户分组 |
| `GET` | `/api/v1/Notification/GetPreferences` | 返回注册表支持分类与合并后的用户偏好 |
| `PUT` | `/api/v1/Notification/UpdatePreferences` | 批量 upsert 分类偏好 |

返回的 LongId 一律是字符串。写接口返回受影响行和最新 `NotificationInboxSummaryVo`，客户端不自行增减权威计数。

现有 `GetNotificationList / GetUnreadCount / MarkAsRead / DeleteNotification` 在 F4-B-C 完成 Web 与 Flutter 兼容迁移前保留并标记 deprecated；F4-B-D 验收后再决定删除，不在同一批强切全部消费者。

### 6.2 SignalR

保留 `/hub/notification` 用户组连接，只对外发送一个权威变化事件：

```text
NotificationInboxChanged
  revision
  unreadGroupCount
  unreadOccurrenceCount
  reason
  latestGroupId?
  realtimePreviewAllowed
```

- Hub 连接成功与重连后读取 `NotificationInboxState` 并发送当前摘要。
- 客户端收到更高 revision 后刷新摘要；当前列表打开时刷新第一页或展示“有新通知”动作。
- `revision <= localRevision` 的乱序 / 重复事件直接忽略。
- 删除 `NewNotification` 完整对象推送，以及 Hub 内只广播不落库的 `MarkAsRead / MarkAllAsRead` 命令。
- HTTP 写入成功后的 Service 推送所有同账号连接；推送失败只记录告警，数据库提交不回滚。
- 多实例 Backplane 不进入本专题。真正需要多 API 实例时再依据部署事实立项；断线期间 HTTP 对账已经保证正确性。

### 6.3 正式 Web

`/notifications` 调整为正式通知工作区：

- PC：主列为分组列表，侧栏承接权威摘要、分类和偏好入口。
- mobile：单列列表，分类使用横向 tabs / 筛选面板，偏好使用独立 sheet 或页面区段；不把桌面侧栏硬塞进窄视口。
- 列表展示服务端 category、kind、聚合数、最近触发者和结构化目标，不再扫描标题 / 正文归类。
- 分类计数、未读数和行动项总数来自服务端摘要；移除只基于最近 20 条生成的伪全量指标。
- 目标缺失时展示稳定原因和只读摘要，不把“复制诊断上下文”作为普通用户的主要处理动作。
- 断线时明确“实时提示离线，列表仍以最近 HTTP 数据可读”；重连依据 revision 对账。
- 中英文覆盖分类、聚合模板、偏好、目标失效、cursor 刷新、离线和错误状态。

Workbench 只消费摘要和少量最近分组；WebOS 复用同一 API / Store 并保持历史窗口入口。Flutter 只做新旧 API 兼容、forum 目标和单条已读回归，不新增偏好页面、SignalR 或系统通知栏推送。

## 七、权限、隐私与失败恢复

- 生产者必须提供租户和接收者；Service 拒绝 `userId <= 0`、跨租户接收者和未登记类型。
- 列表、摘要、偏好、已读和删除只使用当前登录用户，不接受任意 `userId` 参数。
- 分组 ID、通知 ID 和目标 ID 都不授予资源访问权；目标 Controller / 页面重新校验 ACL、软删除和阻断关系。
- 偏好只影响未来入箱，不追溯删除历史通知。强制通知绕过普通关闭开关必须有注册表和测试证据。
- 可靠 Outbox 重试依赖稳定事件 ID、业务键和发生时间；未知类型、损坏模板或非法目标属于永久失败，进入 DeadLetter，不能无休止重试。
- SignalR、缓存或 Toast 失败不影响事件入箱。HTTP 读取失败不得返回 `0` 或空列表冒充成功，必须保留结构化错误与重试动作。
- 迁移 verify 可从事件关系和分组重算摘要；若摘要漂移，doctor 报错并提供受控 rebuild 入口，不在普通请求中层层自愈。

## 八、容量与清理

首批保留策略：

- 已删除分组 30 天后可物理清理。
- 已读的 Discussion / Reaction / Message / Relationship / Growth 分组保留 180 天。
- Commerce / Governance / System 分组保留 365 天；账号安全可按注册表覆盖更长周期。
- 未读分组在保留期内不因用户总量上限被静默删除。
- 每用户事件关系软上限为 5000；优先清理满足保留期的已读 / 已删除关系。若仍超限，记录结构化维护告警，不自动把未读改成已读。

Hangfire 清理任务按小批次执行：先关系与空分组，再清理无任何关系引用的过期通知事件；月表只有在引用为零且整表越过最长保留期时才允许整表删除。清理必须有 SQLite / PostgreSQL 定向测试、重入、并发和计数守恒验证，不建设大而全数据平台。

## 九、实施批次

### F4-B-A：审计与设计（已完成）

- 交叉审计生产者、数据、Service / Repository、Controller、Hub、正式 Web、WebOS、Flutter、测试和历史文档。
- 固定分类、类型、目标、偏好、聚合、摘要、可靠性、容量、迁移和停止线。
- 建立本文档并把旧通知文档降级为历史参考。

### F4-B-B：数据、查询与服务端契约（已完成）

1. 落地定义注册表、稳定类型、模板和目标 DTO。
2. 实现 ledger migration、分组、摘要、偏好与索引。
3. 用专属仓储事务重写通知写入，修复部分接收者、事件幂等和聚合。
4. 移除 unread cache / cache dedup 的正确性职责和虚假 delivery 字段。
5. 提供新 HTTP API 与 revision SignalR 事件，保留旧 API 兼容。
6. 补 SQLite / PostgreSQL、Service、Repository、Controller、Hub 和可靠任务测试。

本批不改正式 Web 页面结构，只允许更新 `@radish/http` / client 类型以验证契约。

完成结论（2026-07-18）：稳定定义注册表、模板参数和结构化目标已覆盖现有通知生产者；Message 库事务已统一事件幂等、部分接收者补齐、偏好抑制、分组、权威摘要 revision 和容量清理。`20260718_003_notification_inbox_governance` 已覆盖 SQLite / PostgreSQL 历史迁移、重入与严格校验；新 HTTP / SignalR 契约及 `@radish/http` 类型已落地，旧接口仅保留到客户端兼容迁移完成。解决方案构建、后端全量测试、Baseline Quick、正式 Web 生产构建与仓库卫生检查通过；PostgreSQL migration / Outbox 在隔离数据库中定向通过。按本批停止线未改 `/notifications` 页面结构，也未启动宿主或执行浏览器 smoke。

### F4-B-C：正式 Web 通知工作区（已完成）

1. 先更新 Pencil 设计源，覆盖 PC / mobile 的列表、聚合、分类、偏好、离线和错误状态。
2. 改造 `/notifications`、共享 Store、Workbench 摘要与 WebOS 复用面。
3. 删除关键字分类和泛化目标回退，接入服务端 target / summary / revision。
4. 完成中英文、键盘、焦点、长文本、空态和移动窄屏。
5. 保持 Flutter 条件维护边界，只处理兼容阻断。

完成结论（2026-07-18）：Pencil 权威源已将 `P12 / P26` 更新为服务端分类、分组、触发者、聚合、未读筛选和新通知提示，并新增 `P12B / P26B / P26C` 覆盖偏好、空态、加载、错误、cursor 过期、离线恢复、长文本与目标失效。正式 Web `/notifications`、共享 Store、导航角标、Workbench 与 WebOS 复用面已迁移到 `GetInbox / GetInboxSummary / GetPreferences / UpdatePreferences`、分组写操作、结构化 target 和 `NotificationInboxChanged` revision；旧 API 继续保留到 F4-B-D。跨标签只广播账号隔离的 revision，筛选、重连、乱序 / 重复事件和 cursor 失效均回源刷新，不再本地模拟未读或扫描标题 / 正文 / `ExtData` 猜分类和目标。

本批已通过 client 全量 `425` 项测试、client / `@radish/http` / `@radish/ui` type-check、client lint、`radish.client` production build、后端通知定向 `25` 项测试（`1` 项 PostgreSQL 环境用例按配置跳过）、Baseline Quick 与 changed repo hygiene；未启动服务、未执行真实双账号或浏览器 smoke。F4-B-C 据此完成，但 F4-B 必须等待 F4-B-D 的真实生产链路与 `zh / en × PC / mobile` 成组验收后才能关闭。

### F4-B-D：回归与成组验收（已完成）

1. 执行通知生产、迁移、Service / Controller / Hub、客户端和仓库卫生定向回归。
2. 使用普通账号完成真实交互通知矩阵，至少覆盖 PC / mobile 与中文 / 英文。
3. 覆盖偏好抑制、聚合、已读 / 全部已读、删除、目标失效、断线重连和多标签页同步。
4. 记录临时账号 / 数据、清理方式、结果和未覆盖风险；按共同根因成组修复并补测试。
5. 完整通过后关闭 F4-B，再从总体规划选择下一专题。

完成结论（2026-07-18）：使用三个普通账号在 Gateway 正式路径完成关注、评论、回复、双触发者点赞聚合、陌生私信请求、真实商城购买、偏好抑制与恢复、聚合已读竞态、分类 / 全部已读、删除分组、cursor 过期、离线恢复、多标签同步和目标失效矩阵。`zh / en × 1920x1080 PC / 390x844 @ DPR 3 mobile` 的分类、模板、聚合、偏好、时间、键盘焦点、长文本和窄屏布局均通过；正式 Web、导航 / Workbench 摘要与 WebOS `/desktop` 使用同一服务端 summary / revision，未出现本地模拟计数、幽灵未读或错误归零。结构化目标已验证 forum 帖子 / 评论、公开用户、私信会话 / 消息和商城订单；软删除目标只保留可解释的失效状态，不再提供伪造链接。

验收按共同根因修复了四类问题：新增 `20260718_004_notification_delivery_cleanup` 前滚清理历史 delivery 列，并阻断非默认历史值；通知仓储对事件、分组、关系和状态写入严格校验受影响行数，避免数据库拒绝写入后可靠任务误判成功；关注恢复改为显式查询软删除关系，点赞任务携带稳定触发者名称；结构化目标在返回前按实体状态、所有权和 ACL 重新解析。WebOS 同时修复 NotificationHub 启动竞态与壳层 hydration 误停连接，旧 `UnreadCountChanged` 只触发权威对账。

Client 全量 `426` 项、后端通知 / 关注 / 可靠任务定向 `44` 项通过，`1` 项 PostgreSQL 环境用例按配置跳过；PostgreSQL 16 隔离容器中的 migration 与 Outbox 集成用例另行实跑 `2/2`。`@radish/http`、`@radish/ui`、client type-check、client lint、`radish.client` production build、解决方案构建、Baseline Quick 和仓库卫生均通过。验收账号、帖子、评论、点赞、关注、会话、通知、订单、偏好、浏览器凭据和临时容器已清理；六个本地数据库完成备份恢复、migration 重放、完整性与精确残留检查。旧通知 API 本批继续保留，删除前需另做消费者与 Flutter 兼容审计，不与验收修复混在同一提交。

## 十、验证与成组验收

### 10.1 静态与数据库验证

- 注册表完整性：每个生产者类型恰好一个定义，类型、分类、模板、目标和强制偏好互相一致。
- SQLite / PostgreSQL migration：旧数据回填、重复 / 孤立阻断、重入、checksum、索引、备份恢复和严格 verify。
- 仓储事务：首次写、可靠重放、部分接收者补齐、全部偏好抑制、并发同组写和事务回滚。
- 摘要一致性：新未读组、已读组再到达、单组已读、分类全部已读、删除未读和并发新事件。
- 聚合：同目标多触发者、同触发者重复、跨窗口、不同目标、事件幂等与展示计数。
- API / Hub：当前用户隔离、跨租户拒绝、LongId 字符串、cursor 过期、重连摘要、乱序 revision 和推送失败不回滚。
- client：目标构造、服务端分类、聚合文案、偏好、分页刷新、离线恢复和中英文。

### 10.2 真实交互矩阵

专题验收不能用页面可达或自动化测试替代真实业务动作。至少准备两个普通账号；验证多触发者聚合时增加第三个普通账号：

| 场景 | 真实动作 | 验收重点 |
| --- | --- | --- |
| 评论 / 回复 | B 评论 A 帖子并回复 A 评论 | 类型不混淆、目标精确到帖子 / 评论、已读正确 |
| 点赞聚合 | B、C 点赞 A 的同一帖子 / 评论 | 一个分组、触发者和计数正确、跨窗口不串组 |
| 关注 | B 关注 A | 关系分类、打开 B 公开主页、取消 / 重关注事件身份明确 |
| 私信请求 / 提及 | B 向 A 发请求并在有权频道提及 A | 不泄露私信正文、返回 `/messages` 精确会话 |
| 订单 / 权益 | A 完成真实可回滚测试购买或测试权益到期 | Commerce 分类与正式目标；不污染真实资产 |
| 偏好 | A 关闭 Reaction 后由 B 点赞，再恢复 | 关闭期间不入箱，恢复后新事件正常；历史不追删 |
| 聚合已读竞态 | A 标记组已读同时 B 产生新事件 | 新事件保持未读，摘要与列表一致 |
| 多标签 / 断线 | A 在两个标签和移动视口操作，主动断开 / 恢复网络 | revision 追平，不出现幽灵已读或计数归零 |
| 目标失效 | 产生通知后删除 / 撤销目标或访问权 | 通知仍可解释，不伪造链接，不绕过 ACL |
| 双语与视口 | `zh / en × PC / mobile` | 分类、模板、聚合、偏好、时间和错误布局稳定 |

验收结束后停止本轮服务，清理临时账号、帖子、评论、点赞、关注、会话、通知、订单 / 权益、偏好、浏览器凭据和备份，并对 Main / Message / Chat 等受影响数据库做精确残留和完整性检查。

## 十一、停止线与剩余风险

本专题不做：

- 邮件通知、短信、Web Push、PWA、移动系统通知栏或 Flutter 新通知页面。
- SignalR Redis Backplane、多区域推送、虚构万级并发压测或大而全通知可观测平台。
- Console 跨用户通知正文浏览、任意广播编辑器或营销触达平台。
- 私聊搜索、Reaction、消息置顶、逐条已读回执或聊天内容复制到通知中心。
- 通知月表合并、Message 分库重构、全仓时间重写或无关大文件拆分。
- 在专题验收前重启主动生产使用证据采集。

关闭后剩余风险：

- 历史 `ExtData` 可能无法完整转换为结构化目标，迁移必须保留 `None` 与兼容快照。
- `20260718_004` 会对非默认历史 delivery 字段值或异常跨月重复键明确阻断，生产前滚仍需坚持 doctor、备份和可恢复演练，不能静默丢弃未知状态。
- 月表数量长期增长仍会增加迁移和孤儿清理成本；只有真实容量证明当前方案不足时再评估分表归档或合并。
- 多 API 实例部署时单实例 SignalR 不能覆盖全部在线连接；本专题依靠 HTTP / revision 保证正确性，Backplane 必须依据实际部署拓扑另行立项。

## 十二、专题完成标准

满足以下条件后才能关闭 F4-B：

1. 所有真实生产者使用注册类型、模板和结构化目标，不再新增裸字符串和导航 `ExtData`。
2. 偏好、聚合、分组、权威摘要、容量清理和迁移在 SQLite / PostgreSQL 均通过测试。
3. 未读正确性不依赖缓存读改写，SignalR 断线不造成数据丢失或虚假已读。
4. `/notifications` 的分类、计数、目标和偏好来自服务端稳定契约，关键字猜测已删除。
5. 中英文、PC / mobile、多标签 / 重连和双账号真实交互矩阵通过。
6. 临时数据完整清理，仓库卫生、`git diff --check` 和专题定向验证通过。
7. 文档与当前规划明确记录结果、剩余风险和下一顺位。

2026-07-18 的 F4-B-D 已满足以上七项条件，F4-B 正式关闭。下一顺位进入 F4-C 聊天历史搜索与消息定位的现状审计和专题设计；消息 Reaction、置顶和移动系统通知继续分别后置。
