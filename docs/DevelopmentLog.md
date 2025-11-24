# 开发日志

## 第三阶段

> OIDC 认证中心与前端框架搭建

### 2025.11.24

- **arch(auth)**: 决定采用 OIDC（OpenID Connect）架构替代原有的简单 JWT 认证，使用 OpenIddict 作为认证服务器实现。主要考量：
  - 一次登录可访问多个客户端（Scalar API 文档、前端、后台管理、未来的商城等）
  - 标准化协议，便于未来扩展第三方登录
  - 支持 Authorization Code + PKCE 流程，更安全
  - 可独立扩展认证服务
- **plan(phase3)**: 重新规划第三阶段工作顺序，调整里程碑 M3/M4 内容：
  - M3 聚焦 OIDC 认证中心：创建 Radish.Auth 项目、设计身份数据模型、实现 DbSeed 初始化、配置 OpenIddict 端点、集成 Scalar OAuth
  - M4 聚焦前端框架与认证：搭建桌面模式骨架、基础组件库、OIDC 客户端集成、资源服务器配置
- **design(identity)**: 规划身份数据模型目录结构 `Radish.Model/Models/Identity/`，包含 User、Role、UserRole、UserClaim、Tenant、Permission、RolePermission 等实体
- **design(clients)**: 预定义 OIDC 客户端注册：
  - `radish-client` - WebOS 前端客户端（包含论坛/聊天/商城/后台管理子应用）
  - `radish-scalar` - API 文档客户端
  - `radish-rust-ext` - Rust 扩展项目客户端
- **docs**: 更新 DevelopmentPlan.md 里程碑概览与周计划，详细描述第 3-4 周的 OIDC 相关任务与验收标准
- **arch(open-platform)**: 确认开放平台需求，OIDC 客户端需支持后台动态配置：
  - 内部应用：radish-client（WebOS）、radish-scalar、radish-rust-ext
  - 第三方应用：未来支持动态注册和接入
  - 后台管理（作为 WebOS 子应用）需提供应用管理界面（CRUD、重置 Secret、启用/禁用）
- **arch(frontend/webos)**: 确认采用 **WebOS/超级应用** 架构，Radish 是运行在浏览器中的操作系统：
  - 单体应用 `radish.client`，不分离前后台
  - 桌面系统（Desktop Shell）：状态栏、图标网格、Dock、窗口管理器
  - 所有功能作为桌面应用：论坛、聊天室、商城、后台管理、API 文档
  - 应用注册表（AppRegistry）：统一管理应用元信息（类型、权限、图标）
  - 权限控制：根据用户角色动态显示/隐藏应用图标
  - 窗口类型：window（可拖拽）、fullscreen（全屏）、iframe（嵌入外部）
  - 后台管理作为 `apps/admin/` 子应用，全屏模式运行，使用 Ant Design 组件
- **arch(gateway)**: 决定 Gateway 暂缓，理由：
  - 当前后端服务仅 Auth + Api，复杂度不高
  - OIDC 已解决认证统一问题
  - 待服务数量增加或需要 BFF 聚合时再引入
- **docs**: 更新 DevelopmentPlan.md M3/M4 任务，添加客户端管理 API 和后台应用管理界面
- **docs(frontend)**: 完全重写 `FrontendDesign.md` 为 WebOS/超级应用架构文档：
  - 详细描述桌面系统、应用注册表、窗口管理器、子应用开发规范
  - 包含完整代码示例：AppRegistry、WindowManager、权限控制
  - 移动端适配策略、技术栈、性能优化、迭代计划
- **docs(plan)**: 调整 `DevelopmentPlan.md` M4 任务为 WebOS 架构：
  - 去掉前后台分离的 Monorepo 结构
  - 改为单体应用 + 桌面系统 + 子应用架构
  - 后台管理作为 `apps/admin/` 子应用，全屏模式
- **docs(open-platform)**: 更新 `OpenPlatformGuide.md` 应用矩阵：
  - 去掉 `radish-admin` 独立客户端
  - `radish-client` 包含所有子应用（论坛/聊天/商城/后台管理）
  - 调整系统架构图反映 WebOS 结构

## 第二阶段

### 阶段总结

- **后端框架落地**：完成 `Radish.Api` 最小宿主与分层架构（Common/Core/Extension/Service/Repository/I* 项目）搭建，Autofac 容器、ConfigurableOptions、`AppSettingsTool`、`App.ConfigureApplication()` 四步绑定、SqlSugarScope、Snowflake ID、Serilog、Redis/内存缓存、泛型仓储与服务、AutoMapper、JWT + PermissionRequirement 等核心能力全部串联，可在开发/生产配置文件中无缝切换并支持多租户、多数据库、日志分流。
- **基础设施增强**：实现 SqlSugar 多库与日志库分离、AOP SQL 日志、`LogContextTool`/`SerilogSetup` 的统一输出策略、`SqlSugarCache`/Redis 缓存、`ServiceAop` 拦截、`TenantController` 的字段/表/库隔离示例、`WeatherForecast`/`RustTest` 的 DI/原生能力验证，确保关键横切关注点（配置、日志、安全、缓存、租户、测试样例）在进入业务阶段前就位。
- **模型与权限体系**：补齐 `ApiModule`、`RoleModulePermission`、`UserRole` 等实体与视图模型，`PermissionExtension` + `PermissionRequirementHandler` 动态拼装 `RadishAuthPolicy`，`IBaseRepository/IBaseService` 支持 Snowflake ID 返回、可空 Where、三表联查等增强，为 RBAC 与业务表 CRUD 提供统一基座。
- **文档与前端计划**：同步更新 DevelopmentFramework/Specifications/AuthenticationGuide/FrontendDesign 等文档，记录配置加载、日志策略、SqlSugar 多库、前端桌面化与标准化组件规划，为第三阶段（领域建模、应用服务、桌面化前端组件、DevOps 补强）提供一致的事实来源与验证 checklist。

### 2025.11.23

- feat(log): 引入 `Radish.Extension.SerilogExtension`（`SerilogSetup` + `LogConfigExtension`），Program 通过 `builder.Host.AddSerilogSetup()` 安装 Serilog，统一输出到控制台与 `Log/` 目录，并用 `LogContextTool.LogSource` 区分普通日志与 SqlSugar AOP 日志；日志落盘使用 `WriteTo.Async()` 防止阻塞请求线程，同时在 `Log/SerilogDebug` 下收集 SelfLog。
- chore(common): `LogContextTool` 重命名并扩展 `SerilogDebug` 常量，供 Serilog 内部日志复用；`SqlSugarAop` 不再直接 `Console.WriteLine`，统一交给 Serilog 输出。
- chore(api/weather): `WeatherForecastController` 更新为注入 `ILogger<WeatherForecastController>`，演示同时使用 `_logger` 与 `Serilog.Log` 输出示例日志，避免 Autofac 无法解析非泛型 `ILogger` 的问题。

### 2025.11.22

- feat(config): `Program` 的 Host 配置阶段在清空默认配置源后同时加载 `appsettings.json` 与 `appsettings.{Environment}.json`，并在 SqlSugar 注册后读取 `Snowflake.WorkId/DataCenterId` 设置 `SnowFlakeSingle`，确保多实例按环境文件划分唯一 WorkId 且公共默认值仍写在基础文件兜底。
- docs(config/snowflake): DevelopmentFramework 与 DevelopmentSpecifications 说明新增环境配置加载顺序与 `Snowflake` 配置段的使用方式，明确所有服务器需在各自的环境文件里维护不同的 WorkId/DatacenterId，避免雪花 ID 冲突。

### 2025.11.21

- chore(host/startup): Program.cs 在执行 `app.Run()` 前输出 “Radish  --by luobo” ASCII 标识，方便在控制台明确当前运行实例与版本信息。
- docs(spec): 补充 `ApiModule.LinkUrl` 的正则写法与 `/` 前缀要求，开发规范强调包含路径参数的 API 必须在表中登记正则化 URL，避免授权策略匹配遗漏。

### 2025.11.20

- feat(infra): 新增 `Radish.Infrastructure` 项目沉淀 SqlSugar/租户相关的基础设施（`Tenant.RepositorySetting`、`TenantUtil` 等），由 Extension/Repository 统一引用，避免跨层循环依赖并集中维护多租户路由逻辑。
- feat(tenant): `TenantController` 与 `Radish.Api.http` 补充字段隔离、分表隔离与分库隔离示例接口，模型层新增 `BusinessTable`、`MultiBusinessTable`、`SubLibBusinessTable` 及其 Vo，Repository 通过 `TenantUtil.GetConnectionConfig()` 自动切换租户库。
- feat(automapper): `CustomProfile` 注册 Business/MultiBusiness/SubLibBusiness 的 Vo 映射，保障多租户示例无需手写转换即可返回 DTO。
- docs(auth): `AuthenticationGuide.md` 汇总当前 JWT 发行、授权策略与 PermissionRequirementHandler 流程，文档化登录接口的 Claim 组合与调试建议，方便新成员快速理解鉴权链路。

### 2025.11.19

- feat(auth): 将鉴权类型拆分到 `PermissionExtension`，新增 `PermissionItem` 与 `PermissionRequirementHandler`，运行时按“角色-API”关系动态组装 `RadishAuthPolicy`，并在 `RoleController`、`UserController` 等控制器上启用策略。
- feat(api/user): `UserController` 改为统一返回 `MessageModel`，新增 `GetUserById` 示例，`Radish.Api.http`/`http-client.env.json` 补充用户接口示例与最新 JWT，便于本地调试。
- feat(service/repo): `IBaseRepository`/`IBaseService` 增加 `QueryMuchAsync` 三表联查封装，`UserService.RoleModuleMaps()` 直接基于 SqlSugar Join 构建 `RoleModulePermission`，为权限处理器提供实时数据。
- chore(config): 轮换 JWT 密钥，`JwtTokenGenerate` 与 `Program` 保持一致；`appsettings.json` 新增 `AppSettings.UseLoadTest`，供鉴权测试时跳过登录校验；`PermissionRequirement` 仅保留配置，授权逻辑迁至 handler。
- test(api): `LoginControllerTest` 适配新的命名空间与假数据，实现 `RoleModuleMaps`/`QueryMuchAsync` 桩方法，继续验证登录流程。

### 2025.11.18

- feat(model/auth): 新增 `ApiModule`、`RoleModulePermission`、`UserRole` 实体与 `UserRoleVo`/`TokenInfoVo` 视图模型，补完 API 模块、角色-权限-按钮及用户-角色关联建模，支撑下一步的 RBAC 鉴权。
- refactor(model/user): User/MessageModel/RoleVo/UserVo 默认值与字段全面校准（含 Uuid、Vo* 命名、性别/年龄/状态默认值、消息默认提示等），保证接口契约在登录与下游消费场景下更一致。
- refactor(service/repo): `IBaseRepository/IBaseService` 的 `Query*` 支持可空 `Expression<Func<...>>`，Repository/Service 的实现亦同步泛化，`UserService` 的种子数据示例也调整为正式命名。
- feat(api/automapper): `LoginController` 标记为标准 API 控制器并注入 `IUserService`，AutoMapper Profile 为 User/Role/UserRole 映射补齐前缀识别与字段对应，便于后续直接复用 DTO。

### 2025.11.17

- feat(repo/db): `BaseRepository` 改由 `SqlSugarScope` + `IUnitOfWorkManage` 承载数据库实例，并读取实体上的 `[Tenant(configId)]` 特性动态切换连接，默认走主库，`AuditSqlLog` 等标注了 `configId="Log"` 的实体自动写入日志库。
- feat(api/tests): WeatherForecast 控制器及其 xUnit 示例同步验证多种依赖解析方式，同时注入 `IBaseService<AuditSqlLog, AuditSqlLogVo>` 以演示日志库的查询链路，便于之后参考缓存、属性注入与多库访问的组合用法。
- feat(api): WeatherForecastController 新增 `[HttpGet("{id}")]` 的 `GetById` 示例，可通过 `api/WeatherForecast/GetById/1` 直接验证路径参数绑定，后续 Controller 添加 REST 风格路由时可参照该写法。

### 2025.11.16

- feat(log): 新增 `Radish.Model.LogModels.BaseLog` 与 `AuditSqlLog`，默认挂载 `Tenant(configId: "log")` 并按月分表落库，同时提供 `AuditSqlLogVo` 作为对外视图模型，保证查询日志时依旧走 DTO。
- fix(automapper): `AutoMapperConfig` 拆分 `RegisterCustomProfile()`，`AutoMapperSetup` 在构建配置时先注册自定义 profile，再集中挂载 `Role/User/AuditSqlLog` 映射；`AuditSqlLogProfile` 中启用了 `RecognizePrefixes/RecognizeDestinationPrefixes("Vo")`，作为首个 Vo 前缀双向映射样例。
- docs(todo): 启动阶段待接入 `AssertConfigurationIsValid()` 以确保所有 profile 均通过 AutoMapper 15 的配置校验，避免运行期才发现字段缺失。

### 2025.11.15

- feat(db): `Radish.Common.DbTool` + `Radish.Extension.SqlSugarExtension` 接入 SqlSugarScope，`BaseDbConfig` 统一读取 `MainDb` 与 `Databases` 列表，默认提供 `Main`（业务库）与 `Log`（日志库）两个 SQLite 示例，并在 Program 中以 `AddSqlSugarSetup()` 自动注入多库配置。
- feat(log/cache): 新增 `SqlSugarCache`、`SqlSugarAop` 与 `LogContextHelper`，将 SqlSugar 内置缓存委托至 `ICaching` 并把 SQL 日志推送到 Serilog，上下文可通过 `LogSource=AopSql` 快速检索。
- feat(model): `Radish.Model.RootEntityTKey<TKey>` 统一约束实体主键，`Role` 等实体继承该基类；`BaseRepository`/`BaseService` 暴露 `ISqlSugarClient` 实例以便服务层调试 Hash，对应接口同步更新。

### 2025.11.14

- feat(core/native): 在 `Radish.Core/test_lib` 引入首个 Rust `cdylib` 示例，通过 `cargo build --release` 输出 `test_lib`，封装累加、斐波那契模拟、埃拉托斯特尼筛及并行质数计算，配合 Rayon/num_cpus 快速验证 CPU 密集算法表现。
- feat(api): 新建 `RustTest` 控制器，提供 `/api/RustTest/TestSum{1-4}` 基准接口，对比 C# 与 Rust 的执行耗时，并统一以 `DllImport("test_lib")` 调用共享库。
- docs(native): 当前 Rust demo 仅用于验证流程，正式扩展需迁至解决方案根目录（如 `native/rust/*`），并通过脚本在构建后自动拷贝 DLL/SO/Dylib，避免继续把 Core 层当成原生模块的承载目录。

### 2025.11.13

- feat(cache): 引入 `Radish.Common.CacheTool` + `Radish.Extension.RedisExtension`，配置项默认启用 Redis，可在 `Redis.Enable` 为 false 时自动退回内存缓存；提供 `ICaching` 与 `IRedisBasketRepository` 两层 API，并在 Program 中统一调用 `AddCacheSetup()` 完成注入。
- refactor(ext): Extension 与 Common 的通用组件重新按功能拆分到 `*Tool`、`AutofacExtension`、`AutoMapperExtension` 等目录，便于后续独立引用；AppSetting/WeatherForecast 控制器与单测同步调整引用路径。
- feat(core): 新增 `Radish.Common.Core.App/InternalApp`，集中感知 Host/Configuration/RootServices，并提供 `GetService*` 与 `GetOptions*` 辅助方法，便于在非 DI 管道内按需解析服务或读取配置。
- chore(host): Program.cs 引入 `ConfigureApplication()` 四步绑定（Host 配置 → Builder → App → `UseApplicationSetup`），运行期即可维护 `App.IsRun` 状态并在停止时刷新 Serilog。
- feat(api): WeatherForecastController 演示 `IServiceScopeFactory` + `App.GetService` 多种解析方式，AppSettingController 也补充 `App.GetOptions<T>` 示例，方便验证配置绑定是否生效。
- docs(config): DevelopmentFramework/Specifications 同步说明新的 App 服务入口使用方式与注意事项，确保后续贡献者遵循统一模式。

### 2025.11.12

- feat(config): `AppSettings.App` 更名为 `AppSettings.RadishApp`，统一入口避免与系统方法混淆，并同步更新 AutoMapper 与示例代码。
- feat(options): 新增 `ConfigurableOptions` + `AllOptionRegister`，集中注册所有实现 `IConfigurableOptions` 的配置项，Redis 配置抽象为 `Radish.Common.Option.RedisOptions` 并通过 `IOptions<T>` 注入。
- feat(api): 新建 `AppSettingController` 演示三种读取配置的方式（`RadishApp`、`GetValue`、`IOptions`），同时为 `appsettings.json` 增补注释与默认前缀。

### 2025.11.10

- feat(aop): 引入 `ServiceAop` + `AopLogInfo`，结合 Autofac 动态代理为泛型服务开启接口拦截，输出统一的请求/响应日志。
- docs(spec): 在 DevelopmentSpecifications 中新增 “AOP 与日志” 章节，说明拦截器、日志模型及扩展方式。
- chore(di): `AutofacModuleRegister` 启用服务/仓储拦截，并确保与 AutoMapperSetup、扩展模块协同；后续若添加新拦截器按此模式接入。

### 2025.11.9

- refactor(di): `AutofacPropertyModuleReg` 改为由宿主传入 `Assembly`，避免引用 `Program` 造成循环依赖，同时扩展层负责注册 Service/Repository 泛型实现。
- chore(project): 调整 Radish.Service 与 Radish.Extension 的引用方向，确保 IoC/扩展层仅被宿主引用，其余业务项目不再依赖扩展层。
- docs(spec): 在 DevelopmentSpecifications 中说明新的分层依赖约束与模块传参约定，方便后续贡献者遵循。

### 2025.11.8

- 重新创建项目，完全舍弃之前的代码，包括 ABP 框架与 MongoDB
- 重新使用 .NET 10 + React + Entity Framework Core/SqlSugar + PostgreSQL 技术栈
- 重新设计项目架构与模块划分

## 第一阶段

> 以下内容为历史记录，保留 ABP + Angular + MongoDB 方案的决策脉络以供参考，但不再作为现行实现依据。

### 2025.10.29

- feat(openiddict): 同时允许 http/https 回调与登出回调（含静默刷新），前端切换协议无需改配置。
- docs(readme): 重写根 README，整合 docs 内容，补充快速开始、HTTPS/CORS/SSO 指南与常用脚本。
- docs: 合并 DevelopmentBoard 至 DevelopmentPlan，统一为“开发计划与看板”，并更新索引与前端文档引用。
- docs(framework): 在 DevelopmentFramework.md 顶部新增“功能期望与范围”（功能/非功能、里程碑、范围外）。
- docs(index): docs/README.md 索引项更名为“项目总体功能与需求（含范围与里程碑）”。
- docs(framework): 增补“非目标与边界（Non-Goals & Boundaries）”详细清单，明确暂不覆盖项与阶段边界。

### 2025.10.28

- feat(host/mobile): 移除 Hero 区小屏“更多”下拉，统一使用下方移动端功能面板。
- feat(host/mobile): 重构移动端功能面板为两列栅格；语言入口改为下拉；“登录”在未登录时独占整行以提高可见性。
- style(host/mobile): 暗色模式适配移动功能卡片（半透明毛玻璃、清晰边框与阴影），并增强下拉与描边按钮在暗色下的对比度；主题切换、我的账户、注销统一为描边样式。
- style(host/mobile): “卡片密度”从整行分段控件调整为“标签 + 三按钮”，最终并入为“一体式分段控件（含标签的不可点击段）”，与其他按钮视觉一致。
- fix(host/mobile): 解决 Razor 编译错误（string 与 LocalizedHtmlString 混用），语言按钮使用 `L["Language"].Value`。
- fix(host/mobile): 分段控件按钮被全局 100% 宽度规则挤压的问题，显式设置 `width:auto` 恢复等分显示。
- feat(host/mobile): 分段控件支持“当前密度”选中高亮，并与本地存储 `radish.density` 联动初始化与更新。

### 2025.10.27

- feat(host): 新增“收藏”分区，将已收藏的应用卡片集中展示；无收藏时自动隐藏分区。
- feat(host): 卡片工具区固定两行两列（2x2），避免在窄宽度时遮挡标题/链接。
- feat(host): 最近访问仅保留最新 2 条，减少信息噪音。
- style(host): 缩小应用/收藏网格的纵向间距（--bs-gutter-y=1rem），两排卡片更紧凑。
- fix(host): 解决全屏点击“密度”按钮时下拉被 Hero 的 overflow 裁剪，展开时允许溢出、收起后恢复。
- docs(host): 同步 Host 首页功能说明（新增“收藏”分区、最近仅 2 条、工具区 2x2、间距与下拉裁剪修复说明）。

### 2025.10.26

- feat(angular/mobile): 新增移动端底部导航（自动从路由构建）、BottomSheet 子菜单与“更多”分组；滚动隐藏/显示；加入“管理”虚拟组；语言入口归入“更多”；桌面端新增明暗主题切换工具；移动端列表卡片化与小屏模态全屏。
- fix(angular/mobile): 修复子菜单交互与导航可靠性、语言切换即时生效并设置 html[lang]、Toolbar 注入与按钮丢失、并发请求降噪、分组检测等问题。
- chore(dev): 本地联调默认策略统一为“Host 以 HTTPS 提供 API(44342)，前端默认 HTTP(4200/5173)”，CORS 建议双协议且 Host 自动补全；为避免 44342 冲突，取消同端口双协议监听。配套文档与示例环境更新。
- docs(backend/frontend/SSO): 同步本地联调与证书信任说明、CORS/Redirect 示例、预检自测命令等；根 README_CN/EN 增补策略说明。
- fix(host,angular): 切换字体资源到国内镜像并增加全局字体回退，修复移动端叠层问题。
- feat(host): 重构 Host 首页（Radish Landing）：Hero 渐变与轻动效、响应式网格卡片、浅/深主题与持久化、移动端功能面板、最近访问、密度切换（含自动）、健康检查按钮、拖拽排序编辑、隐藏与显隐管理、区块标题与 I18N。
- fix(host): 修正 Hero full‑bleed/边缘过渡与容器布局细节；优化移动端操作区排布与卡片工具栏遮挡。
- docs(host): 新增 Host 首页功能说明文档（docs/Host-Home-Features.md），并在 docs/README.md 加入链接，完善导航。

### 2025.10.25

- feat(auth): Angular 端为主要路由加 authGuard，未登录访问自动跳转登录；保留 account 路由匿名访问，避免循环重定向。
- fix(host): 首页 Swagger/Scalar 卡片点击仅“原地刷新”——按应用 ClientId 自动补全 /swagger、/scalar。
- feat(openiddict): 数据种子将 Swagger/Scalar 的 ClientUri 初始化到具体文档页（/swagger、/scalar），并在迁移时同步更新已有记录；保留正确的 redirectUri（/swagger/oauth2-redirect.html、/scalar/...）。
- feat(backend): 启动时打印 CORS 允许来源；通过 .env 的 App__CorsOrigins 管理跨域来源，.env.example 预置本地来源示例（5173/4200）。
- refactor(config): 注释弃用 appsettings.json 的 App:CorsOrigins，统一改为 .env 的 App__CorsOrigins 管理。
- docs(backend/frontend/SSO): 同步统一 HTTPS 与 CORS 方案，完善 dev-certs 脚本与启动日志确认要点；提供 Host/DbMigrator 的 .env.example；为 React/Angular 指南补充配置要点。
- chore(dev): 统一本地 HTTPS 证书目录与引用；为 Angular/React 的 serve 配置证书。

### 2025.10.24

- feat(config): 按环境加载 .env，抽离敏感项并提供示例文件（Host/DbMigrator 支持 .env.development 与 .env.product，忽略本地 .env.development）。
- fix(swagger): 未登录访问 /swagger 时使用 Cookie 挑战，避免回调 HTML 导致 YAML 解析错误；允许已登录会话访问 Swagger，修正 Swagger/Scalar 客户端 RootUrl。
- docs(swagger): 保留 OIDC 所需的 /connect 路径并隐藏不必要的 ABP Schemas，对文档仅保留项目 API，隐藏基础设施端点。
- chore(frontend): CI 严格安装策略与贡献者设置收尾。

### 2025.10.23

- 导航/搜索：顶部导航中部改为搜索框，移动端抽屉提供搜索输入。
- Dock 底部栏：新增玻璃态半透明栏，宽度自适应，窄屏逐级隐藏；向下滚动隐藏、停止/向上滚动显示，提供返回顶部按钮。
- 交互/动效：桌面端支持 Dock 鱼眼放大动效；图标悬浮提示气泡、通知角标示例、右键/长按菜单。
- 布局/样式：主内容区根据 Dock 实际高度自适应底部留白；glass 效果与高光描边、投影；通过 ResizeObserver 写入 `--dock-inset-bottom` 并用于 body padding。
- 实现细节：useScrollDirection 阈值 4px、最小触发高度 80px、空闲显示延迟 260ms；仅 hover 设备通过 `matchMedia('(hover: hover)')` 启用动效；鱼眼放大使用高斯影响（最大缩放约 1.65，半径约 120px）。
- 影响范围：NavBar、BottomBar、DockToggle、StickyStack、全局样式 `index.css`；i18n 新增 `dock.*` 与 `menu.*` 文案。
- 今日提交：无（以上为工作区变更摘要）。

### 2025.10.20

refactor: 将react项目从js重构为ts

### 2025.10.8

feat: 暂时完成了 API 手动和自动控制器的版本控制

* feat: 完成了 Swagger 的多版本配置（鉴权认证还没做）
* feat: 完成了 Scalar 的多版本配置（鉴权认证还没做，而且还读取的是 `/swagger/xx/swagger.json` ）
* 目前主要是 ABP 框架的不兼容导致的，后面再研究怎么手动来实现：
  * 鉴权和 OIDC 认证
  * API 文档的注释
  * API 文档和 Schme 的过滤显示

### 2025.10.6

feat: 增加了 HttpApi.Host 项目首页的项目展示

### 2025.10.5

feat: 增加了 Scalar 配置

### 2025.10.4

docs: 完成了项目目录架构的初版

### 2025.9.29

build: 在 ABP Studio 中增加了 React 项目的启动脚本

### 2025.9.28

docs: 写了开发大纲

### 2025.9.27

feat: 决定使用 Radish 项目名称，创建 ABP 项目
