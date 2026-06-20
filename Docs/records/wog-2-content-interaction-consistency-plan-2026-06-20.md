# WOG-2 内容互动关系写入与计数一致性方案

> 日期：`2026-06-20`（Asia/Shanghai）
>
> 状态：`已确认 / 首批代码已实现`
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)

## 目标

`WOG-2` 只治理帖子 / 评论点赞这条内容互动主路径，目标是让同一用户对同一对象只有一条逻辑关系记录，并让 `Post.LikeCount`、`Comment.LikeCount` 只跟随真实关系变更更新。

本批不改变 API 返回结构，不新增前端参数，不引入 Redis 分布式锁，不扩展完整奖励风控，也不把所有内容互动入口一次性改完。

## 执行结论

`2026-06-20` 已按确认方案完成首批代码实现：

- `UserPostLike`、`UserCommentLike` 已声明用户-目标唯一索引和目标活跃查询索引。
- 已新增部署态 SQL：`Deploy/sql/20260620_add_like_relation_unique_indexes.sql`，覆盖历史重复关系清理、`LikeCount` 校准和索引创建。
- `Radish.DbMigrate` 已补本地结构治理入口：本地 `apply` 会规整点赞关系、校准计数并补齐索引。
- `PostRepository`、`CommentRepository` 已承接点赞关系切换、唯一冲突处理和 `LikeCount` 条件更新。
- `PostService.ToggleLikeAsync`、`CommentService.ToggleLikeAsync` 已改为根据仓储返回的 `Delta` 触发奖励、经验、通知和评论高亮重算。
- 已新增定向测试 `LikeRelationConsistencyTest`，覆盖关系复用、计数不为负，以及 `Delta=0` 不触发奖励 / 通知 / 高亮副作用。

## 当前问题

1. `UserPostLike`、`UserCommentLike` 当前没有用户-目标唯一索引。
2. `PostService.ToggleLikeAsync`、`CommentService.ToggleLikeAsync` 先查询活跃 / 已删除关系，再更新或新增，遇到并发请求时可能重复新增关系。
3. `Post.LikeCount`、`Comment.LikeCount` 当前是读出实体后加减再写回，遇到并发写入可能覆盖计数。
4. 点赞奖励、经验和通知依赖 `isLiked` 分支触发；如果关系层重复写入，可能放大为重复奖励或重复通知。

## 数据真值

- 帖子点赞关系唯一键：`TenantId + UserId + PostId`。
- 评论点赞关系唯一键：`TenantId + UserId + CommentId`。
- 每个用户-目标只保留一条关系记录，`IsDeleted=false` 表示当前已点赞，`IsDeleted=true` 表示当前未点赞。
- `Post.LikeCount`、`Comment.LikeCount` 是读模型计数，关系表中的活跃记录才是最终真值。
- 点赞奖励、经验和通知只在关系状态从未点赞变为已点赞且本次写入确实生效时触发。

## 建议实施

### 1. 实体与索引

为 `UserPostLike` 增加：

- 唯一索引 `idx_userpostlike_tenant_user_post`：`TenantId + UserId + PostId`。
- 查询索引 `idx_userpostlike_post_active`：`TenantId + PostId + IsDeleted`。

为 `UserCommentLike` 增加：

- 唯一索引 `idx_usercommentlike_tenant_user_comment`：`TenantId + UserId + CommentId`。
- 查询索引 `idx_usercommentlike_comment_active`：`TenantId + CommentId + IsDeleted`。

唯一索引不包含 `IsDeleted`，否则同一用户同一对象仍可能同时存在活跃行和已删除行，无法稳定表达一条逻辑关系。

### 2. 历史数据清理与计数校准

迁移前先处理历史重复关系：

1. 按唯一键分组。
2. 每组保留一条规范记录：优先保留活跃记录；若有多条活跃记录，保留 `LikedAt` 最新、`Id` 最大的一条；若全是已删除记录，也保留最新的一条。
3. 删除同组其他重复行。这里删除的是无法继续保留的历史异常关系行，目的是让唯一索引能成为后续真值保护。
4. 基于活跃关系重新校准 `Post.LikeCount` 和 `Comment.LikeCount`。

部署态补 `Deploy/sql/20260620_add_like_relation_unique_indexes.sql`，覆盖 PostgreSQL 的去重、计数校准和索引创建。本地 SQLite 通过 `Radish.DbMigrate apply / doctor / verify` 承接，必要时在 `DbMigrateRunner` 中补同等结构补丁。

### 3. 持久化流程

点赞切换不建议继续由 Service 组合多次普通仓储调用。建议新增实体专属仓储方法：

- `IPostRepository.TogglePostLikeAsync(...)`
- `ICommentRepository.ToggleCommentLikeAsync(...)`

仓储方法负责在一个数据库事务内完成：

1. 校验帖子 / 评论存在且未删除。
2. 查询当前用户-目标关系。
3. 活跃关系执行条件软删除，受影响行为 `1` 时返回 `Delta=-1`。
4. 已删除关系执行条件恢复，受影响行为 `1` 时返回 `Delta=1`。
5. 无关系时新增记录；若唯一冲突说明并发请求已先写入，重新读取最终状态并返回 `Delta=0`。
6. 仅在 `Delta != 0` 时用数据库条件更新维护 `LikeCount`，取消点赞时不允许减成负数。
7. 返回最终 `IsLiked`、`LikeCount`、`Delta` 和目标快照。

这样 Service 层继续负责编排奖励、经验、通知和评论高亮检查，不直接接触 `Db` / `Queryable`。

### 4. Service 行为

- `PostService.ToggleLikeAsync`、`CommentService.ToggleLikeAsync` 改为调用上述仓储方法。
- 返回 DTO 保持现有结构：`IsLiked`、`LikeCount` 不变。
- 只有 `Delta=1` 时触发点赞奖励、点赞经验和点赞通知。
- 只有 `Delta != 0` 时触发评论神评 / 沙发重算；并发重复请求得到同一最终状态时不重复触发。
- 自己点赞自己的通知语义保持现状，不额外发送通知；奖励语义本批不扩展。

## 验证

代码批次完成后建议执行：

1. `dotnet test Radish.Api.Tests`
2. 覆盖帖子点赞：新增、取消、恢复、重复并发请求、计数不为负、奖励只触发一次。
3. 覆盖评论点赞：新增、取消、恢复、重复并发请求、计数不为负、高亮重算只在有效变化时触发。
4. 覆盖迁移 SQL：历史重复关系清理后唯一索引可创建，`LikeCount` 与活跃关系数量一致。
5. `git diff --check`
6. `npm run check:repo-hygiene:changed`

本批已执行：

- `dotnet build Radish.slnx -c Debug`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests`：通过，`485` 个测试全部通过。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过。

## 停止线

- 不处理发帖 / 评论重复提交策略。
- 不处理投票、抽奖、Reaction、关注等其他互动入口。
- 不把点赞奖励事务流水唯一键并入本批；如果后续仍发现奖励服务自身重复发放，再进入 `WOG-4 奖励业务键唯一性`。
- 不引入 Redis 锁、队列、Outbox 或通用计数平台。

## 已确认决策

1. 唯一键采用 `TenantId + UserId + PostId / CommentId`，迁移中删除历史重复关系行。
2. 点赞关系切换和计数条件更新下沉到 `PostRepository` / `CommentRepository` 的专属方法。
3. 本批保持 API / 前端契约不变，只改后端持久化、迁移 SQL 和定向测试。
