# 数据库结构变更协作口径

> 本页用于明确 Radish 业务库与 OpenIddict 独立库的 schema 变更正式路径。

## 当前结论

- SqlSugar 管辖的 Main / Log / Message / Chat 以“实体定义 + 有序 Radish migration + 每库 `RadishSchemaVersion` ledger”为结构真相源；已发布 migration ID 与 checksum 不得原地修改。
- `init` 只面向空库创建当前结构并登记已包含版本；已有库统一通过 `apply` 前滚，不再把删除本地库作为默认治理办法。
- `doctor` 与 `verify` 严格只读；前者报告配置、结构、ledger 与 pending，后者把 Warning / Error 作为非零退出门禁。`apply` 负责迁移、seed，并在末尾自动严格 verify。
- 首个基线为 `20260712_000_baseline`；首个正式业务迁移为 `20260712_001_experience_natural_dates`，用于证明 SQLite / PostgreSQL 的结构与数据补丁可以共享同一 ledger 顺序。
- 自然日 migration 的业务含义、异常历史值拒绝和 API `DateOnly` 边界见 [时间语义与业务自然日](/guide/time-semantics)。
- `20260721_008_content_moderation_case` 是多表结构与历史事实迁移范例：同一 migration 创建案件 / 证据 / 事件 / 当前状态，回填历史举报和动作，并由 doctor / verify 检查来源错配、开放唯一性、序号、状态与业务键。
- migration 在事务内取得 provider 锁并二次读取 ledger：SQLite 使用 non-deferred 写事务，PostgreSQL 使用 transaction-scoped advisory lock。
- migration SQL 和结构审计必须通过数据库元数据解析实际表名、列名并按 provider 正确引用；不得假设 PostgreSQL 物理标识符保留 C# / SqlSugar 配置中的大小写。
- baseline 接管分表实体时，以数据库中实际存在的物理分表族为准，不要求并不存在的逻辑模板表；接管前仍须逐表核对必要列与结构。
- baseline 已登记后，`init`、Code First 和旧式补丁不得再静默修复缺表 / 缺列；结构漂移必须失败并新增更高 migration ID。
- baseline 已登记且存在 pending migration 时，`apply` 必须先按 ledger 顺序执行 migration，再以目标结构执行 seed readiness / strict verify；不能用“尚未迁移的旧结构不满足目标模型”提前阻断负责修复它的 migration。
- migration 的前置检查只验证安全执行所需条件，目标列类型、可空性、索引和数据语义由该 migration 的 `Verify` 检查。SQLite 重建表必须保留索引 / trigger，PostgreSQL 必须通过元数据解析真实标识符后精确修改约束。
- OpenIddict 独立库由 `Radish.Auth.Persistence` 定义模型，SQLite / PostgreSQL 分别维护 provider-specific EF migrations；结构写入同样只由 `Radish.DbMigrate apply` 驱动。
- Auth 启动不执行 `EnsureCreated()` 或 `Migrate()`；存在 pending migration 或数据库缺失时只读失败并提示先执行 DbMigrate。

## 环境分工

### 开发 / 本地

- 新环境执行 `init` 或直接执行 `apply`；已有环境先执行 `doctor`，完成备份后执行 `apply`，最后可单独执行 `verify` 复核。
- 新表、列、索引、约束和数据回填必须进入有序 migration；seed 只维护幂等基础数据，不承载 schema 演进。
- 迁移前置检查、Apply 与后置 Verify 必须同时覆盖 SQLite / PostgreSQL，不能依赖 Code First 静默修正既有正式库。
- 本地临时库仍可删除重建用于开发，但这不能替代已有基线库的升级验证。

### 测试 / 生产

- 不在 Api / Gateway 启动时自动调用 `InitTables`。
- 测试 / 生产数据库必须先备份，再运行同一 `DbMigrate apply` 迁移链；禁止宿主启动自动改 schema。
- 默认只前滚；应用回退必须与数据库备份恢复配套，不能依赖未经演练的 Down SQL。
- 每个发布候选至少覆盖旧基线升级、重复 apply、严格 verify、异常拒绝和备份恢复。

## 标准流程

1. 先定义稳定 migration ID、Scope、checksum source、provider 范围、旧结构前置条件、Apply、目标结构 Verify 与恢复方式。
2. 同步修改实体 / EF 模型和业务调用，确保新库当前结构与旧库前滚结果一致。
3. 在隔离 SQLite 与 PostgreSQL 基线库执行 `doctor → apply → verify`，同时覆盖“旧结构 + pending migration”、重入、已迁移结构和异常历史值拒绝；不得只测试空库当前结构。
4. 对既有数据库制作可恢复备份并完成恢复演练；保留批次级验证记录。
5. 部署时先运行 `Radish.DbMigrate apply`，成功后再启动 Api / Auth / Gateway；宿主只读校验 schema 就绪状态。

## 显式结构补丁口径

既有无账本库只能在关键表、列、索引和数据语义校验通过后采用 `20260712_000_baseline`。后续结构变化必须新增 migration，不能继续把无版本补丁散落在 seed 或宿主启动路径。

`DbMigrate` 有序 migration 承接以下需要：

- 结构初始化：空库按当前实体创建并登记已包含版本。
- 结构演进：已有库按 migration ID 前滚表、列、索引和约束。
- 种子数据：角色、权限、系统设置、保留账号和测试数据按当前模型初始化。
- 数据迁移：与 schema 变化不可分割的一次性回填必须具备前后置校验和 ledger 记录。

运行时边界保持不变：

- Api / Gateway 启动时不自动执行 `InitTables`。
- 查询、登录、发布、购买等业务链路不得临时建表或补结构。
- 结构缺失应通过 `Radish.DbMigrate` 或正式发布 SQL 修复。

## 边界

- 表、字段、索引、约束这类 schema 变更，必须进入“模型定义 + 有序 migration + DbMigrate + 文档 / 验证”路径。
- 涉及已有正式数据库时，必须补备份、前滚、验证与恢复材料。
- 只改查询逻辑、Service 行为、API 返回模型、前端展示时，不需要为了形式新增 schema SQL。
- 一次性数据修复、导入导出、运维排障脚本可以存在，但它们不是数据库结构的真相源，也不能替代正式 schema 变更流程。
- OpenIddict migration 由对应 provider 项目生成，但应用入口仍是 `Radish.DbMigrate apply`，禁止 Auth / API 自行迁移。

## 参考外部项目时的取舍

- 可以借鉴的经验：
  - schema 变更必须有单一真相源；
  - 结构变更、种子数据、一次性修复、运维脚本要分角色治理；
  - 交付物必须可审查、可回滚、可追溯。
- EF Core migrations 只用于 OpenIddict 独立库；SqlSugar 业务库使用 Radish ledger，不混用 EF ModelSnapshot。

## 相关入口

- [部署指南：数据库初始化与迁移](/deployment/guide#数据库初始化与迁移radishdbmigrate)
- [架构框架：数据与持久化策略](/architecture/framework#数据与持久化策略)
- [架构规范：新增实体与字段的标准流程](/architecture/specifications#新增实体与字段的标准流程)
