# CLAUDE.md

本文件为 Claude Code 提供 Radish 项目的独立协作说明。

## 语言规范
- 默认使用中文进行说明、讨论和文档编写。
- 代码、技术标识、配置键、命令、路径和引用名保留原文。

## 协作流程
- 编写任何代码之前，必须先说明方案并等待批准。
- 如果需求不明确，必须先提出问题并澄清后再动手。
- 修改架构、接口、主题规范或流程口径时，优先以 `Docs/` 中的当前事实为准。
- 涉及当前阶段、优先级或范围判断时，优先查看 `Docs/planning/current.md`。
- 涉及本地验证、CI 对齐或回归入口时，优先查看 `Docs/guide/validation-baseline.md`。

## 项目概览
- 后端：ASP.NET Core 10 + SQLSugar ORM + PostgreSQL（本地默认 SQLite）
- 网关：`Radish.Gateway`，统一门户与 API 网关
- 认证：`Radish.Auth`，基于 OpenIddict 的 OIDC 认证服务器
- 前端：React 19 + Vite（Rolldown） + TypeScript，采用 WebOS 桌面化 UI
- UI 组件库：`@radish/ui`，基于 npm workspaces 的共享组件库
- HTTP 客户端：`@radish/http`，统一 API 客户端与相关类型封装
- Rust 扩展：`Lib/radish.lib/`
- 主要协作分支：`dev`
- 文档唯一真相源：`Docs/`

## AI 执行边界

### 禁止直接执行
- `dotnet add package`
- `npm install`
- `dotnet run`
- `npm run dev`

### 正确做法
- 需要安装包时，直接告诉用户应执行的命令和版本
- 需要启动服务时，直接告诉用户应执行的命令和访问端口

### 允许直接执行
- 代码读写
- 构建、测试、类型检查、Lint、静态分析
- Git 操作

### 沙盒验证与提权规则
- 默认优先在沙盒中执行构建、测试和最小验证。
- 如果因沙盒限制、权限隔离、PATH 缺失、网络或证书问题，无法确认真实构建或测试结果，可直接申请提权。
- 提权用途只能是“构建 / 测试 / 必要验证”，不得扩展到安装依赖、启动长期服务或其他高风险行为。
- 即使提权，也仍然禁止直接执行 `dotnet add package`、`npm install`、`dotnet run`、`npm run dev`。

## 仓库结构

### 后端
- 宿主：`Radish.Api`、`Radish.Gateway`、`Radish.Auth`
- 初始化与只读自检：`Radish.DbMigrate`
- 业务层：`Radish.Service`、`Radish.Repository`、`Radish.Core`、`Radish.Model`
- 基础设施：`Radish.Common`、`Radish.Extension`、`Radish.Infrastructure`
- 契约层：`Radish.IService`、`Radish.IRepository`
- 共享层：`Radish.Shared`
- 测试：`Radish.Api.Tests`

### 前端
- `Frontend/radish.http`：统一 HTTP 客户端与相关类型封装
- `Frontend/radish.client`：WebOS 桌面客户端
- `Frontend/radish.console`：管理后台
- `Frontend/radish.ui`：共享组件库，源码直连，无需独立构建

## 环境与命令

### 基础环境
- .NET SDK 10
- Node.js 24+
- PostgreSQL 16+，或本地 SQLite

### 可直接执行的验证命令
```bash
dotnet build Radish.slnx -c Debug
dotnet test Radish.Api.Tests

npm run build --workspace=radish.client
npm run build --workspace=radish.console
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http

npm run validate:baseline:quick
npm run validate:baseline
npm run validate:baseline:host
```

### 供用户手动执行的启动命令
```bash
pwsh ./start.ps1
./start.sh

dotnet run --project Radish.Api
dotnet run --project Radish.Gateway
dotnet run --project Radish.Auth

npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

### 默认端口
- API：`http://localhost:5100`
- Auth：`http://localhost:5200`
- Gateway：`https://localhost:5000`
- Frontend：`http://localhost:3000`
- Console：`http://localhost:3100`
- 文档：固定项目文档统一收口到仓库 `Docs/`，由 WebOS“文档”应用承载，不再维护独立 Docs 站点
- Scalar：Gateway `https://localhost:5000/scalar`，API `http://localhost:5100/scalar`

## 配置与数据库

### 配置优先级
```text
appsettings.Shared.json → appsettings.json → appsettings.{Environment}.json
→ appsettings.Local.json（不提交）→ 环境变量
```

### 关键配置
- `Snowflake.WorkId`：宿主差异配置，每实例唯一
- `Snowflake.DataCenterId`：共享配置
- `MainDb/Databases`：共享数据库配置
- `Redis.Enable/ConnectionString`：共享缓存配置
- `Redis.InstanceName`：宿主差异配置

### 数据库关系
- API 与 Auth 共享 `Radish.db`、`Radish.Log.db`
- Auth 独享 `Radish.OpenIddict.db`
- 所有数据库位于 `DataBases/`

### 多租户模式
- 字段级：`ITenantEntity`
- 表级：`[MultiTenant(Tables)]`
- 库级：`[MultiTenant(DataBases)]`

### 配置读取
- `AppSettings.RadishApp("Section", "Key")`
- 或实现 `IConfigurableOptions`

### 缓存策略
```csharp
builder.Services.AddCacheSetup();
```

- `Redis.Enable = true` 时使用 Redis
- `Redis.Enable = false` 时使用内存缓存

## 分层架构与约束

### 依赖流
```text
Common → Shared → Model → Infrastructure → IRepository/Repository
→ Core → IService/Service → Extension → Api/Gateway/Auth
```

### 核心约束
1. `Common` 仅引用外部 NuGet 包，需要访问业务层的工具放在 `Extension`
2. `Repository` 只返回实体，`Service` 负责映射 DTO / Vo
3. Controller 禁止直接注入 `IBaseRepository`
4. 先定义 `IService` / `IRepository` 再写实现
5. `Service` 层严禁直接使用 `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`
6. 优先复用 `BaseRepository<T>` 和 `BaseService<TEntity, TVo>`
7. 通用聚合优先扩展 `BaseRepository`，复杂查询再做实体专属仓储

### ViewModel / Vo 规范
- 所有返回前端的 ViewModel 必须使用 `Vo` 后缀
- 所有字段必须使用 `Vo` 前缀
- `UserVo` 的混淆字段命名是安全设计，不允许要求后端改名
- Controller 严禁返回匿名对象
- 优先使用 AutoMapper 前缀识别映射，仅在字段名、类型或逻辑不一致时手动映射

## 软删除规范
- 业务数据优先使用软删除
- 新实体应实现 `IDeleteFilter`
- 查询自动过滤 `IsDeleted = true`
- 删除时记录 `DeletedAt` 与 `DeletedBy`
- 恢复使用 `RestoreByIdAsync`

## 编码与前端规范

### 通用代码风格
- C# 使用 4 空格、文件范围命名空间、nullable
- React 组件默认使用 `const`
- 避免 `var`，优先 `const`，需要重赋值时使用 `let`
- 常规 Hooks 以 `useState`、`useMemo`、`useEffect` 为主
- 单文件建议 `500-1000` 行，硬上限 `1000` 行

### 日志规范

#### 后端
```csharp
builder.Host.AddSerilogSetup();
Log.Information("User {UserId} logged in", userId);
```

- 应用日志：`Logs/{ProjectName}/Log.txt`
- SQL 日志：`Logs/{ProjectName}/AopSql/AopSql.txt`
- 审计日志：数据库 `AuditLog_YYYYMMDD`

#### 前端
- Client：`Frontend/radish.client/src/utils/logger.ts`
- Console：`Frontend/radish.console/src/utils/logger.ts`
- 禁止直接使用 `console.log/info/warn/error`
- 必须使用统一 `log` 工具

### 前端环境配置
- `.env.development`
- `.env.production`
- `.env.local`
- `.env.local.example`

规则：
- 所有变量必须以 `VITE_` 开头
- 敏感信息只放 `.env.local`
- 通过 `env.ts` 访问配置，禁止直接使用 `import.meta.env`

### API 客户端规范
- 统一使用 `@radish/http` 提供的 API 客户端
- 禁止自定义 fetch / axios 封装
- 上传进度等特殊场景可使用 `XMLHttpRequest`
- 特殊场景必须通过 `getApiClientConfig()` 获取统一配置，并在代码中注明原因

### 前端视觉与主题规范
- 视觉口径以 `Docs/frontend/visual-theme-spec.md` 与 `Docs/frontend/visual-color-reference.md` 为准
- `radish.client` 必须支持 `default / guofeng` 主题切换
- 风格方向为淡雅新中式，保持留白、克制和阅读优先
- 云纹、山纹、水纹仅用于边缘、角标、分区收边和弱背景修饰
- 颜色必须先抽象为语义 token，再用于页面
- 禁止持续新增硬编码颜色
- 不得破坏 Dock、桌面图标、窗口容器和滚动区域等基础布局

### 前端架构补充
- `@radish/ui` 无需单独构建，前端直接引用源码
- 修改 `radish.ui` 时需保证 client / console 的热更新链路不被破坏

## 新增功能流程

### 后端
1. 在 `Radish.Model/Models` 定义实体
2. 在 `Radish.Model/ViewModels` 定义 Vo
3. 在 `Radish.Extension/AutoMapperExtension/CustomProfiles` 增加映射
4. 优先复用 `IBaseRepository<TEntity>`
5. 简单 CRUD 优先复用 `IBaseService<TEntity, TVo>`
6. 复杂逻辑再创建自定义 Service / Repository
7. Controller 只注入 Service
8. 补充 `Radish.Api.http` 示例和测试

### 前端
- 通用组件放 `@radish/ui`
- API 调用与统一客户端能力优先复用 `@radish/http`
- WebOS 组件放 `Frontend/radish.client/src/`

## Rust 原生扩展
- 位置：`Lib/radish.lib/`
- 构建：
```bash
cd Lib/radish.lib
cargo build --release
```
- 配置：
```json
{
  "ImageProcessor": {
    "UseRustImplementation": true
  }
}
```

## 常见陷阱
1. 不要把业务逻辑写进 Controller
2. 不要直接暴露实体给前端
3. 简单 CRUD 不要滥建 Service / Repository
4. 先定义接口再写实现
5. `Service` 层禁止直接访问 Db
6. 避免内存过滤，优先数据库级聚合
7. 敏感信息不得硬编码
8. 每环境必须保证唯一 `Snowflake.WorkId`
9. `Common` 不要依赖业务层
10. `ApiModule.LinkUrl` 的参数路由需包含正则

## Git 提交规范
- 必须遵循 Conventional Commits
- 复杂提交建议补充 `2-5` 条简洁说明
- 必须使用当前用户 Git 身份
- 严禁任何 AI 协作者署名

示例：
```text
fix(client): 修复桌面窗口与 Dock 遮挡问题

- 调整灵动岛收起态偏移
- 校正最大化窗口视觉关系
- 保持现有窗口交互边界不变
```

## 文档与更新要求
- `Docs/` 是唯一真相源
- 关键文档包括：
  - `Docs/architecture/specifications.md`
  - `Docs/architecture/framework.md`
  - `Docs/frontend/design.md`
  - `Docs/frontend/visual-theme-spec.md`
  - `Docs/frontend/visual-color-reference.md`
  - `Docs/guide/`
  - `Docs/guide/validation-baseline.md`
  - `Docs/development-plan.md`
  - `Docs/planning/current.md`
  - `Docs/changelog/`
  - `Docs/deployment/guide.md`
- 更新日志时区为 Asia/Shanghai（UTC+8）
- 重大架构、接口、视觉和流程变更必须同步更新文档
