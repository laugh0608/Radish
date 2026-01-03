# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 提供 Radish 项目协作指导。

## 语言规范
**默认中文**：所有说明、讨论、文档使用中文，代码/技术标识/引用除外。

## 项目概览
- **后端**: ASP.NET Core 10 + SQLSugar ORM + PostgreSQL (本地默认 SQLite)
- **网关**: Radish.Gateway - 服务门户及 API 网关 (Phase 0:门户页面; P1+:路由/认证/聚合)
- **认证**: Radish.Auth - 基于 OpenIddict 的 OIDC 认证服务器
- **前端**: React 19 + Vite (Rolldown) + TypeScript，WebOS 桌面化 UI
- **UI 库**: @radish/ui - npm workspaces 共享组件库
- **方案**: Radish.slnx 包含所有后端项目

## 核心命令

### 后端
```bash
dotnet build Radish.slnx -c Debug
dotnet run --project Radish.Api      # http://localhost:5100
dotnet run --project Radish.Gateway  # https://localhost:5000
dotnet run --project Radish.Auth     # http://localhost:5200
dotnet watch --project Radish.Api    # 热重载
dotnet test Radish.Api.Tests
```

### 前端
```bash
npm install                          # 根目录，配置 workspaces
npm run dev --workspace=radish.client    # http://localhost:3000
npm run dev --workspace=radish.console   # http://localhost:3200
npm run type-check --workspace=@radish/ui
```

### 快速启动
```bash
pwsh ./start.ps1  # 或 ./start.sh - 交互式菜单启动服务
```

## 分层架构

### 依赖流 (底→顶)
```
Common (工具,日志,配置) → Shared (常量,枚举) → Model (实体,DTO,ViewModel)
→ Infrastructure (SqlSugar扩展,租户) → IRepository + Repository (数据访问)
→ Core (领域逻辑) → IService + Service (业务逻辑)
→ Extension (Swagger,健康检查,Autofac,AutoMapper,JWT) → Api/Gateway/Auth
```

### 核心架构规则

1. **Common** 仅引用外部 NuGet 包，需访问 Model/Service/Repository 的工具放 **Extension**

2. **Repository** 仅返回实体，Service 必须映射为 ViewModel/DTO 后再暴露给 Controller

3. **Controller** 禁止直接注入 `IBaseRepository`，所有数据访问通过 Service

4. **Entity vs ViewModel**:
   - 实体: `Radish.Model/Models`，继承 `RootEntityTKey<TKey>`，仅 Repository 操作
   - ViewModel: `Radish.Model/ViewModels`，后缀 `Vo`，暴露给 Controller
   - AutoMapper: `Radish.Extension/AutoMapperExtension` 处理映射

5. **Infrastructure** 集中 SqlSugar 多租户逻辑，仅 Repository 和 Extension 引用

6. **接口模式**: 先定义 IService/IRepository，再实现。`BaseRepository<T>` 和 `BaseService<TEntity, TModel>` 提供 CRUD 脚手架

7. **Service 层数据库访问约束** (关键):
   - **严禁**直接使用 `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`
   - **必须**通过 Repository 方法访问数据
   - ❌ 错误: `await _repository.Db.Queryable<Entity>().Where(...).GroupBy(...).ToListAsync()`
   - ✅ 正确: `await _repository.QueryDistinctAsync(e => e.Field, e => e.IsEnabled)`

8. **仓储扩展策略** (优先级):
   - **优先**: 扩展 BaseRepository 泛型方法 (`QueryDistinctAsync`, `QuerySumAsync`) - 跨实体复用
   - **次选**: 创建实体专属仓储 (`UserRepository : BaseRepository<User>`) - 复杂查询/联表/性能优化

## 配置管理

### 加载优先级
```
appsettings.json → appsettings.{Environment}.json
→ appsettings.Local.json (不提交) → 环境变量
```

**重点**:
- `appsettings.Local.json` 用于本地开发敏感数据 (密码/密钥)，Git 忽略
- 深度合并策略，数组需完整覆盖
- 参见 [配置指南](radish.docs/docs/guide/configuration.md)

### 快速设置
```bash
# 默认方式: 直接运行 (SQLite + 内存缓存)
dotnet run --project Radish.Api

# 自定义配置:
cp Radish.Api/appsettings.Local.json.example Radish.Api/appsettings.Local.json
# 编辑 Local.json，仅覆盖需要修改的值
```

### 配置读取
```csharp
var value = AppSettings.RadishApp("Section", "Key");  // 简单键值
// 或实现 IConfigurableOptions 自动绑定
```

**关键配置**:
- `Snowflake.WorkId/DatacenterId`: 每部署实例唯一 (0-30)
- `Databases`: 至少 `ConnId=Main` 和 `ConnId=Log`
- `Redis.Enable`: Redis (`true`) 或内存缓存 (`false`)

## 数据库 & SqlSugar

### Program.cs 设置
```csharp
builder.Services.AddSqlSugarSetup();
SnowFlakeSingle.WorkId = builder.Configuration.GetSection("Snowflake").GetValue<int>("WorkId");
```

### 多租户隔离
- **字段级**: 实现 `ITenantEntity`，通过 `TenantId` 过滤
- **表级**: `[MultiTenant(TenantTypeEnum.Tables)]`，表名变为 `TableName_{TenantId}`
- **库级**: `[MultiTenant(TenantTypeEnum.DataBases)]`，动态解析租户连接

### 本地开发
默认 SQLite (`Radish.db`, `Radish.Log.db`)，自动创建。切换 PostgreSQL 更新连接串和 `DbType=4`。

**数据库共享**:
- API 和 Auth **共享**业务数据库 (`Radish.db`, `Radish.Log.db`)
- Auth 独享 `Radish.OpenIddict.db` (OIDC 数据，EF Core 管理)
- 所有数据库在 `DataBases/` 目录

## 认证授权

### JWT 设置
- Issuer: "Radish"
- Audience: "luobo"
- Token 验证每次请求

### 授权策略
- `Client`: 需 `iss=Radish`
- `System`: 需角色 `System`
- `SystemOrAdmin`: 需角色 `System` 或 `Admin`
- `RadishAuthPolicy`: 自定义权限 (`PermissionRequirement` + `ApiModule.LinkUrl` 正则匹配)

**重点**: API 路由必须以 `/` 开头用于权限匹配，参数路由在 `ApiModule.LinkUrl` 中使用正则。

## 日志 (Serilog)

```csharp
builder.Host.AddSerilogSetup();  // Program.cs 初始化
Log.Information("User {UserId} logged in", userId);  // 使用
```

**日志类型**:
- 应用日志: `Log/{ProjectName}/Log.txt`
- SQL 日志: `Log/{ProjectName}/AopSql/AopSql.txt` + 数据库
- 审计日志: 数据库 `AuditLog_YYYYMMDD` 表

详见 [日志系统文档](radish.docs/docs/guide/logging.md)

## 缓存策略

```csharp
builder.Services.AddCacheSetup();  // 根据 Redis.Enable 切换

// 使用
await cache.SetAsync("key", value, TimeSpan.FromMinutes(10));
var result = await cache.GetAsync<MyType>("key");
```

## 前端架构

### UI 组件库 (@radish/ui)
- **位置**: `radish.ui/`
- **内容**: Button, Input, Modal, Icon + Hooks + Utils
- **使用**: `import { Button } from '@radish/ui';`
- **HMR**: 修改自动热更新到 client/console

### WebOS 桌面 UI
- 顶部状态栏 + 底部 Dock + 桌面图标/窗口
- 应用类型:
  - **内置** (`type: 'window'`): Forum, Chat, Settings
  - **嵌入** (`type: 'iframe'`): Docs
  - **外部** (`type: 'external'`): Console (独立 OIDC 认证)

### React 规范
1. 函数组件 (`const` 定义)
2. 避免 `var`，默认 `const`，需重新赋值用 `let`
3. `useState` + `useMemo` + `useEffect`

## 新增功能流程

### 后端
1. **Model**: `Radish.Model/Models` (实体) + `Radish.Model/ViewModels` (ViewModel)
2. **Mapping**: `Radish.Extension/AutoMapperExtension/CustomProfiles`
3. **Repository**: 大多数用 `IBaseRepository<TEntity>` 即可。仅复杂查询才创建自定义仓储
4. **Service**:
   - **简单 CRUD**: 直接用 `IBaseService<TEntity, TVo>`
   - **复杂逻辑**: 创建自定义 Service (继承 `BaseService<TEntity, TVo>`)
5. **Controller**: 注入 Service (简单用 `IBaseService`，复杂用自定义 `IXxxService`)
6. **示例**: `Radish.Api/Radish.Api.http`
7. **测试**: `Radish.Api.Tests/Controllers/`

### 前端
- **通用组件** → `@radish/ui`
- **WebOS 组件** → `radish.client/src/`

## Rust 原生扩展

**位置**: `Radish.Core/radish-lib/`

**构建**:
```bash
cd Radish.Core/radish-lib
cargo build --release
# 或使用脚本: ./build.ps1 (Windows) / ./build.sh (Linux/macOS)
```

**配置切换**:
```json
{
  "ImageProcessor": {
    "UseRustImplementation": true  // false 使用 C# 实现
  }
}
```

## 常见陷阱

1. **业务逻辑放 Service**，不放 Controller
2. **返回 ViewModel**，不直接暴露实体
3. **简单 CRUD 用 BaseService**，避免创建不必要的 Service
4. **先定义接口**，再写实现
5. **Service 层禁止直接访问 Db 实例** - 用 Repository 方法或扩展 BaseRepository
6. **避免内存过滤** - 用 `QueryDistinctAsync`/`QuerySumAsync` 等数据库级聚合
7. **敏感数据用环境变量**，不硬编码
8. **每环境设置唯一 Snowflake WorkId**，避免 ID 冲突
9. **Common 依赖只限外部包**，需访问 Model/Service/Repository 用 Extension
10. **路由参数**确保 `ApiModule.LinkUrl` 包含正则
11. **ViewModel 需业务语义化**，不只是加 `Vo` 后缀

## Git 提交规范

**关键规则**:
1. **禁止** Claude Code 署名
2. **禁止** `Co-Authored-By: Claude`
3. **使用**用户配置的 git 身份
4. **遵循** Conventional Commits (`feat:`, `fix:`, `docs:`, `refactor:`)

**正确示例**:
```
feat: 添加用户权限验证中间件

实现了基于角色的权限验证,支持多级权限控制
```

## 文档与参考

**综合文档** (唯一真相源): `radish.docs/docs/`
- `architecture/specifications.md` - 开发规范详细说明
- `architecture/framework.md` - 架构设计与技术决策
- `frontend/design.md` - 前端设计方案
- `guide/` - 配置/认证/日志/网关指南
- `development-plan.md` - 里程碑与周计划
- `changelog/` - 日常/周进度记录
- `deployment/guide.md` - 部署指南

**更新日志规范**:
- **时区**: Asia/Shanghai (UTC+8)
- **内容**: 简洁扼要，只记关键成果
- **格式**: 月/周组织 (`2026-01/week1.md`)

**重大变更必须同步更新文档**
