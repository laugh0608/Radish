# 开发路线图（按周计划）

> 本计划基于当前仓库分层结构（Radish.Api、Radish.Core、Radish.Service、Radish.Repository、Radish.Model、radish.client 等）制定，聚焦「.NET 10 + SQLSugar + PostgreSQL + React 19 (Vite + Node 24)」技术栈。与 [docs/DevelopmentFramework.md](DevelopmentFramework.md) 描述的总体架构保持一致，但更强调按周可交付的节奏与验收标准。

相关文档
- 开发日志: [DevelopmentLog.md](DevelopmentLog.md)
- 部署指引: [DeploymentGuide.md](DeploymentGuide.md)

## 目标与范围

- **基础基线**：.NET 10 SDK、SQLSugar、PostgreSQL 16、本地或容器化运行；前端统一使用 React（不再维护 Angular）。
- **配置策略**：`appsettings.{Env}.json` + 环境变量 + `.env`; 前端通过 `.env.local` 注入 `VITE_API_BASE_URL` 等；敏感值仅存储在 Secret Manager 或密钥管控服务。
- **交付要求**：每周至少完成一个端到端可演示的用例（后端 API + 前端界面 + 文档/测试），并在 DevelopmentLog 中留下记录。
- **前端体验**：桌面化 UI、移动适配与 React Native 规划须遵循 [FrontendDesign.md](FrontendDesign.md)，各阶段完成度以该文档的 Token/组件/交互 checklist 为准。

## 里程碑概览

| 周次 | 主题 | 目标 | 验收 |
| --- | --- | --- | --- |
| M1 | 基线设施 | API 宿主、SQLSugar 与 PostgreSQL 通路、健康检查、React 脚手架 | `dotnet run Radish.Api` + React dev server 均可访问；数据库初始化脚本可重复执行 |
| M2 | 领域建模 | 分类/帖子/评论聚合、SQLSugar 仓储、迁移/种子脚本 | CRUD + 分页 API 可用，单测覆盖核心聚合 |
| M3 | OIDC 认证中心 | Radish.Auth 项目、OpenIddict 配置、用户/角色/租户/权限模型、DbSeed 初始化、客户端管理 API | OIDC 发现文档可访问；Scalar 可通过 OAuth 授权调试 API；客户端 CRUD API 可用 |
| M4 | 前端框架与认证 | 桌面模式骨架、基础组件库、OIDC 客户端集成、后台应用管理界面 | React 可完成 OIDC 登录/登出/自动续期；后台可动态配置客户端；桌面 Shell 可用 |
| M5 | React MVP | 列表/详情/发帖/评论链路、状态管理（React Query）、桌面化 UI 规范 | 前端提供桌面模式的社区主要流程，含 Loading/Empty/Error 状态 |
| M6 | 积分与商城 | 积分规则、事件订阅、商城商品与库存、购买链路 | 发帖/互动触发积分，商城购买扣减积分并更新权益 |
| M7 | 可观测性与测试 | 日志/Tracing、性能调优、自动化测试、CI 脚本 | `dotnet test` + `npm run test` 通过；Serilog/O11y 配置完成；性能基线达到 P95≤200ms |
| M8 | 部署与运维 | Dockerfile/Compose、自监控、变更文档 | `docker compose up --build` 一键拉起 PostgreSQL + API + Auth + 前端；文档覆盖常见排障 |
| M9（暂缓） | Gateway & BFF 策略 | Gateway PoC（Ocelot 路由 + 认证透传）与聚合 API 设计，仅在多服务/多入口明确后启动 | 在 `DevelopmentLog` 记录启动审批 + 回滚预案；保证现有 API 维持直连能力 |

> 当前迭代聚焦 M3-M4 的 Radish.Auth 与 WebOS OIDC 接入。Gateway 完整能力（路由转发/聚合/BFF 等）仍排在 M9 之后的专门迭代，但 **Phase 0（Gateway 门户 + 健康检查 + `/docs` 在线文档入口）已经提前在当前代码中落地**，并作为本地开发的统一入口使用。

## 按周计划

### 第 1 周｜运行基线 & 数据接入
- **后端**：
  - 生成 SQLSugar 上下文，接入 PostgreSQL（ConnectionStrings__Default）。
  - 添加健康检查 `/health` `/ready`（及 `/api/health`），并通过 `/scalar` 暴露 Scalar（`/api/docs` 仅做重定向兼容）。
  - 引入 Serilog + 配置加载（JSON + env + user-secrets）。
- **数据库**：
  - 创建 `Radish` 数据库，编写初始化脚本（SqlSugar `InitTables` + 种子）。
  - 提供 `scripts/init-db.sql` 或 CLI 命令，确保幂等。
- **前端**：
  - 初始化 React 19 + Vite 项目结构，配置别名、ESLint、Prettier。
  - 设置 `.env.development.local`（示例 `VITE_API_BASE_URL=https://localhost:5000`），所有前端 API 调用统一通过 Gateway `/api`。
- **验收**：`dotnet build/test` 全绿；数据库脚本可重复执行；React `npm run dev` 成功读取健康检查结果。

### 第 2 周｜领域建模与仓储
- **Domain/Core**：建模 Category/Post/Comment/Tag 等聚合，不变式与领域事件。
- **Repository**：SQLSugar 仓储实现，封装分页、过滤、事务、软删除。
- **Service**：实现最小 `CategoryAppService` 与 `PostAppService`（列表、详情、创建）。
- **测试**：`Radish.Api.Tests` 补充聚合与仓储单测。
- **验收**：Swagger 可调试 CRUD；分页/排序参数生效；关键测试通过。

> ⚠️ OIDC 认证中心（M3）是所有后续 WebOS/OIDC 集成与客户端管理的前置条件，资源投入优先保证本阶段可交付；Gateway 相关能力仅保留接口兼容性，不在 M1-M4 执行。

### 第 3 周｜OIDC 认证中心与数据初始化

#### 3.1 创建 Radish.Auth 项目
- **项目结构**：新建 ASP.NET Core 项目，独立运行在 `:7100` 端口
- **依赖引入**：OpenIddict 核心包（OpenIddict.AspNetCore、OpenIddict.EntityFrameworkCore 或自定义 SqlSugar 存储）
- **端点配置**：
  - `/connect/authorize` - 授权端点
  - `/connect/token` - Token 端点
  - `/connect/userinfo` - 用户信息端点
  - `/connect/logout` - 登出端点
  - `/.well-known/openid-configuration` - 发现文档

#### 3.2 身份数据模型设计
- **实体定义**（`Radish.Model/Models/Identity/`）：
  - `User.cs` - 用户实体（支持多租户）
  - `Role.cs` - 角色实体
  - `UserRole.cs` - 用户-角色关联
  - `UserClaim.cs` - 用户声明
  - `Tenant.cs` - 租户实体
  - `Permission.cs` - 权限定义
  - `RolePermission.cs` - 角色-权限关联
- **OpenIddict 实体**：Application、Authorization、Scope、Token

#### 3.3 实现 Radish.DbSeed 项目
- 创建控制台项目或 CLI 工具
- 初始化数据库表结构（SqlSugar InitTables）
- 种子数据：
  - 默认租户（Main Tenant）
  - 管理员用户（admin/System）
  - 基础角色（System、Admin、User）
  - 系统权限与 API 模块
- 预置内部 OIDC 客户端（通过数据库存储，非硬编码）：
  - `radish-client` - WebOS 前端客户端（包含所有子应用）
  - `radish-scalar` - API 文档客户端
  - `radish-rust-ext` - Rust 扩展项目客户端

#### 3.4 客户端管理 API
- **数据模型扩展**：
  ```csharp
  // 扩展 OpenIddict Application 实体
  public class RadishApplication
  {
      public string? Logo { get; set; }
      public string? Description { get; set; }
      public string? DeveloperName { get; set; }
      public string? DeveloperEmail { get; set; }
      public ApplicationStatus Status { get; set; } // Active/Disabled
      public ApplicationType AppType { get; set; } // Internal/ThirdParty
      public DateTime CreatedAt { get; set; }
      public long? CreatedBy { get; set; }
  }
  ```
- **API 端点**（`/api/clients`）：
  - `GET /api/clients` - 获取客户端列表（分页、筛选）
  - `GET /api/clients/{id}` - 获取客户端详情
  - `POST /api/clients` - 创建客户端
  - `PUT /api/clients/{id}` - 更新客户端
  - `DELETE /api/clients/{id}` - 删除客户端
  - `POST /api/clients/{id}/reset-secret` - 重置 ClientSecret
  - `POST /api/clients/{id}/toggle-status` - 启用/禁用客户端
- **权限控制**：仅 System/Admin 角色可访问

#### 3.5 Scalar OAuth 集成
- 修改 `Radish.Api/Program.cs` 配置 Scalar OAuth：
  ```csharp
  app.MapScalarApiReference(options => {
      options.WithOAuth2Configuration(oauth => {
          oauth.ClientId = "radish-scalar";
          oauth.Scopes = new[] { "openid", "profile", "radish-api" };
      });
  });
  ```
- 验收：访问 `/scalar` 可跳转到 Auth 登录，授权后返回并携带 Token

#### 验收标准
- `/.well-known/openid-configuration` 返回正确的 OIDC 发现文档
- DbSeed 可重复执行且幂等
- Scalar 文档通过 OAuth 授权后可调试受保护 API
- 多客户端（Scalar、前端、后台）注册完成

### 第 4 周｜前端 WebOS 架构与 OIDC 认证

#### 4.1 WebOS 架构（超级应用）
- **核心理念**：Radish 是一个运行在浏览器中的操作系统
- **单体应用结构**（`radish.client`）：
  ```
  radish.client/
  ├── src/
  │   ├── desktop/         # 桌面系统核心
  │   ├── apps/            # 子应用（论坛/聊天/商城/后台管理）
  │   ├── widgets/         # 桌面小部件
  │   ├── shared/          # 共享代码
  │   └── stores/          # 全局状态
  ```
- 详见 `FrontendDesign.md`

#### 4.2 桌面系统基础
- **核心组件**：
  - `Shell.tsx` - 桌面外壳容器
  - `StatusBar.tsx` - 顶部状态栏（用户名、IP、消息、系统状态）
  - `Desktop.tsx` - 桌面图标网格（基于权限动态显示）
  - `Dock.tsx` - 底部 Dock 栏（运行中应用）
  - `WindowManager.tsx` - 窗口管理器（窗口/全屏/iframe 模式）
  - `AppRegistry.tsx` - 应用注册表

#### 4.3 应用注册系统
- **注册表定义**：
  ```typescript
  interface AppDefinition {
    id: string;
    name: string;
    icon: string;
    component: React.ComponentType;
    type: 'window' | 'fullscreen' | 'iframe';
    requiredRoles: string[];
  }
  ```
- **预置应用**：
  - 论坛（`forum`）- 窗口模式
  - 聊天室（`chat`）- 窗口模式
  - 商城（`shop`）- 全屏模式
  - 后台管理（`admin`）- 全屏模式，仅 Admin/System 可见
  - API 文档（`docs`）- iframe 模式

#### 4.4 子应用开发
- **论坛应用**（`apps/forum/`）：
  - 帖子列表、详情、发帖、评论
  - 窗口模式，可多开
- **后台管理应用**（`apps/admin/`）：
  - 使用 Ant Design 组件
  - 全屏模式，独占桌面
  - 核心模块：应用管理、用户管理、角色管理
- **聊天室应用**（`apps/chat/`）：预留
- **商城应用**（`apps/shop/`）：预留

#### 4.5 OIDC 客户端集成
- 使用 `oidc-client-ts` 或 `react-oidc-context`
- 配置文件（`shared/auth/oidc-config.ts`）：
  ```typescript
  export const oidcConfig = {
    authority: 'https://localhost:7100',
    client_id: 'radish-client',
    redirect_uri: 'https://localhost:3000/callback',
    post_logout_redirect_uri: 'https://localhost:3000',
    scope: 'openid profile radish-api offline_access',
    response_type: 'code',
    automaticSilentRenew: true
  };
  ```
- 实现认证流程：
  - 登录跳转 → Auth 授权页 → 回调处理 → Token 存储
  - 静默续期（iframe 或 refresh_token）
  - 登出（清除本地 + 调用 Auth 登出端点）
- **桌面权限控制**：根据用户角色动态显示应用图标

#### 4.6 Radish.Api 资源服务器配置
- 配置 JWT Bearer 验证：
  ```csharp
  builder.Services.AddAuthentication()
      .AddJwtBearer(options => {
          options.Authority = "https://localhost:7100";
          options.Audience = "radish-api";
          options.TokenValidationParameters = new TokenValidationParameters {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true
          };
      });
  ```
- 保留 `PermissionRequirementHandler` 动态权限校验
- 更新现有控制器的 `[Authorize]` 策略

#### 4.7 安全增强
- HTTPS 强制（开发/生产均启用）
- CORS 配置（Auth、API、前端、后台跨域）
- 速率限制中间件
- 审计日志（登录/登出/敏感操作）

#### 验收标准
- 桌面系统可正常渲染（状态栏、Dock、桌面图标）
- 桌面根据用户角色动态显示应用图标（普通用户看不到后台管理）
- 双击应用图标可打开窗口/全屏应用
- 论坛应用（窗口模式）基本可用：帖子列表、详情页
- 后台管理应用（全屏模式）可访问应用管理模块
- 后台可动态创建/编辑/删除 OIDC 客户端
- OIDC 认证流程完整：登录 → 授权 → 回调 → 调用 API → 登出
- Token 自动续期正常工作
- 未授权访问返回 401，权限不足返回 403
- 登录后状态栏显示用户信息
- Dock 正确显示运行中的应用

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

## 后续规划：API Gateway

- 详细任务拆解见 [GatewayPlan.md](GatewayPlan.md)，当前阶段专注既有 M1-M8 交付，Gateway 作为 M9 之后的专项迭代。
- 触发条件：后端核心用例和认证机制稳定、日志/监控链路可复用。届时启动 P1（项目基线）→ P2（认证）→ P3（路由/聚合）的工作包，并在 DevelopmentLog 记录进度。
- 在日常开发中提前预留：规范 Header（`X-Request-Id`、`X-Client-Id`）、CORS 配置、登录控制器可被 Gateway 复用，减少后续迁移成本。
