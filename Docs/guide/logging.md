# 日志系统

Radish 项目采用 Serilog 结构化日志。本文描述当前默认的旧链路；统一日志重构已提供 `RadishLogging.Enabled` 候选入口，默认关闭，启用后的配置、输出与迁移限制以[统一事件契约](../features/unified-logging-contract.md)为准，不沿用本文的旧 sink 配置。已治理的生成端安全摘要同时作用于旧 / 候选 sink；Hangfire 重试决定与清理批次的具体口径见该契约第 10 节。

## 架构概述

### 日志分类

Radish 的日志系统分为三个层次：

| 日志类型 | 用途 | 存储位置 | 实现方式 |
|---------|------|---------|---------|
| **应用日志** | 记录应用运行状态、错误、警告等 | 文件系统 + 数据库(可选) | Serilog |
| **SQL 日志** | 记录安全操作摘要和慢链路耗时 | 文件系统 + 数据库(可选) | SqlSugar AOP + Serilog |
| **业务审计日志** | 记录敏感操作（登录、权限变更、数据删除等） | 数据库 + 文件 | 审计中间件 + Serilog |

### 日志流向

```
应用代码
  ├─> Serilog.Log.* 方法
  │     ├─> Logs/{ProjectName}/Log.txt (应用日志文件)
  │     └─> Radish.Log.db -> InformationLog/WarningLog/ErrorLog_YYYYMMDD (可选)
  │
  ├─> SqlSugar AOP
  │     ├─> Logs/{ProjectName}/AopSql/AopSql.txt (SQL 日志文件)
  │     └─> Radish.Log.db -> AuditSqlLog_YYYYMMDD (可选)
  │
  └─> AuditLogMiddleware
        ├─> Logs/{ProjectName}/Log.txt (审计日志文件)
        └─> Radish.Log.db -> AuditLog_YYYYMMDD (审计日志数据库)
```

**注意**：
- 应用日志和 SQL 日志默认只输出到文件系统
- 可通过配置 `Serilog.Database.Enable = true` 启用数据库持久化
- 业务审计日志始终写入数据库和文件

## Serilog 配置

### 初始化

在 `Program.cs` 中调用扩展方法：

```csharp
using var runtimeLogging = new RuntimeLoggingSession(
    builder.Configuration, builder.Environment.EnvironmentName, "api");
builder.Host.AddSerilogSetup(runtimeLogging);
```

该方法由 `Radish.Extension.Log` 提供。默认旧链路按 Serilog 选项选择介质和级别，文件使用既有文本格式；候选开启时使用共享的规范 JSONL 输出，跳过旧文件与数据库 sink。候选 stdout 为同步写入，不能宣称不受管道背压影响。

### 日志目录结构

日志文件位于解决方案根目录的 `Logs/` 文件夹：

```
Logs/
├── Radish.Api/
│   ├── Log.txt                    # 应用日志（滚动文件）
│   ├── AopSql/
│   │   └── AopSql.txt            # SQL 日志
│   └── SerilogDebug/
│       └── Serilog20251220.txt   # Serilog 内部调试日志
├── Radish.Gateway/
│   └── Log.txt
└── Radish.Auth/
    └── Log.txt
```

**自动检测机制**：
1. 向上查找 `*.slnx` 或 `*.sln` 文件确定解决方案根目录
2. 优先尝试从宿主 `ContentRootPath` 与 `AppContext.BaseDirectory` 解析具体项目名；若无法定位源码目录，再回退到 `ApplicationName` / 宿主入口程序集名
3. 当多个候选名称冲突时，优先使用更具体的项目名（如 `Radish.Api`），避免误落到泛化目录 `Logs/Radish/`
4. 在 `Logs/{ProjectName}/` 下创建项目专属日志目录

补充说明：本地开发态优先依赖 `ContentRootPath` / `BaseDirectory`，避免 `ApplicationName=Radish` 这类泛化值把 `Radish.Api`、`Radish.Auth`、`Radish.Gateway` 误写到同一目录；发布目录与 Docker 容器拿不到源码 `.csproj` 时，再回退到宿主名称，避免日志继续落到 `Logs/Unknown/`。

### 日志级别配置

在 `appsettings.json` 中配置:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",

    "Console": {
      "Enable": true,
      "EnableApplicationLog": true,
      "EnableSqlLog": true
    },

    "File": {
      "Enable": true,
      "EnableApplicationLog": true,
      "EnableSqlLog": true,
      "RetainedFileCountLimit": 31
    },

    "Database": {
      "Enable": false,
      "EnableApplicationLog": true,
      "EnableSqlLog": true,
      "LogSelectQueries": true,
      "BatchSizeLimit": 500,
      "PeriodSeconds": 1,
      "EagerlyEmitFirstEvent": true,
      "QueueLimit": 10000
    }
  }
}
```

**配置说明**：

| 配置项 | 说明 | 默认值 |
|-------|------|--------|
| `MinimumLevel` | 最小日志级别 (Verbose/Debug/Information/Warning/Error/Fatal) | Information |
| `Console.Enable` | 是否启用控制台输出 | true |
| `Console.EnableApplicationLog` | 是否输出应用日志到控制台 | true |
| `Console.EnableSqlLog` | 是否输出 SQL 日志到控制台 | true |
| `File.Enable` | 是否启用文件输出 | true |
| `File.EnableApplicationLog` | 是否输出应用日志到文件 | true |
| `File.EnableSqlLog` | 是否输出 SQL 日志到文件 | true |
| `File.RetainedFileCountLimit` | 文件保留天数 | 31 |
| `Database.Enable` | 是否启用数据库输出 | false |
| `Database.EnableApplicationLog` | 是否记录应用日志到数据库 | true |
| `Database.EnableSqlLog` | 是否记录 SQL 日志到数据库 | true |
| `Database.LogSelectQueries` | 是否记录 SELECT 查询 | true |
| `Database.BatchSizeLimit` | 批处理大小限制 | 500 |
| `Database.PeriodSeconds` | 批处理周期(秒) | 1 |
| `Database.EagerlyEmitFirstEvent` | 是否立即发送第一个事件 | true |
| `Database.QueueLimit` | 队列限制 | 10000 |

### `SqlAopLog` 配置

SQL 生成规则同时适用于旧 sink 和统一候选入口。普通 SQL 诊断需要宿主为 Development、`RadishLogging.Mode=Development`、`RadishLogging.Diagnostics=true`，且 `SqlAopLog.Enabled=true`；生产不生成普通 SQL 诊断。

共享默认值：

```json
{
  "SqlAopLog": {
    "Enabled": false,
    "LogQuery": true,
    "LogInsert": true,
    "LogUpdate": true,
    "LogDelete": true,
    "SlowQueryEnabled": true,
    "SlowQueryThresholdMs": 1000,
    "SlowConnectionEnabled": true,
    "SlowConnectionThresholdMs": 500,
    "SkipTables": ["WikiDocument", "WikiDocumentRevision"],
    "SkipUsers": []
  }
}
```

- `Enabled / LogQuery / LogInsert / LogUpdate / LogDelete / SkipTables / SkipUsers` 只筛选普通开发诊断。
- `SlowQueryEnabled / SlowConnectionEnabled` 独立控制慢操作与慢连接，阈值必须大于零；关闭普通 SQL 或排除表名不会屏蔽慢链路。
- 输出只包含受控 `operation`、`parameterCount`、`durationMs`，不输出 SQL 文本、参数名 / 值、用户、表名或连接字符串。原 `OmitLargeText / LargeTextThreshold / OmittedFields` 已退出，不再以字段黑名单判断哪些值可输出。
- Log 库仍排除 SQL 日志，避免递归；PostgreSQL 时间参数规范化不受任何日志开关影响。
- 旧 sink 的介质开关与阈值仍可关闭介质；候选入口按统一策略输出，不能把生成独立性解释为绕过显式存储禁用。


## 应用日志

### 使用方法

**推荐方式**：直接使用 Serilog 静态方法

```csharp
using Serilog;

// 信息日志
Log.Information("User {UserId} logged in from {IpAddress}", userId, ipAddress);

// 警告日志
Log.Warning("Cache miss for key {CacheKey}", cacheKey);

// 错误日志
Log.Error(ex, "Failed to process order {OrderId}", orderId);

// 调试日志
Log.Debug("Processing request with parameters: {@Parameters}", parameters);
```

**依赖注入方式**（仅在需要与外部框架集成时使用）：

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.LogInformation("Work started");
    }
}
```

### 结构化日志

使用 `@` 前缀记录复杂对象：

```csharp
var user = new { Id = 123, Name = "Alice", Email = "alice@example.com" };
Log.Information("User created: {@User}", user);
```

输出：
```json
{
  "Timestamp": "2025-12-20T15:30:00.123Z",
  "Level": "Information",
  "MessageTemplate": "User created: {@User}",
  "User": {
    "Id": 123,
    "Name": "Alice",
    "Email": "alice@example.com"
  }
}
```

### 日志上下文

使用 `LogContext` 为一组操作添加上下文信息：

```csharp
using Serilog.Context;

using (LogContext.PushProperty("TenantId", tenantId))
using (LogContext.PushProperty("TraceId", traceId))
{
    Log.Information("Processing tenant request");
    // 所有日志都会包含 TenantId 和 TraceId
}
```

### 数据库持久化

应用日志支持按级别分表存储到数据库：

**数据模型**：

```csharp
// InformationLog - Information 级别日志
[Tenant(configId: "Log")]
[SplitTable(SplitType.Month)]
[SugarTable("InformationLog_{year}{month}{day}")]
public class InformationLog : BaseLog
{
    // 继承自 BaseLog:
    // - Id: long (Snowflake ID)
    // - DateTime: DateTime
    // - Level: string
    // - Message: string
    // - MessageTemplate: string
    // - Properties: string (JSON 格式的附加属性)
}

// WarningLog - Warning 级别日志
[Tenant(configId: "Log")]
[SplitTable(SplitType.Month)]
[SugarTable("WarningLog_{year}{month}{day}")]
public class WarningLog : BaseLog { }

// ErrorLog - Error/Fatal 级别日志
[Tenant(configId: "Log")]
[SplitTable(SplitType.Month)]
[SugarTable("ErrorLog_{year}{month}{day}")]
public class ErrorLog : BaseLog
{
    public string? Exception { get; set; }  // 异常堆栈信息
}
```

**启用数据库持久化**：

在 `appsettings.Local.json` 中配置：

```json
{
  "Serilog": {
    "Database": {
      "Enable": true,
      "EnableApplicationLog": true
    }
  }
}
```

**查询示例**：

```sql
-- 查询今天的 Information 日志
SELECT * FROM InformationLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 查询今天的错误日志
SELECT * FROM ErrorLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 统计各级别日志数量
SELECT 'Information' as Level, COUNT(*) as Count FROM InformationLog_20251201
UNION ALL
SELECT 'Warning', COUNT(*) FROM WarningLog_20251201
UNION ALL
SELECT 'Error', COUNT(*) FROM ErrorLog_20251201;
```

## SQL 日志

### 生成与异常责任

`SqlSugarSetup` 注册安全 SQL 生成器。普通诊断使用 `database.diagnostic`，执行结束与连接检查超阈值使用 `database.slow`；只记录操作类别、参数数量和耗时。表 / 用户跳过名单只用于本地筛选，不进入输出。

- `OnGetDataReadered` 观测查询读取耗时，`OnLogExecuted` 只观测非 Query 命令，避免同一查询重复生成。
- `CheckConnectionExecuted` 观测连接检查耗时，默认阈值 500ms；慢操作默认阈值 1000ms。
- 不再配置 SQL `OnError` 日志回调。Service AOP 已移除；事务 AOP 与 UnitOfWork 保留提交、回滚、保存点及异常传播，不重复打印失败。
- 已处理的 API 5xx 由 `ApiExceptionHandler` 记录一次 `http.failed`，同时抑制已处理异常的框架重复诊断；普通业务 4xx 只返回原错误契约。无法处理、响应已开始等异常仍由框架最终边界处理，不把它们误称已处理。
- API / Auth / Gateway / DbMigrate 的顶层使用 `RuntimeProcess`，配置加载或运行失败以安全 Fatal 事件输出 stderr，并以 1 退出；成功为 0。测试工具的 `HostAbortedException` 继续传播。无法初始化配置时来源标记为 `bootstrap / unversioned`，不伪造部署身份。

### DbMigrate 命令输出

`doctor / verify / help` 报告继续写 stdout；CLI 诊断写 stderr，使用统一 JSONL 策略，不连接旧文件 / 数据库 sink。诊断入口记录命令类别、阶段、变更数量和耗时；原始 argv、连接串和 provider 异常消息不进入诊断。doctor / verify 的报告保留判定、问题分类和退出成功 / 失败语义，连接目标及异常原文改为安全说明。

Seed 不再捕获或回放 `Console.Out`。每阶段记录受控 `seedStep`、`outcome` 和耗时；缺失资源、冲突、未解决回填使用安全警告，不附路径、邮箱或业务载荷。阶段失败摘要为 Info，异常原样交给顶层记录一次 Error。

具体迁移由 ledger 在事务提交后生成 `dbmigrate.schema.applied`，仅包含登记的迁移 ID、库范围及耗时；重复执行无新提交时不重复记录。新增 migration 时同步登记共享策略中的 `migrationId`，注册表测试防止遗漏。Auth schema adoption 同样在提交后记录，Auth seed 完成时汇总新增、更新和移除数。日志不替代迁移账本或权威审计；其他调用链仍待治理，生产切换保持关闭。

### SQLite 连接初始化

对于默认本地开发使用的 SQLite，当前还会在连接已打开时自动执行以下初始化：

- `PRAGMA busy_timeout = 60000;`
- `PRAGMA synchronous = NORMAL;`
- `PRAGMA journal_mode = WAL;`

其中 `journal_mode = WAL` 只会按“连接配置 + 数据库文件”初始化一次，避免每次连接都重复切模式。这个策略的目标是尽量降低单文件 SQLite 在高频读写下的瞬时锁等待，但它不是生产级数据库治理的替代品；如果业务已经进入多实例、高并发或持续高写入阶段，仍应优先评估 PostgreSQL。

**日志输出位置**：
- 文件：`Logs/{ProjectName}/AopSql/AopSql.txt`
- 控制台：同时输出到控制台（开发环境）
- 数据库：`Radish.Log.db -> AuditSqlLog_YYYYMMDD`（需启用）

### 数据模型

`AuditSqlLog` 实体用于存储 SQL 日志：

```csharp
[Tenant(configId: "Log")]              // 使用独立的日志数据库
[SplitTable(SplitType.Month)]          // 按月分表
[SugarTable("AuditSqlLog_{year}{month}{day}")]
public class AuditSqlLog : BaseLog
{
    // 继承自 BaseLog:
    // - Id: long (Snowflake ID)
    // - DateTime: DateTime
    // - Level: string
    // - Message: string (包含完整的 SQL 语句)
    // - MessageTemplate: string
    // - Properties: string (JSON 格式的附加属性)
}
```

**启用数据库持久化**：

在 `appsettings.Local.json` 中配置：

```json
{
  "Serilog": {
    "Database": {
      "Enable": true,
      "EnableSqlLog": true,
      "LogSelectQueries": true  // false 时过滤普通 SELECT 诊断，保留慢链路告警
    }
  }
}
```

### 查看 SQL 日志

```bash
# 查看最新的 SQL 日志
tail -f Logs/Radish.Api/AopSql/AopSql.txt

# 搜索安全慢链路摘要
rg "duration=" Logs/Radish.Api/AopSql/AopSql.txt

# 查看今天的 SQL 日志
cat Logs/Radish.Api/AopSql/AopSql.txt | grep "$(date +%Y-%m-%d)"
```

## 业务审计日志

### 功能概述

审计日志中间件自动记录敏感操作，包括：
- 用户信息（UserId、UserName、TenantId）
- 请求信息（Method、Path、Body、IP、UserAgent）
- 响应信息（StatusCode、Body、Duration）
- 操作类型（Login、Logout、Create、Update、Delete）

### 配置

在 `appsettings.json` 中配置：

```json
{
  "AuditLog": {
    "Enable": true,                    // 是否启用审计日志
    "EnableLogging": true,             // 是否同时输出到 Serilog
    "LogResponseBody": false,          // 是否记录响应体（可能很大）
    "AuditMethods": ["POST", "PUT", "DELETE"],  // 审计的 HTTP 方法
    "IncludePaths": [],                // 需要审计的路径（为空表示全部）
    "ExcludePaths": [                  // 排除的路径
      "/health",
      "/ready",
      "/metrics",
      "/swagger",
      "/scalar",
      "/api/docs" // 旧兼容路径，可按需移除
    ]
  }
}
```

在 `Program.cs` 中注册：

```csharp
// 注册服务
builder.Services.AddAuditLogSetup();

// 使用中间件（在认证授权之后、路由之前）
app.UseAuthentication();
app.UseAuthorization();
app.UseAuditLogSetup();  // 审计日志中间件
app.MapControllers();
```

### 数据模型

```csharp
[Tenant(configId: "Log")]
[SplitTable(SplitType.Month)]
[SugarTable("AuditLog_{year}{month}{day}")]
public class AuditLog : BaseLog
{
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public long? TenantId { get; set; }
    public string OperationType { get; set; }      // Login, Create, Update, Delete
    public string? Module { get; set; }            // User, Post, Category
    public string? Description { get; set; }       // 操作描述
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestBody { get; set; }       // 脱敏后的请求体
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }      // 可选的响应体
    public long? Duration { get; set; }            // 请求耗时（毫秒）
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### 敏感信息脱敏

中间件自动脱敏请求体中的敏感字段：

```csharp
private string? SanitizeRequestBody(string? body)
{
    var sensitiveFields = new[] {
        "password", "pwd", "secret", "token", "apikey", "api_key"
    };

    // 如果包含敏感字段，返回提示信息
    if (hasSensitiveData)
    {
        return "[包含敏感信息，已脱敏]";
    }

    return body;
}
```

### API 查询

审计日志提供完整的查询 API：

```http
### 分页查询审计日志
POST {{Api_HostAddress}}/api/v1/AuditLog/QueryPage
Content-Type: application/json
Authorization: Bearer {{Access_Token}}

{
  "userId": 20000,
  "operationType": "Create",
  "startTime": "2025-12-20T00:00:00Z",
  "endTime": "2025-12-20T23:59:59Z",
  "pageIndex": 1,
  "pageSize": 20
}

### 根据用户 ID 查询
GET {{Api_HostAddress}}/api/v1/AuditLog/QueryByUserId?userId=20000&pageIndex=1&pageSize=20

### 获取操作类型统计
GET {{Api_HostAddress}}/api/v1/AuditLog/GetOperationTypeStatistics?startTime=2025-12-01&endTime=2025-12-31

### 获取用户操作统计（Top 10）
GET {{Api_HostAddress}}/api/v1/AuditLog/GetUserStatistics?topN=10
```

## 日志数据库

### 数据库配置

日志使用共享的 `ConnId=Log` 日志库，在 `appsettings.json` 中配置：

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 1,
      "ConnectionString": "DataSource=DataBases/Radish.db",
      "IsAutoCloseConnection": true
    },
    {
      "ConnId": "Log",
      "DbType": 1,
      "ConnectionString": "DataSource=DataBases/Radish.Log.db",
      "IsAutoCloseConnection": true
    }
  ]
}
```

**重要说明**：
- `ConnId=Log` 是固定名称，不可更改
- 日志库配置缺失会在启动时抛出异常
- 本地开发使用 SQLite，生产环境建议使用 PostgreSQL
- 当前设计不是“每个宿主项目独立一个日志库”；文件日志按项目分目录，数据库日志则共享一套 `Log` 库并按日志类型 / 日期分表

### 分表策略

日志表采用按月分表策略：

```
InformationLog_20251201   # 2025年12月的 Information 日志
WarningLog_20251201       # 2025年12月的 Warning 日志
ErrorLog_20251201         # 2025年12月的 Error 日志
AuditSqlLog_20251201      # 2025年12月的 SQL 日志
AuditLog_20251201         # 2025年12月的审计日志
```

**优势**：
- 提高查询性能（只查询相关月份的表）
- 便于归档和清理历史数据
- 避免单表数据量过大
- 按日志级别分表,便于分析和统计

### 数据库初始化

首次运行时，SqlSugar 会自动创建日志表：

```bash
# 运行数据库迁移工具
dotnet run --project Radish.DbMigrate init

# 或直接运行 API（会自动创建表）
dotnet run --project Radish.Api
```

## 最佳实践

### 运行事件规则

- 使用 Info / Warning / Error 三级语义；开发细节显式标记 diagnostic，生产不生成普通开发诊断。Fatal 在统一入口映射为 Error + isFatal。
- 新调用优先 `ILogger<T>`，携带登记的 EventCode / SourceCategory 与受控属性。普通运行日志不记录实体、邮件、凭据、正文或任意异常消息。
- 最终处理 / 放弃重试的边界记录一次 Error；事务与数据库层继续抛出，不逐层重复记录。异常当前只允许固定 `failureKind`，完整安全栈帧按专题后续治理。
- 循环处理记录一次 count / duration / outcome 摘要；明确动作的权威审计仍按独立事务与保留规则维护。
- 新旧 sink 过渡期间，调用点也必须安全；不能仅依赖候选 sink 的防御裁剪。

### 审计日志配置建议

**开发环境**：
```json
{
  "AuditLog": {
    "Enable": true,
    "LogResponseBody": true,   // 开发时可以记录响应体
    "AuditMethods": ["POST", "PUT", "DELETE"]
  }
}
```

**生产环境**：
```json
{
  "AuditLog": {
    "Enable": true,
    "LogResponseBody": false,  // 生产环境不记录响应体（节省空间）
    "AuditMethods": ["POST", "PUT", "DELETE"],
    "IncludePaths": [          // 只审计敏感路径
      "/api/v1/User",
      "/api/v1/Role",
      "/api/v1/Permission"
    ]
  }
}
```

## 日志查询与分析

### 文件日志查询

```bash
# 查看最新的应用日志
tail -f Logs/Radish.Api/Log.txt

# 搜索特定用户的日志
grep "UserId: 20000" Logs/Radish.Api/Log.txt

# 查看今天的错误日志
grep "Error" Logs/Radish.Api/Log20251220.txt
```

### 数据库日志查询

```sql
-- 查询今天的应用日志(Information 级别)
SELECT * FROM InformationLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 查询今天的错误日志
SELECT * FROM ErrorLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 查询今天的 SQL 日志
SELECT * FROM AuditSqlLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 查询今天的审计日志
SELECT * FROM AuditLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 统计各操作类型的数量
SELECT OperationType, COUNT(*) as Count
FROM AuditLog_20251201
GROUP BY OperationType;

-- 统计各日志级别的数量
SELECT 'Information' as Level, COUNT(*) as Count FROM InformationLog_20251201
UNION ALL
SELECT 'Warning', COUNT(*) FROM WarningLog_20251201
UNION ALL
SELECT 'Error', COUNT(*) FROM ErrorLog_20251201;

-- 查询失败的操作
SELECT * FROM AuditLog_20251201
WHERE IsSuccess = 0
ORDER BY DateTime DESC;

-- 查询慢请求（超过1秒）
SELECT * FROM AuditLog_20251201
WHERE Duration > 1000
ORDER BY Duration DESC;

-- 查询包含特定关键字的日志
SELECT * FROM InformationLog_20251201
WHERE Message LIKE '%User%'
ORDER BY DateTime DESC;
```

## 日志归档与清理

### 文件日志

Serilog 自动滚动日志文件：
- 每天创建新文件（`Log20251220.txt`）
- 保留最近 31 天的日志
- 超过限制的旧文件自动删除

### 数据库日志

建议定期归档和清理：

```sql
-- 删除3个月前的日志表
DROP TABLE IF EXISTS InformationLog_20250901;
DROP TABLE IF EXISTS WarningLog_20250901;
DROP TABLE IF EXISTS ErrorLog_20250901;
DROP TABLE IF EXISTS AuditSqlLog_20250901;
DROP TABLE IF EXISTS AuditLog_20250901;

-- 或导出后删除
-- 1. 导出数据到文件
-- 2. 删除表
-- 3. 压缩归档
```

**自动化脚本**（可选）：

```bash
#!/bin/bash
# cleanup-logs.sh
# 删除3个月前的日志表

MONTHS_AGO=3
TARGET_DATE=$(date -d "$MONTHS_AGO months ago" +%Y%m01)

sqlite3 DataBases/Radish.Log.db <<EOF
DROP TABLE IF EXISTS InformationLog_$TARGET_DATE;
DROP TABLE IF EXISTS WarningLog_$TARGET_DATE;
DROP TABLE IF EXISTS ErrorLog_$TARGET_DATE;
DROP TABLE IF EXISTS AuditSqlLog_$TARGET_DATE;
DROP TABLE IF EXISTS AuditLog_$TARGET_DATE;
EOF

echo "Cleaned up logs older than $TARGET_DATE"
```

## 故障排查

### 日志未写入文件

**问题**：应用运行但没有生成日志文件

**解决方案**：
1. 检查 `Program.cs` 是否调用了 `builder.Host.AddSerilogSetup(runtimeLogging)`
2. 检查文件系统权限（确保应用有写入 `Logs/` 目录的权限）
3. 查看 `Logs/{ProjectName}/SerilogDebug/` 目录中的调试日志

### 审计日志未写入数据库

**问题**：审计日志只在文件中，数据库中没有记录

**解决方案**：
1. 检查 `AuditLog` 配置是否启用：`"Enable": true`
2. 检查中间件是否注册：`app.UseAuditLogSetup()`
3. 检查日志数据库配置：`ConnId=Log` 必须存在
4. 查看应用日志中的错误信息：`grep "AuditLog" Logs/Radish.Api/Log.txt`

### 应用日志/SQL日志未写入数据库

**问题**：启用了数据库日志但数据库中没有记录

**解决方案**：
1. 检查 `Serilog.Database.Enable` 是否为 `true`
2. 检查 `Serilog.Database.EnableApplicationLog` 或 `EnableSqlLog` 是否启用
3. 检查日志数据库配置：`ConnId=Log` 必须存在且正确
4. 查看 Serilog 调试日志：`Logs/{ProjectName}/SerilogDebug/Serilog*.txt`
5. 确认 SqlSugar 配置正确初始化了 Log 数据库连接

**常见错误**：
```
SqlSugar.SqlSugarException: ConfigId was not found Log
```
**原因**：SqlSugar 将 ConfigId 转换为小写,代码中需使用 `SqlSugarConst.LogConfigId.ToLower()`

### SQL 日志性能影响

普通诊断默认关闭，生产仅保留独立慢操作摘要；排障时调整 `SlowQueryThresholdMs / SlowConnectionThresholdMs`，不要添加直接输出 SQL 的临时回调。

### SQLite 登录链路偶发慢请求

**问题**：登录页输入邮箱和密码后需要等待数十秒，或标签页长时间后台后再次进入登录流程明显变慢。

**建议排查**：
1. 先看 `Logs/Radish.Auth/Log.txt` 中 `[Account/Login]` 的阶段耗时，确认慢点落在用户查询、密码校验还是角色查询。
2. 再看 `Logs/Radish.Auth/AopSql/AopSql.txt` 是否出现 `database.slow` 摘要，并结合最终请求失败日志。
3. 如果当前数据库是旧 SQLite 库，先执行一次 `DbMigrate apply`，确认当前用户身份字段、公开索引和登录查询所需结构已自动补齐。
4. 如果仍频繁出现连接等待，应优先评估是否存在同库高频写入竞争，必要时把环境从 SQLite 切到 PostgreSQL。

## 持久化实现与后续迁移

旧链路由 `LogBatchingSink` 批量分发应用 / SQL 日志，`LogFilterExtensions` 选择来源与普通 SELECT 诊断，`SerilogOptions` 管理介质与队列。具体实现以 `Radish.Extension.Log` 源码为准，不在文档复制实现代码。

后续集中汇聚沿用已确认的[统一日志专题](../features/unified-logging-governance-design.md)：独立 collector、JSONL、内网批量入库和 Console 查询 / 告警。此方案尚未完成生产切换，不额外引入 Seq 或 Elasticsearch 等另一套架构。

## 相关文档

- [配置管理](./configuration.md) - 日志配置详解
- [开发规范](/architecture/specifications) - 日志模型规范
- [开发框架说明](/architecture/framework) - 日志架构设计
- [速率限制](./rate-limiting.md) - 速率限制日志

## 参考资源

- [Serilog 官方文档](https://serilog.net/)
- [SqlSugar 文档](https://www.donet5.com/Home/Doc)
- [结构化日志最佳实践](https://github.com/serilog/serilog/wiki/Structured-Data)
- [Serilog.Sinks.PeriodicBatching](https://github.com/serilog/serilog-sinks-periodicbatching) - 批量写入 Sink
