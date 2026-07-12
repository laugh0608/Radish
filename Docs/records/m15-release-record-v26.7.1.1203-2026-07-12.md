---
releaseTag: v26.7.1.1203-release
productVersion: 26.7.1
imageTag: v26.7.1.1203-release
---

# M15 OpenIddict 修复补发记录（v26.7.1.1203-release，2026-07-12）

> 本页记录 `v26.7.1.1202-release` 生产前滚被 OpenIddict PostgreSQL 初始 migration 阻断后的修复补发，以及该补发在远程 Candidate Quality 中暴露的新阻断。

## 记录信息

- 记录日期：2026-07-12
- 发布类型：正式 Web 部署修复补发
- 当前状态：tag 已推送；Candidate Quality 失败，五个镜像未构建，生产未前滚

## 发布标识

- Git tag：`v26.7.1.1203-release`（已推送，保持不可变）
- 产品版本：`26.7.1`
- 当前修复提交：`f6daa770`
- PR：`#62`
- 最终对应提交：`ae0cd43a`
- 正式发布矩阵：DbMigrate、API、Auth、Gateway、Frontend

## 补发原因与根因

- 1202 已修复 PostgreSQL 小写物理标识符和 DbMigrate OpenIddict provider，Main schema ledger 与自然日 migration 均已成功前滚。
- DbMigrate 初始化 SqlSugar 后，依赖链启用 Npgsql legacy timestamp 全局行为；OpenIddict 运行态模型因此把四个 `DateTime?` 字段推导为 `timestamp without time zone`。
- 已发布 PostgreSQL snapshot 正确使用 `timestamp with time zone`，差异覆盖 `OpenIddictAuthorizations.CreationDate` 与 `OpenIddictTokens.CreationDate / ExpirationDate / RedemptionDate`，EF Core 因此拒绝执行初始 migration。
- 设计时 `has-pending-model-changes` 和直接构造 DbContext 的测试均不包含 DbMigrate 的完整启动顺序，所以未提前发现该运行态污染。

## 修复范围

- 在 OpenIddict PostgreSQL 模型中显式固定四个时间列为 `timestamp with time zone`，隔离 SqlSugar/Npgsql 全局 legacy timestamp 行为。
- 使用 EF Core 自身 `IMigrationsModelDiffer` 比较运行态模型与 migration snapshot，并把具体 operation 纳入 doctor、apply 和 Auth 启动前门禁。
- 增加空 PostgreSQL 首次 migration、重复执行、最终模型一致性测试。
- 增加真实 `Radish.DbMigrate.dll apply` 子进程集成测试，覆盖 SqlSugar 初始化、多个 PostgreSQL 数据库、首次 apply 与重入。
- 保留已发布 PostgreSQL / SQLite 初始 migration、designer、snapshot 与 migration history，不新增无实际 schema 变化的 migration。

## 本地验证

- OpenIddict SQLite / PostgreSQL Release 迁移矩阵：`6 passed`；覆盖空库首次迁移、重复执行、完整 EnsureCreated schema 接管、不完整 schema 拒绝和最终模型一致性。
- PostgreSQL / SQLite Release `has-pending-model-changes`：均报告无变化。
- DbMigrate Release 容器首次 apply：OpenIddict `applied=1`，最终 `pending=0`。
- DbMigrate Release 容器第二次 apply：OpenIddict `applied=0`，最终 `pending=0`。
- DbMigrate Release 容器 verify：通过；Main / Log / Message / Chat ledger 均保持 applied。
- `dotnet build Radish.slnx -c Release --no-restore`：通过，`0 warning / 0 error`。
- 注入 PostgreSQL 的 `dotnet test Radish.Api.Tests -c Release --no-build`：`635 passed / 0 skipped / 0 failed`。
- `npm run validate:ci`：通过。
- changed / staged repo hygiene：分别检查本批代码与文档文件，均通过；全仓扫描仍报告 `110` 个既有历史卫生问题和 `32` 个篇幅提醒，本批未新增也未扩入清理范围。
- `git diff --check`：通过；验证完成后工作区干净。
- 临时 PostgreSQL 容器、隔离数据库、一次性 DbMigrate 容器与本地诊断镜像均已清理。

## 远程候选结果

- Docker Images `#19` 的 Candidate Quality 失败：`632 passed / 3 failed / 0 skipped`，进程退出码为 `1`。
- 失败用例为 `TimeSemanticsPostgresIntegrationTest.Audit_ShouldRecognizePostgreSqlDateContracts`、`FileAccessTokenPostgresIntegrationTest.Consume_ShouldEnforceLimitAcrossPostgreSqlWorkers` 与 `ReliableOutboxPostgresIntegrationTest.Outbox_ShouldPreserveTransactionAndClaimSemantics_OnPostgreSql`。
- 三项失败均为 Npgsql 拒绝把 `DateTimeKind.Unspecified` 写入 PostgreSQL `timestamp with time zone`。这些直接创建 SqlSugar client / scope 的集成测试没有挂载生产参数规范化 AOP，并隐式依赖 Npgsql legacy timestamp 开关的初始化顺序；新增 OpenIddict 测试改变了进程内初始化时序，使门禁缺口稳定暴露。
- Candidate Quality 在镜像构建前失败，因此 Backend、Auth、Gateway、DbMigrate 与 Frontend 镜像 job 均未执行，没有可供生产前滚的 1203 镜像。
- 不移动或复用 1203 tag；根因修复转入 `v26.7.1.1204-release`。

## 服务器恢复边界

- 不删除或重建 PostgreSQL / Redis volume。
- 不修改已应用 schema ledger 或 `__EFMigrationsHistory`，不重写 1202 已成功应用的自然日 migration。
- 不移动或复用 1201 / 1202 / 1203 tag，不部署 `latest`。
- 1204 PR 合并、`master -> dev` 回灌、tag 与五镜像门禁完成前，不在生产服务器重复执行 `up -d`。
- 1204 镜像成功后固定 `RADISH_IMAGE_TAG=v26.7.1.1204-release` 前滚；DbMigrate 成功后才允许 API、Auth、Gateway 启动。

## 发布门禁

- [x] 完成批次级 build、test、`validate:ci`、repo hygiene 与 `git diff --check`
- [x] `dev -> master` PR required checks 全部通过并合并
- [x] 最新 `origin/master` 回灌 `dev`
- [x] 在 `master` 合并提交创建并推送 `v26.7.1.1203-release`
- [ ] Docker Images 五镜像门禁成功（Candidate Quality 失败，镜像未构建）
- [ ] 生产固定 tag 前滚与部署后复核完成（转入 1204）
