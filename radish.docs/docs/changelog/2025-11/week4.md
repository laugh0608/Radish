# 2025年11月 - 第4周（11月18日-11月24日）

> 权限体系、多租户与 OIDC 架构决策

## 2025.11.24

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
  - `radish-console` - 后台管理控制台
  - `radish-shop` - 商城应用（占位，未来实现）
- **docs**: 更新 DevelopmentPlan.md 里程碑概览与周计划，详细描述第 3-4 周的 OIDC 相关任务与验收标准
- **arch(open-platform)**: 确认开放平台需求，OIDC 客户端需支持后台动态配置：
  - 内部应用：`radish-client`（WebOS）、`radish-scalar`、`radish-console`、`radish-shop`
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

## 2025.11.23

- feat(log): 引入 `Radish.Extension.SerilogExtension`（`SerilogSetup` + `LogConfigExtension`），Program 通过 `builder.Host.AddSerilogSetup()` 安装 Serilog，统一输出到控制台与 `Log/` 目录，并用 `LogContextTool.LogSource` 区分普通日志与 SqlSugar AOP 日志；日志落盘使用 `WriteTo.Async()` 防止阻塞请求线程，同时在 `Log/SerilogDebug` 下收集 SelfLog。
- chore(common): `LogContextTool` 重命名并扩展 `SerilogDebug` 常量，供 Serilog 内部日志复用；`SqlSugarAop` 不再直接 `Console.WriteLine`，统一交给 Serilog 输出。
- chore(api/weather): `WeatherForecastController` 更新为注入 `ILogger<WeatherForecastController>`，演示同时使用 `_logger` 与 `Serilog.Log` 输出示例日志，避免 Autofac 无法解析非泛型 `ILogger` 的问题。

## 2025.11.22

- feat(config): `Program` 的 Host 配置阶段在清空默认配置源后同时加载 `appsettings.json` 与 `appsettings.{Environment}.json`，并在 SqlSugar 注册后读取 `Snowflake.WorkId/DataCenterId` 设置 `SnowFlakeSingle`，确保多实例按环境文件划分唯一 WorkId 且公共默认值仍写在基础文件兜底。
- docs(config/snowflake): DevelopmentFramework 与 DevelopmentSpecifications 说明新增环境配置加载顺序与 `Snowflake` 配置段的使用方式，明确所有服务器需在各自的环境文件里维护不同的 WorkId/DatacenterId，避免雪花 ID 冲突。

## 2025.11.21

- chore(host/startup): Program.cs 在执行 `app.Run()` 前输出 "Radish  --by luobo" ASCII 标识，方便在控制台明确当前运行实例与版本信息。
- docs(spec): 补充 `ApiModule.LinkUrl` 的正则写法与 `/` 前缀要求，开发规范强调包含路径参数的 API 必须在表中登记正则化 URL，避免授权策略匹配遗漏。

## 2025.11.20

- feat(infra): 新增 `Radish.Infrastructure` 项目沉淀 SqlSugar/租户相关的基础设施（`Tenant.RepositorySetting`、`TenantUtil` 等），由 Extension/Repository 统一引用，避免跨层循环依赖并集中维护多租户路由逻辑。
- feat(tenant): `TenantController` 与 `Radish.Api.http` 补充字段隔离、分表隔离与分库隔离示例接口，模型层新增 `BusinessTable`、`MultiBusinessTable`、`SubLibBusinessTable` 及其 Vo，Repository 通过 `TenantUtil.GetConnectionConfig()` 自动切换租户库。
- feat(automapper): `CustomProfile` 注册 Business/MultiBusiness/SubLibBusiness 的 Vo 映射，保障多租户示例无需手写转换即可返回 DTO。
- docs(auth): `AuthenticationGuide.md` 汇总当前 JWT 发行、授权策略与 PermissionRequirementHandler 流程，文档化登录接口的 Claim 组合与调试建议，方便新成员快速理解鉴权链路。

## 2025.11.19

- feat(auth): 将鉴权类型拆分到 `PermissionExtension`，新增 `PermissionItem` 与 `PermissionRequirementHandler`，运行时按"角色-API"关系动态组装 `RadishAuthPolicy`，并在 `RoleController`、`UserController` 等控制器上启用策略。
- feat(api/user): `UserController` 改为统一返回 `MessageModel`，新增 `GetUserById` 示例，`Radish.Api.http`/`http-client.env.json` 补充用户接口示例与最新 JWT，便于本地调试。
- feat(service/repo): `IBaseRepository`/`IBaseService` 增加 `QueryMuchAsync` 三表联查封装，`UserService.RoleModuleMaps()` 直接基于 SqlSugar Join 构建 `RoleModulePermission`，为权限处理器提供实时数据。
- chore(config): 轮换 JWT 密钥，`JwtTokenGenerate` 与 `Program` 保持一致；`appsettings.json` 新增 `AppSettings.UseLoadTest`，供鉴权测试时跳过登录校验；`PermissionRequirement` 仅保留配置，授权逻辑迁至 handler。
- test(api): `LoginControllerTest` 适配新的命名空间与假数据，实现 `RoleModuleMaps`/`QueryMuchAsync` 桩方法，继续验证登录流程。

## 2025.11.18

- feat(model/auth): 新增 `ApiModule`、`RoleModulePermission`、`UserRole` 实体与 `UserRoleVo`/`TokenInfoVo` 视图模型，补完 API 模块、角色-权限-按钮及用户-角色关联建模，支撑下一步的 RBAC 鉴权。
- refactor(model/user): User/MessageModel/RoleVo/UserVo 默认值与字段全面校准（含 Uuid、Vo* 命名、性别/年龄/状态默认值、消息默认提示等），保证接口契约在登录与下游消费场景下更一致。
- refactor(service/repo): `IBaseRepository/IBaseService` 的 `Query*` 支持可空 `Expression<Func<...>>`，Repository/Service 的实现亦同步泛化，`UserService` 的种子数据示例也调整为正式命名。
- feat(api/automapper): `LoginController` 标记为标准 API 控制器并注入 `IUserService`，AutoMapper Profile 为 User/Role/UserRole 映射补齐前缀识别与字段对应，便于后续直接复用 DTO。
