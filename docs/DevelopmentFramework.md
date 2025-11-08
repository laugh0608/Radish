# 开发整体架构

> 本文描述 Radish 现阶段的目标模型、技术选型与分层架构，面向参与后端、前端与运维的贡献者。所有内容均基于「.NET 10 + SQLSugar + PostgreSQL + React(Vite)」的新栈，ABP/Angular/MongoDB 方案已归档。

## 功能期望与范围

- **核心模块**
  - 身份与会话：自建帐号体系、邮箱/用户名登录、JWT + 刷新令牌、角色与权限控制、第三方登录预留。
  - 门户与文档：React 单页应用 + Swagger/Scalar 嵌入，支持文档直达、健康检查、运维面板。
  - 内容域：分类 / 标签 / 帖子 / 评论 / 点赞 / 收藏 / 浏览计数，列表分页与过滤，富文本编辑。
  - 搜索：按标题、标签、分类、作者检索；支持时间/热度排序与模糊匹配（PostgreSQL `tsvector` 预留）。
  - 通知与订阅（可选）：帖子互动提醒、积分变动提醒，支持站内信或邮件。
  - 积分系统：积分账户、积分流水、规则引擎（发帖、互动、被采纳等）。
  - 商城系统：商品、库存、购买/退款、效果激活（头像框、昵称色、签名等）。
  - 管理后台（同 React 工程内实现管理视图）：分类、内容、用户、积分与商城配置。

- **非功能性要求**
  - 安全：HTTPS、JWT + Refresh、基于角色的授权、CSP/CORS、参数验证、敏感信息集中管控。
  - 性能：关键查询 P95 ≤ 200ms；SQLSugar Profile + PostgreSQL EXPLAIN 校验索引；读多写少场景可使用内存缓存。
  - 可用性：健康检查 `/health`, `/ready`; SQLSugar 迁移幂等；容器探针。
  - 可观测性：Serilog 结构化日志、请求跟踪 ID、PostgreSQL 慢查询日志、前端监控埋点。
  - 国际化：后端资源文件（zh-Hans 基线），前端 i18n（React i18next）。
  - 质量：后端 xUnit + Shouldly + NSubstitute，前端 Vitest + RTL，关键链路具备 E2E 冒烟。
  - 配置：`appsettings.{Env}.json` + 环境变量 + `.env`；禁止把密钥写入仓库。

- **里程碑（参阅 DevelopmentPlan）**
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
| 语言/运行时 | C# 13, .NET 10 | `global.json` 锁定 SDK 10.0.0，开启 `Nullable` 与 `ImplicitUsings` |
| Web Host | ASP.NET Core WebApplication | Program.cs 中最小宿主，按需要拆 Controller/Minimal API |
| ORM | [SQLSugar](https://github.com/donet5/SqlSugar) | Code First + Migration，仓储层集中管理上下文，支持读写分离配置 |
| 数据库 | PostgreSQL 16 | 默认端口 5432，连接通过 `ConnectionStrings__Default` 注入 |
| 前端 | React 19 + Vite + TypeScript | Node 24，使用 pnpm/npm 均可；建议 React Query + Zustand/TanStack Router |
| 前端构建 | Vite Rolldown，ESLint 9，Vitest | 统一在 `radish.client` 目录运行 |
| 测试 | xUnit 3 + Shouldly + NSubstitute | `Radish.Server.Tests` 目录；后续可按层拆分 |
| 日志 / 配置 | Serilog + `Microsoft.Extensions.Configuration` | 支持 JSON + 环境变量 + 用户密钥；生产日志输出到 Console + Seq/Elastic 预留 |
| 容器 | Dockerfile（Radish.Server）+ Docker Compose | Compose 负责 PostgreSQL + API + 前端静态站点 |

### 分层视图

```
radish.client (React SPA)
        │ REST/JSON
Radish.Server (ASP.NET Core Host)
        │ 调用应用服务 + DTO
Radish.Service (Application Layer)
        │ 调度
Radish.Core (Domain Layer)
        │ 抽象 + 领域事件
Radish.Repository (Infrastructure, SQLSugar)
        │ SQL
PostgreSQL
```

- `Radish.Server`
  - 负责 DI、配置、日志、全局异常、认证授权、Swagger/Scalar、HealthChecks。
  - 仅保留轻量 Controller/Endpoint，所有核心逻辑委派给 Service 层。
- `Radish.Service`
  - 应用服务（`*AppService`）封装用例流程、权限校验、事务控制、DTO 转换。
  - 依赖 `Radish.Core` 接口与 `Radish.Repository` 实现，通过 `IUnitOfWork` 控制 SQLSugar 上下文。
- `Radish.Core`
  - 聚合根（Post、Comment、Category、UserProfile、PointLedger、ShopItem 等）、值对象、领域事件。
  - 定义仓储接口与领域服务，例如 `IPostRepository`, `IPointPolicyService`。
- `Radish.Repository`
  - 持久化实现，集中 SQLSugar 配置（连接池、AOP 日志、软删除、审计字段）。
  - 提供迁移/种子帮助类，必要时拆分模块级仓储。
- `Radish.Model`
  - DTO、ViewModel、查询对象、枚举。
  - 提供 `PagedRequest`, `PagedResponse<T>`, `ApiError` 等复用结构。
- `Radish.Extension`
  - 横切关注：验证、缓存策略、OpenAPI 自定义、JWT 扩展、全局过滤器。
- `Radish.Shared`
  - 常量、错误码、事件名、Options 绑定类型。
- `radish.client`
  - SPA + 内嵌管理视图；共享 DTO 通过 `radish.client/src/types` 维护，与后端模型保持同步。

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

## 数据与持久化策略

- SQLSugar 统一由 `ISqlSugarClient` 提供，使用多租户模式映射不同连接（目前单库）。
- 公共 BaseEntity：`Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsDeleted`, `ConcurrencyStamp`。
- 软删除通过 SQLSugar Filter 全局开启；必要时在仓储层提供 `IncludeDeleted` 选项。
- 迁移策略：
  - 开发：`db.DbMaintenance.CreateDatabase()` + `InitTables()`。
  - 生产：通过 `Radish.Repository.Migrations` 导出 SQL 或在部署阶段运行迁移命令。
- 数据初始化：`SeedRunner` 负责创建默认分类、管理员、积分规则。
- PostgreSQL 特性利用：JSONB 列（存储自定义配置）、`tsvector` 搜索、行级锁（积分/库存扣减）。

## 前端架构与规范

- Vite 配置 HTTPS、代理 API、环境变量区分（`.env.development` / `.env.production`）。
- 目录建议：`features/*`, `entities/*`, `shared/*`, `app/providers`，使用按特性拆包。
- 认证：基于 `fetch`/`axios` 拦截器附带 AccessToken，失效时调用刷新接口（刷新令牌存于 HttpOnly Cookie 或安全存储）。
- 状态管理：React Query 管理异步数据，Zustand/Jotai 管理本地 UI 状态；Form 使用 React Hook Form。
- UI 与可访问性：Tailwind/UnoCSS 原子化 + 自研组件，提供暗色模式与响应式布局。
- 测试：组件级 Vitest，关键流程 Playwright（可选）。

## DevOps 与运维基线

1. **配置与密钥**：
   - `appsettings.json` 仅放默认值，环境差异通过 `appsettings.{Env}.json` + 环境变量。
   - 本地秘密写入 `dotnet user-secrets` 或 `.env.local`（被 .gitignore 忽略）。
2. **日志与监控**：
   - Serilog 写入 Console + File；在容器中输出 JSON 便于收集。
   - 预留 OpenTelemetry Exporter 与 Prometheus 指标。
3. **部署流水线**：
   - Dockerfile 使用多阶段构建（Restore → Build → Publish）。
   - Compose 负责 PostgreSQL + API + 反向代理（如 Caddy/Nginx），可扩展加上前端静态站点。
   - 生产部署建议挂载 `appsettings.Production.json` 与证书目录，使用 `ConnectionStrings__Default` 环境变量。
4. **质量门禁**：
   - PR 必须附带 `dotnet test` 与 `npm run build` 结果；若变更数据库需提供迁移脚本与回滚建议。
   - 关键模块需要 Code Review + Pair Walkthrough。

---

如需在架构或选型上做进一步调整，请同步更新本文件并在 DevelopmentLog 中记录决策，确保贡献者获取到一致的信息。
