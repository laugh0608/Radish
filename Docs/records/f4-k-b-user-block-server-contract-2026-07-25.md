# F4-K-B 用户屏蔽服务端权威契约

## 结论

F4-K-B 已完成服务端实现，下一顺位进入 F4-K-C Pencil 与正式 Web。

本批建立 Main `UserBlock` 唯一真相和统一关系策略，旧 Direct 屏蔽字段只参与迁移兼容读取；没有新增平行 Outbox、删除旧字段、修改 Pencil 或开发正式页面，也没有安装依赖、启动服务或执行浏览器 smoke。

## 已完成范围

- Main migration `20260725_011_user_block_authority` 建立 `UserBlock`、`UserBlockOperation`、唯一关系与 operation key 约束、双方关注一致性检查和既有 `ReliableOutboxMessage` 写入。
- Message migration `20260725_011_user_block_notification_suppression` 建立通知抑制字段、索引和旧数据归一；两项 migration 均进入 doctor / apply / verify、重入与恢复契约。
- 旧 `DirectConversation.BlockedByUserId / BlockedAt` 按可解释时间幂等回填到 Main；异常执行者、自屏蔽和跨租户历史数据由 doctor 阻断，不猜测修复。
- 专属 `IUserBlockRepository` 使用 Main 事务完成 Block / Unblock、软删除恢复、关系版本、operation key 回放 / 冲突和双方 `UserFollow` 解除；SQLite 使用进程级关系 / operation 锁，PostgreSQL 使用事务级 advisory lock。
- `UserFollow` 与 Block 共用用户对事务锁并在写入前复核屏蔽关系，阻止关注与屏蔽并发竞争；Unblock 不恢复关注。
- `IUserInteractionPolicyService` 提供单对、批量和 barrier 列表判定；关系读取失败按稳定 `503` 关闭交互，不以宽松 fallback 放行。
- 统一策略已接入关注写入与列表 / feed / 推荐、Direct 请求与发送、Reaction、Pin、阅读游标与回执裁剪。
- 关系型通知在创建前抑制；Block 可靠任务异步裁剪双方旧通知，列表、未读数和实时摘要在任务完成前仍按 Main 当前关系读修复。系统、账号安全、资产、治理和申诉通知不受影响，Unblock 不恢复历史通知。
- 新增本人 Block / Unblock / 列表 API、稳定 `Code / MessageKey`、HTTP 示例和 `@radish/http` 契约；旧 Direct Block / Unblock 入口生成并转发稳定 operation key，不再写旧字段。

## 验证结果

- `dotnet build Radish.slnx -c Debug --no-restore --disable-build-servers`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests --no-build --disable-build-servers`：1019 通过，28 个 PostgreSQL 环境用例因未配置连接串跳过。
- SQLite migration / Repository / Service 定向用例覆盖旧 Direct 回填、异常 doctor、重入、备份恢复、数据库自屏蔽约束、并发 Block / Unblock、operation key 回放 / 冲突、软删除恢复、关注原子解除和通知抑制。
- PostgreSQL migration 与 Repository 用例已进入环境驱动测试，覆盖小写物理标识、check constraint、事务 advisory lock、并发与幂等；本批按停止线未自行启动 PostgreSQL。
- `npm run validate:baseline:quick`：通过，包含四个前端 workspace type-check、`@radish/http` 19 项、client 455 项、UI 24 项和 Console 59 项测试及仓库规则扫描。
- `npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

## 下一批边界

F4-K-C 先更新公开主页、`/circle`、`/messages` 和 `/me/blocked` 的 PC / mobile Pencil 设计，再开发正式 Web 屏蔽确认、本人列表、解除屏蔽、Direct 只读和通用不可互动状态。本批不提前执行 Gateway 成组验收，也不扩展 WebOS、Flutter 或 Tauri。
