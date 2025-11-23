# 开发规范

- 养成一个好习惯，先写接口（IService），再写实现（Service）
- BaseRepository、BaseService、IBaseRepository、IBaseService 都已定义，后续增加业务只需要定义 Model 和 ViewModel 即可

## 项目结构约定

- docs/：项目文档，实际文件夹，映射解决方案中的 docs 目录，包含开发规范、设计文档等
- others/：其他资源文件，虚拟文件夹，只是解决方案中的文件夹，其中所有文件均为项目根目录下的，包括 Dockerfile、GitHub 配置、start.ps1 脚本 等
- radish.client：主要 - 前端 React 应用代码，TypeScript 编写
- Radish.Api：主要 - 后端服务代码，ASP.NET Core 编写
- Radish.Common：后端服务使用的普通工具类，例如基础日志、基础配置等；**仅能引用外部 NuGet 包，不允许依赖任何内部业务层**。若某工具/扩展需要访问 `Radish.Model`、Service 或 Repository 中的类型（如 DTO、实体、仓储服务等），应放置在 `Radish.Extension` 中，以免 Common 层被反向依赖导致环状引用。
- Radish.Core：后端核心业务逻辑与算法类，保留模块，为后续流程模拟与算法实现做准备
- Radish.Extension：后端扩展功能模块类，例如 Swagger/Scalar、HealthCheck 等
- Radish.Infrastructure：数据库与多租户基础设施层，集中维护 SqlSugar 扩展、租户过滤、表/库路由与连接解析逻辑，供 Extension、Repository 等项目复用
- Radish.IRepository：后端数据访问接口类，定义数据访问层接口
- Radish.IService：后端服务接口类，定义业务逻辑层接口
- Radish.Model：后端数据模型类，仅存放数据库实体；实体（Entity）只能在 Repository 层内被直接操作，Service 层及以上均需转换为视图模型或 DTO
- Radish.Model.LogModels：用于承载日志库实体，`BaseLog` 统一 DateTime/Level/Message 等字段，`AuditSqlLog` 通过 `[Tenant(configId: "log")] + [SplitTable(SplitType.Month)]` 按月写入日志；对应的 `AuditSqlLogVo` 仍位于 `ViewModels`，对外访问必须走 Vo/DTO
- Radish.Repository：后端数据访问实现类，具体实现数据访问接口
- Radish.Service：后端服务实现类，具体实现业务逻辑接口
- Radish.Shared：前后端共享的模型和工具类，例如 DTO、枚举等
- Radish.Api.Tests：xUnit 测试工程，目前包含 UserController 示例测试，约束接口返回示例数据
- native/rust（规划目录）：承载 Rust 扩展库或性能模块源码与 `cargo` 构建脚本，位于解决方案根目录；当前 `Radish.Core/test_lib` 只是互操作示例，正式原生模块应迁移到该目录并作为 Solution Folder 挂载。

## 分层依赖约定

- 前端项目（radish.client）仅依赖 npm 包
- 后端项目按层次结构依赖：
  - Radish.Api 引用 radish.client（用于 SPA 代理）与 Radish.Service，并通过 Program.cs 注入 `IUserService/IUserRepository` 等接口实现；同时依赖 Radish.Common 以注册 `AppSettings` 扩展，避免在其他层重复创建配置源。
  - Radish.Service 依赖 Radish.IService（接口契约）与 Radish.Repository（数据访问实现），负责聚合业务逻辑；Service 层对外仅暴露 DTO/Vo，必须在返回前将仓储层实体映射为视图模型（推荐 AutoMapper）。
  - Radish.Repository 依赖 Radish.IRepository、Radish.Model 以及 Radish.Infrastructure 中的 SqlSugar/租户扩展，只能向 Service 层返回实体或实体集合，禁止直接引用任何 Vo/DTO；接口层 Radish.IRepository 与 Radish.IService 统一依赖 Radish.Model，以便共享实体与视图模型定义。
- Radish.Extension 仅由宿主（Radish.Api）引用，用于集中管理 Autofac/AutoMapper/配置扩展；该项目可以引用 Service/Repository 以及 Infrastructure 以注册实现，但 Service/Repository 项目禁止反向依赖。凡是需要宿主信息的模块（如 Controller 程序集、配置源等）必须通过构造函数参数由宿主传入，例如 `new AutofacPropertyModuleReg(typeof(Program).Assembly)`，避免因为直接引用 `Program` 造成循环依赖。
  - Radish.Core 暂时保留，无直接依赖关系
- `UserController -> IUserService -> IUserRepository` 构成的示例链路是官方范例，任何新功能应当沿用“Controller 调用 Service，再由 Service 访问 Repository”的模式，并补齐对应接口定义

## 数据库与 SqlSugar 配置

- `Program.cs` 需要在 `builder.Build()` 前调用 `builder.Services.AddSqlSugarSetup()`。该扩展定义于 `Radish.Extension.SqlSugarExtension`，内部依赖 `Radish.Infrastructure.Tenant.RepositorySetting`、`TenantUtil` 等组件，使用 `SqlSugarScope` 单例注入并绑定所有连接配置。
- `appsettings.json` 约定结构如下：

```json
"MainDb": "Main",
"Databases": [
  { "ConnId": "Main", "DbType": 2, "Enabled": true, "ConnectionString": "Radish.db" },
  { "ConnId": "Log", "DbType": 2, "Enabled": true, "ConnectionString": "RadishLog.db", "HitRate": 50 }
]
```

  - `MainDb` 指定默认主库的 `ConnId`；当配置多库/主从时，`BaseDbConfig.MutiConnectionString` 会把该连接放在集合首位。
  - `Databases` 中至少包含 `ConnId=Main` 与 `ConnId=Log` 两条记录，后者名称固定（`SqlSugarConst.LogConfigId`），缺失时启动会抛出异常；其余库可自定义 `DbType` 与从库 `Slaves` 列表。
- `Snowflake` 段存放雪花 ID 的 `WorkId` 与 `DataCenterId`，本地/测试/生产必须配置为互不相同的整数（0~30），避免多节点冲突；`appsettings.json` 只保存兜底值，实际部署请在 `appsettings.{Environment}.json`（如 Development/Production）中覆盖，以便按服务器或容器分配固定编号。
- Program 在调用 `builder.Services.AddSqlSugarSetup()` 后会立即读取 `Snowflake` 段并设置 `SnowFlakeSingle.WorkId/DatacenterId`，因此只要在环境配置中写好差异值即可，无需在扩展或仓储层重复配置；若服务器时间曾被回拨，务必同步调整新的 WorkId。
- `BaseRepository` 通过 `IUnitOfWorkManage` 获取 `SqlSugarScope` 单例，内部根据泛型实体的 `[Tenant(configId)]` 特性切换连接；未标注特性的实体沿用 `MainDb`，标注了 `configId="Log"` 等值（大小写不敏感）的实体会自动访问对应库。需要写入日志库或其他独立库的模型务必显式添加 `TenantAttribute`，以免错写入主库；若实体启用了 `[MultiTenant(TenantTypeEnum.DataBases)]`，`BaseRepository` 会通过 `TenantUtil.GetConnectionConfig()` 在运行期动态添加租户库配置。
- 使用 SQLite 时 `ConnectionString` 只需传数据库文件名，运行期会自动拼接 `Environment.CurrentDirectory`；对于 MySQL/SQLServer 等外部数据库，可通过 `dbCountPsw1_*.txt` 本地文件或环境变量隐藏真实连接串，`BaseDbConfig.SpecialDbString` 会优先读取文件值。
- SQL 日志统一通过 `SqlSugarAop.OnLogExecuting` 写入 Serilog，`LogContextTool` 会在上下文中打上 `LogSource=AopSql` 标签；SqlSugar 的缓存实现委托给 `Radish.Common.CacheTool.SqlSugarCache`，保持与 Redis/内存缓存一致的策略。Serilog 输出由 `SerilogSetup + LogConfigExtension` 统一配置，普通日志与 SQL 日志会分别落入 `Log/Log.txt` 与 `Log/AopSql/AopSql.txt`，并启用 `WriteTo.Async()` 防止阻塞请求线程。

### 多租户隔离模式示例

- **字段隔离（单表）**：`BusinessTable : ITenantEntity` 依赖 QueryFilter 自动按 `TenantId` 过滤；调用 `GET /api/Tenant/BusinessTable` 可查看仅当前租户可见的数据，`.http` 示例位于 `Radish.Api/Radish.Api.http`。
- **表隔离**：`MultiBusinessTable` 标注 `[MultiTenant(TenantTypeEnum.Tables)]`，`TenantUtil.SetTenantTable()` 会在运行期改写表名为 `表名_{TenantId}`，示例接口 `GET /api/Tenant/MultiBusinessTable`。
- **分库隔离**：`SubLibBusinessTable` 标注 `[MultiTenant(TenantTypeEnum.DataBases)]`，`TenantUtil.GetConnectionConfig()` 根据租户配置动态注入连接，示例接口 `GET /api/Tenant/SubLibBusinessTable`（参考 `.http` 文件）。租户主数据需在数据库中预配 `TenantConfigId/DbConnectionStr` 等字段，且 `App.HttpContextUser` 必须包含合法 `TenantId`。

## 跨语言扩展（Rust 原生库）

- 目的：为 CPU 密集或高并发算法提供 Rust 实现，并通过 `[DllImport("test_lib")]` 在 `Radish.Api.Controllers.RustTest` 中验证性能差异。本阶段的 `Radish.Core/test_lib` 仅为演示，后续需将实际扩展迁入解决方案根目录的 `native/rust/{library}`，确保 Core 层保持纯 C# 领域模型。
- 构建流程：
  1. 安装 Rust 工具链（rustup + nightly/stable 均可），在仓库根目录执行 `cd Radish.Core/test_lib && cargo build --release`。
  2. 构建完成后会在 `target/release/` 生成 `test_lib.dll`（Windows）、`libtest_lib.so`（Linux）或 `libtest_lib.dylib`（macOS）。MSBuild/Docker 构建需在 `AfterBuild`/脚本阶段把对应文件拷贝到 `Radish.Api/bin/<Configuration>/net10.0/` 或发布目录，以便运行期自动加载。
  3. `RustTest` 控制器提供 `/api/RustTest/TestSum1~4` 四个端点，演示累加、类斐波那契、埃拉托斯特尼筛与并行质数计数；发布前请以 `?iterations=1_000_000` 等参数在本地验证返回结果与耗时。
- 目录规划：真实业务扩展应在 `native/rust/<库名>` 下维护 Cargo 工程，并附 README 说明导出函数签名/调用约定。`Radish.Core/test_lib` 仅保留最小示例，迁移完成后可以删除或仅做文档参考。
- 提交规范：Rust `target/` 目录与生成的 `.dll/.so/.dylib` 依旧忽略，必要时在 `.gitignore` 中新增排除项；若需要在 CI 中编译 Rust，请在构建脚本中加入 `cargo build --release` 与共享库复制步骤，保持与 DevelopmentPlan 中的原生扩展规划一致。

## 实体与视图模型规范

- 仓储层（Radish.Repository）只处理 `Radish.Model` 中定义的实体类型，禁止将实体对象直接向外暴露；Service 层获取实体后必须映射为视图模型再返回给 Controller。
- 视图模型命名以 `Vo` 为前缀，但不得只追加单个前缀；需结合业务含义进行缩写或扩写（例如 `VoUsrAudit`、`VoAssetReport`），从命名上进行模糊化以减少被直接猜测的风险。
- AutoMapper Profile 中维护实体与视图模型的对应关系；如需手动映射，也必须在 Service 层完成，确保 Controller 不访问实体。
- DTO/Vo 定义集中在 `Radish.Model` 或 `Radish.Shared` 的对应目录下，提交前请自检实体与视图模型字段是否同步更新。

## AutoMapper 与配置扩展

- `Radish.Extension/AutoMapperSetup` 负责集中注册全部 profile，并通过 `expression.ConstructServicesUsing` 使用 DI 容器解析依赖；新增 profile 时直接在 `AutoMapperConfig.RegisterProfiles` 中挂载。
- `AutoMapperConfig.RegisterCustomProfile` 可以配置全局规则（例如识别属性前缀/后缀、通用转换器），之后的 `RegisterProfiles` 列表挂载业务 profile（`RoleProfile`、`UserProfile`、`AuditSqlLogProfile` 等）；保持两段式注册可以避免 profile 顺序造成的耦合。
- `AuditSqlLogProfile` 已示范如何同时识别源和目标的 `Vo` 前缀：`RecognizeDestinationPrefixes("Vo")` + `RecognizePrefixes("Vo")` 使 `AuditSqlLog` 与 `AuditSqlLogVo` 能够双向自动映射，开发其他 Vo 模型时直接复用该写法即可。
- AutoMapper 授权：
  - 在 `appsettings.{Environment}.json` 中新增 `AutoMapper:LicenseKey`，严禁提交真实 key，可通过用户密钥或 Secret Manager 注入。
  - 运行时通过 `AppSettings.RadishApp(new[] { "AutoMapper", "LicenseKey" }).ObjToString()` 读取，并在 `expression.LicenseKey` 上设置；为空时自动跳过，避免影响本地调试。
- `Radish.Common.AppSettings` 为自定义配置入口，Program.cs 使用 `builder.Services.AddSingleton(new AppSettings(builder.Configuration));` 注册后即可在任何层注入/静态调用。
  - 当需要分段读取配置时，统一调用 `AppSettings.RadishApp(params string[] sections)`，禁止在业务代码中自行 new ConfigurationBuilder，以保证配置来源一致。
  - 强类型配置建议实现 `IConfigurableOptions`（位于 `Radish.Common.Option.Core`），由 `ConfigurableOptions` + `AllOptionRegister` 自动绑定，再通过 `IOptions<T>` 注入。
- `Radish.Common.Core.App` 负责缓存 `Configuration/WebHostEnvironment/RootServices`，Program.cs 必须依次执行 `hostingContext.Configuration.ConfigureApplication()`、`builder.ConfigureApplication()`、`app.ConfigureApplication()` 与 `app.UseApplicationSetup()` 才能完成注入；仅在无法通过构造函数注入时，才在静态上下文调用 `App.GetService<T>`、`App.GetOptions<T>`，并优先保证线程安全与生命周期一致。
- 对应扩展支持 `Get<T>()`、`ObjToString()` 等常用方法，可在新增配置时同步补充注释，方便多人协作。

## 缓存策略

- `Radish.Extension.RedisExtension.CacheSetup` 提供统一的 `AddCacheSetup()`，需在 Program 的服务注册阶段调用；该方法会读取 `Redis.Enable`/`Redis.ConnectionString`/`Redis.InstanceName` 并自动在 StackExchange.Redis 与内存缓存之间切换。
- 若 `Redis.Enable=true`，启动时会构建单例 `IConnectionMultiplexer` 并注册 `IRedisBasketRepository`（包含 List/Queue 等常用 API），其他场景使用 `Radish.Common.CacheTool.ICaching` 进行增删查；禁用 Redis 时自动回落到 `IMemoryCache` + `IDistributedMemoryCache`。
- 所有新增缓存键应通过 `ICaching.AddCacheKey*` 记录，便于 `DelByPattern`/`Clear` 等工具批量清理；若需发布订阅或队列语义，请依赖 `IRedisBasketRepository`，不要在业务代码中自行 new 连接。
- 请勿将生产 Redis 连接串直接写入仓库，示例配置仅用于本地调试，线上需要通过环境变量或 Secret Manager 注入。

## AOP 与日志

- `Radish.Extension/ServiceAop` 基于 Castle.DynamicProxy 实现接口拦截，当前主要用于捕捉 `BaseService<,>` 等应用服务的入参、响应与耗时信息，并通过 `AopLogInfo` 统一结构化输出。
- `AutofacModuleRegister` 已在泛型服务注册时启用 `.EnableInterfaceInterceptors().InterceptedBy(ServiceAop)`，如果后续服务需要自定义拦截，可在同一位置扩展拦截器数组。
- `Radish.Common/AopLogInfo` 集中维护 AOP 日志字段，调用层仅负责填充必要属性并交给日志基础设施处理，避免在各服务中手写日志模型。

## 项目依赖约定

- 前端依赖管理使用 npm 或 yarn，推荐使用 yarn 以提高安装速度和一致性
- 后端依赖管理使用 NuGet，推荐使用最新稳定版本的包（兼容 .NET 10）
- 所有第三方依赖需经过安全审查，确保无已知漏洞
- 定期更新依赖包，保持项目安全和性能
- 使用依赖锁定文件（如 package-lock.json 或 yarn.lock）确保团队成员使用相同版本的依赖
- 避免使用过时或不再维护的库，优先选择社区活跃且有良好文档支持的库
- 对于大型依赖，考虑使用按需加载或代码拆分以优化性能
- 记录所有依赖变更，确保团队成员了解更新内容和影响

## 示例实现与测试约定

- `Radish.Api/Controllers/UserController`、`Radish.Service/UserService`、`Radish.Repository/UserRepository` 与对应的接口项目组成“用户列表”示例。该示例演示分层调用方式和 DI 注册写法，亦为 Swagger/.http 文件中的演示请求提供数据。
- 示例仓储目前返回内存中的 Alice/Bob 两条静态数据，供开发联调、单元测试与文档说明复用；如需扩展，请保持“接口契约 + 数据映射 + 控制器”结构。
- `Radish.Api.Tests/Controllers/UserControllerTest` 使用 xUnit 校验示例接口返回 `OkObjectResult`，且至少包含两条用户数据；任何对示例链路的改动都必须同步更新该测试。
- `Radish.Api/Radish.Api.http` 已加入“用户列表”调用示例，可在调试时直接复用；如新增演示接口，应当在 .http 文件中同步记录。

## SignalR 实时交互规范

- Hub 位置：所有实时交互入口放在 `Radish.Api/Hubs`（命名为 `*Hub.cs`），继承 `Hub<TClient>` 以启用强类型调用；公共消息 DTO 定义在 `Radish.Model` 中，避免前后端字段不一致。
- DI 与调用：业务服务通过注入 `IHubContext<T>` 或 `IHubContext<T, TClient>` 发送消息，禁止在 Controller 中直接 new Hub；需要跨层推送时在 Service 层聚合，保持仓储层不涉及实时推送。
- 客户端：`radish.client` 使用 `@microsoft/signalr`，连接封装在 `shared/signalr/useSignalrHub.ts`（预留）中，负责断线重连、心跳与 Token 附带；前端只暴露订阅型 API。
- 安全：Hub 仅接受已认证用户，连接时附带 JWT（Query 或 Header），后台在 `OnConnectedAsync` 验证租户/角色；Hub 方法命名遵循 PascalCase，禁止接受动态字符串并在方法内再次鉴权。
- 可扩展性：生产部署多实例时启用 Redis Backplane，并限制客户端分组订阅；压测期间通过 `MaximumReceiveMessageSize` 控制 payload，必要时启用 MessagePack 序列化。
- 调试：添加 `Radish.Api/Hubs/hubs.http` 或在现有 `.http` 文件中记录 `/negotiate` 请求；本地开发启用 `builder.Services.AddSignalR().AddJsonProtocol(...)` 并结合浏览器 Network 面板排查。

## 前后端通信安全要求

- 所有前后端数据交互必须通过 HTTPS 进行，禁止使用明文 HTTP，确保传输链路具备 TLS 加密。
- 登录、密码重置、密保验证等含有敏感字段的请求，客户端需先使用项目约定的 RSA 公钥加密敏感参数，后端使用私钥解密，再进入业务流程。
- 若同时涉及本地缓存或离线存储，需确保不会以明文形式存储账号、密码、令牌等敏感信息。
- 公私钥对统一由后端团队生成和轮换，前端需在构建阶段或运行时安全加载最新公钥。
- 开发联调阶段同样遵循 HTTPS+RSA 约束，以防调试阶段泄漏敏感数据。

## 前端桌面化 UI 规范

> 详细的交互、设计 Token、跨端策略以 [FrontendDesign.md](FrontendDesign.md) 为准，此处保留关键守则，便于与后端规范并列查看。

- radish.client 以桌面模式为核心交互范式，首页加载后呈现类似 macOS 的桌面界面。
- 顶部为状态栏，需显示当前登录用户名、IP 地址以及预留系统状态信息区域。
- 底部为 Dock 栏，用于承载核心功能快捷入口；点击或双击图标都应维持桌面操作逻辑和动效。
- 左侧屏幕区域展示社区功能图标，用户双击后需弹出大弹窗（模态窗口）显示对应功能内容。
- 弹窗左上角包含最小化与关闭按钮，交互样式参考 macOS；最小化后回到 Dock 或桌面图标，关闭后释放资源。
- 状态栏、Dock、桌面图标以及弹窗需要统一的外观主题和响应式策略，优先适配桌面端分辨率，对移动端访问给出限缩体验或引导。
- 参考实现：`radish.client/public/webos.html`（Nebula OS 示例）展示了开机过渡、可拖拽窗口、多任务任务栏、Start 菜单与预置 Terminal/Editor/Browser 等应用图标；任何新页面都应优先在该场景中定义窗口外观与交互再迁移至 React 组件，保持动画节奏、图标尺寸与控件布局一致。

## radish.client 前端规范

> 技术栈、目录结构、状态管理、测试策略等完整说明见 [FrontendDesign.md](FrontendDesign.md)，以下列出与后端协作密切的约束。

1. 前后端传输的敏感字段计划使用 `encryptByPublicKey()` 加密，后端通过 `DecryptByPrivateKey()` 解密；方法虽未实现，但需提前规划调用点，保证接口兼容性。
2. 组件统一使用函数式写法，结合 `useState`、`useMemo`、`useEffect` 等 Hook 管理状态与生命周期，避免继续编写 Class 组件。
3. 使用 `const` 定义组件与内部函数，遵循 React 不可变范式，常规情况下避免 `function` 声明以减少作用域、提升与 `this` 绑定问题。
4. 禁用 `var`，默认 `const`，仅在确需重新赋值时选用 `let`；在 React 顶层逻辑几乎无需 `let`，如需持久化可变值，优先用 `const + useState` 组合处理。
5. React Compiler 仍属实验功能，主干暂不启用；若官方 GA 后可在独立分支尝试 `babel-plugin-react-compiler`，确认与 `radish.client` 的桌面化 UI、第三方组件兼容且能明显减少手写 memo，再提交 PR 合入主线。

## Radish.Api 接口规范

1. API 方法全部位于 Server 层 Controller 命名空间，统一使用 `[Route("api/[controller]/[action]")]` 作为路由前缀。
2. 需鉴权的 API 必须在 Controller 或 Action 上添加 `[Authorize(Permissions.Name)]`；无需鉴权的显式标注 `[AllowAnonymous]`，避免默认放行。
3. Controller Action 默认遵循 `[Produces("application/json")]` 与 RESTful 设计原则，除非业务场景要求其他内容类型或风格。
4. WeatherForecastController 中的 `GetById` 演示了 `[HttpGet("{id}")]` 路径参数写法，访问 `api/WeatherForecast/GetById/1` 可直接验证模型绑定；后续新增路由参数时沿用该示例的命名、注释和返回格式，便于单测覆盖。
5. 实体类存放在 Model 层 `Models` 命名空间并继承 `RootEntityTKey<TKey>`（含主键 Id）；若有自定义外键实体，同样继承该基类。视图模型位于 `ViewModels` 命名空间，按对外暴露字段设计，无继承硬性要求。
6. 实体与视图模型的映射集中在 Extension 层 `AutoMapper` 命名空间，每组实体定义独立 `CustomProfile`，避免在 Controller 或仓储中手动映射。
7. 新增对外接口遵循以下流程：
   （1）在 Model 层定义实体与视图模型；
   （2）在 Extension 层配置映射关系；
   （3）在 Core 层实现算法或数据处理；
   （4）在 IService 层声明接口；
   （5）在 Service 层实现接口，注入 `IBaseRepository` 或专用仓储并完成映射；
   （6）（可选）若泛型仓储不满足需求，在 IRepository/Repository 层定义并实现专用仓储；
   （7）在 Server 层通过依赖注入调用 IService，完成数据返回或存储。
8. Server 层对外暴露的 Controller 禁止直接注入 `IBaseRepository` 或任何业务仓储，所有数据访问需经由 Service 层封装。
9. 角色-API 授权依赖 `ApiModule.LinkUrl` 进行正则匹配，因此 URL 必须以 `/` 开头；若 Action 路由包含路径参数（如 `[HttpGet("{id:long}")]`），请在 `LinkUrl` 中写入对应的正则版本（示例：`/api/User/GetById/\d+`），否则 `RadishAuthPolicy` 无法命中该路由。
