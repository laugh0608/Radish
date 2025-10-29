# 开发路线图（按周计划）

> 本计划依据当前仓库目录结构（Radish.Application*、Radish.Domain*、Radish.HttpApi*、Radish.HttpApi.Host、Radish.MongoDB、Radish.DbMigrator）与文档约定（.env 管理、CORS、React/Angular、MongoDB、OpenIddict、Docker 等）制订；与 docs/DevelopmentFramework.md 的总体架构一致，但聚焦“按周可交付”。

## 目标与范围

- 基线：.NET 9 + ABP，MongoDB，OpenIddict + JWT，Swagger/Scalar，React（Vite）与 Angular（ABP UI）。
- 统一配置：后端以 `.env` 为主；CORS 通过 `App__CorsOrigins`；前端本地默认 HTTP，Host 统一 HTTPS；证书位于 `dev-certs/`。
- 交付标准：每周具备可演示的闭环（后端 API + 至少一端 UI），并同步更新最小文档与测试。

## 按周计划（12 周）

### 第 1 周｜基线与联调打通
- 后端：准备 `src/Radish.HttpApi.Host/.env` 与 `src/Radish.DbMigrator/.env`；运行 DbMigrator；Host 启动并打印 CORS 允许来源。
- 前端：React/Angular 本地可运行；React 配置 `VITE_API_BASE_URL=https://localhost:44342`；Angular 使用 `dynamic-env.json` 联调。
- 文档与测试：补充本地联调与证书自测（curl 预检、dotnet dev-certs）；新增最小健康检查测试；确保 `dotnet build/test` 全绿。
- 验收：Swagger/Scalar 可打开并访问；React/Angular 能请求应用配置并通过 CORS。

### 第 2 周｜领域建模与数据层（Category/Post/Comment）
- 领域：在 `Radish.Domain` 定义聚合/实体/值对象与不变式；在 `Radish.Domain.Shared` 维护本地化键与权限常量。
- 数据：在 `Radish.MongoDB` 实现仓储与必要索引（按分类/创建时间/状态等）；DbMigrator 增加默认数据与索引。
- 测试：聚合与仓储单测（Shouldly + NSubstitute）。
- 验收：CRUD 与分页查询可用；索引生效；测试通过。

### 第 3 周｜应用层与 API（Category/Post/Comment）
- 合同：`Radish.Application.Contracts` 定义 DTO 与接口（分页/过滤/排序模型）。
- 服务：`Radish.Application` 实现权限检查、流程编排与事件发布（发帖、点赞、浏览等）。
- API：Host 暴露 RESTful 接口；Swagger/Scalar 分组与文档过滤（仅项目 API）。
- 验收：授权后可在 Swagger/Scalar 调试完整接口；关键操作带权限校验。

### 第 4 周｜Host 网关与文档硬化
- 身份：完善 OpenIddict 配置与数据种子（Swagger/Scalar ClientUri/RedirectUri 正确）。
- 文档：Swagger/Scalar 登录授权流程打通；统一 API 版本与分组；异常映射为 ProblemDetails。
- 性能：为高频查询添加短缓存策略（只读列表等）。
- 验收：Swagger/Scalar 授权可调试；异常/权限响应一致；热点接口可观测。

### 第 5 周｜React 基础与认证集成
- 架构：全局状态（Auth + I18n + Query/Cache）、路由守卫、基础布局与主题。
- 认证：登录/注册/刷新令牌；按权限动态控制入口/按钮。
- 页面：首页/分类页、帖子列表（分页/过滤/排序）。
- 验收：登录后可浏览列表并正确显示状态；无权限按钮隐藏。

### 第 6 周｜React 论坛核心交互
- 详情：帖子详情（内容/作者/时间/计数）；评论区（扁平或楼中楼）。
- 交互：新发帖/编辑帖（富文本）、评论/回复、点赞/取消点赞；错误/空态/骨架屏。
- 质量：端到端冒烟（可选 Playwright）。
- 验收：发帖-评论-点赞路径闭环；核心 UX 可用。

### 第 7 周｜Angular 管理端（MVP）
- 分类管理：列表/排序/启停/编辑；权限联动。
- 内容管理：帖子与评论搜索/筛选/审核（发布/草稿、置顶、精华）。
- 联调：与 Host 权限与文案一致；移动端规范复用（如需）。
- 验收：管理操作可用；权限与 UI 控制一致。

### 第 8 周｜容器化与一键启动
- Dockerfile：Host、DbMigrator、React、Angular；前端使用 Nginx 承载产物。
- Compose：`mongodb`、`backend-api`、`db-migrator`、`react-frontend`、`angular-admin` 的网络/卷/端口/依赖与环境变量。
- 文档：`docker-compose up --build -d` 自测与常见问题；健康检查。
- 验收：一键启动后链路（DB→迁移→API→前端）完整可用。

### 第 9 周｜积分系统
- 领域：`UserPoints`、`PointTransaction` 与 `PointCalculator`。
- 事件：订阅发帖、点赞、评论被采纳等事件更新积分。
- 应用/UI：`PointAppService`；Admin 用户积分管理；React 个人中心展示与积分历史。
- 验收：事件→积分变更链路可用；前后台一致。

### 第 10 周｜商城系统
- 领域：`ShopItem`、`UserInventory`；`ShopManager` 购买/激活/禁用（扣减积分）。
- 应用/UI：商品列表/详情/购买；我的物品；效果应用（头像框/昵称色/签名尾巴）。
- 验收：购买-库存-效果激活闭环；积分扣减准确。

### 第 11 周｜可观测性、性能与安全
- 观测：结构化日志、健康检查、基础指标；关键 API 耗时与错误率观测。
- 性能：索引优化、缓存策略、分页与投影；Mongo 查询计划复核。
- 安全：输入验证、XSS/CSRF、防刷/限流、CORS 白名单审计；最小权限角色模板。
- 验收：关键页面 P95 ≤ 200ms（本地/容器）；无高风险扫描项。

### 第 12 周｜硬化收尾与文档交付
- 测试：单元/集成/端到端覆盖补全；核心路径稳定。
- 文档：开发指南、联调说明、Docker 部署、权限矩阵、数据字典与架构说明。
- 准入：版本清单、CHANGELOG、里程碑验收记录；后续 Roadmap（搜索、消息、上传、SEO、i18n 等）。
- 验收：文档齐备、测试通过、可演示与可部署。

## 每周交付与验收标准
- 源码与可运行产物：后端 API 与至少一端 UI 的功能闭环。
- 文档：更新 `docs/`（含 DevelopmentProgress.md）；必要的配置样例（.env.example、CORS、自测命令）。
- 测试：`dotnet test` 通过；前端 lint/build 通过；必要端到端冒烟。
- 验收：Swagger/Scalar 授权调试关键接口；权限控制与错误处理就绪；容器化阶段具备一键启动。

## 并行与协作建议
- 前后协同：应用合同（DTO/接口）一经稳定即同步给前端；提供 Swagger JSON 用于前端 Mock。
- 并行推进：React 与 Angular 可并行；Docker 编排可提前孵化并逐周集成。
- 规范与本地化：新增权限/词条统一在 `Radish.Domain.Shared` 维护；变更同步至 docs/README.md 索引。

## 风险与前置条件
- 环境：.NET 9 SDK、Node 18/20、MongoDB 6+；本地信任 HTTPS 证书。
- 配置：后端 `.env` 为唯一可信来源（关键项必须给出）；CORS 需包含两端口与 http/https 协议。
- 安全：仓库不提交机密；仅提交 `.env.example`；生产另行配置证书与密钥。

## 变更记录
- 2025-10-29：创建文档，首次纳入 12 周按周计划；与 DevelopmentFramework.md 对齐并使用 Radish.* 命名。

