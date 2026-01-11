# 开发路线图（按周计划）

> 本文聚焦“按周可交付的节奏与验收标准”。
>
> - 先建立整体认知：见 [架构总览](/architecture/overview)
> - 查看架构与工程细节：见 [开发框架说明](/architecture/framework)
> - 查看前端 WebOS 与应用集成：见 [前端设计](/frontend/design)

相关文档
- 开发日志: [/changelog/](/changelog/)
- 部署指引: [部署指南](/deployment/guide)

## 目标与范围

- **基础基线**：.NET 10 SDK、SQLSugar、PostgreSQL 16、本地或容器化运行；前端统一使用 React（不再维护 Angular）。
- **配置策略**：`appsettings.{Env}.json` + 环境变量 + `.env`; 前端通过 `.env.local` 注入 `VITE_API_BASE_URL` 等；敏感值仅存储在 Secret Manager 或密钥管控服务。
- **交付要求**：每周至少完成一个端到端可演示的用例（后端 API + 前端界面 + 文档/测试），并在 DevelopmentLog 中留下记录。
- **前端体验**：桌面化 UI、移动适配与 React Native 规划须遵循 [frontend/design.md](frontend/design.md)，各阶段完成度以该文档的 Token/组件/交互 checklist 为准。

## 里程碑概览

| 周次 | 主题 | 目标 | 验收 | 状态 |
| --- | --- | --- | --- | --- |
| M1 | 基线设施 | API 宿主、SQLSugar 与 PostgreSQL 通路、健康检查、React 脚手架 | `dotnet run Radish.Api` + React dev server 均可访问；数据库初始化脚本可重复执行 | ✅ 已完成 |
| M2 | 领域建模 | 分类/帖子/评论聚合、SQLSugar 仓储、迁移/种子脚本 | CRUD + 分页 API 可用，单测覆盖核心聚合 | ✅ 已完成 |
| M3 | OIDC 认证中心 | Radish.Auth 项目、OpenIddict 配置、用户/角色/租户/权限模型、DbSeed 初始化、客户端管理 API | OIDC 发现文档可访问；Scalar 可通过 OAuth 授权调试 API；客户端 CRUD API 可用 | ✅ 已完成 (2025.12.14) |
| M4 | 前端框架与认证 | 桌面模式骨架、基础组件库、OIDC 客户端集成、后台应用管理界面、论坛应用完整实现、安全增强 | React 可完成 OIDC 登录/登出/自动续期；后台可动态配置客户端；桌面 Shell 可用；论坛应用基本可用 | ✅ 已完成 (2025.12.21) |
| M5 | 文件上传与内容增强 | 文件上传（图片/文档）、Rust 扩展集成、图片处理、MarkdownEditor 图片上传集成、个人中心功能 | 可上传图片/文档；自动生成缩略图；MarkdownEditor 支持图片上传；文件去重功能；个人中心资料管理 | ✅ 已完成 (2025.12.21~2025.12.28) |
| M6 | 萝卜币系统 | 萝卜币基础服务、论坛奖励规则、神评沙发统计、精确计算系统 | 发帖/互动触发萝卜币奖励，神评/沙发保留奖励正常发放，前端余额显示与钱包页面可用 | ✅ 已完成 (2025.12.28~2026.01.03) |
| M7 | 消息通知系统 | SignalR/WebSocket 实时推送、通知中心、推送模板、多端同步 | 替代轮询机制，实时推送点赞/评论/系统消息；通知中心可查看历史消息 | ✅ P1 已完成 (2026.01.10) |
| M8 | 用户等级与经验值 | 11 级修仙体系、经验值计算、升级奖励、等级徽章、排行榜、详情页 | 用户行为触发经验值增长，升级获得萝卜币奖励，个人主页显示等级徽章，排行榜和详情页完整 | ✅ 已完成 (2026-01-11) |
| M9 | 商城系统 | 商品管理、库存管理、购买链路、订单系统、权益发放 | 可浏览商品列表，萝卜币购买商品并扣减库存，订单记录可追溯 | ⏳ 计划中 |
| M10 | 可观测性与测试 | 日志/Tracing、性能调优、自动化测试、CI 脚本 | `dotnet test` + `npm run test` 通过；Serilog/O11y 配置完成；性能基线达到 P95≤200ms | ⏳ 计划中 |
| M11 | 部署与运维 | Dockerfile/Compose、自监控、变更文档 | `docker compose up --build` 一键拉起 PostgreSQL + API + Auth + 前端；文档覆盖常见排障 | ⏳ 计划中 |
| M12（暂缓） | Gateway & BFF 策略 | Gateway PoC（Ocelot 路由 + 认证透传）与聚合 API 设计，仅在多服务/多入口明确后启动 | 在 `DevelopmentLog` 记录启动审批 + 回滚预案；保证现有 API 维持直连能力 | ⏸️ 暂缓 |

> **当前进度**：✅ M8（用户等级与经验值系统）已于 2026.01.11 完成，准备进入 M9（商城系统）。
>
> **M6 完成情况**：
> - ✅ **萝卜币系统完整实现**（2026.01.03）：
>   - 萝卜币基础服务（余额管理、交易记录、系统赠送）
>   - 论坛奖励服务（点赞、评论、神评、沙发奖励，含防刷机制）
>   - 精确计算系统（CoinCalculator 工具类，52 个单元测试全通过）
>   - 定时任务（神评沙发统计、保留奖励发放，支持可配置阈值）
>   - 前端集成（Toast 组件、余额显示、钱包页面）
>   - 注册奖励集成（新用户 50 胡萝卜）
>   - 详细文档（萝卜币系统设计、精确计算规范）
> - 详见 [2026.01 开发日志](./changelog/2026-01/week1.md)
>
> **M7 P1 完成情况**（2026.01.10）：
> - ✅ **消息通知系统基础功能**：
>   - SignalR Hub 连接和实时推送（P0 完成）
>   - 数据模型和独立消息数据库（Radish.Message.db）
>   - NotificationService 核心业务逻辑
>   - NotificationCacheService 缓存管理（30分钟过期）
>   - NotificationDedupService 去重服务（5分钟窗口）
>   - 点赞和评论通知集成
>   - 前端通知中心和列表组件（NotificationCenter、NotificationList、NotificationApp）
>   - API 接口完整（列表、未读数、标记已读、删除）
> - 详见 [2026.01 开发日志](./changelog/2026-01/week2.md)
> - P2 阶段（多端同步、通知聚合、用户偏好）暂缓，优先 M8
>
> **M8 完成情况**（2026.01.11）：
> - ✅ **用户等级与经验值系统完整实现**：
>   - 11 级修仙体系（凡人→飞升）
>   - 经验值发放、等级计算、升级奖励（萝卜币）
>   - 乐观锁并发控制（6次重试，指数退避）
>   - 每日上限防刷机制（可配置）
>   - 经验值计算公式配置化（4种公式：Hybrid/Exponential/Polynomial/Segmented）
>   - 排行榜功能（分页、排名查询）
>   - 经验值详情页面（趋势图、来源饼图、明细列表）
>   - 前端完整集成（ExperienceBar、LevelUpModal、Leaderboard、ExperienceDetail）
>   - SignalR 实时推送升级通知
>   - 与所有论坛功能集成（发帖/评论/点赞/神评/沙发）
> - ⏳ **待完善项**（非阻塞）：
>   - 冻结/解冻功能（框架已完成，待实现具体逻辑）
>   - 异常检测与风控（短时间大量获得经验值、互刷检测、机器人检测）
>   - 单元测试（等级计算、经验值发放、并发测试）
>   - 管理后台统计面板（每日发放量、等级分布、来源占比）
> - 详见 [经验值系统实施计划](./guide/experience-level-roadmap.md)
>
> **里程碑调整说明**（2026.01.03）：
> - M6 聚焦萝卜币系统（已完成），商城系统推迟到 M9
> - M7 实现消息通知系统（SignalR 实时推送，替代轮询）
> - M8 实现用户等级与经验值系统（修仙主题，与萝卜币集成）
> - M9 实现商城系统（依赖通知和等级系统，功能更完备）
> - 原 M7/M8 依次顺延到 M10/M11

## 工程治理（分层与依赖收敛）

> 目标：减少循环依赖与“超级聚合项目”，让分层约束尽可能被结构/编译期强制，而不是仅靠约定。

### 优先建议（P0/P1）

- [x] **用结构约束替代约定约束**：收口 Service 层直连数据库能力（移除/隔离 `IBaseRepository<TEntity>.DbBase`、`BaseService.Db`），复杂查询通过 Repository 扩展方法或专属仓储承载。
- [x] **拆分 `Radish.Extension` 的职责**：新增 `Radish.Extension.Host` 承载宿主级扩展（如 Serilog），避免 Gateway 因引用 `Radish.Extension` 被迫间接依赖业务层。
- [ ] **DbMigrate 解耦宿主**：避免 `Radish.DbMigrate` 直接引用 `Radish.Api`，改为引用“组合根/基础设施注册”以复用 DI 与配置，降低控制台迁移工具与 WebHost 的耦合。

### 中长期优化（P2+）

- [ ] **抽象下沉、实现上浮**：将可替换策略接口下沉到 `IService`/独立 Abstractions，实现放在 `Infrastructure/Extension` 并由宿主注入；Service 层禁止依赖具体实现类型。
- [ ] **引入组合根项目**：新增 `Radish.Bootstrapper`（或 `Radish.Abstractions` + `Radish.Bootstrapper`）统一装配 DI 与模块注册，避免 `Radish.Extension` 持续膨胀为“超级聚合项目”。

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
  - `radish-console` - 后台管理控制台
  - `radish-shop` - 商城应用（占位，未来实现）

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

#### 第 3 周后端工作项（Auth/OIDC 核心）

> 采用工作项模板：`W{周}-BE-{序号}`，Owner/Estimate/Deps 可在实际排期时补充。

```md
### W3-BE-1 OpenIddict EF Core 存储巩固
- Owner: TBD | Estimate: TBD | Deps: Radish.Auth 已基于 EF Core 存储跑通 OIDC 最小链路
- Checklist:
  - [ ] 明确 Auth 项目长期采用 EF Core + `AuthOpenIddictDbContext` 的架构，不再尝试 SqlSugar Store 切换
  - [ ] 确保 `RadishAuth.OpenIddict.db` 作为独立 SQLite 数据库，仅由 Auth 创建维护
  - [ ] 保留 Api 项目对 `IOpenIddictApplicationManager` 的共享访问（客户端管理 API），同时强调其不负责建表
  - [ ] 在配置与文档示例中统一“Auth 负责 OpenIddict 数据，Api 仅消费”的表述
- Acceptance:
  - DevelopmentPlan/AuthenticationGuide/DevelopmentLog 等文档均指向 EF Core 方案
  - `Radish.Auth/Program.cs` 及配置没有 SqlSugar Store 残留说明
  - 新成员按照文档即可用 `AuthOpenIddictDbContext` 初始化/迁移 OpenIddict 数据库

### W3-BE-2 Radish.DbSeed/DbMigrate 对 Auth + Api 的统一初始化
- Owner: TBD | Estimate: TBD | Deps: Radish.DbMigrate 基础项目已存在
- Checklist:
  - [ ] 梳理当前各处初始化逻辑（Auth 内部 `OpenIddictSeedHostedService` + Radish.DbMigrate 现有种子）
  - [ ] 设计统一初始化流程，明确 Auth 与 Api/主库的职责边界
  - [ ] 在 `Radish.DbMigrate` 中补全以下种子：默认租户、管理员用户、基础角色、系统权限与 API 模块、内置 OIDC 客户端（radish-client/radish-scalar/radish-console/radish-shop）
  - [ ] 确保种子流程幂等，可重复执行
  - [ ] 提供命令调用方式并在 DeploymentGuide 中补充使用说明
- Acceptance:
  - 新环境执行一次 DbMigrate/Seed 可完成主库 + Auth 所需基础数据
  - 重复执行不会产生重复数据或异常

### W3-BE-3 OIDC 客户端管理 API（Auth 后端）
- Owner: TBD | Estimate: TBD | Deps: W3-BE-1（可选）、W3-BE-2
- Checklist:
  - [ ] 基于 `RadishApplication`/VoOidcApp 设计 `Radish.Auth` 侧客户端管理 API（`/api/clients` 全套 CRUD + reset-secret/toggle-status）
  - [ ] 定义 Repository/Service 接口 + 实现，遵循 IService/IRepository 模式
  - [ ] 实现权限控制：仅 System/Admin 角色可访问
  - [ ] 对 ClientSecret 做最小暴露和写入策略：创建时返回一次，后续仅支持重置
  - [ ] 编写基础单测（覆盖创建/禁用/重置 Secret 流程）
- Acceptance:
  - 可通过 HTTP 客户端完整走一遍客户端生命周期（创建→查询→更新→禁用→重置 Secret→删除）
  - 非 System/Admin 角色访问返回 403

### W3-BE-4 Radish.Api 资源服务器 Claim 映射与当前用户接口完善
- Owner: TBD | Estimate: TBD | Deps: AuthenticationGuide 中 Claim 映射约定已更新
- Checklist:
  - [ ] 按文档约定统一 `HttpContextUser` Claim 解析逻辑（UserId: sub/jti，TenantId: tenant_id/TenantId 等）
  - [ ] 修正 `UserController.GetUserByHttpContext`，确保 OIDC 模式下返回正确的 userId/userName/tenantId
  - [ ] 验证多租户（RepositorySetting/BaseRepository）与权限链路（PermissionRequirementHandler）在 OIDC Token 下正常工作
  - [ ] 更新/补充 `.http` 调试脚本与必要单测
- Acceptance:
  - 使用 OIDC Token 调用 `GetUserByHttpContext` 可获得正确的当前用户信息
  - 开启 RadishAuthPolicy 的控制器在 OIDC 模式下按角色正确放行/拒绝

### W3-BE-5 Scalar OAuth 全链路校验与文档对齐
- Owner: TBD | Estimate: TBD | Deps: W3-BE-1/2/4 基本完成
- Checklist:
  - [ ] 确认 `Radish.Api/Program.cs` 中 Scalar OAuth 配置与 Auth 侧 radish-scalar 客户端保持一致
  - [ ] 通过 Scalar UI 完整跑一遍授权→登录→回调→调用 API 的流程
  - [ ] 在 AuthenticationGuide 或 DevelopmentLog 中补充“Scalar OAuth 调试指南”（步骤/截图）
- Acceptance:
  - `/scalar` 页面可通过 OAuth 调试需要 `scope=radish-api` 的接口
```

#### 验收标准
- `/.well-known/openid-configuration` 返回正确的 OIDC 发现文档
- DbSeed 可重复执行且幂等
- Scalar 文档通过 OAuth 授权后可调试受保护 API
- 多客户端（Scalar、前端、后台）注册完成

---

#### ✅ 第三周完成总结（2025.12.14）

**状态**：✅ 所有核心目标已完成，所有验收标准已达成

**核心成果**：
1. ✅ Radish.Auth 项目创建完成，集成 OpenIddict 7.2.0
2. ✅ OIDC 标准端点全部实现并验证通过
3. ✅ 身份数据模型与 OpenIddict 实体完整
4. ✅ Radish.DbMigrate 完善，支持幂等执行
5. ✅ 客户端管理 API 完整可用（5 个单元测试全部通过）
6. ✅ Scalar OAuth 集成验证通过
7. ✅ Gateway + OIDC 全链路打通

**工作项完成情况**：
- ✅ W3-BE-1：OpenIddict EF Core 存储巩固
- ✅ W3-BE-2：Radish.DbMigrate 统一初始化
- ✅ W3-BE-3：OIDC 客户端管理 API
- ✅ W3-BE-4：Radish.Api 资源服务器 Claim 映射
- ✅ W3-BE-5：Scalar OAuth 全链路校验

**额外完成**：
- 🚀 WebOS Desktop Shell（提前完成第四周部分工作）
- 🚀 Console 应用 OIDC 认证完整
- 🚀 @radish/ui 组件库完善
- 🚀 文档结构重构

**详细日志**：见 [2025.12 开发日志](./changelog/2025-12.md) 第三阶段总结

---

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
- 详见 [前端设计文档](./frontend/design.md)

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

---

#### ✅ 第四周完成总结（2025.12.21）

**状态**：✅ 所有核心目标已完成，验收标准 100% 达成

**核心成果**：
1. ✅ 论坛应用完整实现
   - 帖子列表页面（分类筛选、分页、排序、搜索、点赞）
   - 帖子详情页面（Markdown 渲染、评论树、懒加载）
   - 发帖/编辑页面（MarkdownEditor、分类选择、草稿自动保存）
   - 评论功能（回复、编辑、删除、排序、神评/沙发）
   - 内容管理（帖子/评论编辑删除、权限验证）
2. ✅ MarkdownEditor 富文本编辑器组件
   - 完整工具栏（18 个按钮）
   - Emoji 选择器（160+ 表情）
   - 实时预览、快捷键支持
3. ✅ 安全增强
   - 速率限制中间件（4 种算法、黑白名单）
   - 审计日志系统（按月分表、完整记录）
   - 日志系统完整文档（约 600 行）
4. ✅ 窗口模式集成完整
   - 论坛应用作为窗口应用注册
   - 窗口拖拽、最小化、关闭
   - Dock 栏显示运行中应用

**超额完成**：
- 评论排序系统（神评/沙发功能）
- 草稿自动保存功能
- 评论编辑功能（时间窗口限制）
- 完整的日志系统文档

**总结**：第四阶段核心目标 100% 完成，所有验收标准达成。论坛应用和安全增强超出预期，为第五阶段打下坚实基础。

**详细日志**：见 [2025.12 开发日志](./changelog/2025-12.md) 第四阶段总结

---

### 第 5 周｜文件上传与内容增强

> **阶段目标**：实现文件上传功能，支持图片和文档上传，集成 Rust 扩展进行图片处理，完善 MarkdownEditor 图片上传能力。详细方案见 [文件上传设计方案](./features/file-upload-design.md)。

#### 5.1 后端核心功能（3-4 天）

**数据模型和存储接口**：
- 创建 `Attachment` 实体和数据库迁移
  - 字段：原始文件名、存储文件名、扩展名、大小、MIME 类型、哈希值、存储类型、路径、缩略图路径、URL
  - 支持：软删除、审计字段、多租户
- 定义 `IFileStorage` 接口
  - `UploadAsync()` / `DeleteAsync()` / `DownloadAsync()` / `GetFileUrl()` / `ExistsAsync()`
- 实现 `LocalFileStorage`（本地存储）
  - 目录结构：`DataBases/Uploads/{Category}/{Year}/{Month}/{UniqueFileName}`
  - 配置静态文件中间件

**图片处理**：
- 定义 `IImageProcessor` 接口
- 实现 `CSharpImageProcessor`（使用 ImageSharp）
  - 生成缩略图（150x150）
  - 生成多尺寸（Small 400x300、Medium 800x600）
  - 图片压缩（JPEG 质量 85%）
  - 移除 EXIF 信息
  - 水印功能（文字水印）

**Rust 扩展集成**（与上面并行，1-2 天）：
- ✅ 已完成：重构 `Radish.Core/test_lib` 为 `radish-lib`
  - 项目位置：`Radish.Core/radish-lib`
  - Cargo.toml 配置（依赖：image, imageproc, rusttype, sha2；benchmark 额外依赖 rayon, num_cpus）
- ✅ 已完成：实现图片加水印功能（Rust）
  - `add_text_watermark()` FFI 函数
  - 支持位置、字体大小、透明度配置
- ✅ 已完成：实现文件哈希计算（Rust）
  - `calculate_file_sha256()` FFI 函数
- ✅ 已完成：创建 C# FFI 调用封装
  - `RustImageProcessor` 类
  - `ImageProcessorFactory`（配置切换 C#/Rust）
- ✅ 已完成：编写编译脚本（build.sh / build.ps1）

**业务逻辑**：
- 创建 `AttachmentService`（CRUD + 上传逻辑）
- 文件校验
  - 文件类型白名单检查
  - 文件大小限制检查
  - Magic Number 检查（文件头校验）
- 文件去重逻辑
  - 上传时计算 SHA256 哈希
  - 查询数据库是否存在相同哈希
  - 存在则复用文件，只创建新附件记录
- 文件名生成（雪花 ID + 年月目录）

**API 端点**：
- `POST /api/v1/Upload/Image` - 上传图片（可选水印）
- `POST /api/v1/Upload/Document` - 上传文档
- `GET /api/v1/Upload/{id}` - 获取文件信息
- `DELETE /api/v1/Upload/{id}` - 软删除文件
- 预留分片上传 API（不实现）：
  - `POST /api/v1/Upload/Chunk`
  - `POST /api/v1/Upload/Merge`

#### 5.2 前端开发（2-3 天）

**上传组件**：
- 创建 `FileUpload` 组件（@radish/ui）
  - 拖拽 + 点击上传
  - 上传进度显示
  - 图片预览
  - 错误提示和重试逻辑（自动重试 3 次，指数退避）
  - 水印选项（用户可选）
  - 文件类型和大小校验（前端预检）

**集成到 MarkdownEditor**：
- 图片按钮点击触发上传
- 上传成功后插入 Markdown 图片语法
- 支持粘贴图片上传（Ctrl+V）
- 支持拖拽图片上传
- 上传进度显示（进度条或 loading）

**文件管理界面**（可选，Phase 2 可做）：
- 我的附件列表
- 删除附件
- 查看附件详情

#### 5.3 配置和测试

**配置文件**：
- 添加完整的 `FileStorage` 配置到 `appsettings.json`
  - 文件大小限制（Avatar/Image/Document）
  - 允许的文件扩展名白名单
  - 本地存储配置
  - 图片处理配置（使用 Rust/C#、缩略图、多尺寸、压缩质量）
  - 水印配置（内容、位置、透明度、字体大小）
  - 文件去重配置
  - 分片上传配置（预留）
- 添加 `appsettings.Local.json` 示例

**测试**：
- 单元测试（AttachmentService、文件校验、文件去重）
- 集成测试（上传 API、删除 API）
- Rust 扩展性能对比测试（C# vs Rust 水印速度）
- 文件去重测试（上传相同文件验证去重）

#### 验收标准

**后端**：
- ✅ 可以上传图片（jpg/jpeg/png/gif/webp）到本地存储
- ✅ 可以上传文档（pdf/doc/docx/txt）到本地存储
- ✅ 自动生成缩略图（150x150）
- ✅ 自动生成多尺寸（Small/Medium）
- ✅ 图片压缩功能正常工作（JPEG 85%）
- ✅ 文件去重功能正常工作（相同文件不重复存储）
- ✅ 水印功能可选配置（用户可选择是否添加水印）
- ✅ C# 和 Rust 两种实现都能正常工作且可通过配置切换
- ✅ 文件校验正常工作（类型、大小、Magic Number）
- ✅ 软删除功能正常工作

**前端**：
- ✅ FileUpload 组件可用（拖拽、点击、进度、预览、错误处理）
- ✅ MarkdownEditor 集成图片上传（按钮、粘贴、拖拽）
- ✅ 上传成功后自动插入 Markdown 图片语法
- ✅ 错误重试机制正常工作（3 次重试，指数退避）

**配置**：
- ✅ 配置文件完整且可切换 C#/Rust 实现
- ✅ 静态文件中间件配置正确（可访问上传的文件）

**性能**：
- ✅ Rust vs C# 性能对比测试完成（生成性能报告）
- ✅ 单个图片上传处理时间 < 2 秒（< 5MB 图片）

### 第 6 周｜互动、积分与商城基础
- 点赞/收藏：后端提供幂等接口（Upsert），前端按钮即时反馈与回滚。
- 积分：事件（发帖/评论/点赞）触发积分流水；`PointLedger`、`PointTransaction`。
- 商城雏形：商品管理、库存、购买接口；前端展示商品列表与购买流程。
- 验收：触发积分后可在"个人中心"看到余额与流水；购买商品扣减库存与积分。

### 第 7 周｜消息通知系统（M7）

> **阶段目标**：实现 SignalR/WebSocket 实时推送系统，替代当前的轮询机制，提供点赞/评论/系统消息的即时推送能力。详细方案见 [消息通知系统设计](./guide/notification-realtime.md)。

#### 7.1 后端核心功能（Phase 0 - 3-5 天）

**数据模型设计**：
- 创建 `Notification` 实体和数据库迁移
  - 字段：接收者 ID、发送者 ID、通知类型、标题、内容、关联实体类型、关联实体 ID、已读状态、创建时间
  - 支持：软删除、审计字段、多租户、按月分表（`Notification_YYYYMM`）
- 创建 `NotificationSetting` 实体（用户通知偏好配置）
  - 字段：用户 ID、通知类型、是否启用、推送渠道（站内/邮件/短信）

**SignalR Hub 实现**：
- 创建 `NotificationHub`（`/hubs/notification`）
  - `OnConnectedAsync()` - 连接建立时加入用户组
  - `OnDisconnectedAsync()` - 断开时清理连接
  - `MarkAsRead(notificationId)` - 标记已读
  - `MarkAllAsRead()` - 全部已读
- 实现连接管理
  - 用户 ID → 连接 ID 映射（支持多端同时在线）
  - 使用 `Groups.AddToGroupAsync(connectionId, userId)` 管理用户组

**通知推送服务**：
- 创建 `INotificationService` 接口
  - `SendNotificationAsync(userId, type, title, content, relatedId)` - 发送通知
  - `SendBatchNotificationsAsync(userIds, ...)` - 批量发送
  - `GetUnreadCountAsync(userId)` - 获取未读数量
  - `GetNotificationsAsync(userId, pageIndex, pageSize)` - 分页查询
  - `MarkAsReadAsync(notificationId)` - 标记已读
  - `MarkAllAsReadAsync(userId)` - 全部已读
- 实现 `NotificationService`
  - 保存通知到数据库
  - 通过 `IHubContext<NotificationHub>` 推送到客户端
  - 支持推送失败时的降级策略（仅保存数据库）

**集成到论坛业务**：
- 点赞通知：`CommentService.LikeAsync()` → 通知被点赞者
- 评论通知：`CommentService.CreateAsync()` → 通知帖子作者/父评论作者
- 神评/沙发通知：`CommentHighlightJob` → 通知获奖者
- 系统通知：注册奖励、升级奖励等

**API 端点**：
- `GET /api/v1/Notification/Unread` - 获取未读数量
- `GET /api/v1/Notification/List` - 获取通知列表（分页）
- `PUT /api/v1/Notification/{id}/Read` - 标记已读
- `PUT /api/v1/Notification/ReadAll` - 全部已读
- `DELETE /api/v1/Notification/{id}` - 删除通知

#### 7.2 前端开发（2-3 天）

**SignalR 客户端集成**：
- 安装 `@microsoft/signalr`
- 创建 `NotificationHub` 连接管理器（`shared/services/notificationHub.ts`）
  ```typescript
  class NotificationHubService {
    private connection: HubConnection;

    async start() {
      this.connection = new HubConnectionBuilder()
        .withUrl('/hubs/notification', {
          accessTokenFactory: () => authService.getToken()
        })
        .withAutomaticReconnect()
        .build();

      this.connection.on('ReceiveNotification', this.handleNotification);
      await this.connection.start();
    }

    private handleNotification(notification: Notification) {
      // 更新未读数量
      // 显示 Toast 提示
      // 触发事件通知订阅者
    }
  }
  ```

**通知中心组件**：
- 创建 `NotificationCenter` 组件（@radish/ui）
  - 顶部状态栏的通知图标（显示未读数量徽章）
  - 点击展开通知列表下拉面板
  - 通知列表项（标题、内容、时间、已读状态）
  - 全部已读按钮
  - 查看更多跳转到通知页面
- 创建 `NotificationList` 页面（完整的通知列表）
  - 分页加载
  - 按类型筛选（点赞/评论/系统）
  - 单个/批量标记已读
  - 跳转到关联内容（帖子/评论）

**实时推送集成**：
- 登录成功后自动连接 SignalR Hub
- 接收到通知时：
  - 更新未读数量徽章
  - 显示 Toast 提示
  - 如果通知中心已打开，实时追加到列表顶部
- 断线重连机制（自动重连 + 重新加载未读数量）

**状态栏集成**：
- 在 `StatusBar` 组件中添加通知图标
- 显示未读数量徽章（红点或数字）
- 点击展开通知中心下拉面板

#### 7.3 配置和测试

**配置文件**：
- 添加 `Notification` 配置到 `appsettings.json`
  ```json
  "Notification": {
    "Enable": true,
    "SignalR": {
      "HubPath": "/hubs/notification",
      "KeepAliveIntervalSeconds": 15,
      "ClientTimeoutSeconds": 30
    },
    "Retention": {
      "DaysToKeep": 90,
      "AutoCleanup": true
    }
  }
  ```

**测试**：
- 单元测试（NotificationService、推送逻辑）
- SignalR Hub 集成测试（连接、推送、断线重连）
- 前端测试（通知中心组件、实时推送）
- 性能测试（1000+ 用户同时在线推送）

#### 验收标准

**后端**：
- ✅ SignalR Hub 可正常连接（WebSocket 或 Long Polling）
- ✅ 用户登录后自动加入用户组
- ✅ 点赞/评论触发通知推送到对应用户
- ✅ 批量推送功能正常工作（神评/沙发通知）
- ✅ 推送失败时降级策略生效（仅保存数据库）
- ✅ 通知列表 API 可分页查询
- ✅ 标记已读功能正常工作

**前端**：
- ✅ 登录后自动连接 SignalR Hub
- ✅ 接收到通知时显示 Toast 提示
- ✅ 未读数量徽章实时更新
- ✅ 通知中心下拉面板可展开/收起
- ✅ 点击通知可跳转到关联内容
- ✅ 全部已读功能正常工作
- ✅ 断线后自动重连

**性能**：
- ✅ 1000 用户同时在线时推送延迟 < 500ms
- ✅ 单用户未读通知查询 < 100ms

### 第 8 周｜用户等级与经验值系统（M8）

> **阶段目标**：实现修仙主题的 11 级等级体系，用户行为触发经验值增长，升级获得萝卜币奖励。详细方案见 [用户等级与经验值系统设计](./guide/experience-level-system.md)。

#### 8.1 后端核心功能（约 2 周）

**数据模型设计**：
- 创建 `UserLevel` 实体（用户等级信息）
  - 字段：用户 ID、当前等级、当前经验值、总经验值、升级时间、等级称号
- 创建 `ExperienceTransaction` 实体（经验值流水）
  - 字段：用户 ID、经验值变动、变动原因、关联实体类型、关联实体 ID、创建时间
- 创建 `LevelConfig` 实体（等级配置表）
  - 字段：等级、称号、所需经验值、升级奖励（萝卜币）、等级图标、等级颜色

**等级计算服务**：
- 创建 `ILevelService` 接口
  - `GetUserLevelAsync(userId)` - 获取用户等级信息
  - `AddExperienceAsync(userId, amount, reason, relatedId)` - 增加经验值
  - `CalculateLevelAsync(totalExp)` - 根据总经验值计算等级
  - `GetNextLevelRequiredExpAsync(currentLevel)` - 获取下一级所需经验
- 实现经验值规则
  - 发帖：+10 经验
  - 评论：+5 经验
  - 收到点赞：+2 经验
  - 神评：+50 经验
  - 沙发：+30 经验
  - 每日签到：+5 经验

**升级奖励集成**：
- 升级时自动发放萝卜币奖励
- 通过 `ICoinRewardService` 发放奖励
- 通过 `INotificationService` 推送升级通知

**API 端点**：
- `GET /api/v1/Level/Current` - 获取当前用户等级信息
- `GET /api/v1/Level/{userId}` - 获取指定用户等级信息
- `GET /api/v1/Level/Leaderboard` - 等级排行榜
- `GET /api/v1/Experience/History` - 经验值流水历史

#### 8.2 前端开发（1 周）

**等级徽章组件**：
- 创建 `LevelBadge` 组件（@radish/ui）
  - 显示等级称号、图标、颜色
  - 悬停显示经验值进度条
  - 支持不同尺寸（小/中/大）

**个人主页集成**：
- 在个人主页显示等级徽章
- 显示经验值进度条（当前经验/下一级所需经验）
- 显示经验值流水历史

**等级排行榜**：
- 创建等级排行榜页面
- 显示前 100 名用户
- 支持按等级、经验值排序

**升级动画**：
- 升级时显示动画特效
- 播放音效（可选）
- 显示升级奖励（萝卜币）

#### 验收标准

**后端**：
- ✅ 用户行为触发经验值增长
- ✅ 经验值累积到阈值时自动升级
- ✅ 升级时自动发放萝卜币奖励
- ✅ 升级时推送通知到用户
- ✅ 等级排行榜 API 可用

**前端**：
- ✅ 个人主页显示等级徽章和经验值进度条
- ✅ 升级时显示动画特效和奖励提示
- ✅ 等级排行榜可正常显示

### 第 9 周｜商城系统（M9）

- 商品管理：商品 CRUD、分类、上下架、库存管理
- 购买链路：购物车、订单创建、萝卜币支付、库存扣减
- 权益发放：购买成功后自动发放对应权益（VIP、徽章等）
- 订单系统：订单列表、详情、状态追踪
- 验收：可浏览商品列表，萝卜币购买商品并扣减库存，订单记录可追溯

### 第 10 周｜可观测性、测试与性能（M10）
- 自动化：补齐 xUnit/Vitest + Playwright（可选）测试，纳入 CI。
- 性能：PostgreSQL 索引审计、缓存策略（IMemoryCache/redis 预留）、SQLSugar Profiling。
- Observability：Serilog → Seq/Console JSON；OpenTelemetry 采样；健康检查拓展。
- 原生扩展（Rust）：当前统一扩展库为 `Radish.Core/radish-lib`（已替代 `test_lib`）；如后续需要从 Core 抽离再迁到 `native/rust/*`。建议补齐 CI/构建脚本：`cargo build --release` + 拷贝产物到 `Radish.Api/bin/<Configuration>/net10.0/`（Windows: `radish_lib.dll`；Linux: `libradish_lib.so`；macOS: `libradish_lib.dylib`）。
- 验收：
  - `dotnet test`, `npm run test`, `npm run lint`, `npm run build` 均通过。
  - P95 指标满足目标；日志可追踪请求链路。
  - `/api/v2/RustTest/*` 在 CI/本地构建后可直接加载 `radish-lib` 输出的 `radish_lib`（DLL/SO/Dylib），无须手动复制。

### 第 11 周｜部署、运维与交付（M11）
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
- 若出现新的关键技术选型（例如引入 redis、消息队列等），请同步更新 [开发框架说明](/architecture/framework) 与本文件，保持信息一致。
- React Compiler（实验）：React 19 新编译器暂不在主干启用；待官方正式发布并通过 Vite/Babel 稳定支持后，再在独立分支验证收益（减少手写 memo、优化渲染），评估通过后才会合入主线。

## 后续规划：API Gateway

- Gateway 项目已完成 Phase 0 实施，实现了统一服务入口、YARP 路由转发、健康检查聚合等核心功能。详见 [Gateway 服务网关](/guide/gateway)。
- 当前阶段专注既有 M1-M8 交付，Gateway 的进一步增强（如统一认证、API 聚合、服务发现等）将根据实际需求在后续迭代中实施。
- 在日常开发中提前预留：规范 Header（`X-Request-Id`、`X-Client-Id`）、CORS 配置、登录控制器可被 Gateway 复用，减少后续迁移成本。
