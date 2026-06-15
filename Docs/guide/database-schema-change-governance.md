# 数据库结构变更协作口径

> 本页用于明确 Radish 当前主业务库 schema 变更的正式路径，避免把其他仓库的 EF Core migration 机制误套到本仓库。

## 当前结论

- `Radish.DbMigrate` 管辖的主业务库当前以 `实体定义 + SqlSugar Attribute + Radish.DbMigrate + 版本化差异 SQL` 作为数据库结构真相源。
- 开发环境通过 `Radish.DbMigrate` 执行 `CreateDatabase + InitTables` 同步结构；测试 / 生产环境通过审核后的版本化 SQL 显式执行结构变更。
- `doctor` / `verify` 当前是 `Radish.DbMigrate` 的只读自检入口，用于检查环境、连接定义、关键 `ConnId` 与主库业务表状态，不是 EF Core `ModelSnapshot` 式的迁移历史校验。
- 在 `Radish.DbMigrate` 管辖范围内，当前不维护 EF Core `Migrations/*.cs`、`*.Designer.cs`、`ModelSnapshot.cs` 这类正式历史链，也不要求执行 `dotnet ef migrations add ...`。
- 例外边界：`Radish.Auth` 中由 `AuthOpenIddictDbContext` 管理的 OpenIddict 独立库，仍按其 EF Core 机制维护；本页不覆盖该库。

## 环境分工

### 开发 / 本地

- 默认先执行 `doctor`，再根据需要执行 `init` 或 `apply`。
- `init` 会基于当前实体执行 `CodeFirst.InitTables`：
  - 新表会被创建；
  - 新增字段会自动补列；
  - 旧字段不会被自动删除。
- 若部分索引或结构补丁不适合仅靠实体声明表达，可以在 `Radish.DbMigrate` 中补充显式逻辑；但这类变更上线时仍要产出对应的版本化 SQL。

### 测试 / 生产

- 不在 Api / Gateway 启动时自动调用 `InitTables`。
- 先用 `DbMigrate init` 把一套基线库同步到最新实体，再生成“旧版本 -> 新版本”的差异 SQL。
- 差异 SQL 应按版本提交到仓库，建议放在 `Deploy/sql/*.sql`，并在部署新版本前显式执行。

## 标准流程

1. 修改 `Radish.Model` 中的实体、特性、索引定义，以及相关 `Vo`、AutoMapper、Service / Controller 调用。
2. 在开发库执行 `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor`，再执行 `init` 或 `apply` 同步表结构。
3. 使用数据库客户端确认表、字段、默认值、索引与约束符合预期。
4. 在一套用于对比的基线库上执行 `DbMigrate init`，生成本次版本需要的差异 SQL，并随代码一并提交。
5. 若本次改动涉及历史数据回填，可在迁移 SQL 中补安全的 `UPDATE`，或在 `Radish.DbMigrate` 的 `seed` 流程中补受控初始化逻辑。
6. 在测试 / 生产部署前先执行本次版本对应的 SQL，再启动新版本宿主，并用 `doctor` / `verify` 做最小只读复核。

## 显式结构补丁示例

当结构变更同时包含“新增字段 + 历史数据回填 + 唯一索引”时，开发库可以由 `DbMigrate` 显式补丁承接，但测试 / 生产仍需要版本化 SQL。

当前代表案例包括用户公开标识与电子宠物首批表：

### `User.PublicId` 与 `User.PublicIndex`

- 实体声明：`User.PublicId` 为可空字符串列，并声明 `idx_user_public_id` 唯一索引。
- 实体声明：`User.PublicIndex` 为可空 `long / Int64` 列，并声明 `idx_user_public_index` 唯一索引；普通用户公开索引从 `1000` 起，`1-999` 保留给系统、种子和内部账号。
- 开发库补丁：`DbMigrate apply` 会在旧库缺列时补齐字段，为旧用户回填 `usr_` + UUIDv7，并补唯一索引。
- 发布前 SQL：正式发布候选仍要生成旧版本到新版本的差异 SQL，至少覆盖新增列、旧用户回填、保留公开索引回填和唯一索引创建；当前入口为 `Deploy/sql/20260615_add_user_public_index.sql`。
- 运行时边界：Api / Gateway 启动时不自动执行 `InitTables`，避免宿主启动隐式改变测试 / 生产结构。

### `PetProfile` 与 `PetStatLog`

- 实体声明：`PetProfile` 承接每用户一只电子宠物主档，包含 `PublicId`、用户、名称、形态、成长阶段、状态数值、公开展示开关和最后照顾时间；`PetStatLog` 承接状态变化流水。
- 开发库路径：本地仍通过 `Radish.DbMigrate init/apply` 按实体同步 SQLite / 开发库结构。
- 发布前 SQL：测试 / 生产使用 `Deploy/sql/20260615_add_pet_tables.sql` 作为版本化差异 SQL 审核入口，覆盖两张表和 `PublicId`、`UserId`、`PetProfileId + CreateTime` 等索引。
- 运行时边界：`Pet/GetMy` 只读查询不隐式创建宠物；表结构缺失应通过迁移修复，不在 API 启动或查询链路中临时建表。

## 边界

- 表、字段、索引、约束这类 schema 变更，必须进入“实体定义 + DbMigrate + 版本化 SQL”这条正式路径。
- 只改查询逻辑、Service 行为、API 返回模型、前端展示时，不需要为了形式新增 schema SQL。
- 一次性数据修复、导入导出、运维排障脚本可以存在，但它们不是数据库结构的真相源，也不能替代正式 schema 变更流程。
- 若改动目标是 `AuthOpenIddictDbContext` 对应的 OpenIddict 独立库，应回到该库自身的 EF Core 流程处理，而不是套用本页。

## 参考外部项目时的取舍

- 可以借鉴的经验：
  - schema 变更必须有单一真相源；
  - 结构变更、种子数据、一次性修复、运维脚本要分角色治理；
  - 交付物必须可审查、可回滚、可追溯。
- 不直接照搬的机制：
  - EF Core `Migrations/*.cs` / `ModelSnapshot.cs` 历史链；
  - `dotnet ef migrations add ...` 生成迁移文件；
  - 把 `verify` 理解成“基于 migration 历史链”的校验。

## 相关入口

- [部署指南：数据库初始化与迁移](/deployment/guide#数据库初始化与迁移radishdbmigrate)
- [架构框架：数据与持久化策略](/architecture/framework#数据与持久化策略)
- [架构规范：新增实体与字段的标准流程](/architecture/specifications#新增实体与字段的标准流程)
