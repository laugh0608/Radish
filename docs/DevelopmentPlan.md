# 开发路线图（按周计划）

> 本计划基于当前仓库分层结构（Radish.Api、Radish.Core、Radish.Service、Radish.Repository、Radish.Model、radish.client 等）制定，聚焦「.NET 10 + SQLSugar + PostgreSQL + React 19 (Vite + Node 24)」技术栈。与 [docs/DevelopmentFramework.md](DevelopmentFramework.md) 描述的总体架构保持一致，但更强调按周可交付的节奏与验收标准。

相关文档
- 开发日志: [DevelopmentLog.md](DevelopmentLog.md)
- 部署指引: [DeploymentGuide.md](DeploymentGuide.md)

## 目标与范围

- **基础基线**：.NET 10 SDK、SQLSugar、PostgreSQL 16、本地或容器化运行；前端统一使用 React（不再维护 Angular）。
- **配置策略**：`appsettings.{Env}.json` + 环境变量 + `.env`; 前端通过 `.env.local` 注入 `VITE_API_BASE_URL` 等；敏感值仅存储在 Secret Manager 或密钥管控服务。
- **交付要求**：每周至少完成一个端到端可演示的用例（后端 API + 前端界面 + 文档/测试），并在 DevelopmentLog 中留下记录。

## 里程碑概览

| 周次 | 主题 | 目标 | 验收 |
| --- | --- | --- | --- |
| M1 | 基线设施 | API 宿主、SQLSugar 与 PostgreSQL 通路、健康检查、React 脚手架 | `dotnet run Radish.Api` + React dev server 均可访问；数据库初始化脚本可重复执行 |
| M2 | 领域建模 | 分类/帖子/评论聚合、SQLSugar 仓储、迁移/种子脚本 | CRUD + 分页 API 可用，单测覆盖核心聚合 |
| M3 | 应用服务 | DTO、应用服务、权限校验、统一异常/日志 | Swagger/Scalar 可调试所有内容 API；错误为 ProblemDetails |
| M4 | 认证与安全 | JWT + 刷新令牌、角色权限、审计日志、速率限制、HTTPS 强制与 RSA 敏感字段加密 | React 可完成登录/登出/自动续期；敏感请求经 RSA 加密；受保护 API 能正确拒绝未授权请求 |
| M5 | React MVP | 列表/详情/发帖/评论链路、状态管理（React Query）、桌面化 UI 规范 | 前端提供桌面模式的社区主要流程，含 Loading/Empty/Error 状态 |
| M6 | 积分与商城 | 积分规则、事件订阅、商城商品与库存、购买链路 | 发帖/互动触发积分，商城购买扣减积分并更新权益 |
| M7 | 可观测性与测试 | 日志/Tracing、性能调优、自动化测试、CI 脚本 | `dotnet test` + `npm run test` 通过；Serilog/O11y 配置完成；性能基线达到 P95≤200ms |
| M8 | 部署与运维 | Dockerfile/Compose、自监控、变更文档 | `docker compose up --build` 一键拉起 PostgreSQL + API + 前端；文档覆盖常见排障 |

## 按周计划

### 第 1 周｜运行基线 & 数据接入
- **后端**：
  - 生成 SQLSugar 上下文，接入 PostgreSQL（ConnectionStrings__Default）。
  - 添加健康检查 `/health` `/ready`，并暴露 Swagger/Scalar。
  - 引入 Serilog + 配置加载（JSON + env + user-secrets）。
- **数据库**：
  - 创建 `Radish` 数据库，编写初始化脚本（SqlSugar `InitTables` + 种子）。
  - 提供 `scripts/init-db.sql` 或 CLI 命令，确保幂等。
- **前端**：
  - 初始化 React 19 + Vite 项目结构，配置别名、ESLint、Prettier。
  - 设置 `.env.local`（示例 `VITE_API_BASE_URL=http://localhost:5000`）。
- **验收**：`dotnet build/test` 全绿；数据库脚本可重复执行；React `npm run dev` 成功读取健康检查结果。

### 第 2 周｜领域建模与仓储
- **Domain/Core**：建模 Category/Post/Comment/Tag 等聚合，不变式与领域事件。
- **Repository**：SQLSugar 仓储实现，封装分页、过滤、事务、软删除。
- **Service**：实现最小 `CategoryAppService` 与 `PostAppService`（列表、详情、创建）。
- **测试**：`Radish.Api.Tests` 补充聚合与仓储单测。
- **验收**：Swagger 可调试 CRUD；分页/排序参数生效；关键测试通过。

### 第 3 周｜应用层与 API 硬化
- DTO 与验证：利用 FluentValidation 保证输入合法；定义标准响应模型。
- API 对外：统一异常映射、ProblemDetails、trace-id；提供 API 版本与分组。
- 日志：Serilog 写入 Console + 本地文件，并输出 SQL 时间；必要时加请求日志中间件。
- 文档：补充 API 使用说明、错误码表。
- 验收：日志中可看到 SQL 与请求；错误返回结构一致；前端可消费新的 DTO。

### 第 4 周｜身份认证与安全
- **Auth**：账号/角色/权限模型；Hash 密码；JWT + Refresh Token；黑名单/撤销策略。
- **AppService**：登录/注册/刷新/登出；权限守卫（AuthorizeAttribute + Policy）。
- **安全**：CORS、速率限制、输入验证、审计日志（记录用户/路径/IP）、端到端 HTTPS 配置，以及前端使用 RSA 公钥加密登录/敏感数据、后端私钥解密。
- **前端**：实现认证上下文、Token 存储、自动续期、基于角色的路由守卫；封装 RSA 加解密工具并在登录表单中应用。
- **验收**：
  - 成功登录并调用受保护 API，全程 HTTPS。
  - 登录表单提交前对账号/密码进行 RSA 加密，后端成功解密并完成认证。
  - 刷新令牌流程通过自动/手动验证。
  - 未授权访问返回 401/403。

### 第 5 周｜React MVP（内容浏览与创作）
- 页面：
  - 桌面模式首页：顶部状态栏（显示用户名/IP）、底部 Dock 栏、左侧功能图标。
  - 列表（分类筛选、分页、排序）。
  - 帖子详情 + 评论树。
  - 发帖/编辑（富文本或 Markdown）。
- 状态管理：React Query + Zustand；错误/空数据反馈。
- UI：组件库（自研/Tailwind），深色模式，响应式布局；桌面模式窗口（含最小化/关闭按钮）与 Dock/图标交互。
- 验收：登录后可完成“浏览→详情→发帖→评论”闭环；核心接口的数据缓存与更新正确。

### 第 6 周｜互动、积分与商城基础
- 点赞/收藏：后端提供幂等接口（Upsert），前端按钮即时反馈与回滚。
- 积分：事件（发帖/评论/点赞）触发积分流水；`PointLedger`、`PointTransaction`。
- 商城雏形：商品管理、库存、购买接口；前端展示商品列表与购买流程。
- 验收：触发积分后可在“个人中心”看到余额与流水；购买商品扣减库存与积分。

### 第 7 周｜可观测性、测试与性能
- 自动化：补齐 xUnit/Vitest + Playwright（可选）测试，纳入 CI。
- 性能：PostgreSQL 索引审计、缓存策略（IMemoryCache/redis 预留）、SQLSugar Profiling。
- Observability：Serilog → Seq/Console JSON；OpenTelemetry 采样；健康检查拓展。
- 原生扩展（Rust）：将 `Radish.Core/test_lib` 演示项目抽离为解决方案根目录下的 `native/rust/*`，编写 `cargo build --release` + `dotnet` 构建后拷贝脚本，使共享库自动复制到 `Radish.Api` 输出目录，后续所有 Rust 扩展统一按该目录结构维护。
- 验收：
  - `dotnet test`, `npm run test`, `npm run lint`, `npm run build` 均通过。
  - P95 指标满足目标；日志可追踪请求链路。
  - `/api/RustTest/*` 在 CI/本地构建后可直接加载 `native/rust` 输出的 `test_lib`（DLL/SO/Dylib），无须手动复制。

### 第 8 周｜部署、运维与交付
- Docker：完善 `Radish.Api/Dockerfile`（Node/SQLSugar 依赖）与 compose（PostgreSQL + API + 前端静态站点）。
- 配置：`appsettings.Production.json` 模板、环境变量清单、Secret 注入示例。
- 文档：补充运维手册、常见问题、回滚策略。
- 演示：录制或截图 Compose 一键启动流程。
- 验收：`docker compose up --build` 可直接拉起完整链路；文档指引新人可在 1h 内部署。

## 工作项模板（示例）

为方便拆分任务，可沿用以下模板：

- **编号**：`W{周}-BE/FE/OPS-{序号}`，如 `W3-BE-2` 表示第 3 周后端第 2 项。
- **字段**：
  - `Owner`：责任人。
  - `Estimate`：预估工作量（0.5d/1d）。
  - `Deps`：依赖项（可引用其他任务 ID 或外部条件）。
  - `Checklist`：需要完成的细项。
  - `Acceptance`：验收标准（日志、截图、接口响应等）。
  - `Deliverables`：需要附加到 PR/文档的输出。

示例：

```md
### W3-BE-1 API 统一异常与日志
- Owner: TBD | Estimate: 1d | Deps: 无
- Checklist:
  - [ ] 创建 `ProblemDetailsExtensions`，将业务异常映射为标准响应。
  - [ ] Serilog LoggingBehavior，写入 traceId、UserId、耗时。
  - [ ] 更新 `Radish.Api` 的中间件顺序，确保异常优先捕获。
- Acceptance:
  - `/api/posts/{id}` 人为触发异常时返回 4xx/5xx ProblemDetails。
  - 日志包含 `TraceId`、`UserId`、`ElapsedMs`。
- Deliverables:
  - 代码 + 单测
  - DevelopmentLog 中记录操作步骤。
```

## 注意事项
- 周度计划可根据实际迭代情况滚动调整，但需在 DevelopmentLog 中记录偏差原因与新目标。
- 新增数据库字段或模块时，同步更新迁移脚本与文档（架构/部署/贡献者指南）。
- 前端与后端共享的 DTO 请优先在 `Radish.Model` 中维护，React 侧生成对应的 TypeScript 类型，避免漂移。
- 若出现新的关键技术选型（例如引入 redis、消息队列等），请同步更新 DevelopmentFramework 与本文件，保持信息一致。
- React Compiler（实验）：React 19 新编译器暂不在主干启用；待官方正式发布并通过 Vite/Babel 稳定支持后，再在独立分支验证收益（减少手写 memo、优化渲染），评估通过后才会合入主线。
