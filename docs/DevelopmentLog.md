# 开发日志

## 第二阶段

> 当前阶段采用 .NET 10 + SQLSugar + PostgreSQL + React 技术栈；以下记录聚焦新架构的推进，后续如有重要调整也会在此处补充说明。

### 2025.11.19

- feat(repo/db): `BaseRepository` 改由 `SqlSugarScope` + `IUnitOfWorkManage` 承载数据库实例，并读取实体上的 `[Tenant(configId)]` 特性动态切换连接，默认走主库，`AuditSqlLog` 等标注了 `configId="Log"` 的实体自动写入日志库。
- feat(api/tests): WeatherForecast 控制器及其 xUnit 示例同步验证多种依赖解析方式，同时注入 `IBaseService<AuditSqlLog, AuditSqlLogVo>` 以演示日志库的查询链路，便于之后参考缓存、属性注入与多库访问的组合用法。

### 2025.11.18

- feat(log): 新增 `Radish.Model.LogModels.BaseLog` 与 `AuditSqlLog`，默认挂载 `Tenant(configId: "log")` 并按月分表落库，同时提供 `AuditSqlLogVo` 作为对外视图模型，保证查询日志时依旧走 DTO。
- fix(automapper): `AutoMapperConfig` 拆分 `RegisterCustomProfile()`，`AutoMapperSetup` 在构建配置时先注册自定义 profile，再集中挂载 `Role/User/AuditSqlLog` 映射；`AuditSqlLogProfile` 中启用了 `RecognizePrefixes/RecognizeDestinationPrefixes("Vo")`，作为首个 Vo 前缀双向映射样例。
- docs(todo): 启动阶段待接入 `AssertConfigurationIsValid()` 以确保所有 profile 均通过 AutoMapper 15 的配置校验，避免运行期才发现字段缺失。

### 2025.11.16

- feat(db): `Radish.Common.DbTool` + `Radish.Extension.SqlSugarExtension` 接入 SqlSugarScope，`BaseDbConfig` 统一读取 `MainDb` 与 `Databases` 列表，默认提供 `Main`（业务库）与 `Log`（日志库）两个 SQLite 示例，并在 Program 中以 `AddSqlSugarSetup()` 自动注入多库配置。
- feat(log/cache): 新增 `SqlSugarCache`、`SqlSugarAop` 与 `LogContextHelper`，将 SqlSugar 内置缓存委托至 `ICaching` 并把 SQL 日志推送到 Serilog，上下文可通过 `LogSource=AopSql` 快速检索。
- feat(model): `Radish.Model.RootEntityTKey<TKey>` 统一约束实体主键，`Role` 等实体继承该基类；`BaseRepository`/`BaseService` 暴露 `ISqlSugarClient` 实例以便服务层调试 Hash，对应接口同步更新。

### 2025.11.15

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
