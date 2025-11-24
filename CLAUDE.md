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
- **Frontend**: React 19 + Vite (using Rolldown bundler) + TypeScript with a desktop-like UI paradigm
- **Solution**: Radish.slnx contains all backend projects and can be developed cohesively

## Essential Commands

### Backend Development
```bash
# Build the solution
dotnet build Radish.slnx -c Debug

# Run the API (listens on https://localhost:7110 and http://localhost:5165)
dotnet run --project Radish.Api/Radish.Api.csproj

# Hot reload during development
dotnet watch --project Radish.Api

# Run backend tests
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj
```

### Frontend Development
```bash
# Install dependencies (run from repo root)
npm install --prefix radish.client

# Start dev server (default: https://localhost:58794)
npm run dev --prefix radish.client

# Build for production
npm run build --prefix radish.client

# Lint
npm run lint --prefix radish.client
```

### Quick Start Scripts
```bash
# Interactive menu to start frontend, backend, both, or run tests
pwsh ./local-start.ps1    # Windows/PowerShell
./local-start.sh          # Linux/macOS
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

### Configuration Sources
- `appsettings.json` (base config, checked into git)
- `appsettings.Development.json` / `appsettings.Production.json` (environment-specific)
- Environment variables (override file-based config)
- User Secrets (for sensitive local dev data)

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
Default setup uses `Radish.db` (main) and `RadishLog.db` (logs) in project root. Auto-created on first run. For PostgreSQL, update `Databases[].ConnectionString` and `DbType=4`.

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
`Program.cs` calls `builder.Host.AddSerilogSetup()` to configure Serilog. Logs written to `Log/Log.txt` (general) and `Log/AopSql/AopSql.txt` (SQL via AOP).

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
- All requests over HTTPS
- Sensitive fields (login, password reset) encrypted with RSA public key on client, decrypted with private key on server
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

Comprehensive docs in `docs/`:
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

## Development Workflow Tips

- Use `dotnet watch` for backend hot reload
- Use Vite's HMR for instant frontend updates
- Scalar API docs available at `/api/docs` when running locally
- Example requests in `Radish.Api/Radish.Api.http` (use REST Client extension)
- Check `docs/DevelopmentLog.md` for recent changes and known issues
- When adding new repositories/services, register them in Autofac module or use the generic pattern
