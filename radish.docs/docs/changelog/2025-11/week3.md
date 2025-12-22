# 2025年11月 - 第3周（11月11日-11月17日）

> 配置系统、缓存、数据库与日志完善

## 2025.11.17

- feat(repo/db): `BaseRepository` 改由 `SqlSugarScope` + `IUnitOfWorkManage` 承载数据库实例，并读取实体上的 `[Tenant(configId)]` 特性动态切换连接，默认走主库，`AuditSqlLog` 等标注了 `configId="Log"` 的实体自动写入日志库。
- feat(api/tests): WeatherForecast 控制器及其 xUnit 示例同步验证多种依赖解析方式，同时注入 `IBaseService<AuditSqlLog, AuditSqlLogVo>` 以演示日志库的查询链路，便于之后参考缓存、属性注入与多库访问的组合用法。
- feat(api): WeatherForecastController 新增 `[HttpGet("{id}")]` 的 `GetById` 示例，可通过 `api/WeatherForecast/GetById/1` 直接验证路径参数绑定，后续 Controller 添加 REST 风格路由时可参照该写法。

## 2025.11.16

- feat(log): 新增 `Radish.Model.LogModels.BaseLog` 与 `AuditSqlLog`，默认挂载 `Tenant(configId: "log")` 并按月分表落库，同时提供 `AuditSqlLogVo` 作为对外视图模型，保证查询日志时依旧走 DTO。
- fix(automapper): `AutoMapperConfig` 拆分 `RegisterCustomProfile()`，`AutoMapperSetup` 在构建配置时先注册自定义 profile，再集中挂载 `Role/User/AuditSqlLog` 映射；`AuditSqlLogProfile` 中启用了 `RecognizePrefixes/RecognizeDestinationPrefixes("Vo")`，作为首个 Vo 前缀双向映射样例。
- docs(todo): 启动阶段待接入 `AssertConfigurationIsValid()` 以确保所有 profile 均通过 AutoMapper 15 的配置校验，避免运行期才发现字段缺失。

## 2025.11.15

- feat(db): `Radish.Common.DbTool` + `Radish.Extension.SqlSugarExtension` 接入 SqlSugarScope，`BaseDbConfig` 统一读取 `MainDb` 与 `Databases` 列表，默认提供 `Main`（业务库）与 `Log`（日志库）两个 SQLite 示例，并在 Program 中以 `AddSqlSugarSetup()` 自动注入多库配置。
- feat(log/cache): 新增 `SqlSugarCache`、`SqlSugarAop` 与 `LogContextHelper`，将 SqlSugar 内置缓存委托至 `ICaching` 并把 SQL 日志推送到 Serilog，上下文可通过 `LogSource=AopSql` 快速检索。
- feat(model): `Radish.Model.RootEntityTKey<TKey>` 统一约束实体主键，`Role` 等实体继承该基类；`BaseRepository`/`BaseService` 暴露 `ISqlSugarClient` 实例以便服务层调试 Hash，对应接口同步更新。

## 2025.11.14

- feat(core/native): 在 `Radish.Core/test_lib` 引入首个 Rust `cdylib` 示例，通过 `cargo build --release` 输出 `test_lib`，封装累加、斐波那契模拟、埃拉托斯特尼筛及并行质数计算，配合 Rayon/num_cpus 快速验证 CPU 密集算法表现。
- feat(api): 新建 `RustTest` 控制器，提供 `/api/RustTest/TestSum{1-4}` 基准接口，对比 C# 与 Rust 的执行耗时，并统一以 `DllImport("test_lib")` 调用共享库。
- docs(native): 当前 Rust demo 仅用于验证流程，正式扩展需迁至解决方案根目录（如 `native/rust/*`），并通过脚本在构建后自动拷贝 DLL/SO/Dylib，避免继续把 Core 层当成原生模块的承载目录。

## 2025.11.13

- feat(cache): 引入 `Radish.Common.CacheTool` + `Radish.Extension.RedisExtension`，配置项默认启用 Redis，可在 `Redis.Enable` 为 false 时自动退回内存缓存；提供 `ICaching` 与 `IRedisBasketRepository` 两层 API，并在 Program 中统一调用 `AddCacheSetup()` 完成注入。
- refactor(ext): Extension 与 Common 的通用组件重新按功能拆分到 `*Tool`、`AutofacExtension`、`AutoMapperExtension` 等目录，便于后续独立引用；AppSetting/WeatherForecast 控制器与单测同步调整引用路径。
- feat(core): 新增 `Radish.Common.Core.App/InternalApp`，集中感知 Host/Configuration/RootServices，并提供 `GetService*` 与 `GetOptions*` 辅助方法，便于在非 DI 管道内按需解析服务或读取配置。
- chore(host): Program.cs 引入 `ConfigureApplication()` 四步绑定（Host 配置 → Builder → App → `UseApplicationSetup`），运行期即可维护 `App.IsRun` 状态并在停止时刷新 Serilog。
- feat(api): WeatherForecastController 演示 `IServiceScopeFactory` + `App.GetService` 多种解析方式，AppSettingController 也补充 `App.GetOptions<T>` 示例，方便验证配置绑定是否生效。
- docs(config): DevelopmentFramework/Specifications 同步说明新的 App 服务入口使用方式与注意事项，确保后续贡献者遵循统一模式。

## 2025.11.12

- feat(config): `AppSettings.App` 更名为 `AppSettings.RadishApp`，统一入口避免与系统方法混淆，并同步更新 AutoMapper 与示例代码。
- feat(options): 新增 `ConfigurableOptions` + `AllOptionRegister`，集中注册所有实现 `IConfigurableOptions` 的配置项，Redis 配置抽象为 `Radish.Common.Option.RedisOptions` 并通过 `IOptions<T>` 注入。
- feat(api): 新建 `AppSettingController` 演示三种读取配置的方式（`RadishApp`、`GetValue`、`IOptions`），同时为 `appsettings.json` 增补注释与默认前缀。

## 2025.11.11

（无记录）
