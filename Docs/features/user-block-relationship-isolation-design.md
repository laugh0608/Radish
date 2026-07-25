# F4-K 用户屏蔽与关系交互隔离

> **状态**：F4-K-A 已完成；下一顺位进入 F4-K-B 服务端权威契约
>
> **复核日期**：2026-07-25（Asia/Shanghai）
>
> **适用范围**：正式 Web 的公开主页、`/circle`、`/messages`、`/notifications` 与 `/me/blocked`；API、Main / Chat / Message 数据库和 `@radish/http`；Flutter / WebOS / Tauri 不新增实现
>
> **前置专题**：[个人圈子](/features/circle) · [一对一私聊与会话管理](/features/chat-direct-conversation-design) · [通知中心深化与通知治理](/features/notification-center-deepening)

## 一、结论摘要

F4-K 选择“用户屏蔽与关系交互隔离”为当前唯一完整功能专题。该能力不是在关注关系或 Direct 会话上增加一个布尔值，而是建立跨关注、私信、通知和登录态关系分发的统一用户安全策略。

核心裁决如下：

1. `UserBlock` 是 Main 库中方向明确的唯一屏蔽真相源；`BlockerUserId` 表示执行者，`BlockedUserId` 表示目标。
2. 屏蔽由一方发起和管理，但生效后双方进入对称的交互隔离：不能关注、发起或继续私信、回应、置顶、读取对方阅读回执或制造新的互动通知。
3. 屏蔽事务内软删除双方已有关注；取消屏蔽不自动恢复关注、私信请求或其他关系状态。
4. 既有 Direct 会话和消息历史保留、可查看、可举报，但在屏蔽期间只读；不删除历史消息，也不把屏蔽伪装成会话删除。
5. 屏蔽后的新关系型通知在创建前统一抑制；已产生的双方互动通知异步标记为不可见，读取端在异步收口前仍以 Main 当前关系作安全裁剪。
6. 系统、账号安全、资产、治理决定与申诉结果等非人际通知不受屏蔽影响。
7. `/circle` 动态、关注 / 粉丝列表和关系推荐排除任一方向的有效屏蔽；双方的公开帖子、评论和公开资料不因此删除。
8. 屏蔽不是内容隐私墙：匿名访问和其他用户仍可看到原本公开的内容；目标用户在登录态只得到通用“当前无法互动”状态，不获得“对方屏蔽了你”的明确披露。
9. Chat 库既有 `DirectConversation.BlockedByUserId / BlockedAt` 迁入 `UserBlock`，新运行时只写统一真相源；旧字段只允许在迁移兼容窗口读取，随后通过 contract migration 移除。
10. 本专题不建设管理员黑名单、账号封禁、论坛内容过滤、匿名公开聊天或论坛版本回滚，也不扩展 Flutter、WebOS 和 Tauri。

## 二、F4-K-A 候选复核与专题裁决

### 2.1 同口径审计结果

| 候选 | 已有基础 | 真实缺口 | 边界与风险 | 裁决 |
| --- | --- | --- | --- | --- |
| 用户屏蔽与关系交互隔离 | `/circle` 已有关注关系与关系流；公开主页有关注和私信动作；Direct 已有会话内阻断；通知有统一定义与可靠写入 | 没有跨关注、私信、通知和关系分发的一致用户安全策略；会话内阻断不能保护尚未建会话或其他入口 | 需要统一关系真相、双向关注处理、跨库 Chat / Message 协调和公开内容边界，但可沿既有正式主路径纵向完成 | **选定为 F4-K** |
| 匿名公开聊天 | 登录态 `/messages` 已覆盖 Public / Announcement / Private / Direct、搜索、Reaction、Pin 和阅读回执 | 没有匿名可进入的 `/chat` 公共房间 | 与公开论坛讨论和登录态 Public 频道重叠，并新增匿名 ACL、限流、SEO、实时连接和滥用治理暴露面 | 后置；不作为现有聊天补漏 |
| 论坛作者版本恢复 | Post / Comment 已有编辑历史、可靠提交键和历史查看页面 | 没有将历史版本恢复为当前内容的作者工具 | 历史没有完整保存分类、标签和附件状态，也没有内容版本 CAS；直接恢复会改写公开内容并破坏审计完整性 | 后置为“论坛版本完整性与恢复”专题 |

### 2.2 为什么屏蔽优先

现有关系路径已经允许用户关注、在圈子中复访、从公开主页发起私信，并通过通知中心接收提及、回复、点赞、关注和私信请求。Direct 的会话内阻断只在会话已经存在后生效，不能阻止对方从公开主页重新关注，也不能统一裁剪圈子、通知与其他互动入口。

这形成了真实的用户安全缺口：

- 用户无法在尚未建立 Direct 会话时阻止对方继续发起关系互动。
- 解除关注只改变单向订阅，不能表达“双方都不应继续互动”。
- Direct 屏蔽状态位于 Chat 库，关注和通知生产者不能把它作为统一策略。
- 各页面若分别判断，会产生公开主页不可私信但圈子仍推荐、通知仍送达等互相矛盾的结果。
- 已完成的内容治理解决公共规则和平台处置，但不能替代用户本人控制关系边界。

因此 F4-K 必须建立独立关系聚合和共享判定服务，并让已有模块消费同一结果。公开聊天与论坛回滚仍有潜在价值，但前者扩大匿名实时暴露面，后者缺少完整历史快照；二者不与本专题并行。

## 三、术语与边界

### 3.1 术语

- **屏蔽关系**：执行者到目标用户的一条方向关系，由执行者创建、解除和查看。
- **交互隔离**：任一方向存在有效屏蔽时，双方不能执行人际关系写操作，也不能看到对方产生的私域关系分发。
- **公开内容**：无需登录即可访问、原本就面向互联网展示的帖子、评论、公开主页字段和公开宠物名片。
- **关系型通知**：由另一个普通用户行为直接触发的关注、提及、回复、点赞、快速回复和 Direct 请求 / 消息通知。
- **系统型通知**：系统、账号安全、资产、商城、Docs 审核、治理决定和申诉结果等不应被另一普通用户静默阻断的通知。

### 3.2 目标

- 用户可从他人公开主页执行屏蔽，并在 `/me/blocked` 查看和解除自己建立的屏蔽。
- 所有关系与私信写操作在服务端统一校验，不能依赖按钮隐藏保证安全。
- 屏蔽后双方已有关注立即解除，圈子关系数据与分发结果一致。
- Direct 的 Pending / Accepted / Declined 及已有历史均有明确、稳定的屏蔽行为。
- 通知生产、列表、未读数和实时推送对屏蔽关系使用同一分类与裁剪规则。
- 公开内容、治理、租户、软删除与账号可用性边界保持不变。
- SQLite / PostgreSQL 历史迁移能够吸收现有 Direct 屏蔽并识别冲突数据。
- 正式 Web 在 PC / mobile、中英文、多主题、离线和多标签下形成可验收路径。

### 3.3 非目标

- 不承诺对方通过匿名访问、其他账号或搜索引擎无法看到公开内容。
- 不自动删除、隐藏或重写双方历史帖子、评论、私信和举报证据。
- 不把屏蔽扩展为管理员黑名单、IP 黑名单、Mute / Ban 或内容治理动作。
- 不建设帖子级“不感兴趣”、关键词过滤、推荐算法训练或全站内容屏蔽。
- 不新增匿名公开聊天、论坛版本恢复、账号私密模式或好友分组。
- 不增加 Flutter、WebOS 或 Tauri 页面，不恢复多端功能追平。

## 四、权威对象与不变量

| 对象 / 服务 | 权威职责 | 不承担 |
| --- | --- | --- |
| `UserBlock` | 谁屏蔽了谁、当前是否有效、创建与解除审计 | 当前是否关注、会话请求状态、通知投递状态 |
| `UserFollow` | 单向关注当前状态 | 屏蔽、隐私或聊天权限 |
| `DirectConversation` | 双方 Direct 会话、请求状态和历史频道关联 | 新屏蔽真相 |
| `IUserInteractionPolicyService` | 基于账号、租户与 `UserBlock` 给出统一交互能力 | 持久化关注、消息或通知 |
| `NotificationDefinitionRegistry` | 哪类通知需要屏蔽抑制 | 当前屏蔽关系 |
| 原公开领域对象 | 公开内容和资料的当前可见性 | 用户关系状态 |

必须长期保持以下不变量：

1. 同一租户、同一执行者、同一目标最多一条 `UserBlock` 当前记录；软删除行恢复时不得另建重复关系。
2. 用户不能屏蔽自己，不能跨租户建立关系，也不能对不存在、软删除或不可用账号建立新关系。
3. 任一方向存在有效屏蔽时，`HasInteractionBarrier(A, B)` 对 A、B 返回相同结果。
4. 屏蔽与双方关注解除在 Main 库同一事务内完成；事务失败不得留下“已屏蔽但仍关注”的可见状态。
5. 解除屏蔽只软删除执行者拥有的 `UserBlock`，不删除对方独立建立的屏蔽。
6. 解除屏蔽不恢复任何关注、Direct 请求、通知或旧页面状态。
7. Chat 和 Message 数据库中的投影、任务或兼容字段都不能成为屏蔽真相源。
8. 所有写 API 必须重新读取服务端权威关系；前端 `VoCan*` 只用于展示，不能授权写入。
9. 屏蔽目标不能从通用错误、状态字段或通知推断另一方是否屏蔽了自己。
10. 租户、软删除和用户可用性过滤先于关系策略；屏蔽不能穿透既有安全边界。

## 五、数据模型

### 5.1 `UserBlock`

Main 库新增独立实体：

- `Id`
- `TenantId`
- `BlockerUserId`
- `BlockedUserId`
- `CreateTime / CreateId / CreateBy`
- `ModifyTime / ModifyId / ModifyBy`
- `IsDeleted / DeletedAt / DeletedBy`

索引与约束：

```text
(TenantId, BlockerUserId, BlockedUserId) UNIQUE
(TenantId, BlockerUserId, IsDeleted, CreateTime DESC, Id DESC)
(TenantId, BlockedUserId, IsDeleted)
CHECK (BlockerUserId <> BlockedUserId)
```

唯一约束覆盖软删除行，重复屏蔽通过恢复原记录实现。API 不接收原因、内部备注或自由文本，避免把敏感私人判断写入长期关系数据。

### 5.2 可靠 Outbox 任务

复用既有 Main `ReliableOutboxMessage` 与 `IReliableOutboxRepository`，不为屏蔽另建平行 Outbox 表。Block / Unblock 事务内按稳定任务类型和业务键追加可靠任务，payload 至少包含：

- `TenantId / UserBlockId`
- `EventType`：`Blocked / Unblocked`
- `BlockerUserId / BlockedUserId`
- `RelationshipVersion`
- `OccurredAtUtc`

首批 `Blocked` 事件用于：

- 将双方既有关系型通知标记为不可见；
- 使 Chat 兼容投影失效并触发相关 Hub 客户端刷新；
- 记录跨库收口完成状态。

`Unblocked` 只发送关系失效提示和客户端刷新，不恢复历史通知或会话写状态。Outbox 是可靠传递机制，不是权限真相源。

### 5.3 版本与幂等

- `Block / Unblock` 接收 `operationKey`，租户和执行者范围内唯一。
- 同键同目标、同操作返回原结果；同键异目标或异操作返回 `409`。
- Block 响应包含关系版本和当前用户视角能力，不返回对方是否也建立了屏蔽。
- 关系版本单调递增，用于 Outbox、Hub 失效事件和客户端重新拉取；客户端不得自行合并为权限结论。

## 六、统一关系策略

### 6.1 服务职责

新增 `IUserInteractionPolicyService`，由专属 Repository 批量查询 Main 当前关系，至少提供：

- 单对用户关系快照；
- 当前用户对一组用户的屏蔽 / 隔离摘要；
- 给定用户集合排除任一方向屏蔽后的结果；
- 面向服务端写操作的 `EnsureCanInteractAsync`；
- 只向本人暴露的 `IsBlockedByCurrentUser`。

Service 层不得直接访问 `_repository.Db` 或复制 SQL。关注、圈子、Direct、Reaction、Pin、阅读回执、通知等消费者只依赖统一服务或稳定的批量策略接口。

### 6.2 能力快照

内部快照可区分：

- `None`
- `BlockedByCurrentUser`
- `BlockedCurrentUser`
- `Mutual`

对执行者本人可以返回 `BlockedByCurrentUser`，用于显示“解除屏蔽”。对目标用户及普通公开接口只返回：

- `CanFollow`
- `CanDirectMessage`
- `CanInteract`
- 通用 `InteractionUnavailable`

不得向目标用户返回 `BlockedCurrentUser`、执行者、时间或方向。

### 6.3 失败策略

- 关注、私信、Reaction、Pin 和回执等人际写操作在无法确认 Main 权威关系时失败关闭，返回可重试错误。
- 公开内容读取不因关系服务短暂不可用而变成 500 或误判为私密。
- 列表批量裁剪失败时不得返回未经裁剪的圈子、关系或通知结果。
- 不增加“查不到就允许”“缓存过期仍允许”或各模块自行 fallback。

## 七、行为矩阵

### 7.1 关注与圈子

| 场景 | 屏蔽生效后 | 解除屏蔽后 |
| --- | --- | --- |
| A 关注 B | 软删除 | 不恢复；A 可重新发起关注 |
| B 关注 A | 软删除 | 不恢复；B 可重新发起关注 |
| A / B 再次关注 | 服务端拒绝，返回通用不可互动 | 仅在双方均无有效屏蔽时允许 |
| 关注 / 粉丝列表 | 双方互相排除 | 只显示后来真实建立的关系 |
| Following feed | 排除对方内容 | 不回补历史分页，只影响后续查询 |
| 推荐 / 最新 / 热门关系分发 | 排除对方用户和关系项 | 后续查询可重新进入正常候选 |

Block Service 必须在同一 Main 事务内恢复或创建 `UserBlock`、软删除双方 `UserFollow` 并写 Outbox。不能先删关注再尝试写屏蔽，也不能由前端串行调用 unfollow。

### 7.2 Direct 会话

| Direct 状态 | 屏蔽行为 | 历史 |
| --- | --- | --- |
| 尚无会话 | 不能创建请求或首条请求消息 | 无 |
| Pending | 请求停止处理，双方不能 Accept / Decline / Send | 已存在请求消息保留 |
| Accepted | 会话进入只读，双方不能 Send / React / Pin | 消息、附件和举报入口保留 |
| Declined | 不能重新发起或发送 | 既有摘要保留 |
| 已归档 | 归档状态保持，不能借恢复会话绕过屏蔽 | 历史保留 |

补充规则：

- 屏蔽不改变 `RequestStatus`，避免把关系策略写入会话生命周期。
- 屏蔽期间不推进对方阅读游标，不向发送者返回对方阅读回执摘要。
- 既有附件继续按历史会话成员 ACL 访问；不能借屏蔽扩大或收回本来合法的历史证据访问。
- 举报既有 Direct 消息继续可用，治理人员仍按原授权查看。
- 解除屏蔽后，Accepted 会话可以重新发送；Pending / Declined 仍遵循原请求状态，不自动接受或重开。

### 7.3 Reaction、Pin 与阅读回执

- Direct 消息上的新增 / 删除 Reaction 均视为互动写操作，屏蔽期间拒绝。
- Direct 消息置顶的新增 / 取消均拒绝；屏蔽不自动删除既有置顶记录。
- 历史 Reaction 和 Pin 继续作为会话事实显示；它们不会产生新的通知。
- 阅读回执不再推进，也不向任一方展示对方当前回执；解除屏蔽后只从新的真实阅读行为继续推进。
- Public / Announcement / 普通 Private 频道中的共同参与不在首批按用户关系过滤消息、Reaction 或 Pin；频道规则继续是权威 ACL。

### 7.4 公开内容与公开主页

- Post、Comment、PostQuickReply、公开宠物名片和公开主页白名单字段维持原公开可见性。
- 屏蔽不软删除内容，不改变 SEO、sitemap、canonical 或匿名访问。
- 登录态公开主页仍可查看公开字段，但关注、私信及其他关系动作按能力快照禁用。
- 执行者本人看到“已屏蔽，可解除”；目标用户只看到通用不可互动状态。
- 本专题不在论坛列表、详情或评论树隐藏对方公开内容；如未来需要内容过滤，必须单独设计帖子级和作者级偏好、分页一致性与匿名边界。

## 八、通知抑制

### 8.1 定义注册

`NotificationDefinitionRegistry` 增加稳定的关系抑制属性，例如 `SuppressWhenInteractionBlocked`。首批纳入：

- Followed；
- DirectMessageRequested；
- ChatMentioned；
- 帖子 / 评论点赞；
- 帖子评论、评论回复、快速回复；
- 其他明确由 `TriggerId` 普通用户行为直接产生的互动通知。

以下类型不得被屏蔽抑制：

- System / Account / Security；
- Asset / Shop 等业务结果；
- Docs 审核、协作资格变化等权威工作流结果；
- Moderation / Appeal；
- 没有普通用户行为主体的系统聚合通知。

新增通知类型必须显式声明该属性，不能依赖类别名、字符串前缀或调用方猜测。

### 8.2 创建、历史与读取

1. `NotificationService.CreateNotificationAsync` 在定义要求抑制且存在明确 `TriggerId` 时，批量排除与触发者存在交互隔离的接收者。
2. Block Outbox 消费者将双方已经创建的关系型通知标记为 `SuppressedByUserBlock`；不物理删除，不改写原 payload。
3. 通知列表、摘要、分组、未读数和实时推送都排除被抑制记录。
4. 在 Outbox 尚未完成的短暂窗口，读取端按 Main 当前关系再次裁剪，保证屏蔽响应返回后不继续暴露旧互动通知。
5. Unblock 不恢复被抑制通知，也不补发屏蔽期间丢弃的通知。
6. 通知抑制失败可以重试；失败状态不得反向撤销已生效的 `UserBlock`。

## 九、接口与错误契约

### 9.1 HTTP

新增独立 `UserBlockController`，所有接口要求登录：

```text
POST /api/UserBlock/Block
POST /api/UserBlock/Unblock
GET  /api/UserBlock/GetMine?pageIndex=&pageSize=
```

请求与响应原则：

- Block / Unblock 接收目标用户 PublicId 或项目既有安全用户标识与 `operationKey`，不接受客户端 TenantId、执行者 Id 或当前关系状态。
- GetMine 只返回当前用户自己建立的有效屏蔽，不提供“谁屏蔽了我”接口。
- 列表 VO 使用 `Vo` 前缀，返回目标公开资料白名单、屏蔽时间和解除能力。
- 公开主页、关注状态和 Direct 摘要扩展 `VoCan*`，但不改变匿名接口的公开字段。

### 9.2 稳定错误

至少固定：

- `UserBlock.SelfNotAllowed`
- `UserBlock.TargetUnavailable`
- `UserBlock.InteractionUnavailable`
- `UserBlock.OperationConflict`
- `UserBlock.StateConflict`
- `UserBlock.RelationshipTemporarilyUnavailable`

对目标用户隐藏屏蔽方向的接口统一使用 `InteractionUnavailable`。日志可以记录内部原因、租户和追踪标识，但不得记录私人原因、消息正文或完整通知 payload。

### 9.3 权限

- 普通登录用户只能管理自己作为 `BlockerUserId` 的记录，不新增 Console 权限。
- 管理员不能代用户屏蔽、解除或查询私人屏蔽列表。
- 治理人员处理合法举报时仍按治理权限访问证据，不受普通用户关系策略影响。
- 跨租户访问返回既有安全语义，不能通过屏蔽接口枚举其他租户用户。

## 十、迁移与兼容

### 10.1 Expand migration

1. Main 库创建 `UserBlock`、索引和约束；跨库任务复用既有可靠 Outbox 结构。
2. 扫描 Chat 库有效 `DirectConversation.BlockedByUserId`，按租户和参与者验证执行者确为成员。
3. 将每条合法旧状态幂等写入 Main `UserBlock`；重复对合并为一条方向关系，保留最早可解释屏蔽时间。
4. 对旧屏蔽双方执行关注一致性修复，并生成通知 / Chat 收口任务。
5. 非成员执行者、跨租户用户、缺失用户或自屏蔽数据进入 doctor 阻断报告，不静默猜测。
6. apply 重入不得重复创建关系、重复删除关注或重复制造 Outbox 副作用。

### 10.2 运行时兼容

- Expand 发布后所有新 Block / Unblock 只写 `UserBlock`。
- Chat 在兼容窗口读取 Main 真相；仅当数据库迁移版本尚未宣告完成时，旧字段可以作为只读兼容输入。
- 旧 `DirectConversation/Block` 与 `/Unblock` HTTP 入口在 Web / API 迁移期转发到统一服务，返回新契约；不得继续写旧字段。
- 前端改用新 UserBlock API 后，旧接口进入废弃清单。

### 10.3 Contract migration

只有满足以下条件后才能移除 `BlockedByUserId / BlockedAt`：

- 六库 verify 证明所有旧屏蔽均已映射；
- 新旧 HTTP 调用计数在约定观察窗口内为零；
- Chat、`@radish/http` 和正式 Web 已无旧字段读取；
- Block / Unblock / Reblock、Direct 各状态及通知收口已完成成组验收；
- 备份恢复与 migration 重入通过。

Contract migration 只删除旧字段和兼容读取，不改变 `UserBlock` 事实或 Direct 历史。

## 十一、页面与交互

### 11.1 Pencil 先行

F4-K-C 在代码实现前更新现有公开主页、圈子和消息设计源，并新增 `/me/blocked` PC / mobile 画板。必须覆盖：

- 公开主页二级操作中的“屏蔽 / 解除屏蔽”；
- 屏蔽确认框对关注解除、私信只读和公开内容仍可见的明确说明；
- `/me/blocked` 分页列表、空态、解除确认、失败和重试；
- `/messages` 既有历史、只读提示、举报入口和解除入口；
- `/circle` 列表和动态在关系变化后的稳定空态；
- 中英文长文案、键盘、焦点、读屏和四主题。

屏蔽不应做成高频主按钮，也不能使用具有平台封禁含义的图标或文案。

### 11.2 客户端状态

- 屏蔽成功后统一失效公开主页关系摘要、circle feed / lists、Direct 摘要、通知列表 / 未读数和相关用户缓存。
- Hub 只广播“关系版本已变化”的最小事件，客户端收到后重新拉取权威状态；事件不携带屏蔽方向。
- 多标签通过现有共享状态机制同步失效，不能只修改发起标签的按钮。
- 离线提交使用 operation key；恢复网络后重复请求不得重复生成关系或通知任务。
- Back / Forward 不恢复屏蔽前的可写能力，页面必须以服务端版本重新校准。

## 十二、容量、隐私与安全

- GetMine 按 `(TenantId, BlockerUserId, IsDeleted, CreateTime DESC, Id DESC)` 稳定分页，默认 20、上限 100。
- 圈子、关注列表、推荐和通知使用批量关系裁剪，禁止逐行查询双方关系。
- 交互策略可以使用请求级短生命周期缓存，但 Block / Unblock 成功后必须按用户对失效；持久缓存不能成为授权依据。
- 任何公共 API 都不返回屏蔽方向、时间和执行者；只有执行者自己的私域列表可读取这些字段。
- 操作审计记录目标标识、结果和追踪信息，不记录屏蔽理由；普通管理员没有私人列表查询入口。
- 对不存在或不可访问目标的响应遵循既有防枚举策略，不能用不同错误确认账号存在。
- 高频 Block / Unblock 使用既有认证用户写操作限流；不新增验证码或风控平台。

## 十三、开发批次

### F4-K-A：候选审计与权威设计（已完成）

- 交叉复核用户屏蔽、匿名公开聊天和论坛作者版本恢复。
- 选定用户屏蔽与关系交互隔离为唯一专题，记录其余候选后置原因。
- 固定真相源、交互矩阵、通知分类、Direct 兼容、迁移、接口、页面、验证和停止线。
- 本批不修改业务代码、migration、Pencil 或正式页面。

### F4-K-B：服务端权威契约

- 完成 Main `UserBlock` migration、可靠 Outbox 任务类型、Chat 历史回填、doctor / apply / verify 与双数据库回归。
- 新增专属 Repository 和 `IUserInteractionPolicyService`，建立 Block / Unblock 幂等与关注事务。
- 接入关注、圈子查询、Direct、Reaction、Pin、阅读回执和通知创建 / 读取。
- 迁移旧 Direct HTTP 行为并建立兼容退役证据，不提前删除旧字段。
- 补充稳定错误、HTTP 示例、`@radish/http`、批量性能和服务端测试。
- 按停止线不修改 Pencil 或正式页面。

### F4-K-C：Pencil 与正式 Web

- 先更新公开主页、`/circle`、`/messages` 和 `/me/blocked` PC / mobile 设计。
- 实现屏蔽确认、本人屏蔽列表、解除屏蔽、Direct 只读和通用不可互动状态。
- 完成关系与通知缓存失效、多标签 / 离线恢复、Back / Forward、中英文、键盘、无障碍和四主题。
- 不新增 WebOS、Flutter 或 Tauri 功能，不执行完整 Gateway 成组验收。

### F4-K-D：成组验收与专题关闭

- 使用至少三名普通用户覆盖 A 屏蔽 B、B 屏蔽 A、双方屏蔽、解除单边和再次屏蔽。
- 覆盖双方关注、无 Direct、Pending / Accepted / Declined / Archived、历史消息、Reaction、Pin 和阅读回执。
- 覆盖关系型 / 系统型通知、通知历史收口、Outbox 失败重试、未读数和实时失效。
- 覆盖公开内容匿名可见、登录态动作隔离、圈子分发、目标用户不可推断方向和跨租户。
- 覆盖 `zh / en × PC / mobile`、多标签、离线、Back / Forward、键盘、无障碍和四主题代表矩阵。
- 清理临时关系、会话、消息、通知、Outbox、账号与备份，检查六库完整性并执行严格 migration verify。

## 十四、验证矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| Model / migration | 空库、旧 Direct 屏蔽库、重复方向、非法执行者、重入、备份恢复、doctor / verify、contract 前置条件 |
| Repository | 恢复软删除关系、并发 Block / Unblock、双方关注原子删除、operation key、分页与批量双向查询 |
| Policy / Service | 自屏蔽、目标失效、单边 / 双边屏蔽、解除单边、Reblock、跨租户、关系服务失败关闭 |
| Circle / Follow | 两方向关注、列表、feed、推荐、并发关注与屏蔽竞争、解除后不恢复 |
| Direct | 无会话、Pending / Accepted / Declined / Archived、历史只读、附件、举报、解除后状态保持 |
| Chat 互动 | Reaction / Pin 写入拒绝、历史保留、阅读游标不推进、回执不披露、普通频道边界不扩大 |
| Notification | 定义分类、创建抑制、历史收口、未读数、聚合、实时、Outbox 重试、系统通知不受影响 |
| API / 隐私 | 本人列表、PublicId、403 / 404 / 409、稳定错误、防枚举、目标用户不获知方向 |
| Client | 公开主页、`/me/blocked`、circle、messages、notifications、缓存失效、离线和多标签 |
| 运行态 | 三账号、双语、PC / mobile、多主题、匿名公开内容、跨租户、清理与六库完整性 |

## 十五、停止线

- 不把 `UserBlock` 合并进 `UserFollow`、`DirectConversation`、用户状态或治理状态。
- 不长期双写 `UserBlock` 与 `DirectConversation.BlockedByUserId`，也不允许两个读取结果竞争。
- 不由前端串行调用 unfollow、block 和 notification cleanup 来模拟服务端事务。
- 不把按钮隐藏、客户端缓存或 Hub 事件当作权限校验。
- 不因屏蔽删除公开内容、Direct 历史、Reaction、Pin、附件或举报证据。
- 不向目标用户、管理员或公共 API 暴露私人屏蔽方向和时间。
- 不用字符串匹配判断通知是否应抑制，不让各通知生产者自行维护例外。
- 不在 Main 查询失败时放行人际写操作，也不让 Chat / Message 投影升级为真相源。
- 不顺带建设匿名公开聊天、论坛版本回滚、内容“不感兴趣”、账号私密模式或推荐算法。
- 不新增 Flutter、WebOS、Tauri 功能或主动生产行为采集。

## 十六、完成标准

1. 用户可以稳定屏蔽、解除和再次屏蔽另一用户，软删除恢复、并发与重复请求不制造重复事实。
2. 屏蔽与双方关注解除保持 Main 事务一致，解除后不自动恢复任何关系。
3. 关注、圈子、Direct、Reaction、Pin、阅读回执和通知统一消费同一关系策略。
4. 旧 Direct 屏蔽可解释地迁入 Main，兼容期只有一个写入真相，contract migration 具备证据门槛。
5. Direct 各状态在屏蔽期间只读且历史、附件和举报证据保持完整，解除后仍遵循原请求状态。
6. 关系型通知被抑制，系统型通知保持送达，异步失败可重试且读取窗口不泄露。
7. 公开内容与匿名访问契约不被伪装为隐私能力，目标用户不能从正式接口推断屏蔽方向。
8. 正式 Web 的公开主页、`/me/blocked`、circle、messages 和 notifications 在 PC / mobile、中英文、多标签与离线恢复下可用。
9. SQLite / PostgreSQL migration、doctor、apply、verify、重入、备份恢复和相关数据库完整性通过。
10. 定向测试、前后端静态门禁、成组运行态验收和临时数据清理全部通过。
