# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication Guidelines

**Language Preference**: When working in this repository, respond primarily in **Chinese (中文)** for all explanations, discussions, and documentation. Use English or other languages only when:
- Writing code, comments, or technical identifiers
- Referencing specific technical terms that are commonly used in English
- Quoting from English documentation or error messages
- The user explicitly requests a different language

## Code Quality Standards

### File Size Guidelines

**Single Source File Line Count**: To maintain code readability and maintainability, follow these guidelines for source file sizes:

- **Recommended Range**: 500-1000 lines per file
- **Hard Limit**: Avoid exceeding 1000 lines unless absolutely necessary
- **When to Refactor**: If a file approaches or exceeds 1000 lines, consider refactoring using these strategies:
  1. **Extract Custom Hooks** (React): Move state management and side effects into separate hooks
     - Example: `useForumData.ts` for data fetching, `useForumActions.ts` for event handlers
  2. **Create View Components**: Split large components into smaller, focused view components
     - Example: `PostListView.tsx`, `PostDetailView.tsx` for different UI states
  3. **Separate Business Logic**: Extract complex logic into utility functions or service modules
  4. **Split by Feature**: Organize related functionality into feature-specific modules

**Example Refactoring**:
```
Before: ForumApp.tsx (854 lines)
After:
  - ForumApp.tsx (179 lines) - Main component orchestration
  - hooks/useForumData.ts (307 lines) - Data management
  - hooks/useForumActions.ts (443 lines) - Event handlers
  - views/PostListView.tsx (162 lines) - List view
  - views/PostDetailContentView.tsx (108 lines) - Detail view
```

**Benefits**:
- Improved code readability and navigation
- Easier testing and debugging
- Better code reusability
- Reduced cognitive load for developers
- Simplified code reviews

## Package Management & Testing Guidelines

**CRITICAL: Package Installation and Project Startup Rules**

When working with this repository, follow these strict rules for package management and testing:

1. **Package Installation - DO NOT Execute Directly**:
   - **NEVER** run `dotnet add package`, `npm install`, or any package installation commands directly
   - **INSTEAD**: Tell the user which packages need to be installed and let them execute the commands
   - Example: "请安装以下 NuGet 包：`dotnet add package Newtonsoft.Json --version 13.0.3`"
   - Example: "请在根目录运行：`npm install axios --workspace=radish.client`"

2. **Project Startup for Testing - DO NOT Execute Directly**:
   - **NEVER** run `dotnet run`, `dotnet watch`, `npm run dev`, or any project startup commands for testing purposes
   - **INSTEAD**: Tell the user which project needs to be started and how to start it
   - Example: "请启动 API 项目进行测试：`dotnet run --project Radish.Api/Radish.Api.csproj`"
   - Example: "请启动前端开发服务器：`npm run dev --workspace=radish.client`"
   - Example: "请使用 start 脚本启动所有服务：`pwsh ./start.ps1` 并选择选项 11 (ALL)"

3. **What AI CAN Do**:
   - Read and analyze code
   - Write new code or modify existing code
   - Run build commands (`dotnet build`, `npm run build`)
   - Run tests (`dotnet test`, `npm run test`)
   - Run linting and type checking (`npm run lint`, `npm run type-check`)
   - Execute git commands (status, diff, commit, etc.)
   - Use development tools (grep, find, etc.)

4. **Rationale**:
   - Package installation may require network access, authentication, or specific environment setup
   - Project startup for testing requires monitoring output, handling interactive prompts, and may run indefinitely
   - The user has better control over their development environment and can troubleshoot issues more effectively

**Example Workflow**:
```
❌ BAD:
AI: "我现在安装 Serilog 包..."
AI: [Executes] dotnet add package Serilog
AI: "我现在启动 API 进行测试..."
AI: [Executes] dotnet run --project Radish.Api

✅ GOOD:
AI: "我已经添加了日志配置代码。请按以下步骤操作："
AI: "1. 安装依赖包：`dotnet add package Serilog.AspNetCore --version 8.0.0`"
AI: "2. 启动 API 项目测试日志功能：`dotnet run --project Radish.Api/Radish.Api.csproj`"
AI: "3. 访问 http://localhost:5100/api/v2/Test/Log 查看日志输出"
```

## Project Overview

Radish is a modern community platform built with a self-designed layered architecture:
- **Backend**: ASP.NET Core 10 + SQLSugar ORM + PostgreSQL (SQLite for local dev)
- **Gateway**: Radish.Gateway - Service portal and API gateway (Phase 0: portal page; P1+: routing, auth, aggregation)
- **Auth**: Radish.Auth - OIDC authentication server based on OpenIddict
- **Frontend**: React 19 + Vite (using Rolldown bundler) + TypeScript with a desktop-like UI paradigm (WebOS)
- **UI Library**: @radish/ui - Shared component library using npm workspaces
- **Console**: radish.console - Management console frontend
- **Docs**: radish.docs - VitePress documentation site
- **Solution**: Radish.slnx contains all backend projects and can be developed cohesively

## Essential Commands

### Backend Development
```bash
# Build the solution
dotnet build Radish.slnx -c Debug

# Run the API (listens on http://localhost:5100 only in local dev)
dotnet run --project Radish.Api/Radish.Api.csproj

# Run the Gateway (listens on https://localhost:5000 and http://localhost:5001)
dotnet run --project Radish.Gateway/Radish.Gateway.csproj

# Run the Auth Server (listens on http://localhost:5200)
dotnet run --project Radish.Auth/Radish.Auth.csproj

# Hot reload during development
dotnet watch --project Radish.Api          # For API
dotnet watch --project Radish.Gateway      # For Gateway
dotnet watch --project Radish.Auth         # For Auth

# Run backend tests
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj
```

### Frontend Development
```bash
# Install dependencies (run from repo root)
npm install

# Start dev servers
npm run dev --workspace=radish.client    # Frontend (http://localhost:3000)
npm run dev --workspace=radish.console   # Console (http://localhost:3200)

# Build for production
npm run build --prefix radish.client

# Lint
npm run lint --prefix radish.client
```

### UI Component Library Development
```bash
# Type check
npm run type-check --workspace=@radish/ui

# Lint
npm run lint --workspace=@radish/ui

# The UI library uses npm workspaces with symlinks
# Changes to radish.ui/ are automatically reflected in client/console via HMR
```

### Quick Start Scripts
```bash
# Interactive menu to start API, Gateway, Auth, frontend, docs, console or run tests
# Current options include:
# - Single services: API / Gateway / frontend / docs / console / Auth / DbMigrate / tests (1-8)
# - Combinations: Gateway+Auth+API, Frontend+Console+Docs (9-10)
# - Start ALL: All services (11)
pwsh ./start.ps1    # Windows/PowerShell
./start.sh          # Linux/macOS
```

### Running Single Tests
```bash
# List all tests
dotnet test --list-tests

# Run specific test by filter
dotnet test --filter "FullyQualifiedName~UserControllerTest"
```

## Layered Architecture

### Dependency Flow (Bottom → Top)
```
Radish.Common (utilities, logging, base config - no internal dependencies)
    ↓
Radish.Shared (constants, enums, shared types)
    ↓
Radish.Model (entities, DTOs, view models)
    ↓
Radish.Infrastructure (SqlSugar extensions, tenant routing, connection resolver)
    ↓
Radish.IRepository + Radish.Repository (data access contracts + implementations)
    ↓
Radish.Core (domain logic, algorithms - reserved for future simulation/algo modules)
    ↓
Radish.IService + Radish.Service (business logic contracts + implementations)
    ↓
Radish.Extension (Swagger/Scalar, health checks, Autofac/AutoMapper setup, JWT middleware)
    ↓
Radish.Api (ASP.NET Core host, controllers, DI container, configuration)

Radish.Gateway (service portal & API gateway)
    - Phase 0: Depends on Radish.Common + Radish.Extension (portal pages, health checks)
    - P1+: May also reference Radish.Service (for aggregation & unified auth)
```

### Key Architectural Rules

1. **Radish.Common** can only reference external NuGet packages. If a utility needs to access Model/Service/Repository types, put it in **Radish.Extension** instead.

2. **Repository Layer** returns entities only. Service layer MUST map entities to ViewModels/DTOs before exposing to Controllers.

3. **Controllers** must NEVER inject `IBaseRepository` directly. All data access goes through Service layer.

4. **Entity vs ViewModel**:
   - Entities: Located in `Radish.Model/Models`, inherit from `RootEntityTKey<TKey>`, only manipulated in Repository layer
   - ViewModels: Located in `Radish.Model/ViewModels`, suffix with `Vo` (e.g., `AuditSqlLogVo`), exposed to Controllers
   - AutoMapper profiles in `Radish.Extension/AutoMapperExtension` handle mappings

5. **Radish.Infrastructure** centralizes SqlSugar multi-tenant logic, table/database routing, and connection resolution. This layer is referenced by Repository and Extension but never by Service or higher layers directly.

6. **Interface Pattern**: Always define interfaces (IService/IRepository) before implementations. `BaseRepository<T>` and `BaseService<TEntity, TModel>` provide CRUD scaffolding.

## Configuration Management

### Configuration Sources & Priority

Configuration files are loaded in the following order (later sources override earlier ones):

```
1. appsettings.json                      (base config with default values, checked into git)
   ↓
2. appsettings.{Environment}.json        (Development/Production, checked into git)
   ↓
3. appsettings.Local.json                (local overrides, NOT checked into git, highest priority)
   ↓
4. Environment variables                 (production deployments)
```

**IMPORTANT**:
- `appsettings.Local.json` is used for local development and contains sensitive data (database passwords, API keys, etc.). This file is ignored by Git.
- Configuration uses **deep merge** strategy: later configs override earlier ones by key path.
- For arrays (like `Databases`), provide the complete array in Local.json to avoid partial overrides.
- See [配置指南](radish.docs/docs/guide/configuration.md) for detailed merge behavior examples.

### Quick Setup for New Developers

```bash
# Simplest way: Just run the project (works out of the box with SQLite + memory cache)
dotnet run --project Radish.Api
dotnet run --project Radish.Auth
dotnet run --project Radish.Gateway

# Optional: If you need to customize configuration (PostgreSQL, Redis, etc.)
# Note: Gateway does not need Local.json - it has no sensitive data
# 1. Copy the minimal example file for API and Auth only
cp Radish.Api/appsettings.Local.json.example Radish.Api/appsettings.Local.json
cp Radish.Auth/appsettings.Local.json.example Radish.Auth/appsettings.Local.json

# 2. Edit appsettings.Local.json - uncomment and modify only what you need:
#    - Database passwords (if using PostgreSQL)
#    - Redis passwords (if enabling Redis)
#    - OpenIddict keys (Auth service, production only)
#    - Snowflake.WorkId (if different from default)
#
#    All other settings will inherit from appsettings.json automatically!

# 3. Start the project
dotnet run --project Radish.Api
```

**Important Notes**:
- **Radish.Api & Radish.Auth**: Use `appsettings.Local.json` for sensitive data (database passwords, Redis passwords, API keys)
- **Radish.Gateway**: No Local.json needed - no sensitive data. Use environment variables in production to override PublicUrl and service addresses
- Non-sensitive settings should stay in `appsettings.json` (CORS origins, log levels, default ports, etc.)
- Thanks to deep merge, you only need to specify the values you want to change
- See `appsettings.Local.json.example` for minimal templates with common overrides (API & Auth only)
- For Gateway production deployment, see `Radish.Gateway/README.md`

For detailed configuration instructions, see [配置指南](radish.docs/docs/guide/configuration.md).

### Reading Configuration
```csharp
// Simple key-value access (preferred)
var value = AppSettings.RadishApp("Section", "Key");

// Strongly-typed options (implement IConfigurableOptions in Radish.Common.Option)
// Automatically bound via builder.Services.AddAllOptionRegister()
public class MyOptions : IConfigurableOptions { ... }
// Then inject IOptions<MyOptions> or IOptionsSnapshot<MyOptions>
```

### Important Config Sections
- `Snowflake.WorkId` & `Snowflake.DatacenterId`: MUST be unique per deployment instance (0-30)
- `Databases`: Array with at least `ConnId=Main` and `ConnId=Log` (Log name is fixed)
- `Redis.Enable`: Toggle between Redis (`true`) or in-memory cache (`false`)
- `Cors.AllowedOrigins`: Frontend origins for CORS policy

### Security Best Practices
- **NEVER commit sensitive data** to `appsettings.json` (use placeholders or empty strings)
- **ALWAYS use `appsettings.Local.json`** for local development secrets
- **Use environment variables** for production deployments (Docker, Kubernetes)
- **Check `.gitignore`** to ensure `appsettings.Local.json` is excluded

## Database & SqlSugar

### Setup in Program.cs
```csharp
builder.Services.AddSqlSugarSetup();  // Registers SqlSugarScope singleton

// Configure Snowflake IDs
var snowflakeSection = builder.Configuration.GetSection("Snowflake");
SnowFlakeSingle.WorkId = snowflakeSection.GetValue<int>("WorkId");
SnowFlakeSingle.DatacenterId = snowflakeSection.GetValue<int>("DataCenterId");
```

### Multi-Tenant Isolation
1. **Field-level** (single table): Entity implements `ITenantEntity`, filtered by `TenantId` via QueryFilter
2. **Table-level**: Entity annotated with `[MultiTenant(TenantTypeEnum.Tables)]`, table name becomes `TableName_{TenantId}`
3. **Database-level**: Entity annotated with `[MultiTenant(TenantTypeEnum.DataBases)]`, connection dynamically resolved per tenant

### Connection Routing
- Entities without `[Tenant]` attribute use `MainDb`
- Entities with `[Tenant(configId: "Log")]` use the Log database
- Log entities like `AuditSqlLog` use `[Tenant(configId: "log")]` + `[SplitTable(SplitType.Month)]` for monthly partitioning

### Local Dev (SQLite)
Default setup uses `Radish.db` (main) and `Radish.Log.db` (logs) in `DataBases/` folder. Auto-created on first run. For PostgreSQL, update `Databases[].ConnectionString` and `DbType=4`.

**IMPORTANT - Database Sharing Between Projects**:
- **API and Auth projects share the same business databases**: Both `Radish.Api` and `Radish.Auth` use `Radish.db` (main) and `Radish.Log.db` (logs) for business data (users, roles, permissions, tenants, etc.)
- **OpenIddict uses a separate database**: `Radish.OpenIddict.db` is managed by EF Core and stores OIDC-specific data (clients, authorizations, tokens, scopes)
- **Why share business databases?**: Auth and API need access to the same user/role/permission data for authentication and authorization
- **Database location**: All database files are stored in the solution root's `DataBases/` folder

## Authentication & Authorization

### JWT Setup (Program.cs)
- Issuer: "Radish"
- Audience: "luobo"
- Key: Hardcoded in `appsettings.json` (externalize for production)
- Token validation on every authenticated request

### Authorization Policies
- `Client`: Requires claim `iss=Radish`
- `System`: Requires role `System`
- `SystemOrAdmin`: Requires role `System` OR `Admin`
- `RadishAuthPolicy`: Custom policy using `PermissionRequirement` (Role-API authorization via `ApiModule.LinkUrl` regex matching)

### Important for URL Routes
API routes must start with `/` for permission matching. If using path parameters like `[HttpGet("{id:long}")]`, ensure `ApiModule.LinkUrl` contains regex version (e.g., `/api/User/GetById/\d+`).

## Logging with Serilog

Radish 使用 Serilog 结构化日志和数据库持久化，提供完整的应用监控和审计能力。

### Quick Start

**初始化**（在 `Program.cs` 中）：
```csharp
builder.Host.AddSerilogSetup();
```

**使用**（推荐静态方法）：
```csharp
Log.Information("User {UserId} logged in", userId);
Log.Warning("Cache miss for key {Key}", cacheKey);
Log.Error(ex, "Failed to process order {OrderId}", orderId);
```

### 日志类型

| 类型 | 存储位置 | 用途 |
|-----|---------|------|
| **应用日志** | `Log/{ProjectName}/Log.txt` | 应用运行状态、错误、警告 |
| **SQL 日志** | `Log/{ProjectName}/AopSql/AopSql.txt` + 数据库 | SQL 执行和性能 |
| **审计日志** | 数据库 `AuditLog_YYYYMMDD` 表 | 敏感操作记录 |

**详细文档**: 参见 [日志系统文档](radish.docs/docs/guide/logging.md) 了解完整配置、最佳实践和故障排查。

## Caching Strategy

### Setup (Radish.Extension.RedisExtension)
`builder.Services.AddCacheSetup()` switches between Redis and in-memory based on `Redis.Enable`.

### Usage
```csharp
// Inject ICaching
public MyService(ICaching cache) { ... }

// Set/Get
await cache.SetAsync("key", value, TimeSpan.FromMinutes(10));
var result = await cache.GetAsync<MyType>("key");

// Redis-specific: Inject IRedisBasketRepository for List/Queue/Pub-Sub operations
```

### Cache Key Management
Register keys via `ICaching.AddCacheKey*` for batch operations like `DelByPattern` or `Clear`.

## AOP & Service Interception

`Radish.Extension/ServiceAop` uses Castle.DynamicProxy to intercept service methods. Automatically enabled for `BaseService<,>` implementations via Autofac.

AOP captures:
- Input parameters
- Output response
- Execution time
- Exceptions

Logged via `AopLogInfo` structure. To extend interception, add interceptors in `AutofacModuleRegister`.

## Testing

### Backend Tests (xUnit + Shouldly)
Located in `Radish.Api.Tests/`. Example: `LoginControllerTest` validates controller responses.

**Important**: When modifying example endpoints (e.g., `UserController`), update corresponding tests in `Radish.Api.Tests/Controllers/`.

### Frontend Tests (Vitest + Testing Library)
Not yet configured extensively. Plan to add unit/integration tests in `radish.client/src/`.

### Test Isolation
Tests use in-memory services where possible. For integration tests requiring database, consider SQLite in-memory mode or dedicated test DB.

## Frontend Architecture

### UI Component Library (@radish/ui)

**Location**: `radish.ui/`

**Purpose**: Shared component library for radish.client and radish.console

**Key Features**:
- **npm Workspaces**: Uses symlinks for instant hot-reload across projects
- **Components**: Button, Input, Select, Modal, Icon, ContextMenu
- **Hooks**: useDebounce, useLocalStorage, useToggle, useClickOutside
- **Utils**: Date formatting, validation, string manipulation
- **TypeScript**: Complete type definitions for all exports

**Usage**:
```typescript
// Import components
import { Button, Input, Modal, Icon } from '@radish/ui';

// Import hooks
import { useDebounce, useToggle } from '@radish/ui/hooks';

// Import utils
import { formatDate, isEmail } from '@radish/ui/utils';
```

**Development**:
- Changes to `radish.ui/` automatically reflect in client/console via Vite HMR
- No need to restart dev servers or reinstall dependencies
- Run `npm run type-check --workspace=@radish/ui` before committing

**Documentation**: See [UI 组件库](radish.docs/docs/frontend/ui-library.md)

### WebOS Desktop UI (radish.client)

**Desktop-like UI Paradigm**:
- **Top**: Status bar (username, IP, system status)
- **Bottom**: Dock (core feature shortcuts)
- **Left**: Desktop icons (double-click opens modal windows)
- **Windows**: macOS-style minimize/close buttons, draggable, taskbar when minimized
- Reference: `radish.client/public/webos.html` (Nebula OS prototype)

**Component Organization**:
- **Shared components** → `@radish/ui` (Icon, Button, ContextMenu, etc.)
- **WebOS-specific** → `radish.client/src/` (GlassPanel, AppIcon, DesktopWindow, etc.)

**Application Architecture Decision**:

Client 项目采用**混合架构**,根据应用复杂度和独立性选择不同集成方式:

1. **内置应用 (Built-in Apps)** - 使用 `type: 'window'`
   - 简单功能模块,无需独立部署
   - 可直接复用 Client 的认证状态和共享组件
   - 示例:论坛(Forum)、聊天室(Chat)、设置(Settings)
   - 实现:React 组件,直接在 WebOS 窗口中渲染

2. **嵌入应用 (Embedded Apps)** - 使用 `type: 'iframe'`
   - 无需认证或简单认证的展示型应用
   - 无复杂路由需求,用户主要被动浏览
   - 示例:文档站(Docs)、帮助中心
   - 实现:通过 iframe 嵌入,保持独立性但在 WebOS 窗口内显示

3. **外部应用 (External Apps)** - 使用 `type: 'external'`
   - 完整的独立 SPA,有自己的 OIDC 认证流程
   - 复杂的路由系统,需要控制浏览器地址栏
   - 需要独立访问和部署
   - 示例:管理控制台(Console)、商城(Shop - 未来)
   - 实现:在新标签页打开,完全独立运行

**为什么不合并 Console 到 Client?**
- **关注点分离**:Client 面向普通用户,Console 面向管理员
- **权限隔离**:管理功能不应与用户功能混在同一代码库
- **部署灵活性**:Console 可部署到内网,Client 部署到公网
- **代码体积**:避免普通用户加载管理功能代码
- **开发独立性**:两个团队可并行开发,互不影响
- **技术选型自由**:各自可选择适合的 UI 库和技术栈

详见 [前端设计文档](radish.docs/docs/frontend/design.md) 第 10.4 节的架构决策分析。

**React Conventions**:
1. **Function components only** (no class components)
2. Use `const` for component definitions, avoid `function` declarations
3. Avoid `var`, default to `const`, use `let` only when reassignment needed
4. State management: `useState` + `useMemo` + `useEffect`
5. React Compiler: Experimental, not enabled in main branch yet

**Frontend-Backend Communication**:
- All requests over HTTPS (TLS provides transport encryption)
- **Password Security**: Passwords are transmitted as plaintext over HTTPS and hashed with Argon2id on the server. See [密码安全](radish.docs/docs/guide/password-security.md) for details.
- **No frontend encryption**: Frontend code is fully exposed to users, so client-side encryption (like RSA) provides no real security benefit
- VITE_API_BASE_URL env var points to backend
- CORS configured in `appsettings.json` under `Cors.AllowedOrigins`

## Adding New Features (Complete Flow)

### Backend Features

1. **Define Model** in `Radish.Model/Models` (entity) and `Radish.Model/ViewModels` (ViewModel)
2. **Create Mapping** in `Radish.Extension/AutoMapperExtension/CustomProfiles`
3. **Repository Layer** (for custom queries only):
   - Most cases: Use `IBaseRepository<TEntity>` directly, NO need to create custom repository
   - Only create custom repository if you need specialized database operations beyond BaseRepository
4. **Service Layer** (IMPORTANT - avoid over-engineering):
   - **For simple CRUD operations**: Use `IBaseService<TEntity, TVo>` directly in Controller
     - Example: `IBaseService<Category, CategoryVo>` provides all basic operations
     - BaseService includes: Query, QueryById, QueryPage, Add, Update, Delete, etc.
   - **Only create custom Service when**:
     - You have complex business logic (e.g., `PublishPostAsync` that updates multiple tables)
     - You need transaction coordination across multiple entities
     - You have special validation or processing rules
   - **If you create custom Service**:
     - Define interface in `Radish.IService` (inherit from `IBaseService<TEntity, TVo>`)
     - Implement in `Radish.Service` (inherit from `BaseService<TEntity, TVo>`)
     - Only add methods for complex logic; reuse base methods for simple operations
5. **Controller** in `Radish.Api/Controllers`:
   - **Simple CRUD**: Inject `IBaseService<TEntity, TVo>` directly
     ```csharp
     public CategoryController(IBaseService<Category, CategoryVo> categoryService)
     ```
   - **Complex logic**: Inject custom service (e.g., `IPostService`)
   - Never inject `IBaseRepository` directly
   - Add `[Authorize]` or `[AllowAnonymous]` attribute
   - Use `[Route("api/v{version:apiVersion}/[controller]/[action]")]` convention
6. **Add HTTP Examples** in `Radish.Api/Radish.Api.http` for manual testing
7. **Write Tests** in `Radish.Api.Tests/Controllers/`
8. **Update Frontend** in `radish.client/src/` as needed

### Frontend Features

#### Adding UI Components

**For general-purpose components** (usable in both client and console):
1. Create component in `radish.ui/src/components/ComponentName/`
2. Export from `radish.ui/src/components/index.ts`
3. Use in client/console: `import { ComponentName } from '@radish/ui'`
4. Changes auto-reload via HMR

**For WebOS-specific components** (desktop, windows, etc.):
1. Create in `radish.client/src/` (widgets/, desktop/, or shared/ui/desktop/)
2. Keep WebOS-specific logic isolated from general components

#### Component Guidelines

- **General components** → @radish/ui (Button, Input, Modal, Icon, etc.)
- **WebOS components** → radish.client (GlassPanel, AppIcon, DesktopWindow, etc.)
- Use `.radish-` prefix for CSS classes in @radish/ui
- Complete TypeScript types for all props
- JSDoc comments for public APIs

### Service Layer Examples

**✅ Good - Use BaseService directly:**
```csharp
// Controller
public class CategoryController : ControllerBase
{
    private readonly IBaseService<Category, CategoryVo> _categoryService;

    [HttpGet]
    public async Task<MessageModel> GetAll()
    {
        var categories = await _categoryService.QueryAsync(c => c.IsEnabled && !c.IsDeleted);
        return new MessageModel { IsSuccess = true, ResponseData = categories };
    }
}
```

**✅ Good - Create custom Service for complex logic:**
```csharp
// Service interface
public interface IPostService : IBaseService<Post, PostVo>
{
    Task<long> PublishPostAsync(Post post, List<string>? tagNames = null);
    Task<PostVo?> GetPostDetailAsync(long postId);
}

// Service implementation
public class PostService : BaseService<Post, PostVo>, IPostService
{
    public async Task<long> PublishPostAsync(Post post, List<string>? tagNames)
    {
        // Complex logic: insert post, update category count, process tags
        var postId = await AddAsync(post); // Reuse base method
        // ... additional business logic
        return postId;
    }
}
```

**❌ Bad - Creating unnecessary custom Service:**
```csharp
// DON'T do this - CategoryService only wraps BaseService methods
public interface ICategoryService : IBaseService<Category, CategoryVo>
{
    Task<List<CategoryVo>> GetTopCategoriesAsync();  // Just QueryAsync with filter!
}
```

## Rust Native Extensions

Located in `Radish.Core/radish-lib/` - a unified Rust extension library for high-performance operations.

### Building Rust Extensions
```bash
cd Radish.Core/radish-lib
cargo build --release
# Output: target/release/radish_lib.dll (Windows) or libradish_lib.so (Linux) or libradish_lib.dylib (macOS)
```

Or use the provided build scripts:
- Windows: `./build.ps1`
- Linux/macOS: `./build.sh`

The build scripts automatically copy the compiled library to `Radish.Api/bin/Debug/net10.0/`.

### Usage Example
`RustTestController` demonstrates `[DllImport("radish_lib")]` for performance-critical algorithms. See endpoints like `/api/v2/RustTest/TestSum1` for benchmarks.

### Modules
- **image**: Image processing (watermarking)
- **hash**: File hashing (SHA256)
- **benchmark**: Performance testing (migrated from test_lib)
- **utils**: Utility functions

### Configuration
Switch between C# and Rust implementations via `appsettings.json`:
```json
{
  "ImageProcessor": {
    "UseRustImplementation": true  // false to use C# implementation
  }
}
```

The system automatically falls back to C# implementation if Rust library is not available.

## Documentation

Comprehensive docs in `radish.docs/docs/`:
- `docs/architecture/specifications.md` - Directory structure, layering, dependency rules
- `docs/architecture/framework.md` - Overall architecture, tech decisions, milestones
- `docs/development-plan.md` - Weekly delivery plan
- `docs/changelog/` - Daily/weekly progress logs
- `docs/guide/authentication.md` - Auth flow details
- `docs/frontend/design.md` - UI paradigm, component planning, cross-platform strategy
- `docs/guide/gateway.md` - Gateway service architecture and deployment
- `docs/deployment/guide.md` - Containerization and deployment

**Always consult these docs before making architectural changes.**

### Changelog Guidelines

When updating development logs in `docs/changelog/`:
- **Timezone**: Use **Asia/Shanghai (UTC+8)** for all timestamps
- **Content**: Keep it **concise and focused** - only highlight key accomplishments, avoid excessive detail
- **Format**: Organize by month and week (e.g., `2026-01/week1.md`), with monthly summary files (e.g., `2026-01.md`)
- **Focus on**: Core features, technical highlights, important changes - skip implementation details

## Common Pitfalls

1. **Don't put business logic in Controllers** - Logic belongs in Service layer
2. **Don't expose entities directly** - Always map to ViewModels first
3. **Don't create unnecessary Services** - Use `BaseService<TEntity, TVo>` directly for simple CRUD; only create custom Service for complex business logic
4. **Don't skip interface definitions** - Define IService/IRepository before implementations
5. **Don't hardcode secrets** - Use environment variables or User Secrets for sensitive data
6. **Don't forget to update Snowflake WorkId per environment** - Avoid ID collisions in production
7. **Don't create files in Radish.Common that depend on Model/Service/Repository** - Use Radish.Extension instead
8. **Router parameters in authorization** - Ensure `ApiModule.LinkUrl` contains regex for routes with parameters
9. **ViewModels must be obfuscated** - Don't just add `Vo` prefix; combine with business abbreviation

## Git Commit Guidelines

**CRITICAL: When performing git commits, follow these strict rules:**

1. **Never include Claude Code attribution** - Do NOT add "Generated with Claude Code" or similar phrases in commit messages
2. **Never use Claude co-authorship** - Do NOT add `Co-Authored-By: Claude <noreply@anthropic.com>` or similar tags
3. **Use user's own identity** - All commits MUST use the user's configured git name and email only
4. **Keep commit messages clean** - Write concise, professional commit messages that follow the repository's existing commit style (as seen in `git log`)
5. **Follow conventional commits** - Use prefixes like `feat:`, `fix:`, `docs:`, `refactor:`, etc. as appropriate

**Example of CORRECT commit message:**
```
feat: 添加用户权限验证中间件

实现了基于角色的权限验证,支持多级权限控制
```

**Example of INCORRECT commit message (DO NOT USE):**
```
feat: 添加用户权限验证中间件

实现了基于角色的权限验证,支持多级权限控制

🤖 Generated with Claude Code
Co-Authored-By: Claude <noreply@anthropic.com>
```

## Development Workflow Tips

- Use `dotnet watch` for backend hot reload
- Use Vite's HMR for instant frontend updates
- Scalar API docs available at `/scalar` when running locally (API direct: `http://localhost:5100/scalar`, via Gateway: `https://localhost:5000/scalar`)
- Example requests in `Radish.Api/Radish.Api.http` (use REST Client extension)
- Check `radish.docs/docs/changelog/` for recent changes and known issues
- When adding new repositories/services, register them in Autofac module or use the generic pattern
