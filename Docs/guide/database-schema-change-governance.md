# 数据库结构变更协作口径

> 本页用于明确 Radish 当前主业务库 schema 变更的正式路径，避免把其他仓库的 EF Core migration 机制误套到本仓库。

## 当前结论

- `Radish.DbMigrate` 管辖的主业务库当前处于正式上线前阶段，以 `实体定义 + SqlSugar Attribute + Radish.DbMigrate` 作为数据库结构真相源。
- 开发 / 本地环境通过 `Radish.DbMigrate` 执行 `CreateDatabase + InitTables` 同步结构；遇到破坏性 schema 收口时，可以删除本地 SQLite 后按当前实体重新初始化。
- 项目正式上线、存在需要保护的测试 / 生产数据库之后，再按发布版本生成、审核和执行发布 SQL；上线前不维护历史发布脚本。
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
- 若部分索引、种子或结构补丁不适合仅靠实体声明表达，可以在 `Radish.DbMigrate` 中补充显式逻辑。
- 破坏性字段清理、字段重命名和历史兼容层移除完成后，开发者应删除本地 SQLite 数据库并重新初始化，避免旧列继续干扰验证。

### 测试 / 生产

- 不在 Api / Gateway 启动时自动调用 `InitTables`。
- 正式上线前若需要测试库，优先按当前实体和 `DbMigrate` 初始化一套干净基线库，不携带预上线历史发布脚本。
- 项目正式上线或存在需要保护的正式数据库后，必须从已发布基线生成“旧版本 -> 新版本”的发布 SQL，审核后再部署。
- 发布 SQL 的目录、命名和回滚材料在进入正式数据库治理阶段时重新确认；不把上线前本地 SQLite 迭代历史当成生产迁移链。

## 标准流程

1. 修改 `Radish.Model` 中的实体、特性、索引定义，以及相关 `Vo`、AutoMapper、Service / Controller 调用。
2. 在开发库执行 `dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor`，再执行 `init` 或 `apply` 同步表结构。
3. 使用数据库客户端确认表、字段、默认值、索引与约束符合预期。
4. 若改动属于上线前破坏性 schema 收口，清理旧字段兼容逻辑，并提醒开发者删除本地 SQLite 后重新初始化。
5. 若改动涉及种子数据或开发库初始化，更新 `Radish.DbMigrate` 的受控初始化逻辑和相关测试数据构造。
6. 若项目已经存在正式数据库，再从已发布基线生成本次版本需要的发布 SQL，并随发布材料审核；否则不提交上线前历史发布脚本。
7. 在测试 / 生产部署前先执行本次版本对应的正式 SQL，再启动新版本宿主，并用 `doctor` / `verify` 做只读复核。

## 显式结构补丁口径

当前用户公开索引、电子宠物、写操作可靠性等结构已经沉淀为实体、索引声明、服务契约和 `Radish.DbMigrate` 初始化逻辑，不再维护上线前阶段的历史发布脚本。

开发库仍可以通过 `DbMigrate` 显式补丁承接以下需要：

- 结构初始化：新表、新列、索引和约束按当前实体同步。
- 种子数据：角色、权限、系统设置、保留账号和测试数据按当前模型初始化。
- 开发期纠偏：仅面向本地或测试基线库的受控修正，不作为生产历史迁移链。

运行时边界保持不变：

- Api / Gateway 启动时不自动执行 `InitTables`。
- 查询、登录、发布、购买等业务链路不得临时建表或补结构。
- 结构缺失应通过 `Radish.DbMigrate` 或正式发布 SQL 修复。

## 边界

- 正式上线前，表、字段、索引、约束这类 schema 变更，必须进入“实体定义 + DbMigrate + 文档 / 验证口径”这条路径。
- 正式上线后，涉及已有正式数据库的 schema 变更，必须补“发布 SQL + 发布 / 回滚材料”。
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
