# 开发规范

- 养成一个好习惯，先写接口（IService），再写实现（Service）
- BaseRepository、BaseService、IBaseRepository、IBaseService 都已定义，后续增加业务只需要定义 Model 和 ViewModel 即可

## 项目结构约定

- docs/：项目文档，实际文件夹，映射解决方案中的 docs 目录，包含开发规范、设计文档等
- others/：其他资源文件，虚拟文件夹，只是解决方案中的文件夹，其中所有文件均为项目根目录下的，包括 Dockerfile、GitHub 配置、start.ps1 脚本 等
- radish.client：主要 - 前端 React 应用代码，TypeScript 编写
- Radish.Server：主要 - 后端服务代码，ASP.NET Core 编写
- Radish.Common：后端服务使用的普通工具类，例如基础日志、基础配置等
- Radish.Core：后端核心业务逻辑与算法类，保留模块，为后续流程模拟与算法实现做准备
- Radish.Extension：后端扩展功能模块类，例如 Swagger/Scalar、HealthCheck 等
- Radish.IRepository：后端数据访问接口类，定义数据访问层接口
- Radish.IService：后端服务接口类，定义业务逻辑层接口
- Radish.Model：后端数据模型类，仅存放数据库实体；实体（Entity）只能在 Repository 层内被直接操作，Service 层及以上均需转换为视图模型或 DTO
- Radish.Repository：后端数据访问实现类，具体实现数据访问接口
- Radish.Service：后端服务实现类，具体实现业务逻辑接口
- Radish.Shared：前后端共享的模型和工具类，例如 DTO、枚举等
- Radish.Server.Tests：xUnit 测试工程，目前包含 UserController 示例测试，约束接口返回示例数据

## 分层依赖约定

- 前端项目（radish.client）仅依赖 npm 包
- 后端项目按层次结构依赖：
  - Radish.Server 引用 radish.client（用于 SPA 代理）与 Radish.Service，并通过 Program.cs 注入 `IUserService/IUserRepository` 等接口实现；同时依赖 Radish.Common 以注册 `AppSettings` 扩展，避免在其他层重复创建配置源。
  - Radish.Service 依赖 Radish.IService（接口契约）与 Radish.Repository（数据访问实现），负责聚合业务逻辑；Service 层对外仅暴露 DTO/Vo，必须在返回前将仓储层实体映射为视图模型（推荐 AutoMapper）。
  - Radish.Repository 依赖 Radish.IRepository 与 Radish.Model 中的实体类型，只能向 Service 层返回实体或实体集合，禁止直接引用任何 Vo/DTO；接口层 Radish.IRepository 与 Radish.IService 统一依赖 Radish.Model，以便共享实体与视图模型定义。
  - Radish.Extension 仅由宿主（Radish.Server）引用，用于集中管理 Autofac/AutoMapper/配置扩展；该项目可以引用 Service/Repository 以注册实现，但 Service/Repository 项目禁止反向依赖。凡是需要宿主信息的模块（如 Controller 程序集、配置源等）必须通过构造函数参数由宿主传入，例如 `new AutofacPropertyModuleReg(typeof(Program).Assembly)`，避免因为直接引用 `Program` 造成循环依赖。
  - Radish.Core 暂时保留，无直接依赖关系
- `UserController -> IUserService -> IUserRepository` 构成的示例链路是官方范例，任何新功能应当沿用“Controller 调用 Service，再由 Service 访问 Repository”的模式，并补齐对应接口定义

## 实体与视图模型规范

- 仓储层（Radish.Repository）只处理 `Radish.Model` 中定义的实体类型，禁止将实体对象直接向外暴露；Service 层获取实体后必须映射为视图模型再返回给 Controller。
- 视图模型命名以 `Vo` 为前缀，但不得只追加单个前缀；需结合业务含义进行缩写或扩写（例如 `VoUsrAudit`、`VoAssetReport`），从命名上进行模糊化以减少被直接猜测的风险。
- AutoMapper Profile 中维护实体与视图模型的对应关系；如需手动映射，也必须在 Service 层完成，确保 Controller 不访问实体。
- DTO/Vo 定义集中在 `Radish.Model` 或 `Radish.Shared` 的对应目录下，提交前请自检实体与视图模型字段是否同步更新。

## AutoMapper 与配置扩展

- `Radish.Extension/AutoMapperSetup` 负责集中注册全部 profile，并通过 `expression.ConstructServicesUsing` 使用 DI 容器解析依赖；新增 profile 时直接在 `AutoMapperConfig.RegisterProfiles` 中挂载。
- AutoMapper 授权：
  - 在 `appsettings.{Environment}.json` 中新增 `AutoMapper:LicenseKey`，严禁提交真实 key，可通过用户密钥或 Secret Manager 注入。
  - 运行时通过 `AppSettings.App(new[] { "AutoMapper", "LicenseKey" }).ObjToString()` 读取，并在 `expression.LicenseKey` 上设置；为空时自动跳过，避免影响本地调试。
- `Radish.Common.AppSettings` 为自定义配置入口，Program.cs 使用 `builder.Services.AddSingleton(new AppSettings(builder.Configuration));` 注册后即可在任何层注入/静态调用。
  - 当需要分段读取配置时，统一调用 `AppSettings.App(params string[] sections)`，禁止在业务代码中自行 new ConfigurationBuilder，以保证配置来源一致。
- 对应扩展支持 `Get<T>()`、`ObjToString()` 等常用方法，可在新增配置时同步补充注释，方便多人协作。

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

- `Radish.Server/Controllers/UserController`、`Radish.Service/UserService`、`Radish.Repository/UserRepository` 与对应的接口项目组成“用户列表”示例。该示例演示分层调用方式和 DI 注册写法，亦为 Swagger/.http 文件中的演示请求提供数据。
- 示例仓储目前返回内存中的 Alice/Bob 两条静态数据，供开发联调、单元测试与文档说明复用；如需扩展，请保持“接口契约 + 数据映射 + 控制器”结构。
- `Radish.Server.Tests/Controllers/UserControllerTest` 使用 xUnit 校验示例接口返回 `OkObjectResult`，且至少包含两条用户数据；任何对示例链路的改动都必须同步更新该测试。
- `Radish.Server/Radish.Server.http` 已加入“用户列表”调用示例，可在调试时直接复用；如新增演示接口，应当在 .http 文件中同步记录。

## SignalR 实时交互规范

- Hub 位置：所有实时交互入口放在 `Radish.Server/Hubs`（命名为 `*Hub.cs`），继承 `Hub<TClient>` 以启用强类型调用；公共消息 DTO 定义在 `Radish.Model` 中，避免前后端字段不一致。
- DI 与调用：业务服务通过注入 `IHubContext<T>` 或 `IHubContext<T, TClient>` 发送消息，禁止在 Controller 中直接 new Hub；需要跨层推送时在 Service 层聚合，保持仓储层不涉及实时推送。
- 客户端：`radish.client` 使用 `@microsoft/signalr`，连接封装在 `shared/signalr/useSignalrHub.ts`（预留）中，负责断线重连、心跳与 Token 附带；前端只暴露订阅型 API。
- 安全：Hub 仅接受已认证用户，连接时附带 JWT（Query 或 Header），后台在 `OnConnectedAsync` 验证租户/角色；Hub 方法命名遵循 PascalCase，禁止接受动态字符串并在方法内再次鉴权。
- 可扩展性：生产部署多实例时启用 Redis Backplane，并限制客户端分组订阅；压测期间通过 `MaximumReceiveMessageSize` 控制 payload，必要时启用 MessagePack 序列化。
- 调试：添加 `Radish.Server/Hubs/hubs.http` 或在现有 `.http` 文件中记录 `/negotiate` 请求；本地开发启用 `builder.Services.AddSignalR().AddJsonProtocol(...)` 并结合浏览器 Network 面板排查。

## 前后端通信安全要求

- 所有前后端数据交互必须通过 HTTPS 进行，禁止使用明文 HTTP，确保传输链路具备 TLS 加密。
- 登录、密码重置、密保验证等含有敏感字段的请求，客户端需先使用项目约定的 RSA 公钥加密敏感参数，后端使用私钥解密，再进入业务流程。
- 若同时涉及本地缓存或离线存储，需确保不会以明文形式存储账号、密码、令牌等敏感信息。
- 公私钥对统一由后端团队生成和轮换，前端需在构建阶段或运行时安全加载最新公钥。
- 开发联调阶段同样遵循 HTTPS+RSA 约束，以防调试阶段泄漏敏感数据。

## 前端桌面化 UI 规范

- radish.client 以桌面模式为核心交互范式，首页加载后呈现类似 macOS 的桌面界面。
- 顶部为状态栏，需显示当前登录用户名、IP 地址以及预留系统状态信息区域。
- 底部为 Dock 栏，用于承载核心功能快捷入口；点击或双击图标都应维持桌面操作逻辑和动效。
- 左侧屏幕区域展示社区功能图标，用户双击后需弹出大弹窗（模态窗口）显示对应功能内容。
- 弹窗左上角包含最小化与关闭按钮，交互样式参考 macOS；最小化后回到 Dock 或桌面图标，关闭后释放资源。
- 状态栏、Dock、桌面图标以及弹窗需要统一的外观主题和响应式策略，优先适配桌面端分辨率，对移动端访问给出限缩体验或引导。

## radish.client 前端规范

1. 前后端传输的敏感字段计划使用 `encryptByPublicKey()` 加密，后端通过 `DecryptByPrivateKey()` 解密；方法虽未实现，但需提前规划调用点，保证接口兼容性。
2. 组件统一使用函数式写法，结合 `useState`、`useMemo`、`useEffect` 等 Hook 管理状态与生命周期，避免继续编写 Class 组件。
3. 使用 `const` 定义组件与内部函数，遵循 React 不可变范式，常规情况下避免 `function` 声明以减少作用域、提升与 `this` 绑定问题。
4. 禁用 `var`，默认 `const`，仅在确需重新赋值时选用 `let`；在 React 顶层逻辑几乎无需 `let`，如需持久化可变值，优先用 `const + useState` 组合处理。

## Radish.Server 接口规范

1. API 方法全部位于 Server 层 Controller 命名空间，统一使用 `[Route("api/[controller]/[action]")]` 作为路由前缀。
2. 需鉴权的 API 必须在 Controller 或 Action 上添加 `[Authorize(Permissions.Name)]`；无需鉴权的显式标注 `[AllowAnonymous]`，避免默认放行。
3. Controller Action 默认遵循 `[Produces("application/json")]` 与 RESTful 设计原则，除非业务场景要求其他内容类型或风格。
4. 实体类存放在 Model 层 `Models` 命名空间并继承 `RootEntityTkey<Tkey>`（含主键 Id）；若有自定义外键实体，同样继承该基类。视图模型位于 `ViewModels` 命名空间，按对外暴露字段设计，无继承硬性要求。
5. 实体与视图模型的映射集中在 Extension 层 `AutoMapper` 命名空间，每组实体定义独立 `CustomProfile`，避免在 Controller 或仓储中手动映射。
6. 新增对外接口遵循以下流程：
   （1）在 Model 层定义实体与视图模型；
   （2）在 Extension 层配置映射关系；
   （3）在 Core 层实现算法或数据处理；
   （4）在 IService 层声明接口；
   （5）在 Service 层实现接口，注入 `IBaseRepository` 或专用仓储并完成映射；
   （6）（可选）若泛型仓储不满足需求，在 IRepository/Repository 层定义并实现专用仓储；
   （7）在 Server 层通过依赖注入调用 IService，完成数据返回或存储。
7. Server 层对外暴露的 Controller 禁止直接注入 `IBaseRepository` 或任何业务仓储，所有数据访问需经由 Service 层封装。

