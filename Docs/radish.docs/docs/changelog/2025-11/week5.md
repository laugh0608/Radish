# 2025年11月 - 第5周（11月25日-11月30日）

> Gateway 暂缓与第二阶段总结

## 2025.11.27

- **feat(gateway/portal)**: 优化 Gateway 门户页面 URL 显示与配置管理
- **feat(gateway/status)**: Gateway Phase 0 调整为仅负责 `/server` 简单欢迎页与健康检查透传，其余服务总览迁移到 Console 控制台页面；控制台通过 Gateway `/console` 访问，并在内部展示前端、API、Docs、Console 的统一状态（所有探活均基于 Gateway 路径 `/`、`/docs`、`/api/health`、`/console`）。
- **chore(dev/ports)**: 统一本地开发端口约定：`Radish.Api` 使用 `http://localhost:5100`（内部），`Radish.Gateway` 使用 `https://localhost:5000` 与 `http://localhost:5001` 作为唯一对外入口；前端 `radish.client` 使用 `http://localhost:3000`，Docs 使用 `http://localhost:3001`（`base=/docs/`），Console 使用 `http://localhost:3002`。更新 `start.sh`/`start.ps1` 菜单文案与 ASPNETCORE_URLS，所有前端/API 对外访问统一通过 Gateway 转发（如 `/api`、`/docs`、`/console`）。

  - 修复服务卡片 URL 溢出问题：添加 `word-break`、`overflow-wrap` 自动换行支持
  - 增加服务卡片最小宽度至 280px，为 URL 提供更多显示空间
  - 实现从配置文件读取服务 URL：新增 `GatewayService.PublicUrl` 配置项
  - 移除 HTTP 端口显示，每个服务仅显示主 URL（开发环境使用 HTTPS 端口）
  - JavaScript 健康检查 URL 从配置动态读取，通过 `data-*` 属性传递
- **feat(gateway/config)**: 创建生产环境配置示例 `appsettings.Production.example.json`
  - 说明反向代理场景下的配置方式（公网域名 + 内网 HTTP 端口）
  - 提供生产环境 CORS、服务地址等配置示例
- **docs(deployment)**: 明确反向代理最佳实践：TLS 终止在反向代理层，后端服务使用 HTTP 端口
- **refactor(gateway/portal)**: Gateway 门户页面配置化改造，为生产环境部署做好准备

## 2025.11.25

- **decision(gateway)**: 复盘 Gateway 与 Auth 的优先级，确认当前阶段仅有 `Radish.Api + Radish.Auth` 两个宿主，复杂度有限，Gateway 投入收益不高；因此 Gateway 项目整体移至 M9（暂缓），只在接口/配置中保留将来透传所需的 Header/Trace/Token 字段。
- **plan(auth-first)**: 将"Radish.Auth + OIDC 客户端集成"明确为 M3-M4 的唯一主线，后续 WebOS/桌面子应用、Scalar OAuth、客户端管理均依赖该服务，团队资源集中支持 Auth 交付。
- **docs(plan/framework/gateway)**: `DevelopmentPlan.md` 更新里程碑表与周计划提示，强调 Gateway 暂缓、Auth 为前置；`DevelopmentFramework.md` 与 `GatewayPlan.md` 追加状态说明及执行条件，统一文档口径。

## 第二阶段总结

- **后端框架落地**：完成 `Radish.Api` 最小宿主与分层架构（Common/Core/Extension/Service/Repository/I* 项目）搭建，Autofac 容器、ConfigurableOptions、`AppSettingsTool`、`App.ConfigureApplication()` 四步绑定、SqlSugarScope、Snowflake ID、Serilog、Redis/内存缓存、泛型仓储与服务、AutoMapper、JWT + PermissionRequirement 等核心能力全部串联，可在开发/生产配置文件中无缝切换并支持多租户、多数据库、日志分流。
- **基础设施增强**：实现 SqlSugar 多库与日志库分离、AOP SQL 日志、`LogContextTool`/`SerilogSetup` 的统一输出策略、`SqlSugarCache`/Redis 缓存、`ServiceAop` 拦截、`TenantController` 的字段/表/库隔离示例、`WeatherForecast`/`RustTest` 的 DI/原生能力验证，确保关键横切关注点（配置、日志、安全、缓存、租户、测试样例）在进入业务阶段前就位。
- **模型与权限体系**：补齐 `ApiModule`、`RoleModulePermission`、`UserRole` 等实体与视图模型，`PermissionExtension` + `PermissionRequirementHandler` 动态拼装 `RadishAuthPolicy`，`IBaseRepository/IBaseService` 支持 Snowflake ID 返回、可空 Where、三表联查等增强，为 RBAC 与业务表 CRUD 提供统一基座。
- **文档与前端计划**：同步更新 DevelopmentFramework/Specifications/AuthenticationGuide/FrontendDesign 等文档，记录配置加载、日志策略、SqlSugar 多库、前端桌面化与标准化组件规划，为第三阶段（领域建模、应用服务、桌面化前端组件、DevOps 补强）提供一致的事实来源与验证 checklist。
