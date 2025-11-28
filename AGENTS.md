# AGENTS 指南

## 快速认知
- Radish 是基于 ASP.NET Core 10 + SQLSugar + PostgreSQL（本地默认 SQLite）的分层内容社区，前端为 React 19 + Vite + TypeScript，网关 Radish.Gateway 承载门户及后续路由能力。
- 当前协作主分支为 `dev`；所有沟通、文档说明默认使用中文（代码与技术标识除外）。
- `Radish.slnx` 收纳全部后端项目，`docs/` 保持规范/计划/日志的唯一真相源；如需了解架构与需求，优先查阅 `docs/DevelopmentFramework.md` 与 `docs/README.md` 的索引。

## 仓库结构与分层职责
- 目录要点：`Radish.Api`（Web API 宿主）、`Radish.Gateway`（门户&未来网关）、`Radish.Service`/`Radish.Repository`/`Radish.Core`/`Radish.Model`（业务分层）、`Radish.Common`（通用工具，仅能引用外部包）、`Radish.Extension`（宿主引用的扩展/Autofac/AutoMapper/Redis/Serilog 注册）、`Radish.IService` 与 `Radish.IRepository`（接口契约）、`Radish.Shared`（跨端常量/DTO）、`Radish.Api.Tests`（xUnit 示例）、`radish.client`（React 应用）、`docs/`（所有文档）、`Radish.Core/test_lib`（Rust POC，正式原生库放 `native/rust`）。
- 层级依赖：Common → Shared → Model → Infrastructure → IRepository/Repository → IService/Service → Extension → Api/Gateway；`radish.client` 仅依赖 npm 包。Gateway Phase 0 只依赖 Common+Extension，后续 P1+ 才可引用 Service。
- 示例链路 `UserController -> IUserService -> IUserRepository` 体现 Controller → Service → Repository 调用顺序，任何新功能必须沿用该模式并补齐接口定义。

## 环境要求与启动方式
- 基础环境：.NET SDK 10（`global.json` 已锁定 10.0.0），Node.js 24+，PostgreSQL 16+（或使用仓库附带的 SQLite）。
- 一键脚本：`pwsh ./local-start.ps1` 或 `./local-start.sh`，提供启动前端/后端/Gateway/组合以及执行 `Radish.Api.Tests` 的 6 个菜单项，可通过 `Configuration` 参数或环境变量注入构建配置。
- 常用命令：
  - 后端：`dotnet restore && dotnet build Radish.slnx -c Debug`、`dotnet run --project Radish.Api/Radish.Api.csproj`、`dotnet watch --project Radish.Api`、`dotnet run --project Radish.Gateway/Radish.Gateway.csproj`、`dotnet test Radish.Api.Tests`。
  - 前端：`npm install --prefix radish.client`、`npm run dev --prefix radish.client`、`npm run build --prefix radish.client`、`npm run lint --prefix radish.client`。
  - 单测筛选：`dotnet test --list-tests`、`dotnet test --filter "FullyQualifiedName~UserControllerTest"`。
- 默认端口：API `https://localhost:7110` / `http://localhost:5165`、Gateway `https://localhost:5001` / `http://localhost:5000`、前端 Vite `https://localhost:58794`（亦支持 5173/localhost 组合，见 `appsettings.json` 的 CORS 列表）。Scalar UI 位于 `/api/docs`。

## 配置、数据库与安全
- 配置加载顺序：`appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json`（忽略提交） → 环境变量。新成员应复制 `Radish.Api/appsettings.Local.example.json`，并通过 `AppSettings.RadishApp` 或实现 `IConfigurableOptions` 读取强类型配置。
- `Snowflake.WorkId/DataCenterId` 在每个环境必须唯一；多实例部署需在对应 `appsettings.{Env}.json` 中覆盖，避免 ID 冲突。
- `Databases` 节至少包含 `ConnId=Main` 与 `ConnId=Log`（名称固定），缺少日志库将导致启动异常；`builder.Services.AddSqlSugarSetup()` 注入 `SqlSugarScope` 并关联租户/多库配置。需要按租户隔离时：字段隔离实现 `ITenantEntity`，表隔离使用 `[MultiTenant(TenantTypeEnum.Tables)]`，分库隔离使用 `[MultiTenant(TenantTypeEnum.DataBases)]`；日志实体（如 `AuditSqlLog`）需显式 `[Tenant(configId: "log")] + [SplitTable(SplitType.Month)]`。
- 默认 SQLite（`Radish.db`、`RadishLog.db`）自动创建，切换 PostgreSQL 时仅需更新连接串与 `DbType=4`。连接串、Redis、密钥等敏感信息只能出现在 Local/环境变量中，严禁写入版本库。
- Serilog 由 `builder.Host.AddSerilogSetup()` 配置，日志落盘 `Log/Log.txt` 与 `Log/AopSql/AopSql.txt` 并启用异步写入；AOP SQL 日志通过 `LogContextTool.LogSource` 区分。安全相关策略：所有前后端流量强制 HTTPS、登录等敏感字段在前端用 RSA 公钥加密、API 权限依赖 JWT+角色策略、CORS 白名单位于 `appsettings.json` 的 `Cors:AllowedOrigins`。

## 版本、里程碑与分支策略
- 遵循语义化版本号 `vMAJOR.MINOR.PATCH`，当日热修或阶段性合并可追加日期后缀 `v1.2.3.YYMMDD`。正式发布前移除日期后缀并在 GitHub Release 中记录 Feature/Bug Fix/Performance/Breaking Changes/Dependencies。
- 发布前需：规划版本 → 代码冻结 → 完整测试 → 同步更新 `Radish.Api/Radish.Api.csproj` `<Version>` 与 `radish.client/package.json` `version` → 打标签（`git tag -a vX.Y.Z`）→ 推送 Release → 按 `docs/DeploymentGuide.md` 部署。
- 项目里程碑与范围参阅 `docs/DevelopmentFramework.md`、`docs/DevelopmentPlan.md`，重大变更请同步到 `docs/DevelopmentLog.md`。

## 编码规范与架构约束
- “先接口后实现”：新增服务/仓储必须先在 `Radish.IService`/`Radish.IRepository` 声明，再在 `Radish.Service`/`Radish.Repository` 扩展；继承 `BaseService`/`BaseRepository` 以复用通用逻辑。
- 责任边界：`Radish.Common` 只容纳不依赖内部项目的通用工具；需要访问 Model/Service 的扩展请放 `Radish.Extension`。`Radish.Api`/`Radish.Gateway` 只作为宿主，配置/DI/日志统一在 Extension 中注册。
- 实体不可离开仓储层：实体定义位于 `Radish.Model/Models`，Controller 对外只返回 DTO/Vo（`Radish.Model/ViewModels`），AutoMapper Profile 统一在 `Radish.Extension/AutoMapperExtension` 注册。Service 层负责实体→DTO 的转换与业务编排。
- Controller 不得直接注入 Repository/IBaseRepository，所有数据访问必须通过 IService。业务逻辑、事务与权限判断放在 Service；全局拦截/日志/缓存等横切逻辑通过 `Radish.Extension` 的 Autofac 拦截器完成。
- C# 代码采用 4 空格缩进、文件范围命名空间、启用 nullable；公共成员 PascalCase，局部变量 camelCase，接口以 `I` 开头。React 组件/Hook 均用 TypeScript，组件文件 PascalCase.tsx、Hook `use` 前缀，使用 `const` 函数组件、避免 `var`，优先 `useState/useMemo/useEffect`，提交前运行 `npm run lint`。
- `radish.client` 采用桌面化 UI（顶栏状态、Dock、桌面图标/窗口交互），接口地址通过 `VITE_API_BASE_URL` 管理；敏感请求使用 HTTPS+RSA。
- Rust 扩展当前示例位于 `Radish.Core/test_lib`; 真正的原生库放置于根目录 `native/rust/{library}` 并在构建后拷贝到 `Radish.Api/bin/<Configuration>/net10.0/`。

## 常见开发流程
1. 在 `Radish.Model` 中添加实体/DTO/ViewModel，并按需扩展 `Radish.Shared` 的常量/枚举。
2. 在 `Radish.IRepository`/`Radish.Repository` 定义并实现仓储，使用 SqlSugar 特性（`TenantAttribute`、`SplitTable` 等）描述多租户或分表需求。
3. 在 `Radish.IService`/`Radish.Service` 补齐接口与实现，利用 AutoMapper/ICaching/IUnitOfWork 组织业务逻辑。
4. 在 `Radish.Api` 控制器中注入 IService 并暴露 API；需要示例请求时同步维护 `Radish.Api/Radish.Api.http`。
5. 更新 `Radish.Extension`（AutoMapper、Autofac 模块、配置）或 `Radish.Infrastructure`（租户/连接配置）以注册新模块。
6. 编写/更新单元测试（`Radish.Api.Tests`）与前端联动逻辑，必要时在 `radish.client` 同步 DTO 并更新页面/Hook。
7. 重大流程或约定变更请在 `docs` 中追加说明，并在 `DevelopmentLog.md` 写明日期和影响面。

## 测试与质量保障
- 后端：`dotnet test Radish.Api.Tests`（或 `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj`），调试单例测试可借助 `--filter`。修改示例 Controller（如 UserController、RustTest）时务必同步更新对应测试。
- 前端：规划使用 Vitest + React Testing Library；在 `radish.client` 中创建 `*.test.tsx` 并通过 `npm run test --prefix radish.client` 执行。提交前至少运行 `npm run lint`。
- 持续运行：`dotnet watch --project Radish.Api`、`npm run dev --prefix radish.client` 提供热重载；`local-start` 菜单的第 6 项可快速执行后端测试。
- 关键链路需补充 `.http` 手工验证、SQLSugar Profile 和 Gateway 健康页检查；性能或安全改动要记录验证方法。

## 文档、协作与提交要求
- 文档更新：任何流程/配置/脚本/规范变更同步到 `docs/`（如 DevelopmentSpecifications、ConfigurationGuide、DeploymentGuide），并在 `docs/DevelopmentLog.md` 记录关键节点。
- PR 说明需包含：变更摘要、测试结果、关联 Issue；前端 UI 贴截图/GIF，后端接口提供 `.http` 示例。多语言文档优先中文描述，可附英文注解。
- 提交遵循 Conventional Commits（`feat:`、`fix:`、`chore:`、`docs:` 等），描述具体变更，禁止使用诸如“update files”或带 AI 签名/Co-Authored 标记。一次提交聚焦单一主题，必要时拆分。
- 合规提醒：禁止把真实连接串、证书、`.user` 文件或其他敏感数据加入版本库；正式部署前依据 `docs/DeploymentGuide.md` 准备 Docker/Compose 配置与探针。

## 参考资料
- 设计与规范：`docs/DevelopmentFramework.md`、`docs/DevelopmentSpecifications.md`、`docs/FrontendDesign.md`、`docs/GatewayPlan.md`、`docs/AuthenticationGuide.md`、`docs/ConfigurationGuide.md`、`docs/DeploymentGuide.md`。
- 计划与记录：`docs/DevelopmentPlan.md`（里程碑/周计划）、`docs/DevelopmentLog.md`（日更日志）。
- 运行指南：根目录 `README.md`（快速开始）、`CLAUDE.md`（AI 协作者说明，包含命令、分层图与常见陷阱）。
