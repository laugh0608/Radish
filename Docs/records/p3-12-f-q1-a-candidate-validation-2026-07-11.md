# P3-12-F Q1-A 候选级可靠性验证记录

> 状态：`通过；Q1-A 具备独立集成条件`
>
> 日期：`2026-07-11`（Asia/Shanghai）
>
> 范围：只验证 Q1-A Outbox、Hangfire 恢复、通知幂等、PostgreSQL 结构升级与候选门禁，不包含 Q1-B、页面调整、正式 tag 或部署。

## 摘要

Q1-A 已完成 SQLite 自动化、PostgreSQL 集成、DbMigrate 重入和真实 Hangfire Dispatcher 恢复验证。Main / Chat Outbox 的唯一键与调度索引、Message 通知业务键与接收关系唯一索引均已在 PostgreSQL 实物核对；一条源事务已提交但尚未入队的 `Pending` 任务和一条模拟 Worker 中断的过期 `Processing` 任务均被运行中的 API 恢复为 `Succeeded`，重复业务通知最终只形成一条 `Notification` 和一条 `UserNotification`。

验证期间发现并修复 PostgreSQL 聊天频道种子重入的可空时间参数类型错误，同时补齐 ReliableOutbox 重放权限在 Console 常量、ApiModule 种子与资源 API 映射间的契约。临时 API 与 PostgreSQL 容器均已停止并清理。

## 一、验证环境

- PostgreSQL：临时 `postgres:16` 容器，仅绑定 `127.0.0.1:55432`。
- 数据库：Main、Log、Message、Chat、Hangfire 与 OpenIddict 均使用独立临时数据库。
- API：只启动 `Radish.Api`，监听 `http://localhost:5100`；Redis 关闭。
- 未启动：Auth、Gateway、Frontend、Console。
- 清理：API 正常接收 Ctrl-C 并停止；PostgreSQL 容器以 `--rm` 运行，停止后已确认删除。

## 二、PostgreSQL 自动化矩阵

新增环境变量驱动的 `ReliableOutboxPostgresIntegrationTest`。未配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING` 时明确跳过，提供隔离连接串时执行以下断言：

1. Main 源业务写与 Outbox 同事务回滚，二者均不残留。
2. 20 条到期任务由两个 Worker 并发领取，领取集合无交集且总数不丢失。
3. 相同 `(TenantId, IdempotencyKey)` 的 Outbox 被 PostgreSQL 唯一约束拒绝。
4. 失败任务在退避时间前不可领取，到期后可领取；租约截止点使用严格 `<`，截止点后一秒完成恢复。
5. Main 与 Chat 的同名 Outbox 表保持来源隔离。
6. Message 通知与全部接收关系在同一事务完成；接收关系批量写失败时通知回滚。
7. 不同 NotificationId 使用相同 BusinessKey 时只保留首条通知，重复写按幂等成功处理。

结果：PostgreSQL 集成测试 `1/1` 通过。默认后端全量测试为 `584` 通过、`1` 项 PostgreSQL 环境测试明确跳过，总计 `585`。

## 三、DbMigrate 升级与重入

执行顺序：

1. 干净 PostgreSQL 四业务库执行 `doctor`，正确报告结构缺失。
2. 首次 `apply` 创建 Main / Log / Message / Chat 当前实体结构并完成受控系统种子；开发默认账号保持关闭。
3. 第二次 `apply` 首次暴露 Chat 默认频道更新中 `DBNull.Value` 被 PostgreSQL 推断为 text、无法写入 timestamp 的问题。
4. 将 `DeletedAt / DeletedBy` 改为 SQL `NULL` 后重复执行 `apply`，结构与全部 15 类种子重入通过。
5. 补齐 ReliableOutbox 权限种子后再次 `apply` 通过。
6. 最终 `verify` 报告四个连接配置有效、主库业务表齐全。

PostgreSQL 系统目录实物核对：

- Main：`idx_outbox_main_tenant_key` 唯一索引与 `idx_outbox_main_dispatch` 调度索引存在。
- Chat：`idx_outbox_chat_tenant_key` 唯一索引与 `idx_outbox_chat_dispatch` 调度索引存在。
- Message：月分表 `(TenantId, BusinessKey)` 唯一索引存在；`UserNotification (TenantId, UserId, NotificationId)` 唯一索引存在。

## 四、真实 Hangfire 恢复

运行前在临时 Main 库预置：

- 一条 `Pending` 的 `NotificationRequested`，代表源事务已提交、Dispatcher 尚未入队时进程退出。
- 一条 `LockedAtUtc` 已超过五分钟租约的 `Processing` 重复任务，代表 Worker 领取后进程中断。
- 两条任务使用不同 Outbox 幂等键，但复用同一 NotificationId 与 BusinessKey，用于验证目标写幂等。

API 启动后，`reliable-outbox-dispatch` 在下一个整分钟领取两条任务，Hangfire PostgreSQL Worker 完成处理。最终结果：

- 两条 Outbox：均为 `Succeeded`，且 `ProcessedAtUtc` 已写入。
- Notification：相同 BusinessKey 只有 `1` 行。
- UserNotification：相同接收者与 NotificationId 只有 `1` 行。
- 未读缓存按 Message 数据库 count 刷新为 `1`；实时推送不参与持久状态判定。

## 五、候选门禁

- `npm run validate:ci -- --report`：通过，Backend Guard 已实际执行。
- `npm run validate:baseline`：通过。
- `npm run validate:baseline:host -- --report`：通过；本地既有 SQLite 仅只读报告缺少新 Outbox 表，未在本批修改该开发数据库。
- `npm run validate:identity`：通过，身份定向测试 `29/29`。
- `npm run check:dependency-security`：通过，npm / NuGet High / Critical 均为 `0`。
- Console 权限扫描：通过；`console.hangfire.replay` 只作为受权 API 权限存在，未新增页面入口，因此保留“未被页面守卫引用”的非阻断警告。
- `git diff --check` 与 changed/untracked 仓库卫生检查：在最终文档收口后复核。

## 六、结论与后续边界

Q1-A 已证明源事务与任务事实可原子提交、Dispatcher 可恢复未入队和过期租约任务、目标通知写可承受重复执行，PostgreSQL 结构与种子可重入，具备独立集成条件。

本批不把本地既有 SQLite 自动升级为交付事实；后续若用该库启动开发宿主，应先显式执行 `DbMigrate apply`。下一工程顺位进入 Q1-B API 错误契约的全仓审计与兼容方案，方案确认前不修改错误响应运行时。
