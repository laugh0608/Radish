# P3-12-F Q2-B 数据库演进与 schema ledger 方案

> 状态：`Release Go 必要子集已完成；验证见 Q2-B 数据库演进验证记录`
>
> 日期：`2026-07-12`（Asia/Shanghai）
>
> 范围：SqlSugar 管辖的 Main / Log / Message / Chat、OpenIddict EF Core 独立库、DbMigrate 命令职责、版本账本、生产相似升级与备份 / 前滚材料；不包含 Q2-C 产品版本真值、发布 tag 或生产部署。

## 摘要

当前 `Radish.DbMigrate apply` 实际执行“缺表 / 缺列时 Code First + 每次 seed + 若干无版本显式补丁”，没有迁移 ID、checksum、已应用记录或 pending 状态。新库初始化可用，但无法证明一套需要保护的旧库经历了哪些结构与数据补丁。

OpenIddict 独立库由 Auth 启动时 `EnsureCreated()` 写结构，仓库没有 EF migrations。该方式不能演进已有 schema，也把结构写入绑定到业务宿主启动，不符合 Release Go 的“先迁移、后启动、可验证”要求。

Q2-B 建立两个互补真相源：SqlSugar 业务库使用 Radish 自有 schema ledger 与有序迁移注册表；OpenIddict 使用 provider-specific EF Core migrations。所有结构写入统一由 `Radish.DbMigrate apply` 驱动，Auth / API / Gateway 启动不写 schema。

## 实施进度（2026-07-12）

- 已实现 `20260712_000_baseline`、逐 ConnId ledger、稳定 SHA-256 checksum 与 drift 拒绝；baseline adoption 会检查实体表 / 列和 Q2-A 时间语义。
- `apply` 已调整为“前置 doctor 错误门禁 → 业务 schema / ledger → OpenIddict migrations → seed → 严格 verify”；`doctor / verify` 会只读报告 OpenIddict applied / pending。
- 新增 `Radish.Auth.Persistence`、`Radish.Auth.Migrations.Sqlite` 与 `Radish.Auth.Migrations.PostgreSql`，Auth 移除 `EnsureCreated()` 并在启动时只读 fail-fast。
- SQLite / PostgreSQL 17 已覆盖空库迁移、provider 列类型、重入，以及旧 `EnsureCreated` 完整 schema adoption；缺索引 adoption 会被拒绝。
- EF Design 10.0.0 的 `System.Security.Cryptography.Xml` 传递依赖已钉住安全版本 `10.0.6`，依赖审计 High / Critical 为 `0`。
- `20260712_001_experience_natural_dates` 已实现：SQLite 通过受控重建保留索引，PostgreSQL 对 `timestamp with/without time zone` 分别按业务时区 / UTC 自然日转换；三列物理类型、异常拒绝、重入和 ledger 记账均有测试。
- 最终全量后端 `618` 通过、`7` 个 PostgreSQL 环境测试按约定跳过；date 新库契约、旧 timestamp 前滚、timestamptz 数据库侧审计和双 apply 锁均已在隔离 PostgreSQL 17 实跑通过。
- SQLite non-deferred 写事务、PostgreSQL transaction-scoped advisory lock 与 ledger 二次检查已完成，首次 baseline 与后续双 apply 实测均不会重复登记或执行 migration。
- SQLite 文件备份恢复自动化测试、PostgreSQL custom-format dump / restore / 再前滚演练均通过；证据见 [Q2-B 数据库演进验证记录](/records/p3-12-f-q2-b-database-evolution-validation-2026-07-12)。

## 一、版本账本

### 1. 表与字段

每个 SqlSugar 物理数据库维护 `RadishSchemaVersion`：

- `MigrationId`：稳定且全局唯一，例如 `20260712_000_baseline`。
- `Scope`：`Main / Log / Message / Chat`。
- `Description`：人工可读说明。
- `Checksum`：迁移定义的稳定 SHA-256，用于发现已发布迁移被修改。
- `AppliedAtUtc`：由 `TimeProvider` 写入的 UTC 时刻。
- `DurationMs`：执行耗时。
- `AppVersion`：可空；Q2-C 统一版本前不把 schema ID 绑定到当前漂移的产品版本。

迁移失败不写成功 ledger；错误由命令非零退出和日志留痕。单个迁移必须在目标 provider 支持的事务边界内完成，不能先记账后写数据。

### 2. 首个基线

- `20260712_000_baseline` 代表 Q2-B 接管前、Q2-A 已完成后的结构基线。
- 新空库由当前实体创建最新结构后登记基线和所有已包含迁移。
- 既有库只有在 `doctor / verify` 确认关键表、列、索引与 Q2-A 时间审计无异常后才能采用基线；不允许仅因“表存在”直接认领。

## 二、命令职责

### `doctor`

- 严格只读。
- 输出每个 ConnId 的 ledger 状态、当前 / 目标 migration、pending、checksum drift 和 adoption 阻断。
- 输出 OpenIddict provider、已应用 / pending EF migrations；连接失败只报告，不写库。

### `apply`

1. 执行 `doctor` 前置检查。
2. 对新库初始化当前结构并登记基线；对既有无账本库执行受控 adoption。
3. 按 ConnId 与 MigrationId 顺序执行所有 pending SqlSugar migrations。
4. 执行 OpenIddict 当前 provider 的 EF migrations。
5. 最后执行幂等 seed，并自动调用严格 `verify`。

### `verify`

- 严格只读，任何 pending、checksum drift、失败的迁移后置条件、OpenIddict pending migration 或关键时间异常均非零退出。

### `init`

- 仅面向空库 / 新环境创建最新结构。
- 不作为已有正式库的升级入口。
- 完成后登记最新 schema 状态，使后续 `apply` 不重复执行已包含的补丁。

## 三、迁移注册表

- 迁移实现必须显式声明 ID、Scope、Description、ChecksumSource、provider 支持范围、前置检查、Apply 与 Verify。
- 已进入 ledger 的迁移不可原地修改；修正必须新增更高 ID。
- SQL 与数据回填按 provider 分支集中在迁移类，禁止继续散落到 seed 或宿主启动路径。
- seed 只负责当前基线的幂等基础数据，不再承担结构版本演进。

首批候选：

1. `20260712_000_baseline`：接管当前 Q2-A 后结构。
2. `20260712_001_experience_natural_dates`：将 `ExpTransaction.CreatedDate`、`UserExpDailyStats.StatDate`、`UserExperienceGovernanceAction.StatDate` 从兼容时间戳迁移为 `date`；执行前复用 Q2-A 对齐审计，异常数据直接阻断。

## 四、OpenIddict 显式迁移

- 固定 `Microsoft.EntityFrameworkCore.Design 10.0.0` 与仓库本地 `dotnet-ef 10.0.0`。
- 同一个领域模型维护 SQLite / PostgreSQL 两套 provider-specific migration assembly / 目录，避免把 SQLite 列类型固化到 PostgreSQL migration。
- `Radish.DbMigrate` 负责选择 provider 并执行 `Database.MigrateAsync()`；Auth 移除 `EnsureCreated()`。
- Auth 启动只读检查 pending migrations；schema 未就绪时失败并提示先执行 DbMigrate，不自动修复。
- `OpenIddictSeedHostedService` 仅在 schema 已达目标版本后运行。

## 五、备份、前滚与停止线

- 任何既有库写迁移前必须完成可恢复备份；SQLite 为数据库文件副本，PostgreSQL 为目标 schema / database dump。
- 已执行 migration 默认只前滚；回退通过恢复备份并回滚应用版本完成，不维护未经演练的 down SQL。
- 生产相似演练必须覆盖旧基线 → 最新、重复 apply、严格 verify、异常数据拒绝和备份恢复。
- 不直接改当前业务数据库；开发期先在临时 SQLite 副本和隔离 PostgreSQL schema 验证。
- 不在 Q2-B 处理产品版本、tag、镜像标签或 Q3 Gateway smoke。

## 六、验证矩阵

- 空库：`init → apply → verify`，无 pending，重复执行不写重复 ledger / seed。
- 既有无账本库：符合基线可 adoption；缺表、缺列、索引漂移或时间异常拒绝 adoption。
- checksum：修改已应用迁移定义后 `doctor / verify` 失败。
- 并发：两个 apply 不能重复执行同一迁移；PostgreSQL 使用 advisory lock，SQLite 使用写事务 / 文件锁语义。
- 自然日：迁移前后业务日期保持不变，异常非业务日起点不自动平移。
- OpenIddict：SQLite / PostgreSQL 空库迁移、重入、pending 检查和种子均通过；Auth 启动不产生 DDL。
- 恢复：从备份恢复旧基线后可以再次完成前滚。

## 七、退出条件

- 所有正式数据库结构写入都有单一迁移 ID、checksum 和已应用记录。
- `doctor / apply / verify / init` 职责与退出码可测试且文档一致。
- OpenIddict 不再依赖 Auth `EnsureCreated()`，SQLite / PostgreSQL 显式迁移可重放。
- 首个生产相似快照升级、重入、严格 verify 与备份恢复演练通过。
- Q2-B 发布必要子集完成后，工程顺位进入 Q2-C 版本单一真值。
