# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication Guidelines

**Language Preference**: When working in this repository, respond primarily in **Chinese (中文)** for all explanations, discussions, and documentation. Use English or other languages only when:
- Writing code, comments, or technical identifiers
- Referencing specific technical terms that are commonly used in English
- Quoting from English documentation or error messages
- The user explicitly requests a different language

## Project Overview

Radish is a modern community platform built with a self-designed layered architecture:
- **Backend**: ASP.NET Core 10 + SQLSugar ORM + PostgreSQL (SQLite for local dev)
- **Gateway**: Radish.Gateway - Service portal and API gateway (Phase 0: portal page; P1+: routing, auth, aggregation)
- **Frontend**: React 19 + Vite (using Rolldown bundler) + TypeScript with a desktop-like UI paradigm
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
npm install --prefix radish.client

# Start dev server (default: http://localhost:3000)
npm run dev --prefix radish.client

# Build for production
npm run build --prefix radish.client

# Lint
npm run lint --prefix radish.client
```

### Quick Start Scripts
```bash
# Interactive menu to start API, Gateway, Auth, frontend, docs, console or run tests
# Current options include:
# - Single services: API / Gateway / frontend / docs / console / Auth / tests (1-8)
# - Combinations (PowerShell): Gateway+Auth+API, or start ALL (1 0)
# - Combinations (Shell): Gateway+API, Gateway+frontend, Gateway+docs, Gateway+console,
#   Gateway+Auth, Gateway+Auth+API, start ALL (9-15)
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
- See [ConfigurationGuide.md](radish.docs/docs/ConfigurationGuide.md) for detailed merge behavior examples.

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

For detailed configuration instructions, see [ConfigurationGuide.md](radish.docs/docs/ConfigurationGuide.md).

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
Default setup uses `Radish.db` (main) and `RadishLog.db` (logs) in `DataBases/` folder. Auto-created on first run. For PostgreSQL, update `Databases[].ConnectionString` and `DbType=4`.

**IMPORTANT - Database Sharing Between Projects**:
- **API and Auth projects share the same business databases**: Both `Radish.Api` and `Radish.Auth` use `Radish.db` (main) and `RadishLog.db` (logs) for business data (users, roles, permissions, tenants, etc.)
- **OpenIddict uses a separate database**: `RadishAuth.OpenIddict.db` is managed by EF Core and stores OIDC-specific data (clients, authorizations, tokens, scopes)
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

### Initialization
`Program.cs` calls `builder.Host.AddSerilogSetup()` to configure Serilog. Logs are written to the solution root's `Log/` folder with the following structure:
- `Log/{ProjectName}/Log.txt` - General application logs
- `Log/{ProjectName}/AopSql/AopSql.txt` - SQL logs via AOP
- `Log/{ProjectName}/SerilogDebug/Serilog{date}.txt` - Serilog internal debug logs

Where `{ProjectName}` is automatically detected from the running project (e.g., Radish.Api, Radish.Gateway, Radish.Auth).

### Log Location Auto-Detection
The logging system automatically:
1. Finds the solution root by searching upward for `*.slnx` or `*.sln` files
2. Identifies the current project name from the `.csproj` file
3. Creates project-specific subdirectories under the solution's `Log/` folder

This ensures all projects log to a centralized location while maintaining clear separation.

### Usage
Prefer static Serilog methods over injecting `ILogger<T>`:
```csharp
Log.Information("User {UserId} logged in", userId);
Log.Warning("Cache miss for key {Key}", cacheKey);
Log.Error(ex, "Failed to process order {OrderId}", orderId);
```

### SQL Logging
`SqlSugarAop.OnLogExecuting` captures all SQL executions. `LogContextTool` tags them with `LogSource=AopSql`. Use `WriteTo.Async()` to avoid blocking requests.

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

## Frontend Architecture (radish.client)

### Desktop-like UI Paradigm
- **Top**: Status bar (username, IP, system status)
- **Bottom**: Dock (core feature shortcuts)
- **Left**: Desktop icons (double-click opens modal windows)
- **Windows**: macOS-style minimize/close buttons, draggable, taskbar when minimized
- Reference: `radish.client/public/webos.html` (Nebula OS prototype)

### React Conventions
1. **Function components only** (no class components)
2. Use `const` for component definitions, avoid `function` declarations
3. Avoid `var`, default to `const`, use `let` only when reassignment needed
4. State management: `useState` + `useMemo` + `useEffect`
5. React Compiler: Experimental, not enabled in main branch yet

### Frontend-Backend Communication
- All requests over HTTPS (TLS provides transport encryption)
- **Password Security**: Passwords are transmitted as plaintext over HTTPS and hashed with Argon2id on the server. See [PasswordSecurity.md](radish.docs/docs/PasswordSecurity.md) for details.
- **No frontend encryption**: Frontend code is fully exposed to users, so client-side encryption (like RSA) provides no real security benefit
- VITE_API_BASE_URL env var points to backend
- CORS configured in `appsettings.json` under `Cors.AllowedOrigins`

## Adding New Features (Complete Flow)

1. **Define Model** in `Radish.Model/Models` (entity) and `Radish.Model/ViewModels` (ViewModel)
2. **Create Mapping** in `Radish.Extension/AutoMapperExtension/CustomProfiles`
3. **Repository Layer**:
   - Define interface in `Radish.IRepository`
   - Implement in `Radish.Repository` (inherit from `BaseRepository<T>`)
4. **Service Layer**:
   - Define interface in `Radish.IService`
   - Implement in `Radish.Service` (inherit from `BaseService<TEntity, TModel>`)
   - Map entity to ViewModel before returning
5. **Controller** in `Radish.Api/Controllers`:
   - Inject IService (never IRepository)
   - Add `[Authorize]` or `[AllowAnonymous]` attribute
   - Use `[Route("api/[controller]/[action]")]` convention
6. **Add HTTP Examples** in `Radish.Api/Radish.Api.http` for manual testing
7. **Write Tests** in `Radish.Api.Tests/Controllers/`
8. **Update Frontend** in `radish.client/src/` as needed

## Rust Native Extensions (Experimental)

Located in `Radish.Core/test_lib` (example only, production modules should go in `native/rust/{library}`).

### Building Rust Libs
```bash
cd Radish.Core/test_lib
cargo build --release
# Output: target/release/test_lib.dll (Windows) or libtest_lib.so (Linux)
```

Copy the compiled library to `Radish.Api/bin/Debug/net10.0/` for runtime loading.

### Usage Example
`RustTestController` demonstrates `[DllImport("test_lib")]` for performance-critical algorithms. See endpoints like `/api/RustTest/TestSum1` for benchmarks.

## Documentation

Comprehensive docs in `radish.docs/docs/`:
- `DevelopmentSpecifications.md` - Directory structure, layering, dependency rules
- `DevelopmentFramework.md` - Overall architecture, tech decisions, milestones
- `DevelopmentPlan.md` - Weekly delivery plan
- `DevelopmentLog.md` - Daily progress log
- `AuthenticationGuide.md` - Auth flow details
- `FrontendDesign.md` - UI paradigm, component planning, cross-platform strategy
- `GatewayPlan.md` - API Gateway refactoring plan
- `DeploymentGuide.md` - Containerization and deployment

**Always consult these docs before making architectural changes.**

## Common Pitfalls

1. **Don't put business logic in Controllers** - Logic belongs in Service layer
2. **Don't expose entities directly** - Always map to ViewModels first
3. **Don't skip interface definitions** - Define IService/IRepository before implementations
4. **Don't hardcode secrets** - Use environment variables or User Secrets for sensitive data
5. **Don't forget to update Snowflake WorkId per environment** - Avoid ID collisions in production
6. **Don't create files in Radish.Common that depend on Model/Service/Repository** - Use Radish.Extension instead
7. **Router parameters in authorization** - Ensure `ApiModule.LinkUrl` contains regex for routes with parameters
8. **ViewModels must be obfuscated** - Don't just add `Vo` prefix; combine with business abbreviation

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
- Check `Docs/DevelopmentLog.md` for recent changes and known issues
- When adding new repositories/services, register them in Autofac module or use the generic pattern
