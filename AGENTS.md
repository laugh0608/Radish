# AGENTS 指南

本文件为 AI 协作者提供 Radish 项目协作指导。详细规范请参考 `CLAUDE.md`。

## 快速认知
- **技术栈**: ASP.NET Core 10 + SQLSugar ORM + PostgreSQL (本地默认 SQLite) / React 19 + Vite (Rolldown) + TypeScript (WebOS 桌面化 UI)
- **前端**: npm workspaces 管理 `radish.client` (WebOS)、`radish.console` (管理后台)、`radish.ui` (共享组件库)
- **协作分支**: `dev` (主开发分支)
- **文档源**: `radish.docs/docs/` 为唯一真相源
- **语言规范**: 所有说明、讨论、文档使用中文，代码/技术标识/引用除外
- **参考文档**: 详细规范参见 `CLAUDE.md`

## AI 协作规则

**禁止直接执行**:
- ❌ 包安装: `dotnet add package`, `npm install`
- ❌ 项目启动: `dotnet run`, `npm run dev` (用于测试)

**正确做法**:
- ✅ 告知用户需要安装的包: "请安装：`dotnet add package Serilog.AspNetCore --version 8.0.0`"
- ✅ 告知启动方式: "请启动 API 测试：`dotnet run --project Radish.Api`"

**可执行操作**:
- ✅ 代码读写、构建 (`dotnet build`, `npm run build`)、测试 (`dotnet test`, `npm run test`)、代码检查、git 操作

## 仓库结构

### 后端项目
- **宿主**: `Radish.Api` (Web API)、`Radish.Gateway` (门户&网关)、`Radish.Auth` (OIDC 认证)
- **业务分层**: `Service/Repository/Core/Model` (业务逻辑/数据/领域/模型)
- **基础设施**: `Common` (仅外部包依赖)、`Extension` (宿主扩展/Autofac/AutoMapper/Redis/Serilog)、`IService/IRepository` (接口契约)、`Shared` (常量/枚举)
- **测试**: `Radish.Api.Tests` (xUnit)
- **Rust扩展**: `radish.lib` (统一原生库，构建后拷贝到 `Radish.Api/bin/`)

**层级依赖**: Common → Shared → Model → Infrastructure → IRepository/Repository → IService/Service → Extension → Api/Gateway/Auth

### 前端项目
- `radish.client`: WebOS 桌面，面向用户
- `radish.console`: 管理后台，面向管理员
- `radish.ui`: 共享 UI 组件库（**无需构建**，直接引用源码）
- `radish.docs`: VitePress 文档站
- **依赖**: client 和 console 依赖 `@radish/ui`，通过 npm workspaces 热更新

## 环境与启动

**基础环境**: .NET SDK 10、Node.js 24+、PostgreSQL 16+ (或 SQLite)

**快速启动**:
```bash
pwsh ./start.ps1  # 或 ./start.sh - 交互式菜单
```

**注意**: 项目可以编译，但在 WSL + Windows 混合环境下启动时需谨慎，可能会因为网络/证书/端口/文件系统等因素导致项目一直启动失败或卡住。

**常用命令**:
```bash
# 后端
dotnet build Radish.slnx -c Debug
dotnet run --project Radish.Api       # http://localhost:5100
dotnet run --project Radish.Gateway   # https://localhost:5000
dotnet run --project Radish.Auth      # http://localhost:5200
dotnet test Radish.Api.Tests

# 前端
npm install                           # 根目录
npm run dev --workspace=radish.client # http://localhost:3000
npm run dev --workspace=radish.console # http://localhost:3100
npm run type-check --workspace=@radish/ui

# 注意：@radish/ui 组件库无需构建
# 前端项目通过 npm workspaces 直接引用源码，支持 HMR 热更新
```

**默认端口**:
- API `http://localhost:5100` (内部)
- Auth `http://localhost:5200` (内部)
- Gateway `https://localhost:5000` (外部唯一入口)
- Frontend `http://localhost:3000`
- Console `http://localhost:3100`
- Docs `http://localhost:4000`
- Scalar `/scalar` (Gateway: `https://localhost:5000/scalar`，API 直连: `http://localhost:5100/scalar`)

## 配置与数据库

**配置加载**: `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json` (Git 忽略) → 环境变量

**关键配置**:
- `Snowflake.WorkId/DatacenterId`: 每环境唯一 (0-30)
- `Databases`: 至少 `ConnId=Main` 和 `ConnId=Log`
- `Redis.Enable`: true (Redis) / false (内存缓存)

**数据库共享**:
- API 和 Auth **共享** `Radish.db` 和 `Radish.Log.db` (业务数据)
- Auth 独享 `Radish.OpenIddict.db` (OIDC 数据，EF Core)
- 所有数据库在 `DataBases/` 目录

**多租户**: 字段级 (`ITenantEntity`) / 表级 (`[MultiTenant(Tables)]`) / 库级 (`[MultiTenant(DataBases)]`)

**配置读取**: `AppSettings.RadishApp("Section", "Key")` 或实现 `IConfigurableOptions`

### 前端配置

**环境变量文件**:
- `.env.development` - 开发环境（提交）
- `.env.production` - 生产环境（提交）
- `.env.local` - 本地覆盖（不提交）
- `.env.local.example` - 配置示例（提交）

**配置规则**:
- 所有变量以 `VITE_` 开头
- 通过 `env.ts` 工具访问，不直接用 `import.meta.env`
- 敏感信息只放 `.env.local`

**示例**:
```typescript
// ✅ 正确
import { env } from '@/config/env';
const apiUrl = env.apiBaseUrl;

// ❌ 错误
const apiUrl = import.meta.env.VITE_API_BASE_URL;
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

5. **ViewModel 设计规范** (重要):
   - **类名规范**: 所有返回给前端的ViewModel类必须添加`Vo`后缀（如UserVo, ProductVo, OrderVo）
   - **字段名规范**: 所有字段必须添加`Vo`前缀
     - **UserVo特殊设计**: `Vo`前缀 + 混淆字段名（如VoLoName, VoUsName, VoUsPwd）- 安全设计，体现自定义映射能力
     - **其他Vo模型**: `Vo`前缀 + 清晰字段名（如VoName, VoDescription, VoCreateTime）- 便于理解和维护
   - **前端适配规则**: 前端必须适配后端的Vo模型字段名，不得要求后端修改
   - **匿名对象禁用**: Controller方法严禁返回匿名对象，必须使用定义好的Vo类
   - **AutoMapper映射策略** (关键):
     - **优先使用前缀识别自动映射** (推荐):
       - ✅ Entity和Vo字段只有`Vo`前缀差异
       - ✅ 字段类型完全一致
       - ✅ 不需要特殊的转换逻辑
       - 示例: `RecognizeDestinationPrefixes("Vo"); CreateMap<Category, CategoryVo>();`
     - **仅在必要时使用手动映射**:
       - ⚠️ 字段名完全不同（如`Id` → `VoUserId`）
       - ⚠️ 需要忽略某些字段（如Service层手动填充的计算属性）
       - ⚠️ 需要特殊的转换逻辑（如计算字段、null默认值）
       - ⚠️ 字段类型不同需要转换
       - 示例: `.ForMember(dest => dest.CategoryName, opt => opt.Ignore())`

6. **Infrastructure** 集中 SqlSugar 多租户逻辑，仅 Repository 和 Extension 引用

7. **接口模式**: 先定义 IService/IRepository，再实现。`BaseRepository<T>` 和 `BaseService<TEntity, TModel>` 提供 CRUD 脚手架

8. **Service 层数据库访问约束** (关键):
   - **严禁**直接使用 `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`
   - **必须**通过 Repository 方法访问数据
   - ❌ 错误: `await _repository.Db.Queryable<Entity>().Where(...).GroupBy(...).ToListAsync()`
   - ✅ 正确: `await _repository.QueryDistinctAsync(e => e.Field, e => e.IsEnabled)`

9. **仓储扩展策略** (优先级):
   - **优先**: 扩展 BaseRepository 泛型方法 (`QueryDistinctAsync`, `QuerySumAsync`) - 跨实体复用
   - **次选**: 创建实体专属仓储 (`UserRepository : BaseRepository<User>`) - 复杂查询/联表/性能优化

## 软删除规范

### 核心原则
- ✅ **推荐**: 业务数据使用软删除，保留完整审计轨迹
- ❌ **避免**: 物理删除业务数据（已标记 `[Obsolete]`）
- ✅ **自动过滤**: 查询方法自动过滤 `IsDeleted = true` 的记录
- ✅ **可恢复**: 支持恢复已软删除的记录

### 实体要求
```csharp
// 实体必须实现 IDeleteFilter 接口
public class UserBalance : RootEntityTKey<long>, IDeleteFilter
{
    // 业务字段...

    // 软删除字段（自动添加）
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

### 使用方法
```csharp
// Service 层软删除
await _userService.SoftDeleteByIdAsync(userId, "Admin");
await _userService.RestoreByIdAsync(userId);

// Repository 层软删除
await _repository.SoftDeleteAsync(u => u.IsEnabled == false, "System");
```

### 自动化特性
- **AddAsync 自动初始化**: 新记录自动设置 `IsDeleted = false`
- **查询自动过滤**: 所有查询方法自动过滤软删除记录
- **审计信息记录**: 自动记录删除时间和操作者

## 编码规范

### 代码质量
**文件行数**: 500-1000 行/文件，硬限 1000 行，超过需重构（提取 Hooks/组件/Service/工具函数）

### 代码风格
- C# 4 空格、文件范围命名空间、nullable
- React TypeScript、`const` 组件、避免 `var`、`useState`+`useMemo`+`useEffect`

## 日志规范（Serilog）

**后端日志**:
```csharp
builder.Host.AddSerilogSetup();  // Program.cs 初始化
Log.Information("User {UserId} logged in", userId);  // 使用
```

- 应用日志: `Logs/{ProjectName}/Log.txt`
- SQL 日志: `Logs/{ProjectName}/AopSql/AopSql.txt` + 数据库
- 审计日志: 数据库 `AuditLog_YYYYMMDD` 表

**前端日志**:
- Client: `radish.client/src/utils/logger.ts`
- Console: `radish.console/src/utils/logger.ts`

**使用规则**:
1. **禁止**直接使用 `console.log/info/warn/error`
2. **必须**使用统一的 `log` 工具
3. 调试日志使用 `log.debug()`（仅 debug 模式输出）
4. 错误日志使用 `log.error()`（总是输出）

**示例**:
```typescript
import { log } from '@/utils/logger';

// ✅ 正确
log.debug('NotificationHub', '连接成功');
log.warn('Token 即将过期');
log.error('API', '请求失败:', error);

// ❌ 错误
console.log('连接成功');
console.error('请求失败:', error);
```

**日志级别**:
- `log.debug()` - 调试信息（仅 debug 模式）
- `log.info()` - 一般信息（仅 debug 模式）
- `log.warn()` - 警告信息（总是输出）
- `log.error()` - 错误信息（总是输出）

## 缓存策略

```csharp
builder.Services.AddCacheSetup();  // 根据 Redis.Enable 切换

// 使用
await cache.SetAsync("key", value, TimeSpan.FromMinutes(10));
var result = await cache.GetAsync<MyType>("key");
```

根据配置自动选择：
- `Redis.Enable = true`: 使用 Redis
- `Redis.Enable = false`: 使用内存缓存

## 前端架构

### UI 组件库 (@radish/ui)
- **位置**: `radish.ui/`
- **内容**: Button, Input, Modal, Icon + Hooks + Utils
- **使用**: `import { Button } from '@radish/ui';`
- **HMR**: 修改自动热更新到 client/console
- **重要**: 无需构建，前端项目直接引用源码，支持实时热更新

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

### API 客户端规范 (重要)

**统一使用 @radish/ui 提供的 API 客户端**，禁止自定义 fetch/axios 封装。

```typescript
import { apiGet, apiPost, configureApiClient } from '@radish/ui';

// 配置（在 API 文件顶部）
configureApiClient({
  baseUrl: import.meta.env.VITE_API_BASE_URL || 'https://localhost:5000',
});

// 使用
export async function getProducts() {
  const response = await apiGet<Product[]>('/api/v1/Shop/GetProducts', { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取失败');
  }
  return response.data;
}
```

**特殊场景**（如上传进度）：使用 XMLHttpRequest 但必须从 `getApiClientConfig()` 获取配置。详见 `CLAUDE.md`。

**禁止事项**：
- ❌ 自定义 `apiFetch` 函数
- ❌ 直接使用 `fetch` 或 `axios`
- ❌ 重复实现认证逻辑

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

## 前端开发规范

### 环境配置

**配置文件**：
- `.env.development` - 开发环境配置（提交到 Git）
- `.env.production` - 生产环境配置（提交到 Git）
- `.env.local` - 本地覆盖配置（不提交，需手动创建）
- `.env.local.example` - 本地配置示例（提交到 Git）

**配置规则**：
1. 所有环境变量必须以 `VITE_` 开头
2. 敏感信息（密码、密钥）只放在 `.env.local`
3. 代码中通过 `env.ts` 工具访问配置，不直接使用 `import.meta.env`

**示例**：
```typescript
// ✅ 推荐：使用 env 工具
import { env } from '@/config/env';
const apiUrl = env.apiBaseUrl;
const isDebug = env.debug;

// ❌ 不推荐：直接使用
const apiUrl = import.meta.env.VITE_API_BASE_URL;
```

## Rust 原生扩展

**位置**: `radish.lib/`

**构建**:
```bash
cd radish.lib
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
11. **ViewModel 设计规范**:
    - **必须使用Vo前缀**: 所有ViewModel类添加Vo前缀，保持命名统一性
    - **禁止匿名对象**: Controller严禁返回匿名对象，必须定义具体的Vo类
    - **UserVo特殊性**: UserVo字段混淆是安全设计，前端必须适配，不得要求后端修改
    - **其他Vo清晰性**: 除UserVo外，其他Vo使用清晰字段名，便于理解和维护
12. **软删除规范** (重要):
    - **优先使用软删除**: 业务数据必须使用 `SoftDeleteByIdAsync` 而非 `DeleteByIdAsync`
    - **实现IDeleteFilter接口**: 新实体必须实现 `IDeleteFilter` 接口支持软删除
    - **避免物理删除**: 物理删除方法已标记 `[Obsolete]`，仅用于系统数据清理
    - **查询自动过滤**: 所有查询自动过滤 `IsDeleted = true` 的记录
    - **审计信息完整**: 软删除时记录 `DeletedAt` 和 `DeletedBy` 信息
    - **支持恢复**: 使用 `RestoreByIdAsync` 恢复软删除的记录

## Git 提交规范

**关键规则**:
1. **严格禁止** AI 协作者署名（如 `Co-Authored-By: Claude`）
2. **严格禁止** Claude Code 署名
3. **必须使用**用户配置的 git 身份
4. **必须遵循** Conventional Commits 规范

**提交格式**:
```
<type>(<scope>): <subject>

<body>
```

**Type 类型**:
- `feat`: 新功能
- `fix`: Bug 修复
- `docs`: 文档更新
- `refactor`: 代码重构
- `test`: 测试相关
- `chore`: 构建/工具相关

**正确示例**:
```
feat(ui): 添加 Ant Design 主题配置

- 创建主题配置文件
- 支持亮色/暗色主题
- 集成到 Console 项目
```

**错误示例**（禁止）:
```
feat(ui): 添加主题配置

Co-Authored-By: Claude Sonnet 4.5 <noreply@anthropic.com>  ❌ 禁止
```

**正确示例**:
```
feat: 添加用户权限验证中间件

实现了基于角色的权限验证,支持多级权限控制
```

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
