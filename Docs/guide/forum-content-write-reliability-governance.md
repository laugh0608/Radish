# 论坛内容发布可靠性与编辑历史治理

> 状态：`创建 / 编辑提交意图、创建类限频、Flutter 既有入口与论坛业务写原子事务均已完成`
>
> 最后复核：`2026-07-16`（Asia/Shanghai）
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[论坛帖子/评论编辑历史设计与实现](/features/forum-edit-history)、[P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)

## 摘要

`WOG-1` 至 `WOG-6` 首轮写操作治理与论坛内容提交专题均已阶段收束。当前实现同时覆盖重复提交、短窗口刷屏、编辑历史和数据库原子性：提交意图、业务对象、可靠 Outbox 与成功状态在同一外层事务内提交，任一环节失败都不会向客户端返回未真实落库的成功结果。

本专题的目标不是把论坛发布改造成资产交易式幂等平台，也不是新增完整反垃圾系统，而是为用户真实发帖、评论、回答和编辑提供稳定的服务端保护：

- 同一次发布意图因网络重试、超时或刷新重复提交时，不创建多条内容。
- 没有客户端提交意图 key 的旧客户端，仍通过短窗口内容指纹降低重复提交风险。
- 发布频率限制按用户、内容类型和目标对象分层，不误伤跨帖子正常互动。
- 编辑历史继续以现有 `PostEditHistory` / `CommentEditHistory` 为真值，不新增通用编辑历史表。

当前代码已按确认方案覆盖创建链路、首批编辑重试幂等和创建类短窗口频率限制：`PublishPostDto`、`CreateCommentDto`、`CreateAnswerDto`、`UpdatePostDto` 与 `UpdateCommentDto` 新增可选 `ClientSubmissionId`，服务端新增 `ContentSubmissionRecord` 作为内容提交意图记录，Web 论坛发帖、评论、回答、帖子编辑和评论编辑会按提交意图生成并复用 key。正式 Web 首批作者态通过 `/forum/compose` 与 `/forum/post/:postId?intent=answer|edit|history` 复用同一提交意图规则，不新增临时 API 调用通道。Flutter 原生纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑也已复用同一 `clientSubmissionId` 字段与 `forum-post:` / `forum-comment:` / `forum-answer:` / `forum-post-edit:` / `forum-comment-edit:` 前缀规则。发帖、评论 / 回复和回答已基于近期成功的 `ContentSubmissionRecord` 做创建类短窗口限频；帖子 / 评论编辑历史继续使用既有 `PostEditHistory` / `CommentEditHistory`，不新增通用编辑历史表。

## 当前代码事实

| 范围 | 当前事实 | 本专题判断 |
| --- | --- | --- |
| 发帖 | `PublishPostDto` 已支持可选 `ClientSubmissionId`，创建路径经 `ForumContentWriteService.PublishPostAsync` 包装后再进入 `PostService.PublishPostAsync` | 同 key 成功重试返回已有帖子；无 key 时按短窗口内容指纹降低重复发帖 |
| 评论 | `CreateCommentDto` 已支持可选 `ClientSubmissionId`，创建路径经 `ForumContentWriteService.CreateCommentAsync` 包装后再进入 `CommentService.AddCommentAsync` | 同帖同 key 成功重试返回已有评论；避免重复评论和重复实时事件 |
| 回答 | `CreateAnswerDto` 已支持可选 `ClientSubmissionId`，创建路径经 `ForumContentWriteService.AddAnswerAsync` 包装后再进入 `PostService.AddAnswerAsync` | 同 key 成功重试返回已有回答，避免同一回答重复创建 |
| 帖子编辑 | `UpdatePostDto` 已支持可选 `ClientSubmissionId`，编辑路径经 `ForumContentWriteService.UpdatePostAsync` 包装后再进入 `PostService.UpdatePostAsync` | 同 key 成功重试不重复更新；无变化保存不写历史、不递增 `EditCount` |
| 评论编辑 | `UpdateCommentDto` 已支持可选 `ClientSubmissionId`，编辑路径经 `ForumContentWriteService.UpdateCommentAsync` 包装后再进入 `CommentService.UpdateCommentAsync` | 同 key 成功重试不重复更新；无变化保存不写历史、不重复推送评论实时事件 |
| Flutter 发帖 / 评论 / 回答 / 首批编辑 | `Clients/radish.flutter` 原生纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑已生成并提交 `clientSubmissionId` | 失败后直接重试复用同一 key；成功、草稿 / 回复对象 / 回答内容 / 编辑目标 / 编辑内容变化或账号变化后生成新 key |
| 创建类限频 | 发帖、评论 / 回复、回答在 `ContentSubmissionService.BeginAsync` 中基于近期成功记录判断短窗口 | 成功重放和内容重复优先；限频命中不创建新提交记录，避免误把失败重试写成 `Pending` |
| 编辑历史 | 已存在 `ForumEditHistoryOptions`、`PostEditHistory`、`CommentEditHistory` 和查询接口 | 首批沿用既有模型，不新增 `ContentEditHistory` |
| 轻回应 | `PostQuickReplyService` 已有内容长度、返回条数、冷却和重复内容窗口设置 | 只做边界复核，不并入本批重做 |

## 治理目标

1. 服务端承接重复提交的最终保护，前端禁用按钮和 loading 只作为体验辅助。
2. `clientSubmissionId` 表达“同一次提交意图”，比单纯按内容相同判断更可靠。
3. 内容指纹只作为旧客户端和异常请求的短窗口保护，不长期限制用户发布相同文本。
4. 编辑成功后才写历史；无内容变化、旧提交重试或冲突提交都不新增编辑历史。
5. 频率限制按业务对象分层，避免用户在不同帖子之间正常参与时被全局拦截。

## 已完成范围

本次已确认并完成以下范围：

| 类型 | DTO | 服务入口 | 可靠性目标 |
| --- | --- | --- | --- |
| 发帖 | `PublishPostDto` | `PostService.PublishPostAsync` | 同 key 重试返回已有帖子，短窗口相同内容提示已提交 |
| 评论 / 回复 | `CreateCommentDto` | `CommentService.AddCommentAsync` | 同帖同 key 重试返回已有评论，避免重复评论和重复通知 |
| 问答回答 | `CreateAnswerDto` | `PostService.AddAnswerAsync` | 同 key 重试返回已有回答，避免同一回答重复创建 |
| 创建类限频 | `ContentSubmissionBeginRequest` 内部字段 | `ContentSubmissionService.BeginAsync` | 发帖 `30` 秒、同帖评论 / 回复 `10` 秒、同帖回答 `30` 秒内拦截新的创建意图 |
| 帖子编辑 | `UpdatePostDto` | `PostService.UpdatePostAsync` | 同 key 重试不重复写历史，不重复递增编辑次数；无变化保存不写历史 |
| 评论编辑 | `UpdateCommentDto` | `CommentService.UpdateCommentAsync` | 同 key 重试不重复写历史，不绕过时间窗与次数限制；无变化保存不写历史、不重复推送实时事件 |

本阶段不纳入：

- 独立内容发布频率限制平台或可配置频控。
- `PostQuickReplyService` 轻回应完整重做。
- 投票、抽奖开奖、采纳回答、点赞、关注、Reaction 等其他互动写入。
- 完整反垃圾系统、内容风控评分、人工审核工作流或用户信誉体系。
- 不新建论坛专属 Redis 锁、队列、第二套 Outbox 或完整可观测性平台；业务写继续复用既有可靠 Outbox。
- Flutter 转账、完整移动商城、服务端强制资产写入口必须传 `idempotencyKey`。

## 已确认决策

### 决策 1：新增内容提交意图记录

首批已新增专用提交意图记录，而不是复用资产交易的 `OperationIdempotencyRecord`。

模型名：

- `ContentSubmissionRecord`

字段：

| 字段 | 说明 |
| --- | --- |
| `TenantId` | 租户边界 |
| `UserId` | 提交用户 |
| `OperationType` | `ForumPostCreate`、`ForumCommentCreate`、`ForumAnswerCreate`、`ForumPostEdit`、`ForumCommentEdit` |
| `ClientSubmissionId` | 客户端生成的提交意图 key |
| `TargetType` | 目标对象类型，例如 `Post`、`Comment` |
| `TargetId` | 目标对象 Id；发帖可为空或记录分类 Id 作为上下文 |
| `RequestDigest` | 完整请求摘要，用于同 key 不同内容冲突判断 |
| `ContentFingerprint` | 标题 / 内容 / 关键上下文的短窗口指纹 |
| `Status` | `Pending`、`Succeeded`、`Failed` |
| `ResultType` | 终态对象类型，例如 `Post`、`Comment`、`Answer` |
| `ResultId` | 终态对象 Id |
| `ResultPublicId` | 可选，帖子等公开对象后续可用于回流 |
| `ExpiresAt` | 去重记录过期时间 |

唯一约束：

- `TenantId + UserId + OperationType + ClientSubmissionId`

写入语义：

1. 客户端提交 `clientSubmissionId` 时，服务端先尝试创建 `Pending` 记录。
2. 创建成功后执行真实发布、回答或编辑，成功后把记录更新为 `Succeeded` 并写入结果。
3. 唯一冲突时读取已有记录：
   - `RequestDigest` 一致且状态为 `Succeeded`：返回已有结果，不重复写内容。
   - `RequestDigest` 一致且状态为 `Pending`：返回“正在提交，请稍后确认”，不重复执行写入。
   - `RequestDigest` 不一致：返回明确冲突，提示刷新或重新发布。
4. `Pending` 重试直接返回“内容正在提交，请稍后确认”，不等待、不轮询。
5. `Failed` 或已过期记录可被同 key 新请求重置为 `Pending` 后重新执行。

### 决策 2：客户端提交 key 由前端生成并按提交意图复用

建议 key 前缀：

- 发帖：`forum-post:{uuid}`
- 评论 / 回复：`forum-comment:{uuid}`
- 回答：`forum-answer:{uuid}`
- 帖子编辑：`forum-post-edit:{uuid}`
- 评论编辑：`forum-comment-edit:{uuid}`

前端行为：

- 打开编辑器或开始一次发布意图时生成 key。
- 网络失败、超时或用户点击重试时复用同一个 key。
- 发布成功、用户清空草稿或切换到新的发布对象后生成新 key。
- 服务端返回重复提交结果时，跳转到已有内容或展示“刚刚已提交”。
- 服务端返回频率限制时，展示剩余等待时间。

Web 论坛创建和首批编辑路径已传 key；Flutter 当前已有的纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑也已传 key。旧客户端或尚未接入的客户端不传 key 时走内容指纹短窗口保护。若 Flutter 后续新增子评论编辑、回答编辑或更完整编辑器入口，继续复用同一字段和前缀规则，但需按对应入口范围单独评审。

### 决策 3：内容指纹只做短窗口保护

内容指纹用于旧客户端、刷新重提或异常请求场景，不作为长期禁止重复内容的规则。

建议规范化口径：

- 文本统一换行为 `\n`。
- 去掉首尾空白。
- 连续空白折叠为单个空格；不把大小写或中文标点做宽泛归一，避免误伤真实表达。
- 发帖摘要包含 `Title + Content + CategoryId + IsQuestion + Tags + Poll/Lottery` 关键上下文。
- 评论摘要包含 `PostId + ParentId + ReplyToCommentId + Content`。
- 回答摘要包含 `PostId + Content`。
- 编辑摘要包含目标 Id 和编辑后的主要字段。

建议短窗口：

| 类型 | 建议窗口 | 处理 |
| --- | ---: | --- |
| 发帖 | `180` 秒 | 同用户同类型相同指纹提示“刚刚已发布，请勿重复提交” |
| 评论 / 回复 | `60` 秒 | 同用户同帖相同指纹提示“刚刚已评论，请勿重复提交” |
| 回答 | `120` 秒 | 同用户同问答帖相同指纹提示“刚刚已提交回答” |
| 编辑 | `60` 秒 | 相同编辑重试不新增历史，提示内容已保存或返回已有结果 |

窗口数值首批先做代码默认值，不直接开放到 Console 系统设置。若后续真实运营需要调整，再按系统设置治理专题独立评审。

### 决策 4：创建类频率限制按对象分层

该项已完成首批创建类写入口治理，继续不实现独立频率限制平台。频率限制复用 `ContentSubmissionRecord` 中的近期成功记录作为数据库真值，判断顺序位于同 key 成功重放和短窗口内容指纹去重之后、真实写入之前；命中频率限制时不创建新的提交记录。

首批分层：

| 类型 | 限制维度 | 默认窗口 | 默认语义 |
| --- | --- | ---: | --- |
| 发帖 | `TenantId + UserId + OperationType` | `30` 秒 | 限制连续发帖，不影响评论其他帖子 |
| 评论 / 回复 | `TenantId + UserId + OperationType + PostId` | `10` 秒 | 限制同帖刷屏，不影响跨帖子互动 |
| 回答 | `TenantId + UserId + OperationType + PostId` | `30` 秒 | 限制同一问答帖连续回答 |
| 编辑 | 不纳入本批频率限制 | - | 沿用既有编辑次数 / 评论时间窗，并通过提交意图记录处理重试 |

当前发帖、评论和回答入口均使用当前用户身份进入服务层，没有新增管理员特权旁路。管理员编辑仍沿用既有权限、审计和编辑历史语义。

首批不引入 Redis 计数，不开放到 Console 系统设置。数据库记录是短窗口、重试判断和创建类频率限制的真值；多实例、高频压力或真实刷屏问题出现后，再按 Redis 与缓存治理专题评审缓存 / 限流辅助。

### 决策 5：编辑历史沿用既有真值

现有 `PostEditHistory` / `CommentEditHistory` 继续作为编辑历史唯一真值。

已落地规则：

1. 内容没有变化时直接返回业务提示，不写历史，不递增 `EditCount`。
2. 同一 `clientSubmissionId` 重试时返回上一次编辑结果，不写第二条历史。
3. `clientSubmissionId` 相同但请求摘要不同，返回冲突，不覆盖内容。
4. 编辑失败、权限不足、时间窗过期或次数超限时，不写提交成功记录。
5. 管理员覆盖继续按 `ForumEditHistoryOptions.AdminOverride` 执行，不新增通用审批流。

## 响应语义

建议业务提示：

| 场景 | 响应语义 |
| --- | --- |
| 同 key 成功重放 | `刚刚已提交，已返回已有内容` |
| 同 key 仍在处理中 | `内容正在提交，请稍后确认` |
| 同 key 不同内容 | `提交内容已变化，请刷新后重新提交` |
| 短窗口重复内容 | `刚刚已提交相同内容，请勿重复发布` |
| 频率限制 | `操作过于频繁，请{秒数}秒后再试`，并返回可展示的等待秒数 |
| 编辑无变化 | `内容没有变化，无需保存` |

同 key 不同内容等冲突统一返回稳定 `409 Conflict` 契约；重复提交、频率限制、参数错误与权限不足必须继续通过 HTTP status、`Code / MessageKey` 和测试明确区分。

`InvalidKey / Conflict / Processing / FrequencyLimited` 分别保持稳定的 `400 / 409 / 409 / 429` 业务契约；提交记录缺失、插入或更新未命中使用专属一致性异常进入安全 `500` 边界，不得被帖子或评论 Controller 误归类为用户输入或权限错误。

`2026-07-16` 一致性补强：`ForumContentWriteService` 的发帖、评论、回答和编辑写入口以外层 `[UseTran]` 统一包住提交意图 `Begin`、真实业务写入、可靠 Outbox 与成功完成记录。内层业务 Service 继续复用 `Required` 事务传播，不得先提交业务对象再单独补写 `ContentSubmissionRecord`；完成记录或最终数据库 commit 失败必须整体回滚并向上抛出，禁止向客户端返回实际未提交的成功结果。

同一外层事务内不重试 `CompleteSuccessAsync`，业务异常后也不写 `Failed` 覆盖原始异常；两类失败都直接交给事务边界整体回滚。`CompleteSuccessAsync` 遇到记录缺失或更新未命中时按失败关闭。`BeginAsync` 的并发唯一冲突插入通过 `UnitOfWorkManage` 保存点隔离：PostgreSQL / SQLite 在插入前创建保存点，唯一冲突后先 `ROLLBACK TO SAVEPOINT` 并释放保存点，再查询竞争事务已经创建的记录；没有外层事务时不创建保存点，非唯一异常继续向上抛出。

## 验证入口

本批代码已按以下口径覆盖：

1. 后端定向测试
   - 同一 `clientSubmissionId` 重试发帖只创建一条帖子。
   - 同一 `clientSubmissionId` 不同内容返回冲突。
   - 无 key 的同用户短窗口相同评论被拒绝。
   - `Pending` 重试直接返回“正在提交”。
   - 成功业务写入与 `ContentSubmissionRecord=Succeeded` 原子提交；业务失败整体回滚，不留下可误判的成功记录。
   - 完成记录失败时不在同一事务内重试、不补写失败状态，外层事务同时回滚提交记录、业务对象与可靠 Outbox。
   - `CompleteSuccessAsync` 对记录缺失、更新未命中按失败关闭。
   - SQLite 真实 AOP 事务测试覆盖提交记录、业务事实和 Outbox 同时回滚；PostgreSQL 条件式集成测试覆盖唯一冲突后保存点恢复及事务继续可用。
   - 数据库 commit 失败且 rollback 成功时，事务管理器仍原样抛出 commit 异常。
   - 创建类频率限制命中时返回 `FrequencyLimited`，不创建新提交记录。
   - 评论 / 回复限频按 `PostId` 隔离，不影响跨帖子互动。
   - 发帖、评论 / 回复、回答写入口传入正确默认限频窗口和作用域。
   - 帖子 / 评论编辑无变化不写历史、不执行更新。
   - 评论编辑成功记录重放不再次执行更新。
2. 后端完整测试
   - `dotnet test Radish.Api.Tests`
3. 前端契约
   - `radish.client` 发布、评论、回答、帖子编辑和评论编辑请求生成并复用 `clientSubmissionId`。
   - `radish.flutter` 纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑请求生成并复用 `clientSubmissionId`。
   - 网络失败重试不生成新 key。
4. 数据库结构
   - `ContentSubmissionRecord`、唯一索引与既有 DbMigrate / schema ledger 路径保持一致；PostgreSQL 唯一冲突恢复必须继续经过事务保存点。
5. 页面复核
   - 若只改后端和前端请求契约，可先以构建 / 测试为主。
   - 若明显改变发布表单反馈或详情页跳转，再按 Gateway PC + 移动视图集中复核。

## 不做范围

- 不把内容发布治理并入商城购买 / 转账的资产幂等表。
- 不要求所有客户端立刻强制传 `clientSubmissionId`；旧客户端先走短窗口保护。
- 不把内容指纹做成长期唯一约束。
- 不新增通用 `ContentEditHistory`，不改变既有编辑历史查询口径。
- 不实现独立频率限制平台。
- 不把短窗口、频率限制和编辑历史参数默认开放到 Console 系统设置。
- 不重做轻回应、投票、抽奖、点赞、关注、Reaction 或通知系统。
- 不启动完整反垃圾、用户信誉、审核队列、论坛专属 Redis 分布式锁、第二套 Outbox 或 E2E 平台。
- 不扩展 Flutter 转账、完整移动商城或服务端强制资产写入口必须传 key。

## 已完成与维护边界

当前长期口径如下：

1. 接受新增 `ContentSubmissionRecord` 表作为内容提交意图真值。
2. 覆盖创建链路和首批帖子 / 评论编辑重试治理。
3. 短窗口默认值采用发帖 `180` 秒、评论 `60` 秒、回答 `120` 秒。
4. 创建类频率限制采用发帖 `30` 秒、同帖评论 / 回复 `10` 秒、同帖回答 `30` 秒。
5. `Pending` 重试直接返回“正在提交”，不等待、不轮询。
6. Web 首批传 key，Flutter 已承接当前存在的纯文本发帖、根评论 / 回复、问答回答、作者帖子正文编辑和作者根评论编辑入口。

后续只有在新增论坛写入口、调整事务传播、改变提交意图状态机或引入新的可靠任务时，才扩展本专题；Flutter 子评论编辑和回答编辑仍分别按评论线程或问答能力单独评审，不顺带扩大资产幂等范围。
