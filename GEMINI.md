# Radish Project - Gemini CLI Instructions

本文件为 Gemini CLI 提供 Radish 项目的独立协作说明。

## 语言规范
- 默认使用中文进行说明、讨论和文档编写。
- 代码、技术标识、配置键、命令、路径和引用名保留原文。

## 协作流程
- 编写任何代码之前，必须先说明方案并等待批准。
- 如果需求不明确，必须先提出问题并澄清后再动手。
- 处理项目规则、接口、主题或架构变更时，优先保证与 `Docs/` 中的事实一致。

## 1. 项目概览
- 后端：ASP.NET Core 10 + SQLSugar ORM + PostgreSQL（本地默认 SQLite）
- 网关：`Radish.Gateway`，统一门户与 API 网关
- 认证：`Radish.Auth`，基于 OpenIddict 的 OIDC 认证服务器
- 前端：React 19 + Vite（Rolldown） + TypeScript，采用 WebOS 桌面化 UI
- UI 组件库：`@radish/ui`，使用 npm workspaces 共享源码
- Rust 扩展：`Lib/radish.lib/`
- 协作分支：`dev`
- 文档唯一真相源：`Docs/`

## 2. AI 执行边界

### 禁止直接执行
- `dotnet add package`
- `npm install`
- `dotnet run`
- `npm run dev`

### 正确做法
- 如需安装依赖，直接告诉用户应执行的命令与版本
- 如需启动服务，直接告诉用户应执行的命令与端口

### 可直接执行
- 代码读写
- 构建、测试、类型检查、Lint、静态分析
- Git 操作

### 沙盒验证与提权规则
- 默认优先在沙盒环境中执行构建、测试和最小验证。
- 如果因权限、PATH、网络、证书、文件系统或沙盒隔离问题，导致无法确认真实结果，可直接申请提权。
- 提权用途必须限制在“构建 / 测试 / 必要验证”。
- 即使提权，也仍然禁止直接执行 `dotnet add package`、`npm install`、`dotnet run`、`npm run dev`。

## 3. 环境与命令

### 基础环境
- .NET SDK 10
- Node.js 24+
- PostgreSQL 16+，或本地 SQLite

### AI 可执行命令
```bash
dotnet build Radish.slnx -c Debug
dotnet test Radish.Api.Tests

npm run build --workspace=radish.client
npm run build --workspace=radish.console
npm run type-check --workspace=@radish/ui
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
- Docs：`http://localhost:4000`
- Scalar：Gateway `https://localhost:5000/scalar`，API `http://localhost:5100/scalar`

## 4. 仓库结构

### 后端
- 宿主：`Radish.Api`、`Radish.Gateway`、`Radish.Auth`
- 业务层：`Radish.Service`、`Radish.Repository`、`Radish.Core`、`Radish.Model`
- 基础设施：`Radish.Common`、`Radish.Extension`、`Radish.Infrastructure`
- 契约层：`Radish.IService`、`Radish.IRepository`
- 共享层：`Radish.Shared`
- 测试：`Radish.Api.Tests`

### 前端
- `Frontend/radish.client`：WebOS 桌面客户端
- `Frontend/radish.console`：管理后台
- `Frontend/radish.ui`：共享 UI 组件库，源码直连，无需单独构建

## 5. 配置与数据库

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

### 多租户
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

## 6. 开发规范与架构

### 依赖流
```text
Common → Shared → Model → Infrastructure → IRepository/Repository
→ Core → IService/Service → Extension → Api/Gateway/Auth
```

### 核心架构约束
1. `Common` 仅引用外部 NuGet 包，需要访问业务层的工具放到 `Extension`
2. `Repository` 只返回实体，`Service` 必须映射为 DTO / Vo 后再暴露给 Controller
3. Controller 禁止直接注入 `IBaseRepository`
4. 先定义 `IService` / `IRepository` 再写实现
5. `Service` 层严禁直接使用 `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`
6. 优先扩展 `BaseRepository` 的泛型方法，如 `QueryDistinctAsync`、`QuerySumAsync`
7. 复杂联表和性能优化需求再创建实体专属仓储

### ViewModel / Vo 规范
- 所有返回前端的 ViewModel 类必须使用 `Vo` 后缀
- 所有字段必须使用 `Vo` 前缀
- `UserVo` 的混淆字段命名是安全设计，前端必须适配
- Controller 严禁返回匿名对象
- 优先使用 AutoMapper 前缀识别映射，仅在字段名、类型或逻辑不一致时手动映射

### 软删除规范
- 业务数据优先使用软删除
- 实体需实现 `IDeleteFilter`
- 查询自动过滤 `IsDeleted = true`
- 删除时记录 `DeletedAt`、`DeletedBy`
- 支持 `RestoreByIdAsync` 恢复

## 7. 编码与前端规范

### 通用代码风格
- C# 使用 4 空格、文件范围命名空间、nullable
- React 组件默认使用 `const`
- 避免 `var`，优先 `const`，需要重赋值时使用 `let`
- 常规 Hooks 以 `useState`、`useMemo`、`useEffect` 为主
- 单文件建议 `500-1000` 行，硬上限 `1000` 行

### 后端日志
```csharp
builder.Host.AddSerilogSetup();
Log.Information("User {UserId} logged in", userId);
```

- 应用日志：`Logs/{ProjectName}/Log.txt`
- SQL 日志：`Logs/{ProjectName}/AopSql/AopSql.txt`
- 审计日志：数据库 `AuditLog_YYYYMMDD`

### 前端日志
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
- 所有环境变量必须以 `VITE_` 开头
- 敏感信息只放 `.env.local`
- 通过 `env.ts` 访问配置，禁止直接使用 `import.meta.env`

### API 客户端规范
- 统一使用 `@radish/ui` 提供的 API 客户端
- 禁止自定义 fetch / axios 封装
- 特殊场景如上传进度可使用 `XMLHttpRequest`
- 特殊场景必须通过 `getApiClientConfig()` 获取统一配置，并在代码中注明原因

### UI 与视觉规范
- 视觉口径以 `Docs/frontend/visual-theme-spec.md` 与 `Docs/frontend/visual-color-reference.md` 为准
- `radish.client` 必须支持 `default / guofeng` 主题切换
- 风格方向为淡雅新中式，保持留白、克制和阅读优先
- 云纹、山纹、水纹仅作边缘、角标、分区收边和弱背景修饰
- 颜色必须先抽象为语义 token，再应用到页面
- 禁止持续新增硬编码颜色
- 不得破坏 Dock、桌面图标、窗口容器和滚动区域等基础布局

### 前端架构补充
- `@radish/ui` 无需单独构建，前端直接引用源码
- 修改 `radish.ui` 后需保证 client / console 热更新链路可用

## 8. 新增功能流程

### 后端
1. 在 `Radish.Model/Models` 定义实体
2. 在 `Radish.Model/ViewModels` 定义 Vo
3. 在 `Radish.Extension/AutoMapperExtension/CustomProfiles` 增加映射
4. 优先复用 `IBaseRepository<TEntity>`
5. 简单 CRUD 优先复用 `IBaseService<TEntity, TVo>`
6. 复杂逻辑再创建自定义 Service / Repository
7. Controller 只注入 Service
8. 补充 `Radish.Api.http` 示例和对应测试

### 前端
- 通用组件放 `@radish/ui`
- WebOS 组件放 `Frontend/radish.client/src/`

## 9. Rust 原生扩展
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

## 10. 常见陷阱
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

## 11. Git 提交规范
- 必须遵循 Conventional Commits
- 复杂提交建议补充 `2-5` 条简洁说明
- 必须使用当前用户 Git 身份
- 严禁任何 AI 协作者署名

示例：
```text
feat(ui): 统一组件库主题并优化桌面布局

- 调整灵动岛收起态 Dock 顶部偏移
- 重做 Showcase 展示页
- 修复图表初始化尺寸告警
```

## 12. 文档与更新要求
- `Docs/` 为唯一真相源
- 关键文档包括：
  - `Docs/architecture/specifications.md`
  - `Docs/architecture/framework.md`
  - `Docs/frontend/design.md`
  - `Docs/frontend/visual-theme-spec.md`
  - `Docs/frontend/visual-color-reference.md`
  - `Docs/guide/`
  - `Docs/development-plan.md`
  - `Docs/changelog/`
  - `Docs/deployment/guide.md`
- 更新日志使用 Asia/Shanghai（UTC+8）
- 重大架构、接口、视觉和流程变更必须同步更新文档
