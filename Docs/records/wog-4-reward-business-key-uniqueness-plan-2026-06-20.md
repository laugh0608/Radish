# WOG-4 奖励业务键唯一性方案评审

> 日期：`2026-06-20`（Asia/Shanghai）
>
> 状态：`已确认 / 首批代码已实现`
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)、[WOG-2 内容互动关系写入与计数一致性方案](/records/wog-2-content-interaction-consistency-plan-2026-06-20)、[WOG-3 背包 / 权益发放可靠性方案](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20)

## 目标

`WOG-4` 聚焦奖励流水自身的业务去重真值，优先覆盖当前已经暴露为读后写防重的高价值链路：

- 帖子 / 评论点赞产生的萝卜币互动奖励与经验奖励。
- 评论发布、评论被回复、首次发帖 / 首次评论等异步经验 / 萝卜币奖励。
- 神评 / 沙发基础奖励、点赞加成奖励、保留奖励和对应经验奖励。

本批目标不是新增奖励玩法，而是让同一个业务奖励事实在并发任务、重复异步触发、后台 job 重跑或网络重试后最多落一条有效流水，并且余额、经验、每日统计只随真正新增的奖励流水变化一次。

## 执行结论

`2026-06-20` 已按确认方案完成首批代码实现：

- `CoinTransaction`、`ExpTransaction` 已新增 nullable `RewardBusinessKey`，并声明 `TenantId + RewardBusinessKey` 唯一索引。
- `CoinService.GrantCoinOnceAsync`、`ExperienceService.GrantExperienceOnceAsync` 已作为奖励业务键发放入口落地；重复命中时返回既有 / no-op 语义，不重复更新余额、经验或每日统计。
- `GrantCoinOnceAsync`、`GrantExperienceOnceAsync` 已将业务键预查与实际发放写入放入同一次 UnitOfWork 尝试，乐观锁重试会重新检查同一个 `RewardBusinessKey`。
- `CoinRewardService` 已改为按服务端业务键发放互动萝卜币奖励、评论奖励、神评 / 沙发基础奖励、点赞加成奖励和保留奖励。
- 发帖、评论、首次发帖 / 首次评论、点赞经验、神评 / 沙发经验已改为通过 `GrantExperienceOnceAsync` 发放；背包经验卡和管理员调整仍保留原有非幂等奖励入口。
- 神评 / 沙发点赞加成已纳入首批，并通过 `highlightId + likeCountAfter` 形成稳定结算键；历史点赞加成因旧流水缺少 `likeCountAfter`，迁移不强行回填。
- `Radish.DbMigrate` 已补本地结构治理入口；当时阶段性结构脚本已于 2026-06-22 按上线前数据库口径清理，不再作为当前执行入口。
- 已新增 `CoinRewardBusinessKeyTest`，并补 `CoinServiceTest` / `ExperienceServiceTest` 对重复业务键 no-op 的覆盖。

## 当前代码事实

1. `WOG-2` 已让帖子 / 评论点赞关系成为奖励触发前置真值：只有 `Delta=1` 才触发奖励、经验、通知和评论高亮重算。
2. `CoinRewardService.CheckRewardExistsAsync` 当前通过 `BusinessType + BusinessId + ToUserId + Status=SUCCESS` 做读后写判断，指定日期时再在内存中过滤 `CreateTime`，并没有数据库唯一约束。
3. `CoinService.GrantCoinAsync` 负责写入 `CoinTransaction`、更新 `UserBalance` 和 `BalanceChangeLog`；如果上层读后写防重被并发击穿，会产生真实重复余额变更。
4. `ExpTransaction` 已有 `idx_dedup` 普通索引，字段为 `UserId + ExpType + BusinessType + BusinessId + CreatedDate`，但不是唯一索引；`ExperienceService.GrantExperienceAsync` 本身也不会自动检查该索引口径。
5. 神评 / 沙发实时重算和后台 job 都可能触发高亮相关奖励；当前基础奖励和保留奖励依赖 `CheckRewardExistsAsync`，点赞加成奖励没有明确的业务唯一键。

## 数据真值

奖励业务键应成为奖励流水的最终去重真值；上层 `HasExperienceTransactionAsync`、`CheckRewardExistsAsync`、任务调度窗口和前端按钮状态只能作为体验或减少无效调用的辅助。

建议新增 nullable 字符串列承载规范化业务键：

- `CoinTransaction.RewardBusinessKey`：仅奖励类萝卜币流水填写，非奖励交易保持 `NULL`。
- `ExpTransaction.RewardBusinessKey`：仅需要幂等去重的经验奖励填写，管理员调整、背包道具使用等不具备同一自然奖励事实的流水保持 `NULL`。

唯一约束建议为：

- `CoinTransaction`：`TenantId + RewardBusinessKey` 唯一。
- `ExpTransaction`：`TenantId + RewardBusinessKey` 唯一。

`RewardBusinessKey` 必须由服务端按固定模板生成，不接受前端传入，不使用中文，不写入随机数，不依赖当前时间戳秒级值；需要按天去重的奖励显式把 `yyyyMMdd` 写入 key。

## 业务键矩阵

| 范围 | 当前写入 | 去重真值 | 建议 key 模板 | 备注 |
| --- | --- | --- | --- | --- |
| 帖子被点赞萝卜币 | `LIKE_REWARD / POST_LIKE / postId` | 同一作者、同一帖子、同一天最多一次 | `coin:post-like:author:{authorId}:post:{postId}:day:{yyyyMMdd}` | 维持当前“每日一次”语义 |
| 点赞帖子互动萝卜币 | `LIKE_REWARD / POST_LIKE_ACTION / postId` | 同一点赞者、同一帖子、同一天最多一次 | `coin:post-like:giver:{likerId}:post:{postId}:day:{yyyyMMdd}` | 当前作者奖励重复会提前返回，代码批次应让两侧各自独立去重 |
| 评论被点赞萝卜币 | `LIKE_REWARD / COMMENT_LIKE / commentId` | 同一作者、同一评论、同一天最多一次 | `coin:comment-like:author:{authorId}:comment:{commentId}:day:{yyyyMMdd}` | 维持当前“每日一次”语义 |
| 点赞评论互动萝卜币 | `LIKE_REWARD / COMMENT_LIKE_ACTION / commentId` | 同一点赞者、同一评论、同一天最多一次 | `coin:comment-like:giver:{likerId}:comment:{commentId}:day:{yyyyMMdd}` | 与帖子点赞口径一致 |
| 评论发布萝卜币 | `COMMENT_REWARD / COMMENT_POST / commentId` | 同一评论发布奖励终身一次 | `coin:comment-create:author:{authorId}:comment:{commentId}` | 不按天重复 |
| 评论被回复萝卜币 | `COMMENT_REWARD / COMMENT_REPLY / parentCommentId` | 同一父评论作者、同一父评论、同一天最多一次 | `coin:comment-reply:author:{parentAuthorId}:comment:{parentCommentId}:day:{yyyyMMdd}` | 维持当前“每日一次”语义，`replyCommentId` 只作为触发来源 |
| 神评基础萝卜币 | `HIGHLIGHT_REWARD / GOD_COMMENT / commentId` | 同一评论成为神评基础奖励终身一次 | `coin:highlight-base:god-comment:author:{authorId}:comment:{commentId}` | 不使用 `CommentHighlight.Id`，避免同一评论多次高亮记录重复基础奖励 |
| 沙发基础萝卜币 | `HIGHLIGHT_REWARD / SOFA / commentId` | 同一评论成为沙发基础奖励终身一次 | `coin:highlight-base:sofa:author:{authorId}:comment:{commentId}` | 与神评一致 |
| 神评 / 沙发点赞加成萝卜币 | `HIGHLIGHT_REWARD / {Type}_LIKE_BONUS / highlightId` | 同一高亮状态迁移的同一点赞目标值最多结算一次 | `coin:highlight-like-bonus:{type}:highlight:{highlightId}:to-like:{likeCountAfter}` | 已纳入首批，方法入参带 `likeCountAfter` 作为稳定结算标记 |
| 神评 / 沙发保留萝卜币 | `HIGHLIGHT_REWARD / {Type}_RETENTION_W{week} / highlightId` | 同一高亮记录、同一保留周最多一次 | `coin:highlight-retention:{type}:highlight:{highlightId}:week:{week}:author:{authorId}` | 保持最多 3 周 |
| 发帖经验 | `POST_CREATE / Post / postId` | 同一帖子发帖经验终身一次 | `exp:post-create:author:{authorId}:post:{postId}` | 不按天重复 |
| 首次发帖经验 | `FIRST_POST / Post / postId` | 同一用户首次发帖奖励终身一次 | `exp:first-post:user:{userId}` | 不绑定首帖 ID，避免并发首帖统计漂移 |
| 评论发布经验 | `COMMENT_CREATE / Comment / commentId` | 同一评论发布经验终身一次 | `exp:comment-create:author:{authorId}:comment:{commentId}` | 不按天重复 |
| 首次评论经验 | `FIRST_COMMENT / Comment / commentId` | 同一用户首次评论奖励终身一次 | `exp:first-comment:user:{userId}` | 不绑定首评 ID |
| 被点赞经验 | `RECEIVE_LIKE / Post|Comment / targetId` | 同一接收者、同一目标、同一天最多一次 | `exp:receive-like:{targetType}:user:{userId}:target:{targetId}:day:{yyyyMMdd}` | 对齐点赞萝卜币每日奖励语义 |
| 点赞他人经验 | `GIVE_LIKE / Post|Comment / targetId` | 同一点赞者、同一目标、同一天最多一次 | `exp:give-like:{targetType}:user:{userId}:target:{targetId}:day:{yyyyMMdd}` | 防止同日取消 / 恢复点赞刷经验 |
| 神评经验 | `GOD_COMMENT / Comment / commentId` | 同一评论神评经验终身一次 | `exp:highlight-base:god-comment:author:{authorId}:comment:{commentId}` | 调用方可不再先查 `HasExperienceTransactionAsync` |
| 沙发经验 | `SOFA_COMMENT / Comment / commentId` | 同一评论沙发经验终身一次 | `exp:highlight-base:sofa:author:{authorId}:comment:{commentId}` | 与神评一致 |

等级升级萝卜币奖励也属于奖励流水，但不是 WOG-4 首批重点。若代码批次顺手接入，应采用 `coin:level-up:user:{userId}:level:{newLevel}`；若会扩大改动面，则保留为后续经验体系治理项。

## 建议实施

### 1. 奖励发放入口

不建议继续让所有调用方先查再调用普通 `GrantCoinAsync` / `GrantExperienceAsync`。建议新增明确的“按业务键发放一次”入口：

- `CoinService` 或奖励专属仓储提供按 `RewardBusinessKey` 发放萝卜币的方法，内部在同一事务中写 `CoinTransaction`、更新 `UserBalance` 和写 `BalanceChangeLog`。
- `ExperienceService` 提供按 `RewardBusinessKey` 发放经验的方法，内部在同一事务中更新 `UserExperience`、`UserExpDailyStats` 和写 `ExpTransaction`。
- `CoinRewardService` 负责生成奖励业务键并调用上述入口；重复键命中时返回“已发放 / no-op”，不写错误日志，不重复修改余额。

唯一冲突处理建议：

1. 先尝试按 `RewardBusinessKey` 写入奖励流水。
2. 如果唯一约束冲突，重新查询既有成功流水，返回既有流水号或 `no-op` 结果。
3. 如果余额 / 经验乐观锁冲突，整段事务回滚后按现有重试策略重新执行；重试过程中仍使用同一个 `RewardBusinessKey`。
4. 不依赖 private 方法上的 `[UseTran]` 作为事务真值；需要事务时应落在可被 AOP 拦截的公开服务方法，或下沉到专属仓储事务方法。

### 2. 神评 / 沙发点赞加成边界

点赞加成奖励不能只用 `HighlightId` 唯一，因为同一高亮记录的点赞数增长可以多次结算。

WOG-4 首批已纳入点赞加成，并同步调整调用口径：奖励方法收到稳定结算标记 `likeCountAfter`，用 `highlightId + likeCountAfter` 生成业务键。后台 job 或实时任务在同一次点赞目标值上重复触发时只能发一次。

### 3. 历史数据与迁移

代码批次若确认实施，需要同步：

1. `Radish.Model` 为 `CoinTransaction` / `ExpTransaction` 增加 nullable `RewardBusinessKey` 和唯一索引声明。
2. `Radish.DbMigrate` 本地 `apply` 补结构列、历史 key 回填和索引创建。
3. 正式数据库存在后再生成发布 SQL；上线前不维护该阶段历史发布脚本。

历史回填不应直接删除或改写既有成功流水，也不应自动回滚历史余额 / 经验：

- 对能从现有字段可靠推导的奖励流水，按业务键分组，只给每组一条规范记录回填 `RewardBusinessKey`。
- 若历史上已有重复成功奖励，同组其他重复流水保持 `RewardBusinessKey = NULL`，并在 SQL 末尾提供审计查询，交给后续人工对账或治理记录处理。
- 对无法可靠推导的历史流水不强行伪造 key，避免把错误语义固化进唯一索引。
- 唯一索引必须在历史规范记录回填后创建。

## 验证入口

代码批次完成后建议执行：

1. `dotnet test Radish.Api.Tests`
2. 新增 `CoinRewardBusinessKeyTest`：
   - 同一帖子 / 评论点赞奖励重复触发时，作者和点赞者余额各最多增加一次。
   - 同一评论发布奖励、评论被回复每日奖励重复触发时不重复写 `CoinTransaction`。
   - 神评 / 沙发基础奖励和保留奖励重复触发时返回 no-op。
   - 如纳入点赞加成，同一 `highlightId + likeCountAfter` 重复触发只发一次。
3. 新增 `ExperienceRewardBusinessKeyTest`：
   - 发帖、评论、首次发帖、首次评论重复异步触发时不重复写 `ExpTransaction`。
   - 帖子 / 评论点赞经验同一目标同一天重复触发时只发一次。
   - 神评 / 沙发经验不再依赖调用方先查，重复触发不增加经验和每日统计。
   - 未传 `RewardBusinessKey` 的背包经验卡、管理员调整等既有行为不被唯一索引误伤。
4. 正式数据库阶段覆盖发布 SQL：
   - 历史重复奖励只给规范记录回填 key。
   - 唯一索引可创建。
   - `RewardBusinessKey IS NULL` 的普通流水可保留多条。
5. `dotnet build Radish.slnx -c Debug`
6. `git diff --check`
7. `npm run check:repo-hygiene:changed`

首批实现已执行以下验证：

1. `dotnet build Radish.slnx -c Debug`
2. `dotnet test Radish.Api.Tests`
3. `git diff --check`
4. `npm run check:repo-hygiene:changed`
5. `node Scripts/check-repo-hygiene.mjs Docs/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20.md Radish.Api.Tests/Services/CoinRewardBusinessKeyTest.cs`

其中 `dotnet test Radish.Api.Tests` 覆盖 `496` 个测试，全部通过。

## 不做范围

- 不启动完整经济系统、资产风控平台、Redis 分布式锁、Outbox、通用奖励平台或开放 API 签名。
- 不改变奖励金额、每日上限、神评 / 沙发排名规则、稳定窗口、替换阈值或任务调度策略。
- 不把所有 `CoinTransaction` / `ExpTransaction` 都强制要求 `RewardBusinessKey`。
- 不自动修正历史重复奖励造成的余额、经验或每日统计差异；历史重复只做可审计识别，不在结构迁移中做资产回滚。
- 不改变前端 API 入参，不要求客户端传幂等 key。
- 不治理管理员调账、订单退款、商品编辑、系统设置版本语义或 Flutter 购买幂等；这些继续留在对应 WOG 后续候选。

## 已确认决策

1. 新增 `RewardBusinessKey` 作为奖励流水唯一真值，不直接把 `BusinessType + BusinessId + CreatedDate` 改成唯一索引。
2. 点赞奖励和点赞经验继续维持“同一目标同一用户每日一次”的现有语义。
3. 神评 / 沙发点赞加成纳入 WOG-4 首批，并通过 `likeCountAfter` 或等价结算标记表达稳定奖励事实。
4. 历史重复成功奖励只做 key 回填和审计查询，不在本批迁移中自动扣回余额或经验。
