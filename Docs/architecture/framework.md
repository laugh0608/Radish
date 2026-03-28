# 开发框架说明

> 本文是 Radish 的“架构与工程实现细节”说明，聚焦：技术栈基线、分层边界、关键中间件/宿主配置、演进与落地约定。
>
> 如果你只想快速建立全局认知，请先读 [架构总览](/architecture/overview)。若你在看当前进度与计划，请跳到 [开发路线图](/development-plan)。前端交互范式与 WebOS 细节见 [前端设计](/frontend/design)。

## 功能期望与范围

- **核心模块**
  - 身份与会话：自建帐号体系、邮箱/用户名登录、JWT + 刷新令牌、角色与权限控制、第三方登录预留。
  - 门户与文档：Radish.Gateway 提供统一对外入口和 `/server` 欢迎页面，并透传健康检查与文档入口；React 单页应用（radish.client）提供业务功能界面；Scalar 嵌入至 API 项目并通过 Gateway `/scalar` 暴露（`/api/docs` 仅保留重定向到 `/scalar` 以兼容旧链接）。
  - 内容域：分类 / 标签 / 帖子 / 评论 / 点赞 / 收藏 / 浏览计数，列表分页与过滤，富文本编辑。
  - 搜索：按标题、标签、分类、作者检索；支持时间/热度排序与模糊匹配（PostgreSQL `tsvector` 预留）。
  - 通知与订阅（可选）：帖子互动提醒、积分变动提醒，支持站内信或邮件。
  - 积分系统：积分账户、积分流水、规则引擎（发帖、互动、被采纳等）。
  - 商城系统：商品、库存、购买/退款、效果激活（头像框、昵称色、签名等）。
  - 管理后台（同 React 工程内实现管理视图）：分类、内容、用户、积分与商城配置。

- **非功能性要求**
  - 安全：所有外部访问统一收口到 Gateway 公共入口并以 HTTPS 对外暴露；测试部署可由 Gateway 容器直接提供 TLS，生产部署则由外部 `Nginx / Traefik / Caddy` 终止 TLS。前端不做自定义“二次加密”（不做 RSA 前端加密）。结合 JWT + Refresh、基于角色的授权、CSP/CORS、参数验证与敏感信息集中管控。开发阶段 Radish.Api 保留 HTTPS 端口便于直接调试，但在完成 Gateway/OIDC 接入并进入生产环境前，应关闭或限制直接暴露的 API HTTPS 端口，仅通过 Gateway/反向代理对外提供服务。
  - 性能：关键查询 P95 ≤ 200ms；SQLSugar Profile + PostgreSQL EXPLAIN 校验索引；读多写少场景可使用内存缓存。
  - 可用性：健康检查 `/health`, `/ready`; SQLSugar 迁移幂等；容器探针。
  - 可观测性：Serilog 结构化日志、请求跟踪 ID、PostgreSQL 慢查询日志、前端监控埋点。
  - 国际化：后端资源文件（zh-Hans 基线）+ `MessageModel` 三件套，前端 i18n（React i18next），详见 [国际化指南](/architecture/i18n)。
  - 质量：后端以 xUnit + Shouldly + Moq 为当前基线；前端当前以 `type-check`、`node --test`、`HttpTest` 与最小人工验收为主，Vitest / RTL / Playwright 仍属于后续增强方向。
  - 配置：`appsettings.{Env}.json` + 环境变量 + `.env`；禁止把密钥写入仓库。

- **里程碑**：参阅 [开发路线图](/development-plan)
  1. M1｜基础设施：解决方案构建、PostgreSQL + SQLSugar 通路、健康检查、React 脚手架跑通。
  2. M2｜领域建模：分类/帖子/评论/用户聚合与仓储、基础 CRUD API。
  3. M3｜应用服务：权限、DTO、业务编排、错误映射与日志规范。
  4. M4｜React MVP：认证、导航、列表/详情/发帖链路、态势监测。
  5. M5｜积分 + 商城：事件驱动积分、商城商品与库存闭环。
  6. M6｜容器与交付：Dockerfile/Compose、自检脚本、CI 验证。

- **范围外 / 边界**
  - 第三方支付、发票、合规认证（GDPR/等保）。
  - 大文件转码与 CDN；多端（小程序/Native App）。
  - 大规模分布式（消息队列、CQRS、多租户），当前阶段聚焦单体 + 水平扩展预留。

## 技术与架构基线

| 领域 | 选型 | 说明 |
| --- | --- | --- |
| 语言/运行时 | C# 13, .NET 10 | 使用 .NET 10 SDK，开启 `Nullable` 与 `ImplicitUsings` |
| Web Host | ASP.NET Core WebApplication | Program.cs 中最小宿主，按需要拆 Controller/Minimal API |
| ORM | [SQLSugar](https://github.com/donet5/SqlSugar) | Code First + Migration，仓储层集中管理上下文，支持读写分离配置 |
| 数据库 | PostgreSQL 16 | 默认端口 5432，连接通过 `ConnectionStrings__Default` 注入 |
| 前端 | React 19 + Vite + TypeScript | radish.client (WebOS)、radish.console (管理控制台)、radish.ui (@radish/ui 组件库)；使用 npm workspaces；当前以 Zustand 与共享 API 客户端为主要前端基线 |
| 前端构建 | Vite Rolldown，ESLint 9，TypeScript | 各项目独立构建：radish.client、radish.console；固定文档统一存放于 `Docs/`，由 API 启动时同步到 WebOS 文档应用 |
| 测试 | xUnit 3 + Shouldly + Moq，辅以 `HttpTest` | `Radish.Api.Tests` 目录承载后端测试与专题 `.http` 资产；前端当前仅有最小 `node --test` 与 `type-check` 基线 |
| 日志 / 配置 | Serilog + `Microsoft.Extensions.Configuration` | 支持 JSON + 环境变量 + 用户密钥；生产日志输出到 Console + Seq/Elastic 预留 |
| 容器 | `Radish.Api / Radish.Auth / Radish.Gateway / Frontend` Dockerfile 已落地 | 仓库已提供四个镜像入口、`Deploy/docker-compose*.yml` 最小编排与 `Docker Images` workflow；当前 `radish-api / radish-auth / radish-gateway / radish-frontend` 已统一纳入 `GHCR` 推送口径，`Frontend/Dockerfile` 也已收口为轻量 Node 多阶段镜像，本地验证体积约 `300MB`，剩余事项收束到上线前交付复核 |

### 本地启动脚本

- `start.ps1`（Windows/PowerShell）与 `start.sh`（Linux/macOS）提供交互式菜单，统一启动/组合 API、Gateway、前端、控制台与认证服务。
- 当前菜单大致包含两类选项：
  - **单服务**：仅启动 `Radish.Api` / `Radish.Gateway` / `radish.client`（前端）/ `radish.console`（控制台）/ `Radish.Auth`，或执行 `Radish.Api.Tests`。
  - **组合启动**：
    - PowerShell (`start.ps1`)：提供 “Gateway + Auth + API”“Frontend + Console”“Start ALL”。
    - Shell (`start.sh`)：提供 “Gateway + Auth + API”“Frontend + Console”“Start ALL”。
- 如需自定义构建配置，可在运行脚本前设置 `Configuration`（PowerShell 参数）或 `CONFIGURATION` 环境变量（Shell 脚本）。

### 分层视图

```
浏览器访问 Gateway (https://localhost:5000)
        │
        ├─→ /         (radish.client - WebOS 桌面与文档应用)
        ├─→ /console  (radish.console - 管理控制台)
        ├─→ /api      (Radish.Api - REST API)
        └─→ /scalar   (API 文档)

radish.client (WebOS)           radish.console (独立 SPA)
        │                              │
        ├─ 内置应用 (window)            ├─ OIDC 认证
        ├─ 文档/论坛/设置               ├─ 独立路由
        └─ 外部应用 (external)          └─ 管理功能
              │
              └─ Console (新标签页打开)

        共享组件库: @radish/ui

Radish.Api (ASP.NET Core)  ←────── Radish.Gateway
        │                          (YARP 反向代理)
Radish.Service (应用服务)
        │
Radish.Core (领域模型)
        │
Radish.Repository (SQLSugar)
        │
PostgreSQL / SQLite
```

**Phase 0 阶段**：Gateway 仅提供门户页面展示，不参与 API 请求转发
**P1+ 阶段**：Gateway 接管所有外部请求，转发至 Radish.Api 和其他服务

## 身份语义收敛（当前工程治理重点）

- 自 **2026-03-07** 起，Radish 将“Claim 解析收口”升级为“身份语义收敛”专项治理。
- 目标不是继续在控制器、Hub、中间件层补 `FindFirst(...)`，而是建立运行时唯一身份视图 `CurrentUser`，让业务与基础设施代码不再直接理解 Claim 结构。
- 协议边界（Auth 签发、`userinfo`、JWT/OIDC 配置）继续保留显式 Claim 语义；非协议边界运行时代码统一通过 `ICurrentUserAccessor` 获取当前用户。
- 详细设计见 [身份语义收敛与 Claim 治理设计](/architecture/identity-claim-convergence)，执行顺序见 [身份语义收敛迁移计划](/guide/identity-claim-migration)。

- `Radish.Api`
  - 负责 DI、配置、日志、全局异常、认证授权、Swagger/Scalar、HealthChecks。
  - 仅保留轻量 Controller/Endpoint，所有核心逻辑委派给 Service 层。
  - 配置加载：`ConfigureAppConfiguration` 会先清空默认源，再依次加载 `appsettings.Shared.json`、`appsettings.json`、`appsettings.{Environment}.json` 与 `appsettings.Local.json`，保证共享默认值先落地、宿主差异后覆盖，本地敏感值最后生效。
  - 雪花 ID：`Program` 在注册 SqlSugar 之后从 `Snowflake` 节读取 `WorkId`、`DataCenterId` 并写入 `SnowFlakeSingle`；当前约定 `WorkId` 保留宿主差异配置，`DataCenterId` 放在 `appsettings.Shared.json` 统一维护。多实例部署必须保证 `WorkId` 唯一，禁止把生产与本地设置为同一个编号。
  - 日志：宿主调用 `builder.Host.AddSerilogSetup()`，由 `Radish.Extension.Log` 统一配置输出目标。Serilog 默认读取 appsettings，写入控制台与 `Log/` 目录（普通日志 -> `Log.txt`，SqlSugar AOP 日志 -> `AopSql/AopSql.txt`），内部基于 `LogContextTool.LogSource` 区分日志类型并通过 `WriteTo.Async()` 异步落盘，避免阻塞请求线程；SQL AOP 生成策略由共享配置 `SqlAopLog` 控制，可按操作类型、表名、操作人和大文本字段做过滤或脱敏，当前默认跳过 `WikiDocument` 与 `WikiDocumentRevision` 两张文档同步表；业务代码默认直接使用 `Serilog.Log` 静态方法输出日志，仅在依赖外部框架时才注入 `ILogger<T>`。
  - API 文档：开发环境把 Scalar UI 映射到 `/scalar`（`/api/docs` 重定向到 `/scalar`），并通过 `builder.Services.AddOpenApi("v1|v2")` + `options.AddDocument(...)` 维护多版本；如需定制交互，可在 `Radish.Api/wwwroot/scalar/config.js` 中追加 JS 配置并在 `MapScalarApiReference` 中调用 `WithJavaScriptConfiguration`。
  - API 版本控制：采用 **URL 路径版本控制**（`/api/v{version}/[controller]/[action]`），基于 `Asp.Versioning.Mvc` (8.1.0) 实现；Controller 通过 `[ApiVersion("x.0")]` 特性声明版本，未指定版本时默认使用 v1.0。v1 包含核心稳定接口（Login、User），v2 包含新功能与实验接口（AppSetting、RustTest）；OpenAPI 文档通过 `IApiVersionDescriptionProvider` 自动发现所有版本，为每个版本生成独立文档（`/openapi/v1.json`、`/openapi/v2.json`），Scalar UI 提供版本下拉菜单，切换时文档自动过滤只显示对应版本的接口。详细规范见 [开发规范](./specifications.md) 的"API 版本控制规范"章节。
  - 本地调试：`Properties/launchSettings.json` 提供 `http`/`https`（仅启动 API）与 `https+spaproxy`（同时拉起 `radish.client` Vite 服务）两种 Profile，可在 VS/`dotnet run --launch-profile` 间切换作为"联调开关"。
  - 跨域：`appsettings.json` 中的 `Cors:AllowedOrigins` 维护允许访问 API 的前端地址，预设了 `localhost:3000` 以及 Rolldown 默认端口 `58794` 的多个别名（`vite.dev.localhost`、`host.*` 等）。更换端口或外网域名时记得同步更新该列表。
- `Radish.Gateway`
  - **Phase 0 职责**：承载 `/server` 欢迎页面（Razor Pages）、提供基础健康检查透传与文档入口链接，静态资源服务。服务总览与多服务健康状态表已迁移到 Console（`/console`）。
  - **依赖**：复用 `Radish.Common`（配置工具）和 `Radish.Extension`（日志扩展），保持与 `Radish.Api` 一致的配置加载和日志输出方式。
  - **监听端口**：`https://localhost:5000`（主要对外入口）和 `http://localhost:5001`（HTTP 仅用于重定向到 5000）。
  - **健康检查**：自身暴露 `/health`、`/healthz`，下游 `Radish.Api` 健康通过 `/api/health` 统一对外；Gateway 聚合首页、`/api/health`、`/console` 等关键入口状态。
  - **架构演进**：Phase 0 为简化门户，已实现 YARP 路由转发、统一入口规划等核心功能。详见 [Gateway 服务网关](/guide/gateway)。
- 配置与服务访问：Program.cs 依次调用 `hostingContext.Configuration.ConfigureApplication()` → `builder.ConfigureApplication()` → `app.ConfigureApplication()` → `app.UseApplicationSetup()`，把 Configuration/HostEnvironment/RootServices 注入到 `Radish.Common.Core.App`；在非 DI 管道下可使用 `App.GetService*`、`App.GetOptions*` 手动解析服务或配置。常规字符串读取仍统一使用 `AppSettings.RadishApp("Section", ...)`，批量强类型配置通过 `ConfigurableOptions + AddAllOptionRegister` 自动绑定 `IConfigurableOptions`。
- `Radish.Service`
  - 应用服务（`*AppService`）封装用例流程、权限校验、事务控制、DTO 转换。
  - 依赖 `Radish.Core` 接口与 `Radish.Repository` 实现，通过 `IUnitOfWork` 控制 SQLSugar 上下文。
  - 对外仅返回 DTO/Vo，禁止把 `Radish.Model` 中的实体直接暴露给 Controller；实体需在此层通过 AutoMapper（一律在 `Radish.Extension/AutoMapperSetup` 注册）转换为视图模型。
  - 当单个服务承担同一聚合下的多段复杂逻辑时，可在服务目录下继续按聚合建子目录，并使用 `partial class` 按职责拆分实现文件；例如论坛帖子服务可落在 `Radish.Service/Posts/PostService*.cs`，分别承载查询、发布、编辑、互动逻辑。
- `Radish.Core`
  - 聚合根（Post、Comment、Category、UserProfile、PointLedger、ShopItem 等）、值对象、领域事件。
  - 定义仓储接口与领域服务，例如 `IPostRepository`, `IPointPolicyService`。
- `Radish.Repository`
  - 持久化实现，集中 SQLSugar 配置（连接池、AOP 日志、软删除、审计字段）。
  - 提供迁移/种子帮助类，必要时拆分模块级仓储。
  - 仓储层仅依赖实体类型；返回 Service 层的对象必须是实体或匿名结构，禁止引用 DTO/Vo，保持“实体只存在于仓储层”原则。
- `Radish.Model`
  - DTO、ViewModel、查询对象、枚举。
  - 提供 `PagedRequest`, `PagedResponse<T>`, `ApiError` 等复用结构。
  - 视图模型需以 `Vo` 开头，并结合业务含义做缩写/扩写（如 `VoUsrAudit`, `VoAssetReport`），避免简单加前缀即可猜测真实用途。
- `Radish.Extension`
  - 横切关注：验证、缓存策略、OpenAPI 自定义、JWT 扩展、全局过滤器；按照职责拆分 `AutofacExtension/*`, `AutoMapperExtension/*`, `RedisExtension/*` 等子目录。
  - `RedisExtension.CacheSetup` 提供统一入口 `AddCacheSetup()`，根据 `Redis.Enable` 自动在 Redis（StackExchange.Redis）与内存缓存间切换，并在启用 Redis 时预先创建 `IConnectionMultiplexer`；当前约定 `Redis.Enable/ConnectionString` 放在 `appsettings.Shared.json`，`Redis.InstanceName` 保留在各宿主配置中做命名空间隔离。
  - `SqlSugarExtension.SqlSugarSetup` 负责注入 `ISqlSugarClient` 单例：内部读取 `Radish.Common.DbTool.BaseDbConfig` 生成的连接集合（含 `MainDb`、`Log` 以及所有从库），并在缺失日志库配置时直接抛出异常，确保多库配置在启动阶段即被验证。
- `Radish.Shared`
  - 常量、错误码、事件名、Options 绑定类型，以及跨模块共享的业务枚举（集中位于 `Radish.Shared.CustomEnum` 命名空间，例如 `UserStatusCodeEnum`、`UserSexEnum`、`DepartmentStatusCodeEnum`、`AuthorityScopeKindEnum`、`HttpStatusCodeEnum` 等）。
- `radish.client`
  - SPA + 内嵌管理视图；共享 DTO 通过 `Frontend/radish.client/src/types` 维护，与后端模型保持同步。
- `UserController -> IUserService -> IUserRepository` 示例链路
  - 通过内存仓储 + 应用服务演示分层合作，Program.cs 负责注入 `IUserService/IUserRepository`。
  - `.http` 文件与 `Radish.Api.Tests/Controllers/UserControllerTest` 均以该示例为基准，确保开发者可以快速验证分层约定。

### 架构示意

```mermaid
graph LR
    subgraph Frontend
        C[React SPA]
    end
    subgraph Backend
        A[Radi sh.Server]
        B[Radish.Service]
        D[Radish.Core]
        E[Radish.Repository]
    end
    subgraph Data
        F[(PostgreSQL)]
    end

    C -- REST/GraphQL? --> A
    A -- DI 调用 --> B
    B -- 领域接口 --> D
    B -- 仓储接口 --> E
    E -- SQLSugar --> F
```

## 核心功能模块（MVP）

| 模块 | 后端职责 | 前端职责 |
| --- | --- | --- |
| 帐号与权限 | 用户、角色、权限、JWT/刷新令牌、密码策略、邮箱验证、登录日志 | 登录/注册/忘记密码，角色控制菜单，Session 续期提示 |
| 分类/标签 | 聚合建模、层级/顺序维护、缓存/快照、只读列表接口 | 分类导航、筛选器、管理表单 |
| 帖子/评论 | CRUD、全文搜索预留、交互指标累加、幂等处理 | 列表、详情、编辑器、Markdown/富文本、骨架屏 |
| 点赞/收藏 | 多表事务（帖子计数 + 用户关系）、幂等 API | 双态按钮、Lazy Update、错误回滚 |
| 积分 | 规则配置、事件订阅（发帖、点赞、被采纳）、流水记录、积分账户锁定 | 积分面板、历史记录、规则展示 |
| 商城 | 商品/库存/价格、购买/退款/激活、权益发放、过期策略 | 商品列表/详情、购买流程、权益展示、我的物品 |
| 管理视图 | 统一权限守卫、数据导出、批量操作、操作日志 | React Admin Layout、表格与筛选、审计信息 |

## 实时交互（SignalR）

- 场景：帖子评论/点赞即时刷新、在线用户状态、积分变化提示、运营公告推送等需要“秒级到达”体验的功能统一通过 SignalR Hub 承载，避免重复造轮子。
- 架构：`Radish.Api` 内新增 `Hubs/*Hub.cs` 定义 strongly-typed Hub；业务层通过注入 `IHubContext<T>` 推送消息，或在 Service 内调度事件。客户端由 `radish.client` 使用 `@microsoft/signalr` SDK 建立连接，统一封装 `useSignalrHub` Hook。
- 协议：默认使用 JSON over WebSocket，自动降级为 Server-Sent Events/Long Polling；如需 Binary 可切换 MessagePack，需在前后端同时开启。
- 安全：连接时附带 JWT/AccessToken，服务端在 `OnConnectedAsync` 中验证身份与租户；Hub Method 名称使用 PascalCase 并在共享 DTO 中定义负载，禁止随意拼字符串。
- 扩展：多实例部署时启用 Redis Backplane（`Microsoft.AspNetCore.SignalR.StackExchangeRedis`）保持消息一致性；对公网推送需配置 TLS 与速率限制，可在 Nginx/Ingress 层结合 IP 限流。

## 数据与持久化策略

- SQLSugar 统一由 `SqlSugarScope` 单例提供，`Radish.Common.DbTool.BaseDbConfig.MutiConnectionString` 负责解析配置中的 `MainDb` 与 `Databases` 列表并生成所有连接配置。当前约定这两项放在 `appsettings.Shared.json` 统一维护，宿主按需在本地文件/环境变量覆盖。默认示例包含 `Main`（业务库）与 `Log`（日志库）两个 SQLite 文件，可扩展到 PostgreSQL/MySQL 等；若缺少 `Log` 库会在启动阶段直接抛出异常。
- `SqlSugarExtension.SqlSugarSetup` 会为日志库之外的所有连接注册到 `BaseDbConfig.ValidConfig`，并将 `SqlSugarConst.Log` 标记的配置注入日志上下文；SqlSugar 内部缓存通过 `SqlSugarCache` 委托给现有 `ICaching`，AOP 事件统一写入 Serilog，便于分析 SQL。
- 公共实体基类统一继承 `Radish.Model.Root.RootEntityTKey<TKey>`，并在派生类中补充审计字段（`CreatedAt/By`, `UpdatedAt/By`, `IsDeleted`, `ConcurrencyStamp` 等），保证主键类型可控且能被 SqlSugar 的 Attribute 正确识别。
- 软删除通过 SQLSugar Filter 全局开启；必要时在仓储层提供 `IncludeDeleted` 选项。
- 迁移策略：
  - 开发：`db.DbMaintenance.CreateDatabase()` + `InitTables()`。
  - 生产：通过 `Radish.Repository.Migrations` 导出 SQL 或在部署阶段运行迁移命令。
- 数据初始化：`SeedRunner` 负责创建默认分类、管理员、积分规则。
- PostgreSQL 特性利用：JSONB 列（存储自定义配置）、`tsvector` 搜索、行级锁（积分/库存扣减）。

## 前端架构与规范

- 详细的前端设计、桌面/移动交互规范与未来 React Native 规划均见 [前端设计](/frontend/design)，下文仅保留核心约束。
- Vite 配置 HTTPS、代理 API、环境变量区分（`.env.development` / `.env.production`），并在 `radish.client` 中启用 React 19 + Rolldown。
- 目录建议：`app/`（入口、providers、路由）、`features/*`（按业务拆包）、`widgets/*`（桌面组件）、`shared/*`（api/ui/config），保持与未来 RN 工程一致，方便共享包。
- 认证：封装 API 客户端自动附带 Token，失效触发刷新；敏感数据依赖 HTTPS 传输加密，后端负责安全存储与校验。
- 状态管理：当前以 Zustand 与共享 API 客户端为主，围绕窗口系统、通知、用户态等客户端状态组织；是否统一引入 TanStack Query / React Hook Form / Zod，留待后续阶段再评估。
- UI 与可访问性：当前以前端源码样式模块与 `@radish/ui` 自研组件为主；主题系统、全局动效体系与更完整的可访问性治理仍留作后续增强。
- 组件库：计划自研一套基础组件（Button/Input/Select/Checkbox/Radio/Switch/Transfer/Form 等），或在 antd、Arco 等库上做白标二次封装，封装层统一输出 API、主题 Token、交互规范与 Storybook 文档，供桌面/移动/RN 共享。推荐参考 [Uiverse Galaxy](https://github.com/uiverse-io/galaxy)（3500+ 社区驱动的开源 UI 组件，支持 CSS/Tailwind，MIT 许可）寻找设计灵感和参考实现。
- 桌面化交互规范：桌面 Shell + Dock + 窗口系统为核心体验，移动端自动切换至 Tab/Stack 结构；未来 React Native/Expo 应重用相同的 Design Token 与组件语义。
- 测试：当前前端以 `type-check`、最小 `node --test`、专题 `HttpTest` 与人工验收为主；组件级 Vitest / React Testing Library 与 Playwright 暂未成为仓库统一基线。

## DevOps 与运维基线

1. **配置与密钥**：
   - `appsettings.json` 仅放默认值，环境差异通过 `appsettings.{Env}.json` + 环境变量。
   - 本地秘密写入 `dotnet user-secrets` 或 `.env.local`（被 .gitignore 忽略）。
2. **日志与监控**：
   - Serilog 写入 Console + File；API / Gateway 已具备健康检查入口，当前以“日志 + 健康检查 + `DbMigrate doctor/verify`”作为最小自检基线。
   - OpenTelemetry Exporter、Prometheus 指标与更完整的 Tracing 仍处于后续规划阶段，尚未作为当前仓库既成能力。
3. **部署流水线**：
   - 当前仓库已具备 `Radish.Api`、`Radish.Auth`、`Radish.Gateway` 与 `Frontend` 四个 Dockerfile，多阶段构建资产已形成最小镜像链。
   - `Deploy/docker-compose.yml` 及其 `local / test / prod` 覆盖文件已形成仓库级最小编排；开发运行已明确独立于 Compose；`Docker Images` workflow 也已覆盖 `PR -> build only`、`push dev -> unified push`、`push v* -> unified release push` 三类触发。
   - 当前部署口径已明确分层：开发运行继续使用本地默认开发证书；测试部署由 Gateway 容器内直接提供 HTTPS，并自动生成 / 复用测试 TLS 与 Auth OIDC 证书；生产部署由外部反代终止 HTTPS，容器内部保持 HTTP，Auth OIDC 证书通过持久化挂载目录自动生成或预置后复用。
4. **质量门禁**：
   - PR 应附带与改动相匹配的构建 / 测试 / `type-check` / `HttpTest` 结果；若变更数据库需提供迁移脚本与回滚建议。
   - 关键模块需要 Code Review + Pair Walkthrough。

## API Gateway 规划（已完成 Phase 0）

- **当前状态**：Gateway 项目已完成 Phase 0 实施，实现了服务门户、YARP 路由转发、健康检查聚合等核心功能。
- **Phase 0 成果**：实现关注点分离，API 项目专注于提供接口服务，Gateway 承担统一入口和路由转发职责。
- **技术实现**：使用 YARP (Yet Another Reverse Proxy) 实现反向代理，支持 API、Auth、Docs、Console 等服务的路由转发。
- **详细文档**：完整的架构说明、配置指南、部署方案见 [Gateway 服务网关](/guide/gateway)。
- 新增服务/接口需要在 Gateway 配置中添加相应的路由规则，确保统一入口访问。

---

如需在架构或选型上做进一步调整，请同步更新本文件并在 DevelopmentLog 中记录决策，确保贡献者获取到一致的信息。
