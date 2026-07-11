# P3-12-F Q1-A 事务后可靠任务审计与实施方案

> 状态：`审计、方案与首批实现已完成；等待审阅`
>
> 日期：`2026-07-11`（Asia/Shanghai）
>
> 范围：只覆盖 Q1-A 的事务后可靠任务，不包含 Q1-B、Q2、Q3、页面调整、服务启动、部署或无关重构。

## 摘要

全仓代码盘点确认，正式 Web 发布矩阵内共有 14 处实际裸 `_ = Task.Run`。其中 12 处继续执行奖励、萝卜币、经验或通知持久化，进程终止会直接丢失业务结果；1 处是可重算的评论高亮；1 处混合未读缓存刷新与 SignalR 在线推送。所有 14 处都捕获了当前 Service / Repository 等 scoped 依赖，没有建立新的依赖作用域。

此外，通知持久化虽然在部分调用点被 `await`，但来源业务数据位于 `Main` 或 `Chat`，通知位于独立 `Message` 数据库，且部分调用方捕获异常后继续返回成功，因此仍存在来源业务已提交、通知尚未持久化的中断窗口。`NotificationService` 自身又分两步写入 `Notification` 与 `UserNotification`，当前没有事务和数据库级幂等键。

建议采用“源库事务内 Outbox + Hangfire 持久执行”的组合：Outbox 负责保证业务事实与待处理任务同生共死，Hangfire 负责调度、进程恢复和执行承载；任务写端继续以数据库唯一键保证幂等。SignalR 只做 best-effort，不参与业务提交判定。

方案经确认后已按本记录边界实施：14 处裸 fire-and-forget 均已移除，Main / Chat 同构 Outbox、可靠处理器、Message 通知事务、重试 / DeadLetter、受权人工重放 API 与定向测试已落地；没有新增页面，也没有改变订单资产与权益同步完成的边界。

## 一、审计范围与判定口径

本轮检查了：

- C# 中的 `_ = Task.Run`、未等待异步调用、`async void`、`ContinueWith`、线程池、请求完成回调和自建后台线程。
- Hangfire 注册、Job 实现、依赖作用域创建和现有事务入口。
- 前端 `void promise`、timer 和轮询。它们属于事件处理、资源预加载或读取刷新，不会在服务端请求结束后继续持有业务 Repository，不纳入 Q1-A 运行时迁移。
- 奖励、经验、萝卜币、订单、背包、权益、通知及未读缓存的幂等和事务现状。

以下已等待的 `Task.Run` 不属于 fire-and-forget：`FileCleanupJob` 的文件移动、`LocalFileStorage` 的文件删除，以及 `TranAop.SuccessAction` 的空操作。它们不进入本批。

## 二、裸 fire-and-forget 清单

| 位置 | 当前后台工作 | 分类 | 风险与结论 |
| --- | --- | --- | --- |
| `NotificationService.cs:99` | 增量未读缓存并通过 SignalR 推送未读数和通知内容 | 可重算派生数据 + 可丢失实时推送 | 持久通知已先写库；缓存应从库刷新，SignalR 允许丢失，但当前任务捕获请求 scope，且增量重放会造成缓存重复加一 |
| `ExperienceService.AdminAdjustments.cs:144` | 管理员调经验后的升级奖励与升级通知 | 不可丢失业务写 | 可能丢失萝卜币与持久通知；升级奖励当前没有业务幂等键 |
| `Jobs/CommentHighlightJob.cs:207` | 神评点赞加成萝卜币 | 不可丢失业务写 | 已在 Hangfire Job 内却再次裸起 Task；外层 Job 可先成功，子任务随后丢失 |
| `Jobs/CommentHighlightJob.cs:235` | 神评首次经验 | 不可丢失业务写 | 同上；经验写已有业务键，但任务事实不可靠 |
| `Jobs/CommentHighlightJob.cs:437` | 沙发点赞加成萝卜币 | 不可丢失业务写 | 同上 |
| `Jobs/CommentHighlightJob.cs:465` | 沙发首次经验 | 不可丢失业务写 | 同上 |
| `Posts/PostService.Publish.cs:78` | 发帖经验与首次发帖奖励 | 不可丢失业务写 | `PublishPostAsync` 已有事务，但奖励任务不在事务内；延迟统计 `PostCount == 1` 还会因后续发帖而漏掉首次奖励 |
| `Posts/PostService.Interaction.cs:176` | 帖子点赞双方萝卜币、经验和被点赞通知 | 不可丢失业务写 | 点赞关系在专属仓储事务提交后才起任务，存在提交后丢任务窗口 |
| `CommentService.cs:156` | 评论、首次评论、被回复奖励及回复 / 帖子评论通知 | 不可丢失业务写 | 评论主写完成后起任务；任何中断都可能造成部分奖励或通知缺失 |
| `CommentService.cs:445` | 实时重算神评 / 沙发 | 可重算派生数据 | 高亮本身可由定时扫描重算；但重算产生的奖励不能继续 fire-and-forget |
| `CommentService.cs:485` | 评论点赞双方萝卜币、经验和被点赞通知 | 不可丢失业务写 | 与帖子点赞相同，专属仓储事务和任务事实之间有窗口 |
| `CommentService.cs:1522` | 新高亮的基础萝卜币与经验 | 不可丢失业务写 | 高亮已写入后才起任务；重算看到高亮已存在时不一定再次补发 |
| `CommentService.cs:1554` | 高亮点赞增量萝卜币 | 不可丢失业务写 | 当前奖励写有到达点赞数业务键，但任务仍可能根本没有执行 |
| `ExperienceService.cs:443` | 普通经验发放触发的升级奖励与升级通知 | 不可丢失业务写 | 可能丢失萝卜币与持久通知，并捕获经验服务当前 scope |

结论：14 处中有 12 处必须迁移到可靠任务；评论高亮重算可以作为 Hangfire 可重算任务；通知 SignalR 推送保持 best-effort，但必须与持久通知、缓存正确性解耦。

## 三、相关链路的既有可靠性

### 1. 已有可复用基础

- `CoinTransaction` 已有 `(TenantId, RewardBusinessKey)` 唯一索引，`GrantCoinOnceAsync` 能在唯一冲突后读取既有成功流水。
- `ExpTransaction` 已有 `(TenantId, RewardBusinessKey)` 唯一索引，`GrantExperienceOnceAsync` 具备同类保护。
- 订单权益以 `(TenantId, SourceOrderId)` 唯一；背包发放另有 `UserInventoryGrantRecord` 的 `(TenantId, SourceOrderId)` 唯一记录。
- `PurchaseAsync` 已有事务与操作幂等记录；订单、扣币、权益 / 背包不是当前 fire-and-forget，不应为了本批而改成后台完成。
- Hangfire 已有独立 SQLite / PostgreSQL 存储、Server、Dashboard 和 recurring job 基础。
- 未读数能够通过 `RefreshUnreadCountAsync` 从 `Message.UserNotification` 重建，不需要把缓存增量当作可靠业务事实。

### 2. 仍需补齐的缺口

- `HandleLevelUpAsync` 使用普通 `GrantCoinAsync`，重复任务会重复发放升级币。
- `Notification` 和 `UserNotification` 没有通知业务幂等键；两步写也未处于明确的 `Message` 库事务。
- 点赞关系由 `PostRepository / CommentRepository` 内部事务提交，Service 随后才知道是否需要奖励；当前无法把任务事实与点赞状态变化原子提交。
- `AddCommentAsync` 没有覆盖评论、计数、附件绑定和任务事实的统一事务。
- 评论高亮替换、基础奖励任务和增量奖励任务之间没有原子边界。
- 多处通知调用虽然被等待，但来源写与 `Message` 库通知写无法形成单库原子事务；购买、关注、轻回应等链路还会吞掉通知失败并继续成功。

## 四、目标架构与 Outbox / Hangfire 边界

### 1. 可靠链路

```text
Main / Chat 源业务事务
  ├── 写业务事实
  └── 同库写 ReliableOutboxMessage
          │
          ▼
Hangfire OutboxDispatcher（只传 Source + OutboxId）
          │
          ▼
Scoped ReliableTaskJob
  ├── 幂等写奖励 / 资产 / 权益
  ├── 幂等写 Message 通知
  └── 标记 Outbox Succeeded；失败则记录并按策略重试

Message 持久通知成功
  ├── 刷新未读缓存（可重算）
  └── SignalR 推送（best-effort）
```

### 2. 必须使用 Outbox 的场景

- 任务是否存在必须与来源业务事实一起提交：发帖、评论、点赞、关注、轻回应、聊天提及、开奖、订单完成、经验升级和评论高亮变化。
- 来源事实落在 `Main` 时，Outbox 同表结构写入 `Main`；聊天消息来源落在 `Chat` 时写入 `Chat`。Dispatcher 按 `SourceDatabase` 扫描，不做跨库事务。
- 业务事务回滚时 Outbox 一并回滚；业务事务提交后即使进程在 Hangfire 入队前终止，Dispatcher 仍能重新发现任务。

### 3. 可以直接使用 Hangfire 的场景

- 没有来源业务事务、任务记录本身就是事实的定时扫描、文件清理、超时订单扫描和人工维护任务。
- 可重算评论高亮可由 Hangfire 触发；但当高亮变化已经形成新的奖励资格时，必须在高亮写事务内追加 Outbox，不能在 Job 中再次 `_ = Task.Run`。
- 已经处于 Hangfire Job 内不代表子任务可靠。不可丢失子写要么被当前 Job `await` 且全程幂等，要么由同一业务事务产生 Outbox。

### 4. 不使用 Outbox / Hangfire 的场景

- SignalR 在线消息、未读数变更推送和客户端即时刷新。它们允许 best-effort；客户端重连后从持久通知和数据库未读数恢复。
- 未读缓存本身是派生数据。通知落库后清除或按数据库重新计算缓存，不采用可重复执行的 `+1` 作为真相。

## 五、事务边界

| 来源链路 | 建议事务边界 | 事务内 Outbox |
| --- | --- | --- |
| 发帖 | 复用 `PublishPostAsync [UseTran]`，帖子、附件引用、分类计数、标签 / 投票 / 问答 / 抽奖与任务事实同事务 | `ForumPostPublishedRewards` |
| 评论 | 为 `AddCommentAsync` 建立 Main 库事务，评论、父评论回复数、帖子评论数、附件引用与任务事实同事务 | `ForumCommentPublishedEffects` |
| 帖子 / 评论点赞 | 调整专属仓储事务，使点赞关系、聚合计数与不可变事件草稿在同一仓储事务落库；不在事务外 enqueue | `ForumPostLikedEffects` / `ForumCommentLikedEffects` |
| 经验升级 | 用户经验、每日统计、经验流水与升级事件同事务；Outbox 引用触发它的 `ExpTransactionId` | `ExperienceLevelChanged` |
| 评论高亮 | 每个帖子 / 父评论的高亮替换、点赞快照和对应奖励事件同一个 Main 库事务 | `CommentHighlightRewardQualified` / `CommentHighlightBonusQualified` |
| 订单完成 | 保持扣库存、订单、扣币、权益 / 背包和操作幂等的同步事务，不异步化核心资产；只把购买成功通知事实放入同事务 Outbox | `NotificationRequested` |
| 关注、轻回应、开奖 | 来源关系 / 回应 / 开奖事实与通知 Outbox 同 Main 事务 | `NotificationRequested` |
| 聊天提及 | 聊天消息与提及通知 Outbox 同 Chat 事务 | `NotificationRequested` |
| 通知落库 | `Notification` 与全部 `UserNotification` 在明确的 Message 库事务内完成；任一步失败整体回滚 | 目标库不再产生第二层 Outbox；成功后仅做可丢失推送 |

不引入分布式事务，也不让一个请求事务跨 `Main / Chat / Message / Hangfire`。跨库一致性由源 Outbox、目标写幂等和最终完成状态保证。

## 六、任务载荷与版本

通用信封只保存可序列化的值，不保存实体、Repository、Service、`HttpContext`、ClaimsPrincipal 或请求 `CancellationToken`：

| 字段 | 用途 |
| --- | --- |
| `OutboxId`、`SourceDatabase`、`TenantId` | 唯一定位源任务与数据库 |
| `TaskType`、`SchemaVersion` | 选择处理器并支持载荷演进 |
| `IdempotencyKey` | Outbox 自身去重；同租户同键唯一 |
| `AggregateType`、`AggregateId` | 运维检索与关联业务事实 |
| `OccurredAtUtc`、`AvailableAtUtc` | 固定事件时间与下一次执行时间 |
| `PayloadJson` | 业务载荷；只放执行所需不可变快照和 ID |

首批业务载荷：

- `ForumPostPublishedRewardsV1`：`PostId`、`AuthorId`、固定奖励日期、首次发帖候选依据。
- `ForumCommentPublishedEffectsV1`：`CommentId`、`PostId`、作者、回复目标、帖子作者、通知内容 / 导航快照、固定奖励日期。
- `ForumPostLikedEffectsV1` / `ForumCommentLikedEffectsV1`：点赞事件 ID、目标、作者、点赞者、点赞后计数、固定奖励日期、通知去重窗口桶和展示快照。
- `ExperienceLevelChangedV1`：触发经验流水 ID、用户、旧 / 新等级、奖励金额和通知展示快照。
- `CommentHighlightRewardQualifiedV1`：高亮 ID、评论、作者、类型、资格时点赞数。
- `CommentHighlightBonusQualifiedV1`：高亮 ID、作者、类型、点赞增量和 `LikeCountAfter`。
- `NotificationRequestedV1`：确定的 `NotificationId`、通知业务键、接收者列表、类型、标题、内容、业务导航与触发者快照。

奖励日期和通知窗口桶必须在来源事件创建时固定，重试时不重新读取 `DateTime.Today`，避免跨日重试改变幂等键。本批只固定现有业务日期语义，不扩大为 Q2 的全仓时间治理。

## 七、幂等键

采用两层幂等：Outbox 防止同一业务事件重复建任务，目标写唯一键防止同一任务并发或重放产生重复副作用。

| 类型 | Outbox / 目标写键 |
| --- | --- |
| 发帖 / 评论基础奖励 | `task:post-published:{postId}`、`task:comment-published:{commentId}`；币和经验沿用各自 `coin:*` / `exp:*` 业务键 |
| 帖子 / 评论点赞 | 使用一次真实“由未点赞变为点赞”的事件 ID；每日奖励键包含来源事件固定的日期，不以执行日期计算 |
| 升级 | `task:level-change:exp-transaction:{expTransactionId}`；升级币改用 `GrantCoinOnceAsync`，目标键同样引用该经验流水 ID |
| 高亮基础奖励 | `task:highlight-base:{highlightId}`；币 / 经验继续按高亮类型、作者和评论唯一 |
| 高亮增量 | `task:highlight-bonus:{highlightId}:to-like:{likeCountAfter}`；现有币业务键可复用 |
| 权益 / 背包 | 继续使用 `(TenantId, SourceOrderId)` 和背包发放记录唯一键，不新造平行口径 |
| 通知 | `notification:{sourceEventId}:{notificationKind}`；Notification 使用载荷内确定的 `NotificationId`，UserNotification 增加 `(TenantId, UserId, NotificationId)` 唯一保护 |

通知的 Redis 时间窗去重只负责产品聚合体验，不再承担可靠性。点赞通知的窗口桶在来源事务中确定并进入持久业务键；Redis 不可用时也不能导致持久通知丢失。

## 八、重试、失败记录与人工重放

### 1. 状态模型

`ReliableOutboxMessage` 至少包含：`Pending / Processing / Succeeded / DeadLetter`、`AttemptCount`、`MaxAttempts`、`AvailableAtUtc`、`LockedAtUtc`、`LockedBy`、`LastErrorCode`、截断后的 `LastErrorSummary`、`ProcessedAtUtc`、`ReplayCount` 和最近重放操作人。

- Dispatcher 原子领取到期任务并设置短租约；enqueue 失败或进程中断后由租约回收重新变为可领取。
- Worker 以 `SourceDatabase + OutboxId` 重新读取任务，并在自己的 Hangfire job scope 内解析对应处理器。
- 同一 Outbox 可能被重复 enqueue；处理器必须依赖目标唯一键，而不是依赖“只执行一次”。

### 2. 重试策略

- 业务重试由 Outbox 状态统一控制，Hangfire Job 设置 `AutomaticRetry(Attempts = 0)`，避免 Hangfire 重试与业务退避叠加。
- 默认最多 6 次：立即、1 分钟、5 分钟、30 分钟、2 小时、12 小时；每次加小幅随机抖动。
- 数据库暂时不可用、锁冲突、超时和网络失败可重试；载荷版本未知、字段非法、业务目标永久不存在等直接进入 `DeadLetter`。
- 处理器不得吞掉失败后返回成功。组合任务允许部分子写已成功，但下一次必须靠子写幂等键补齐剩余步骤。

### 3. 失败记录与人工重放

- Outbox 行本身是最终失败记录；保留任务类型、聚合、尝试次数、最后错误摘要和时间，不在载荷或错误摘要记录密钥、token 或完整敏感请求。
- Hangfire Dashboard 继续用于执行观察，不作为业务完成状态的唯一真相。
- 提供受 Console 权限保护的只读查询与 `replay` API，不在 Q1-A 增加新页面。人工重放只把原 Outbox 以相同载荷和幂等键重置为 `Pending`，记录操作人、原因和次数，不复制成新的业务事件。
- `Succeeded` 默认禁止重放；如需业务补偿，必须创建显式补偿事件，不能绕过幂等键强制重复发奖。

## 九、依赖作用域

- Request Service 只调用 Outbox Writer，在当前事务内写不可变消息，不启动线程，不捕获 scoped 依赖。
- Hangfire Job 方法只接收 `SourceDatabase` 与 `OutboxId`。由 ASP.NET Core / Hangfire JobActivator 为每次执行创建新 scope，再解析仓储、奖励、通知和缓存服务。
- Dispatcher 若实现为 Hangfire recurring job，同样使用 job scope；不把 Service 实例、实体对象或请求上下文闭包传入 Hangfire 表达式。
- 任务使用 Hangfire 取消令牌或自己的执行超时，不沿用已经结束请求的 `RequestAborted`。
- 增加作用域释放测试：请求 scope 释放后再执行任务，且并发任务之间不共享 SqlSugar client、Repository 或缓存服务实例。

## 十、实施结果

实现按以下批次完成，并保持在 Q1-A 范围内：

1. **基础设施与契约**：建立 Main / Chat 同构 Outbox 模型、状态机、原子领取、Dispatcher、Scoped Job、处理器注册、失败查询 / 重放 API 和基础测试。
2. **写端幂等补强**：通知 Message 库事务、确定 NotificationId、用户通知唯一键；升级币改用业务键；复核现有 Coin / Exp / Benefit / Inventory 唯一约束。
3. **迁移奖励与升级链路**：发帖、评论、点赞、经验升级和高亮资格写 Outbox，移除 12 处不可丢失 `_ = Task.Run`。
4. **迁移通知来源**：订单、关注、轻回应、开奖、聊天提及等来源事务写 `NotificationRequested`；NotificationService 只负责幂等持久化。
5. **收口派生与实时链路**：评论高亮重算改为可重算 Hangfire 任务；通知持久化后刷新未读缓存并 best-effort 推送，移除剩余 2 处裸 `_ = Task.Run`。
6. **候选证据**：SQLite 测试覆盖来源事务回滚、租约恢复、非领取态重复 Job 门禁、通知重复持久化、退避 / DeadLetter 与人工重放；目标写继续由 Coin / Exp / Benefit / Inventory 既有业务唯一键保护。

实现后的静态复扫未发现业务侧裸 `_ = Task.Run`、`ContinueWith`、`async void` 或丢弃的异步业务写。剩余 `Task.Run` 均已被等待，且只用于文件 I/O 或现有事务 AOP；通知持久化入口已收口到 `NotificationService` 与可靠任务处理器。

## 十一、测试口径

### 1. 进程中断

必须覆盖以下故障点：

1. 来源业务写后、事务提交前中断：业务事实与 Outbox 一起回滚。
2. 来源事务提交后、Hangfire enqueue 前中断：Outbox 保留，恢复后可被 Dispatcher 发现。
3. enqueue 成功后、Outbox 状态更新前中断：允许重复 enqueue，但只产生一次业务结果。
4. Worker 领取后、首个子写前中断：租约 / Hangfire 恢复后完成。
5. 组合任务完成部分奖励后中断：重放不重复已完成奖励，并补齐其余奖励和通知。
6. Notification 已插入、UserNotification 尚未完成时中断：Message 事务回滚或重试后形成完整通知，不保留无接收者孤儿。

### 2. 重复与并发执行

- 同一 Outbox 顺序执行两次、并发执行两次、人工重放一次，余额、经验、权益、背包与通知接收关系都只能出现一次。
- 两个不同的合法业务事件不能因粗粒度幂等键互相吞掉；特别验证点赞跨窗口、不同评论回复和不同订单。
- 升级组合任务在币已成功、通知失败后重放，只补通知，不重复发币。
- 未读缓存最终与 Message 数据库 count 一致；重复推送不影响持久状态，断线重连能恢复。

### 3. 数据库与验证层级

- SQLite 定向测试覆盖状态机、载荷版本、失败分类、基础事务和顺序重放。
- PostgreSQL 集成测试覆盖原子领取、唯一约束冲突、并发 Worker、租约回收以及 Main / Chat 源库到 Message 目标库的最终一致性。
- Q1-A 代码批次执行后端定向测试、`dotnet build Radish.slnx -c Debug`、必要的 `dotnet test Radish.Api.Tests`、changed repo hygiene 与 `git diff --check`。
- 本轮审计不启动服务；候选运行态验证仍等待用户当轮确认服务已经启动。

## 十二、已确认决策与停止线

本批按以下已确认决策实施：

1. 采用源库 Outbox 作为可靠事实，Hangfire 作为执行器，不采用“事务提交后直接 enqueue”作为可靠边界。
2. Main 与 Chat 使用同构 Outbox 表并由统一 Dispatcher 扫描；不引入分布式事务或外部消息中间件。
3. 订单核心资产 / 权益保持同步事务，只异步处理事务后的通知等副作用。
4. Outbox 自管 6 次退避重试，Hangfire 自动业务重试关闭；最终失败进入 `DeadLetter`。
5. 人工重放先提供受权 API 与审计记录，不增加 Console 页面。

本批停止线：不建立通用消息平台，不引入 Kafka / RabbitMQ，不扩 Q1-B / Q2 / Q3，不调整页面，不顺手重构无关 Service，不启动服务、不暂存、不提交。

## 十三、验证结果

- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter FullyQualifiedName~ReliableOutbox`：`9/9` 通过。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore`：`584/584` 通过。
- `dotnet build Radish.slnx -c Debug --no-restore`：通过，`0` 警告、`0` 错误。
- 未启动服务，未执行浏览器或 PostgreSQL 运行态故障注入；这部分保留为 Q1-A 合并准备 / Release Go 证据，不把 SQLite 与静态验证外推为运行态结论。
