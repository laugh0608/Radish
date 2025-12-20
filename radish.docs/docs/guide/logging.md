# 日志系统

Radish 项目采用 Serilog 结构化日志，提供完整的应用监控和审计能力。

## 架构概述

### 日志分类

Radish 的日志系统分为三个层次：

| 日志类型 | 用途 | 存储位置 | 实现方式 |
|---------|------|---------|---------|
| **应用日志** | 记录应用运行状态、错误、警告等 | 文件系统 (`Log/` 目录) | Serilog |
| **SQL 日志** | 记录所有 SQL 执行语句和性能 | 文件系统 (`Log/AopSql/` 目录) | SqlSugar AOP + Serilog |
| **业务审计日志** | 记录敏感操作（登录、权限变更、数据删除等） | 数据库 (`AuditLog` 表) + 文件 | 审计中间件 + Serilog |

### 日志流向

```
应用代码
  ├─> Serilog.Log.* 方法
  │     └─> Log/{ProjectName}/Log.txt (应用日志文件)
  │
  ├─> SqlSugar AOP
  │     └─> Log/{ProjectName}/AopSql/AopSql.txt (SQL 日志文件)
  │
  └─> AuditLogMiddleware
        ├─> Log/{ProjectName}/Log.txt (审计日志文件)
        └─> Radish.Log.db -> AuditLog_YYYYMMDD (审计日志数据库)
```

**注意**：
- 应用日志和 SQL 日志目前只输出到文件系统，不写入数据库
- 只有业务审计日志通过中间件写入数据库
- 如需将应用日志和 SQL 日志写入数据库，需要实现 Serilog 自定义 Sink（参见"扩展功能"章节）

## Serilog 配置

### 初始化

在 `Program.cs` 中调用扩展方法：

```csharp
builder.Host.AddSerilogSetup();
```

该方法由 `Radish.Extension.SerilogExtension` 提供，自动配置：
- 日志输出目标（Console + File）
- 日志格式（结构化 JSON）
- 日志级别（从 `appsettings.json` 读取）
- 异步写入（避免阻塞请求线程）

### 日志目录结构

日志文件位于解决方案根目录的 `Log/` 文件夹：

```
Log/
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
2. 从 `.csproj` 文件识别当前项目名称
3. 在 `Log/{ProjectName}/` 下创建项目专属日志目录

### 日志级别配置

在 `appsettings.json` 中配置：

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

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

## SQL 日志

### 配置

SqlSugar AOP 在 `SqlSugarSetup` 中自动配置，将 SQL 执行日志输出到文件：

```csharp
// Radish.Extension/AopExtension/SqlSugarAop.cs
public static void OnLogExecuting(ISqlSugarClient sqlSugarScopeProvider, string user, string table, string operate,
    string sql, SugarParameter[] p, ConnectionConfig config)
{
    // 使用 LogContextTool 标记日志来源
    using (LogContextTool.Create.SqlAopPushProperty(sqlSugarScopeProvider))
    {
        Log.Information($"User:[{user}] Table:[{table}] Operate:[{operate}] ConnId:[{config.ConfigId}] SQL: {sql}");
    }
}
```

**日志输出位置**：
- 文件：`Log/{ProjectName}/AopSql/AopSql.txt`
- 控制台：同时输出到控制台（开发环境）

**注意**：SQL 日志目前只输出到文件系统，不写入数据库。如需写入数据库，参见"扩展功能"章节。

### 数据模型（预留）

`AuditSqlLog` 实体已定义，但目前未使用：

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
    // - Message: string
}
```

**说明**：该实体为将来实现 SQL 日志数据库持久化预留，目前 SQL 日志只通过 Serilog 输出到文件。

### 查看 SQL 日志

```bash
# 查看最新的 SQL 日志
tail -f Log/Radish.Api/AopSql/AopSql.txt

# 搜索特定表的 SQL
grep "Table:\[User\]" Log/Radish.Api/AopSql/AopSql.txt

# 查看今天的 SQL 日志
cat Log/Radish.Api/AopSql/AopSql.txt | grep "$(date +%Y-%m-%d)"
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
      "/api/docs"
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
  "startTime": "2025-12-20T00:00:00",
  "endTime": "2025-12-20T23:59:59",
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

日志使用独立的数据库，在 `appsettings.json` 中配置：

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

### 分表策略

日志表采用按月分表策略：

```
AuditSqlLog_20251201   # 2025年12月的 SQL 日志
AuditSqlLog_20260101   # 2026年1月的 SQL 日志
AuditLog_20251201      # 2025年12月的审计日志
AuditLog_20260101      # 2026年1月的审计日志
```

**优势**：
- 提高查询性能（只查询相关月份的表）
- 便于归档和清理历史数据
- 避免单表数据量过大

### 数据库初始化

首次运行时，SqlSugar 会自动创建日志表：

```bash
# 运行数据库迁移工具
dotnet run --project Radish.DbMigrate init

# 或直接运行 API（会自动创建表）
dotnet run --project Radish.Api
```

## 最佳实践

### 1. 日志级别选择

| 级别 | 使用场景 | 示例 |
|-----|---------|------|
| **Debug** | 开发调试信息 | `Log.Debug("Cache hit for key {Key}", key)` |
| **Information** | 正常业务流程 | `Log.Information("User {UserId} logged in", userId)` |
| **Warning** | 潜在问题 | `Log.Warning("API rate limit approaching for {UserId}", userId)` |
| **Error** | 错误但不影响系统运行 | `Log.Error(ex, "Failed to send email to {Email}", email)` |
| **Fatal** | 严重错误导致系统崩溃 | `Log.Fatal(ex, "Database connection failed")` |

### 2. 避免日志泄露敏感信息

```csharp
// ❌ 错误：记录完整的用户对象（可能包含密码）
Log.Information("User: {@User}", user);

// ✅ 正确：只记录必要的字段
Log.Information("User {UserId} ({UserName}) logged in", user.Id, user.UserName);

// ✅ 正确：使用匿名对象过滤敏感字段
Log.Information("User: {@User}", new { user.Id, user.UserName, user.Email });
```

### 3. 使用结构化日志

```csharp
// ❌ 错误：字符串拼接
Log.Information("User " + userId + " created post " + postId);

// ✅ 正确：结构化参数
Log.Information("User {UserId} created post {PostId}", userId, postId);
```

### 4. 异常日志记录

```csharp
try
{
    await ProcessOrderAsync(orderId);
}
catch (Exception ex)
{
    // ✅ 正确：记录异常对象和上下文
    Log.Error(ex, "Failed to process order {OrderId}", orderId);
    throw;
}
```

### 5. 性能考虑

```csharp
// ❌ 错误：在循环中记录大量日志
foreach (var item in items)
{
    Log.Debug("Processing item {ItemId}", item.Id);  // 可能产生数千条日志
}

// ✅ 正确：批量记录或只记录关键信息
Log.Information("Processing {ItemCount} items", items.Count);
```

### 6. 审计日志配置建议

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
tail -f Log/Radish.Api/Log.txt

# 搜索特定用户的日志
grep "UserId: 20000" Log/Radish.Api/Log.txt

# 查看今天的错误日志
grep "Error" Log/Radish.Api/Log20251220.txt
```

### 数据库日志查询

```sql
-- 查询今天的审计日志
SELECT * FROM AuditLog_20251201
WHERE DateTime >= '2025-12-20 00:00:00'
ORDER BY DateTime DESC;

-- 统计各操作类型的数量
SELECT OperationType, COUNT(*) as Count
FROM AuditLog_20251201
GROUP BY OperationType;

-- 查询失败的操作
SELECT * FROM AuditLog_20251201
WHERE IsSuccess = 0
ORDER BY DateTime DESC;

-- 查询慢请求（超过1秒）
SELECT * FROM AuditLog_20251201
WHERE Duration > 1000
ORDER BY Duration DESC;
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
DROP TABLE IF EXISTS AuditLog_20250901;
DROP TABLE IF EXISTS AuditSqlLog_20250901;

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
DROP TABLE IF EXISTS AuditLog_$TARGET_DATE;
DROP TABLE IF EXISTS AuditSqlLog_$TARGET_DATE;
EOF

echo "Cleaned up logs older than $TARGET_DATE"
```

## 故障排查

### 日志未写入文件

**问题**：应用运行但没有生成日志文件

**解决方案**：
1. 检查 `Program.cs` 是否调用了 `builder.Host.AddSerilogSetup()`
2. 检查文件系统权限（确保应用有写入 `Log/` 目录的权限）
3. 查看 `Log/{ProjectName}/SerilogDebug/` 目录中的调试日志

### 审计日志未写入数据库

**问题**：审计日志只在文件中，数据库中没有记录

**解决方案**：
1. 检查 `AuditLog` 配置是否启用：`"Enable": true`
2. 检查中间件是否注册：`app.UseAuditLogSetup()`
3. 检查日志数据库配置：`ConnId=Log` 必须存在
4. 查看应用日志中的错误信息：`grep "AuditLog" Log/Radish.Api/Log.txt`

### SQL 日志性能影响

**问题**：SQL 日志导致性能下降

**解决方案**：
1. 使用异步写入（已默认配置）
2. 生产环境提高日志级别（只记录慢查询）
3. 考虑只在开发环境启用 SQL 日志

```csharp
// 只记录慢查询（超过100ms）
db.Aop.OnLogExecuting = (sql, pars) =>
{
    var sw = Stopwatch.StartNew();
    // ... 执行后
    if (sw.ElapsedMilliseconds > 100)
    {
        Log.Warning("Slow SQL ({Duration}ms): {Sql}", sw.ElapsedMilliseconds, sql);
    }
};
```

## 扩展功能

### 将日志写入数据库

目前应用日志和 SQL 日志只输出到文件系统。如需将它们写入数据库，可以实现 Serilog 自定义 Sink。

**参考实现**（位于 `reference/Blog.Core/Blog.Core.Serilog/`）：

1. **创建自定义 Sink**：

```csharp
// Radish.Extension/SerilogExtension/LogBatchingSink.cs
public class LogBatchingSink : IBatchedLogEventSink
{
    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        var sugar = App.GetService<ISqlSugarClient>();

        // 分离 SQL 日志和普通日志
        await WriteSqlLog(sugar, batch.FilterSqlLog());
        await WriteLogs(sugar, batch.FilterRemoveOtherLog());
    }

    private async Task WriteSqlLog(ISqlSugarClient db, IEnumerable<LogEvent> batch)
    {
        var logs = batch.Select(e => new AuditSqlLog
        {
            Message = e.RenderMessage(),
            DateTime = e.Timestamp.DateTime,
            Level = e.Level.ToString()
        }).ToList();

        await db.AsTenant().InsertableWithAttr(logs).SplitTable().ExecuteReturnSnowflakeIdAsync();
    }
}
```

2. **注册 Sink**：

```csharp
// Radish.Extension/SerilogExtension/SerilogSetup.cs
public static IHostBuilder AddSerilogSetup(this IHostBuilder host)
{
    var loggerConfiguration = new LoggerConfiguration()
        .ReadFrom.Configuration(AppSettingsTool.Configuration)
        .Enrich.FromLogContext()
        .WriteToConsole()
        .WriteToFile()
        .WriteToLogBatching();  // 添加数据库 Sink

    Log.Logger = loggerConfiguration.CreateLogger();
    host.UseSerilog();
    return host;
}
```

3. **配置开关**：

```json
{
  "AppSettings": {
    "LogToDb": true  // 控制是否写入数据库
  }
}
```

**注意事项**：
- 批量写入以提高性能（使用 `PeriodicBatchingSink`）
- 区分不同日志级别（Information、Warning、Error）
- 使用 `LogSource` 属性区分 SQL 日志和普通日志
- 考虑数据库写入失败的降级策略

### 集成日志中心

对于生产环境，建议集成集中式日志管理系统：

**Seq**（推荐用于 .NET 项目）：

```csharp
var loggerConfiguration = new LoggerConfiguration()
    .WriteTo.Seq("http://localhost:5341", apiKey: "your-api-key");
```

**Elasticsearch + Kibana**：

```csharp
var loggerConfiguration = new LoggerConfiguration()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = "radish-logs-{0:yyyy.MM.dd}"
    });
```

**配置示例**：

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341",
          "apiKey": "your-api-key"
        }
      }
    ]
  }
}
```

## 相关文档

- [配置管理](./configuration.md) - 日志配置详解
- [架构规范](../architecture/specifications.md) - 日志模型规范
- [开发框架](../architecture/framework.md) - 日志架构设计
- [速率限制](./rate-limiting.md) - 速率限制日志

## 参考资源

- [Serilog 官方文档](https://serilog.net/)
- [SqlSugar 文档](https://www.donet5.com/Home/Doc)
- [结构化日志最佳实践](https://github.com/serilog/serilog/wiki/Structured-Data)
- [Serilog.Sinks.PeriodicBatching](https://github.com/serilog/serilog-sinks-periodicbatching) - 批量写入 Sink
