# 数据库连接管理

本文档详细说明 Radish 项目中 SqlSugar 的数据库连接配置和管理机制。

## 数据库配置

### 配置文件结构

在 `appsettings.json` 中配置数据库连接：

```json
{
  "MainDb": "Main",
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 2,
      "Enabled": true,
      "ConnectionString": "Radish.db"
    },
    {
      "ConnId": "Log",
      "DbType": 2,
      "Enabled": true,
      "ConnectionString": "Radish.Log.db"
    }
  ]
}
```

**配置说明**：

| 字段 | 说明 | 示例 |
|-----|------|------|
| `MainDb` | 主数据库标识 | "Main" |
| `ConnId` | 连接标识(唯一) | "Main", "Log" |
| `DbType` | 数据库类型 | 2=SQLite, 4=PostgreSQL |
| `Enabled` | 是否启用 | true/false |
| `ConnectionString` | 连接字符串 | "Radish.db" |

**数据库类型对照表**：

```csharp
MySql = 0
SqlServer = 1
Sqlite = 2
Oracle = 3
PostgreSql = 4
DaMeng = 5      // 达梦
Kdbndp = 6      // 金仓
```

## ConfigId 大小写规则

### 重要说明

**SqlSugar 在初始化时会自动将所有 ConfigId 转换为小写**。这是一个关键的设计决策,需要特别注意。

### 转换机制

在 `SqlSugarSetup.cs` 中：

```csharp
BaseDbConfig.MutiConnectionString.allDbs.ForEach(m =>
{
    var config = new ConnectionConfig()
    {
        ConfigId = m.ConnId.ObjToString().ToLower(),  // 自动转换为小写
        ConnectionString = m.ConnectionString,
        DbType = (DbType)m.DbType,
        // ...
    };

    BaseDbConfig.AllConfigs.Add(config);
});
```

### 实际影响

| 位置 | 值 | 说明 |
|------|-----|------|
| appsettings.json | `"ConnId": "Log"` | 配置文件中使用大写 |
| SqlSugar 内部 | `"log"` | 自动转换为小写 |
| 代码中获取连接 | `"log"` | 必须使用小写 |

### 正确用法

```csharp
// ✅ 正确 - 使用小写
var logDb = ((SqlSugarScope)_db).GetConnectionScope("log");

// ✅ 正确 - 使用常量 + ToLower()
var logDb = ((SqlSugarScope)_db).GetConnectionScope(SqlSugarConst.LogConfigId.ToLower());

// ❌ 错误 - 使用大写会报错
var logDb = ((SqlSugarScope)_db).GetConnectionScope("Log");
// 错误信息: SqlSugar.SqlSugarException: ConfigId was not found Log
```

### 最佳实践

1. **配置文件**: 保持使用大写 `"Log"` - 更符合约定,易读
2. **代码中**: 使用 `SqlSugarConst.LogConfigId.ToLower()` - 明确表达意图
3. **添加注释**: 在关键位置说明原因

```csharp
// 注意: SqlSugar 在初始化时会将 ConfigId 转换为小写
var logDb = ((SqlSugarScope)_db).GetConnectionScope(SqlSugarConst.LogConfigId.ToLower());
```

## 数据库连接管理

### 连接分类

Radish 项目使用两个独立的数据库：

1. **Main 数据库** (`ConnId="Main"`)
   - 存储业务数据
   - 用户、角色、权限、帖子、评论等
   - 默认连接,大部分实体使用

2. **Log 数据库** (`ConnId="Log"`)
   - 存储日志数据
   - 应用日志、SQL 日志、审计日志
   - 通过 `[Tenant(configId: "Log")]` 属性指定

### 连接初始化

在 `SqlSugarSetup.cs` 中初始化：

```csharp
public static void AddSqlSugarSetup(this IServiceCollection services)
{
    // 读取配置并创建 ConnectionConfig
    BaseDbConfig.MutiConnectionString.allDbs.ForEach(m =>
    {
        var config = new ConnectionConfig()
        {
            ConfigId = m.ConnId.ObjToString().ToLower(),
            ConnectionString = m.ConnectionString,
            DbType = (DbType)m.DbType,
            IsAutoCloseConnection = true,
            // ...
        };

        // Log 数据库单独存储
        if (SqlSugarConst.LogConfigId.ToLower().Equals(m.ConnId.ToLower()))
        {
            BaseDbConfig.LogConfig = config;
        }
        else
        {
            BaseDbConfig.ValidConfig.Add(config);
        }

        BaseDbConfig.AllConfigs.Add(config);
    });

    // 注册 SqlSugarScope 单例
    services.AddSingleton<ISqlSugarClient>(o =>
    {
        return new SqlSugarScope(BaseDbConfig.AllConfigs, db =>
        {
            // 为所有连接配置 AOP
            BaseDbConfig.AllConfigs.ForEach(config =>
            {
                var dbProvider = db.GetConnectionScope((string)config.ConfigId);

                // 只对非 Log 库配置实体数据权限（多租户）
                if (!SqlSugarConst.LogConfigId.ToLower().Equals(config.ConfigId.ToString().ToLower()))
                {
                    RepositorySetting.SetTenantEntityFilter(dbProvider);
                }

                // 配置 SQL 日志 AOP
                dbProvider.Aop.OnLogExecuting = (s, parameters) =>
                {
                    SqlSugarAop.OnLogExecuting(dbProvider, /*...*/);
                };
            });
        });
    });
}
```

### 获取特定数据库连接

```csharp
// 方式1: 通过 ISqlSugarClient 获取
public class MyService
{
    private readonly ISqlSugarClient _db;

    public MyService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task DoWork()
    {
        // 获取 Main 数据库连接
        var mainDb = ((SqlSugarScope)_db).GetConnectionScope("main");

        // 获取 Log 数据库连接
        var logDb = ((SqlSugarScope)_db).GetConnectionScope("log");

        // 使用连接
        await mainDb.Insertable(entity).ExecuteCommandAsync();
    }
}

// 方式2: 通过实体的 [Tenant] 属性自动路由
[Tenant(configId: "Log")]
public class InformationLog : BaseLog
{
    // SqlSugar 会自动使用 Log 数据库连接
}

// 使用时无需手动指定连接
await _db.Insertable(new InformationLog { /*...*/ }).ExecuteCommandAsync();
```

## 多租户配置

### Log 数据库的特殊处理

Log 数据库不需要多租户过滤,因为：
1. 日志数据不属于任何租户
2. 日志需要记录所有租户的操作
3. 避免多租户过滤影响日志完整性

在 `SqlSugarSetup.cs` 中：

```csharp
BaseDbConfig.AllConfigs.ForEach(config =>
{
    var dbProvider = db.GetConnectionScope((string)config.ConfigId);

    // 只对非 Log 库配置实体数据权限（多租户）
    if (!SqlSugarConst.LogConfigId.ToLower().Equals(config.ConfigId.ToString().ToLower()))
    {
        RepositorySetting.SetTenantEntityFilter(dbProvider);
    }
});
```

## 常见问题

### 1. ConfigId was not found

**错误信息**：
```
SqlSugar.SqlSugarException: ConfigId was not found Log
```

**原因**：使用了大写的 ConfigId,但 SqlSugar 内部已转换为小写

**解决方案**：
```csharp
// ❌ 错误
var db = ((SqlSugarScope)_db).GetConnectionScope("Log");

// ✅ 正确
var db = ((SqlSugarScope)_db).GetConnectionScope("log");
```

### 2. 日志数据库未初始化

**错误信息**：
```
ApplicationException: 未配置 Log 库连接
```

**原因**：`appsettings.json` 中缺少 `ConnId="Log"` 的配置

**解决方案**：
```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 2,
      "ConnectionString": "Radish.db"
    },
    {
      "ConnId": "Log",  // 必须存在
      "DbType": 2,
      "ConnectionString": "Radish.Log.db"
    }
  ]
}
```

### 3. 实体路由到错误的数据库

**问题**：实体应该存储在 Log 数据库,但实际存储在 Main 数据库

**原因**：缺少 `[Tenant(configId: "Log")]` 属性

**解决方案**：
```csharp
// ❌ 错误 - 会使用 Main 数据库
public class InformationLog : BaseLog { }

// ✅ 正确 - 使用 Log 数据库
[Tenant(configId: "Log")]
public class InformationLog : BaseLog { }
```

### 4. 多租户过滤影响日志查询

**问题**：查询日志时被多租户过滤,看不到其他租户的日志

**原因**：Log 数据库连接也配置了多租户过滤

**解决方案**：确保 `SqlSugarSetup.cs` 中排除了 Log 数据库：

```csharp
if (!SqlSugarConst.LogConfigId.ToLower().Equals(config.ConfigId.ToString().ToLower()))
{
    RepositorySetting.SetTenantEntityFilter(dbProvider);
}
```

## 生产环境配置

### PostgreSQL 配置示例

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 4,
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish;Username=postgres;Password=your_password"
    },
    {
      "ConnId": "Log",
      "DbType": 4,
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish_log;Username=postgres;Password=your_password"
    }
  ]
}
```

### 连接池配置

```csharp
var config = new ConnectionConfig()
{
    ConfigId = m.ConnId.ObjToString().ToLower(),
    ConnectionString = m.ConnectionString,
    DbType = (DbType)m.DbType,
    IsAutoCloseConnection = true,

    // 连接池配置
    MoreSettings = new ConnMoreSettings()
    {
        IsAutoRemoveDataCache = true,
        SqlServerCodeFirstNvarchar = true,
    }
};
```

## 相关文档

- [日志系统](./logging.md) - 日志数据库使用说明
- [配置管理](./configuration.md) - 数据库配置详解
- [开发规范](/architecture/specifications) - 数据库设计规范

## 参考资源

- [SqlSugar 官方文档](https://www.donet5.com/Home/Doc)
- [SqlSugar 多库配置](https://www.donet5.com/Home/Doc?typeId=1183)
- [SqlSugar 租户配置](https://www.donet5.com/Home/Doc?typeId=1310)
