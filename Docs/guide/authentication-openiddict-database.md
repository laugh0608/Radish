# OpenIddict 数据库与迁移

> 本页定义 OpenIddict 独立数据库的模型归属、provider 配置、migration 生成、验证与正式应用入口。认证协议、Claims 与授权策略见 [鉴权与授权指南](/guide/authentication)。

## 当前边界

- `Radish.Auth.Persistence` 中的 `AuthOpenIddictDbContext` 是模型真相源。
- `Radish.Auth.Migrations.Sqlite` 与 `Radish.Auth.Migrations.PostgreSql` 分别维护 provider-specific EF migrations 和 snapshot。
- 本地默认使用 SQLite；测试 / 生产通过 `OpenIddict:Database` 使用 PostgreSQL。
- OpenIddict schema 只能由 `Radish.DbMigrate apply` 写入；Auth / API 启动不得调用 `EnsureCreated()`、`Migrate()` 或直接更新数据库。
- Auth 启动只读检查数据库存在性、pending migration 与运行态模型差异，未就绪时 fail-fast 并提示先执行 DbMigrate。

此前探索过的自定义 SqlSugar OpenIddict Store 未正式接入，也不再维护。业务数据继续由 SqlSugar 管理，OpenIddict 客户端、授权、Scope 与 Token 统一由 EF Core 管理。

## Provider 与存储位置

本地 SQLite 文件位于解决方案根目录 `DataBases/`：

```text
DataBases/
├── Radish.db
├── RadishLog.db
└── Radish.OpenIddict.db
```

部署态默认使用独立 PostgreSQL 数据库 `radish_openiddict`。DbMigrate、API 与 Auth 必须收到相同的 `OpenIddict:Database` 配置，不能让任一宿主回退到 SQLite。

PostgreSQL 模型中的授权和 Token 时间列显式固定为 `timestamp with time zone`。运行态模型必须与 PostgreSQL snapshot 一致，不得通过 `ConfigureWarnings`、`NoWarn` 或 legacy timestamp 全局开关压制差异。

## 配置

根 `appsettings.Shared.json` 的本地默认值使用：

```json
{
  "OpenIddict": {
    "Database": {
      "DbType": 2,
      "ConnectionString": "Radish.OpenIddict.db"
    }
  }
}
```

相对 SQLite 文件名会解析到 `{SolutionRoot}/DataBases/`。部署态通过嵌套环境变量切换 PostgreSQL：

```bash
OpenIddict__Database__DbType=4
OpenIddict__Database__ConnectionString='Host=postgres;Port=5432;Database=radish_openiddict;Username=radish;Password=...'
```

旧 `ConnectionStrings:OpenIddict` 只作为兼容入口；新配置必须优先使用 `OpenIddict:Database:*`。Compose 的正式配置见 `Deploy/docker-compose.yaml`。

## 运行职责

- **DbMigrate**：选择 provider，检查运行态模型与 snapshot，接管符合条件的旧 `EnsureCreated` 完整 schema，执行 pending migrations，并在末尾严格 verify。
- **Auth**：只读确认 schema 已就绪，再维护运行期官方客户端配置；不创建或迁移 schema。
- **API**：使用同一数据库和 OpenIddict manager 提供客户端治理能力；不迁移 schema。

旧 `EnsureCreated` schema 只允许在表、列、索引等完整契约满足时登记初始 migration；不完整 schema 必须拒绝，禁止伪造 `__EFMigrationsHistory`。

## 生成 migration

模型变化时必须为 SQLite 与 PostgreSQL 分别生成并审阅 migration。设计时 factory 读取可选的 `RADISH_EF_CONNECTION_STRING`，该变量只能指向隔离设计数据库。

```bash
# SQLite
dotnet ef migrations add <MigrationName> \
  --project Radish.Auth.Migrations.Sqlite \
  --startup-project Radish.Auth.Migrations.Sqlite \
  --context AuthOpenIddictDbContext \
  --output-dir Migrations

# PostgreSQL
RADISH_EF_CONNECTION_STRING='<isolated-design-database>' \
dotnet ef migrations add <MigrationName> \
  --project Radish.Auth.Migrations.PostgreSql \
  --startup-project Radish.Auth.Migrations.PostgreSql \
  --context AuthOpenIddictDbContext \
  --output-dir Migrations
```

已经发布的 migration、designer 和 snapshot 不得原地重写。模型真实变化应新增更高 migration；若只存在运行态配置错误，应修正模型配置并证明 snapshot 无差异。

## 验证门禁

每个 OpenIddict 模型或 migration 批次至少覆盖：

1. SQLite / PostgreSQL `has-pending-model-changes`。
2. 空数据库首次直接 `MigrateAsync`。
3. migration 重复执行与最终 pending 为零。
4. 完整旧 `EnsureCreated` schema 接管。
5. 不完整 schema 拒绝。
6. 运行态模型与 snapshot 最终一致。
7. Release / 容器路径中的真实 DbMigrate 首次 apply、重入与 verify。

PostgreSQL 用例必须使用真实临时数据库，并覆盖与生产一致的 Npgsql / EF Core 初始化顺序；不能只用 SQLite 或 mock 证明 provider 行为。

## 正式应用

正式数据库只能通过 DbMigrate 前滚：

```bash
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- apply
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- verify
```

测试 / 生产部署必须先备份，再运行 `apply`；DbMigrate 成功后才能启动 API、Auth 与 Gateway。禁止：

- 对测试 / 生产执行 `dotnet ef database update`。
- 删除数据库或 volume 规避 migration。
- 手工修改 `__EFMigrationsHistory`。
- 关闭 `PendingModelChangesWarning`。
- 让 Auth / API 启动自动修复 schema。

业务 Main / Log / Message / Chat 库不使用 EF snapshot，而使用有序 Radish migration 与每库 `RadishSchemaVersion` ledger，详见 [数据库结构变更协作口径](/guide/database-schema-change-governance)。
