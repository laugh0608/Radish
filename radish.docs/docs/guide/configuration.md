# 配置管理指南

## 概述

Radish 项目采用多层配置管理策略，以实现开发环境与生产环境的配置隔离，确保敏感数据不会被提交到 Git 仓库。

## 配置文件结构

### Radish.Api 和 Radish.Auth（包含敏感信息）

```
Radish.Api/
├── appsettings.json                       ✅ 提交到 Git（完整配置模板，包含默认值和注释）
├── appsettings.Development.json           ✅ 提交到 Git（开发环境特定配置，可选）
├── appsettings.Production.json            ✅ 提交到 Git（生产环境特定配置，可选）
├── appsettings.Local.json.example         ✅ 提交到 Git（精简配置示例，仅包含常见敏感项）
└── appsettings.Local.json                 ❌ 不提交（本地敏感数据）
```

### Radish.Gateway（无敏感信息）

```
Radish.Gateway/
├── appsettings.json                       ✅ 提交到 Git（完整配置，适用于开发和生产）
├── appsettings.Development.json           ✅ 提交到 Git（可选）
├── appsettings.Production.json            ✅ 提交到 Git（可选）
└── README.md                              ✅ 配置说明（使用环境变量覆盖配置）
```

**重要说明**：

**Radish.Api 和 Radish.Auth**：
- `appsettings.json` - 完整的配置模板，包含所有配置项和详细注释，作为默认配置使用
- `appsettings.Local.json.example` - **精简的配置示例**，仅包含常见需要修改的敏感配置项（推荐使用）
- `appsettings.Local.json` - **只需包含你想要覆盖的配置项**，其他配置会自动继承 `appsettings.json`

**Radish.Gateway**：
- `appsettings.json` - 完整配置，开发和生产环境均可使用
- **不需要 `appsettings.Local.json`** - Gateway 没有敏感信息（无数据库密码、API 密钥）
- 生产环境通过**环境变量**覆盖配置（PublicUrl、服务地址等）

**配置策略**：
- ✅ **敏感信息放在 Local.json**（仅 API 和 Auth）：数据库密码、Redis 密码、API 密钥、加密密钥等
- ✅ **Gateway 使用环境变量**：公开域名、内部服务地址等非敏感配置
- ✅ **非敏感配置放在 appsettings.json**：CORS 地址、日志级别、默认端口、功能开关等
- ✅ **利用深度合并**：Local.json 只写需要修改的配置项，其他自动继承

## 配置加载优先级

ASP.NET Core 按以下顺序加载配置（后面的会覆盖前面的）：

```
1. appsettings.json                     (基础结构，默认值)
   ↓
2. appsettings.{Environment}.json       (环境特定配置)
   ↓
3. appsettings.Local.json               (本地敏感数据，最高优先级)
   ↓
4. 环境变量                              (可选，生产环境推荐)
```

### 配置合并机制

ASP.NET Core 配置系统使用**深度合并**策略：

#### 1. 简单值的覆盖

后加载的配置会直接覆盖前加载的配置：

**appsettings.json**：
```json
{
  "Redis": {
    "Enable": false,
    "ConnectionString": "localhost:6379"
  }
}
```

**appsettings.Local.json**：
```json
{
  "Redis": {
    "Enable": true,
    "ConnectionString": "production-redis:6379,password=secret"
  }
}
```

**结果**：
```json
{
  "Redis": {
    "Enable": true,  // ✅ 被 Local 覆盖
    "ConnectionString": "production-redis:6379,password=secret"  // ✅ 被 Local 覆盖
  }
}
```

#### 2. 对象的深度合并

如果只在 Local.json 中覆盖部分字段，未覆盖的字段会保留原值：

**appsettings.json**：
```json
{
  "Snowflake": {
    "WorkId": 0,
    "DataCenterId": 0
  }
}
```

**appsettings.Local.json**（只覆盖 WorkId）：
```json
{
  "Snowflake": {
    "WorkId": 2
  }
}
```

**结果**：
```json
{
  "Snowflake": {
    "WorkId": 2,         // ✅ 被 Local 覆盖
    "DataCenterId": 0    // ✅ 保留 appsettings.json 的值
  }
}
```

#### 3. 数组的索引覆盖

数组配置按**索引位置**进行覆盖（不是追加）：

**appsettings.json**：
```json
{
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
      "ConnectionString": "RadishLog.db"
    }
  ]
}
```

**appsettings.Local.json**（切换到 PostgreSQL）：
```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 4,
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish;Username=postgres;Password=mypassword"
    },
    {
      "ConnId": "Log",
      "DbType": 4,
      "Enabled": true,
      "HitRate": 50,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish_log;Username=postgres;Password=mypassword"
    }
  ]
}
```

**结果**：完全使用 Local.json 中的 PostgreSQL 配置，覆盖默认的 SQLite 配置。

**重要提示**：如果要修改数组中的某个元素，建议提供完整的数组配置，以避免配置混淆。

### 验证配置优先级

可以在启动时打印配置值来验证加载顺序：

**Program.cs**（开发环境调试用）：
```csharp
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var dbType = app.Configuration["Databases:0:DbType"];
    var connStr = app.Configuration["Databases:0:ConnectionString"];
    var redisEnabled = app.Configuration["Redis:Enable"];

    app.Logger.LogInformation("=== 配置加载验证 ===");
    app.Logger.LogInformation("Database Type: {DbType}", dbType);
    app.Logger.LogInformation("Database Connection: {ConnStr}", connStr);
    app.Logger.LogInformation("Redis Enabled: {RedisEnabled}", redisEnabled);
    app.Logger.LogInformation("====================");
}
```

## 快速开始

### 新开发者配置步骤

**最简单的方式（推荐）**：

```bash
# 直接启动项目，使用默认 SQLite 配置（开箱即用）
dotnet run --project Radish.Api
dotnet run --project Radish.Auth
dotnet run --project Radish.Gateway
```

默认配置已经启用 SQLite 数据库和内存缓存，无需任何额外配置即可运行。

**如需自定义配置（可选）**：

**Radish.Api 和 Radish.Auth（使用 Local.json）**

方式一：使用精简配置示例（推荐）

```bash
# 1. 复制精简配置示例（仅包含常见的敏感配置项）
cp Radish.Api/appsettings.Local.json.example Radish.Api/appsettings.Local.json
cp Radish.Auth/appsettings.Local.json.example Radish.Auth/appsettings.Local.json

# 2. 编辑 appsettings.Local.json
#    取消注释并修改你需要的配置项：
#    - 数据库密码（切换到 PostgreSQL 时）
#    - Redis 密码（启用 Redis 时）
#    - Snowflake.WorkId（如需修改）
#    - API 密钥和其他敏感信息
#
#    未指定的配置会自动继承 appsettings.json 中的默认值！

# 3. 启动项目
dotnet run --project Radish.Api
dotnet run --project Radish.Auth
```

方式二：从头开始写（适合高级用户）

```bash
# 1. 创建空的 appsettings.Local.json
touch Radish.Api/appsettings.Local.json

# 2. 只添加你想要覆盖的配置项
#    参考 appsettings.json 中的完整配置和注释说明
#    例如只修改数据库密码：
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 4,
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish;Username=postgres;Password=mypassword"
    },
    {
      "ConnId": "Log",
      "DbType": 4,
      "Enabled": true,
      "HitRate": 50,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish_log;Username=postgres;Password=mypassword"
    }
  ]
}
```

**Radish.Gateway（使用环境变量）**

Gateway 不需要 `appsettings.Local.json`，生产环境通过环境变量覆盖配置：

```bash
# Docker Compose 示例
services:
  radish-gateway:
    image: radish-gateway:latest
    environment:
      - GatewayService__PublicUrl=https://your-domain.com
      - Cors__AllowedOrigins__0=https://your-frontend-domain.com
      - DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
      - DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200
```

详细配置说明请参考 `Radish.Gateway/README.md`

**重要**：
- `appsettings.Local.json` 已被 Git 忽略，不会被提交到仓库
- **只在 Local.json 中写需要覆盖的配置项**，利用深度合并机制自动继承其他配置
- **敏感信息**（密码、密钥）必须放在 Local.json，**非敏感配置**（CORS、日志级别）应保留在 appsettings.json
- `appsettings.Local.json.example` 提供了精简的配置模板，仅包含常见需要修改的项
- `appsettings.json` 包含完整的配置说明和示例，可作为参考

## 配置项说明

### 1. Snowflake ID 配置

雪花 ID 算法用于生成分布式唯一 ID，每个部署实例必须有唯一的 WorkId。

```json
{
  "Snowflake": {
    "WorkId": 0,          // 建议：本地开发=0, 生产服务器1=2, 生产服务器2=3
    "DataCenterId": 0     // 数据中心 ID，通常保持 0
  }
}
```

**注意事项**：
- 不同环境的 `WorkId` 必须不同，否则可能生成重复 ID
- 取值范围：0-31
- 推荐约定：本地开发统一使用 0，生产环境从 2 开始递增

### 2. 数据库配置

**重要说明 - API 和 Auth 项目共享业务数据库**：
- **Radish.Api** 和 **Radish.Auth** 项目使用**相同的业务数据库**（`Radish.db` 和 `RadishLog.db`）
- 这两个数据库存储用户、角色、权限、租户等业务数据，需要被两个项目共同访问
- **OpenIddict 使用独立的数据库**（`RadishAuth.OpenIddict.db`），由 EF Core 管理，存储 OIDC 认证相关数据（客户端、授权码、令牌等）
- **所有数据库文件统一存放在解决方案根目录的 `DataBases/` 文件夹**

### 2.1 文件存储配置（FileStorage）

文件上传相关的所有配置集中在 `FileStorage` 节点中，包含存储后端、图片处理、水印、去重等选项。

**核心配置示例**：
```json
{
  "FileStorage": {
    "Type": "Local",
    "MaxFileSize": {
      "Avatar": 2097152,
      "Image": 5242880,
      "Document": 31457280,
      "Video": 52428800,
      "Audio": 10485760
    },
    "AllowedExtensions": {
      "Image": [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg"],
      "Document": [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md"],
      "Video": [".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm"],
      "Audio": [".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma"]
    },
    "Local": {
      "BasePath": "DataBases/Uploads",
      "BaseUrl": "/uploads"
    },
    "ImageProcessing": {
      "GenerateThumbnail": true,
      "GenerateMultipleSizes": false,
      "Sizes": {
        "Small": { "Width": 400, "Height": 300 },
        "Medium": { "Width": 800, "Height": 600 },
        "Large": { "Width": 1200, "Height": 900 }
      },
      "CompressQuality": 85,
      "RemoveExif": true
    },
    "Watermark": {
      "Enable": false,
      "Type": "Text",
      "Text": {
        "Content": "Radish",
        "Position": "BottomRight",
        "FontSize": 24,
        "FontSizeRelative": 0.05,
        "Color": "#FFFFFF",
        "Opacity": 0.5
      }
    },
    "Deduplication": {
      "Enable": true,
      "HashAlgorithm": "SHA256"
    }
  }
}
```

**说明**：
- `Type`：存储后端类型（`Local`/`MinIO`/`OSS`）
- `Local.BasePath`：本地文件存储根目录（相对于仓库根目录）
- `Local.BaseUrl`：对外访问 URL 前缀
- `ImageProcessing.GenerateMultipleSizes`：是否生成多尺寸图片
- `Watermark`：水印配置（默认关闭）
- `Deduplication`：文件去重配置（默认启用）

### 2.2 Hangfire 定时任务配置

Hangfire 用于执行后台定时任务（如文件清理），配置位于 `Hangfire` 节点：

```json
{
  "Hangfire": {
    "ConnectionString": "Data Source=DataBases/Radish.Hangfire.db",
    "Dashboard": {
      "Enable": true,
      "RoutePrefix": "/hangfire",
      "AllowLocalOnly": true
    },
    "FileCleanup": {
      "DeletedFiles": {
        "Enable": true,
        "Schedule": "0 3 * * *",
        "RetentionDays": 30
      },
      "TempFiles": {
        "Enable": true,
        "Schedule": "0 * * * *",
        "RetentionHours": 2
      },
      "RecycleBin": {
        "Enable": true,
        "Schedule": "0 4 * * *",
        "RetentionDays": 90
      },
      "OrphanAttachments": {
        "Enable": true,
        "Schedule": "0 5 * * *",
        "RetentionHours": 24
      }
    }
  }
}
```

**说明**：
- `Dashboard.RoutePrefix`：Dashboard 访问路径
- `FileCleanup.*.Schedule`：Cron 表达式
- `FileCleanup.*.Retention*`：保留周期（天/小时）
- 详细说明见：`/guide/hangfire-scheduled-jobs`

#### SQLite（默认，适合本地开发）

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 2,                    // 2 = SQLite
      "Enabled": true,
      "ConnectionString": "Radish.db" // 数据库文件名（API 和 Auth 共享）
    },
    {
      "ConnId": "Log",
      "DbType": 2,
      "Enabled": true,
      "HitRate": 50,
      "ConnectionString": "RadishLog.db" // 日志数据库（API 和 Auth 共享）
    }
  ]
}
```

数据库文件会自动创建在解决方案根目录的 `DataBases/` 文件夹（例如：`/home/luobo/Code/Radish/DataBases/`）。

#### PostgreSQL（生产环境推荐）

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 4,                    // 4 = PostgreSQL
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish;Username=postgres;Password=your_password"
    },
    {
      "ConnId": "Log",
      "DbType": 4,
      "Enabled": true,
      "HitRate": 50,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish_log;Username=postgres;Password=your_password"
    }
  ]
}
```

**DbType 枚举对照表**：
```
0 = MySql
1 = SqlServer
2 = Sqlite
3 = Oracle
4 = PostgreSql
5 = DaMeng (达梦)
6 = Kdbndp (金仓)
```

### 3. Redis 配置

#### 方式一：使用内存缓存（默认，无需安装 Redis）

```json
{
  "Redis": {
    "Enable": false,                               // 禁用 Redis，使用内存缓存
    "ConnectionString": "localhost:6379,allowAdmin=true",
    "InstanceName": "Radish"
  }
}
```

适合：
- 本地开发
- 单机部署
- 不需要跨进程缓存共享

#### 方式二：使用 Redis（生产环境推荐）

```json
{
  "Redis": {
    "Enable": true,                                // 启用 Redis
    "ConnectionString": "localhost:6379,password=your_password,allowAdmin=true",
    "InstanceName": "Radish"
  }
}
```

适合：
- 生产环境
- 多实例部署（负载均衡）
- 需要缓存持久化

**连接字符串参数说明**：
- `password=xxx`：Redis 密码
- `allowAdmin=true`：允许执行管理命令
- `ssl=true`：启用 SSL 加密（远程连接推荐）
- `abortConnect=false`：连接失败时不抛异常
- `connectTimeout=5000`：连接超时（毫秒）

### 4. AutoMapper 许可证

如果你购买了 AutoMapper 的商业许可证：

```json
{
  "AutoMapper": {
    "LicenseKey": "your-license-key-here"
  }
}
```

如果没有许可证，保持空字符串即可（开源项目通常使用免费版）。

### 5. CORS 配置

前端开发服务器地址，根据实际端口调整：

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000"      // Vite 本地开发默认端口（HTTP）
      // 如需在本地启用 HTTPS dev，可按需追加 "https://localhost:3000"
    ]
  }
}
```

### 6. Gateway 配置（仅 Radish.Gateway 项目）

Gateway 门户页面需要配置服务的公开访问地址，用于页面展示和链接跳转。

#### 开发环境配置

`appsettings.json`：

```json
{
  "GatewayService": {
    "PublicUrl": "https://localhost:5000"
  },
  "DownstreamServices": {
    "ApiService": {
      "BaseUrl": "http://localhost:5100",
      "HealthCheckPath": "/health"
    }
  },
  "FrontendService": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

在本地开发环境下，Gateway 还通过 YARP 暴露若干常用路由（具体规则在 `Radish.Gateway/appsettings.Local.json` 中配置）：

- `/` → 前端 webOS（转发到 `http://localhost:3000`）
- `/docs` → 文档站点（转发到 radish.docs dev 根路径 `http://localhost:4000`，内部 `base=/docs/`）
- `/api` → 后端 API（转发到 `http://localhost:5100`）
- `/scalar` → Scalar API 文档 UI（Gateway 直接代理到 Radish.Api 的 `/scalar`，`/api/docs` 仅作为旧路径重定向到 `/scalar`）
- `/console` → 管理控制台前端 `radish.console`（转发到 `http://localhost:3100`）

**配置说明**：
- `GatewayService.PublicUrl`：Gateway 自身的公开访问地址（用于门户页面展示）
- `DownstreamServices.ApiService.BaseUrl`：API 服务的访问地址
- `DownstreamServices.ApiService.HealthCheckPath`：API 健康检查端点路径
- `FrontendService.BaseUrl`：前端应用的访问地址

#### 生产环境配置

参考 `appsettings.Production.example.json`：

```json
{
  "GatewayService": {
    "PublicUrl": "https://radish.com"
  },
  "DownstreamServices": {
    "ApiService": {
      "BaseUrl": "http://api:5100",           // 内网地址，反代到 HTTP 端口
      "HealthCheckPath": "/health"
    }
  },
  "FrontendService": {
    "BaseUrl": "https://app.radish.com"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://app.radish.com",
      "https://www.radish.com"
    ]
  }
}
```

**注意事项**：
- 生产环境 `DownstreamServices.ApiService.BaseUrl` 使用内网地址（如 Docker 容器名 `http://api:5100`）
- 公开地址（`PublicUrl`）使用域名，配合反向代理使用
- Nginx 反向代理会将公网 HTTPS 请求转发到内网 HTTP 端口
- 详细的反向代理配置请参考 [部署指南](/deployment/guide)

## 配置示例

### 示例 1：本地开发（SQLite + 内存缓存）

`appsettings.Local.json`：
```json
{
  "Snowflake": {
    "WorkId": 0
  },
  "Redis": {
    "Enable": false
  }
}
```

这是最简单的配置，使用默认的 SQLite 数据库和内存缓存。

### 示例 2：本地开发（PostgreSQL + Redis）

`appsettings.Local.json`：
```json
{
  "Snowflake": {
    "WorkId": 0
  },
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 4,
      "Enabled": true,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish_dev;Username=postgres;Password=dev123"
    },
    {
      "ConnId": "Log",
      "DbType": 4,
      "Enabled": true,
      "HitRate": 50,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish_log_dev;Username=postgres;Password=dev123"
    }
  ],
  "Redis": {
    "Enable": true,
    "ConnectionString": "localhost:6379,password=dev123,allowAdmin=true"
  }
}
```

### 示例 3：生产环境（使用环境变量）

生产环境推荐使用环境变量（Docker/K8s）：

```bash
# Docker Compose 示例
environment:
  - Snowflake__WorkId=2
  - Databases__0__ConnectionString=Host=postgres-prod;Port=5432;Database=radish;Username=radish_user;Password=${DB_PASSWORD}
  - Databases__1__ConnectionString=Host=postgres-prod;Port=5432;Database=radish_log;Username=radish_user;Password=${DB_PASSWORD}
  - Redis__Enable=true
  - Redis__ConnectionString=redis-prod:6379,password=${REDIS_PASSWORD},ssl=true
  - AutoMapper__LicenseKey=${AUTOMAPPER_LICENSE}
```

环境变量使用双下划线 `__` 表示嵌套层级。

## 兼容性说明

### 现有代码无需修改

配置改造完全兼容现有的 `AppSettings` 扩展类：

```csharp
// ✅ 这些调用完全不受影响
var workId = AppSettings.RadishApp("Snowflake", "WorkId");
var connStr = AppSettings.RadishApp("Databases", "0", "ConnectionString");
var redisEnabled = AppSettings.RadishApp("Redis", "Enable");

// ✅ IOptions 模式也正常工作
public class MyService(IOptions<RedisOptions> redisOptions)
{
    private readonly RedisOptions _redis = redisOptions.Value;
}
```

### Program.cs 中的配置加载

ASP.NET Core 会自动加载 `appsettings.Local.json`（如果存在），无需额外代码。

如果想让代码更明确，可以添加：

```csharp
var builder = WebApplication.CreateBuilder(args);

// 可选：显式加载 appsettings.Local.json
builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,         // 文件不存在也不报错
    reloadOnChange: true    // 文件修改时自动重新加载
);
```

## 安全最佳实践

### ✅ 应该做的

1. **始终使用 `appsettings.Local.json` 存储敏感数据**
   - 数据库密码
   - Redis 密码
   - API 密钥
   - JWT 密钥

2. **生产环境使用环境变量**
   ```bash
   export Databases__0__ConnectionString="Host=prod-db;..."
   ```

3. **定期检查 Git 历史**
   ```bash
   # 确保没有敏感数据被提交
   git log --all --full-history -- "**/appsettings*.json"
   ```

4. **团队共享配置模板**
   - `appsettings.json` 已作为完整的配置模板
   - 添加新配置项时，务必在 `appsettings.json` 中添加注释说明

### ❌ 不应该做的

1. **不要提交 `appsettings.Local.json`**
   - 该文件已被 `.gitignore` 忽略
   - 如果 `git status` 显示该文件，说明配置错误

2. **不要在 `appsettings.json` 中写入真实密码**
   - 该文件会被提交到 Git
   - 使用占位符或空字符串

3. **不要在代码中硬编码敏感数据**
   ```csharp
   // ❌ 错误示例
   var password = "my-secret-password";

   // ✅ 正确示例
   var password = _configuration["Databases:0:Password"];
   ```

## 常见问题

### Q0: appsettings.Local.json 一定会覆盖 appsettings.json 吗？

**是的！** `appsettings.Local.json` 是最后加载的配置文件（除了环境变量），具有最高优先级。

**配置加载代码**（在 `Program.cs` 中）：
```csharp
config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
config.AddJsonFile($"appsettings.{Environment}.json", optional: true, reloadOnChange: false);
config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);  // 最后加载，最高优先级
```

**示例验证**：

1. **appsettings.json**（默认配置）：
```json
{
  "Redis": {
    "Enable": false,
    "ConnectionString": "localhost:6379"
  }
}
```

2. **appsettings.Local.json**（你的本地配置）：
```json
{
  "Redis": {
    "Enable": true,
    "ConnectionString": "production:6379,password=secret"
  }
}
```

3. **最终生效的配置**：
```json
{
  "Redis": {
    "Enable": true,                                           // ✅ 来自 Local.json
    "ConnectionString": "production:6379,password=secret"     // ✅ 来自 Local.json
  }
}
```

**注意事项**：
- ✅ 对于简单值（字符串、数字、布尔值），Local.json 的值会完全覆盖
- ✅ 对于对象，会进行深度合并（未指定的字段保留 appsettings.json 的值）
- ⚠️ 对于数组（如 `Databases`），建议提供完整的数组配置，避免索引混淆

**验证方法**：
```csharp
// 在 Program.cs 中添加（开发环境）
if (app.Environment.IsDevelopment())
{
    app.Logger.LogInformation("Redis Enabled: {Value}",
        app.Configuration["Redis:Enable"]);
}
```

### Q1: 如何验证配置是否生效？

在 `Program.cs` 中添加调试代码：

```csharp
var app = builder.Build();

// 开发环境打印配置信息（生产环境请移除）
if (app.Environment.IsDevelopment())
{
    var workId = app.Configuration["Snowflake:WorkId"];
    var redisEnabled = app.Configuration["Redis:Enable"];
    app.Logger.LogInformation("Snowflake WorkId: {WorkId}", workId);
    app.Logger.LogInformation("Redis Enabled: {Enabled}", redisEnabled);
}
```

### Q2: 如何在不同分支使用不同配置？

`appsettings.Local.json` 不在 Git 管理中，所以切换分支不会影响本地配置。每个开发者维护自己的本地配置文件。

### Q3: 如何在 CI/CD 中配置？

#### GitHub Actions 示例：

```yaml
- name: Run tests
  run: dotnet test
  env:
    Snowflake__WorkId: 1
    Redis__Enable: false
    Databases__0__ConnectionString: "Data Source=:memory:"
```

#### Docker 示例：

```yaml
services:
  radish-api:
    image: radish-api:latest
    environment:
      - Snowflake__WorkId=2
      - Redis__Enable=true
      - Redis__ConnectionString=${REDIS_URL}
    env_file:
      - .env.production  # 敏感数据从文件加载（不提交到 Git）
```

### Q4: 团队成员如何快速配置？

新成员加入时：

**方式一：直接运行（最简单）**
```bash
# 1. 克隆仓库
git clone https://github.com/your-org/Radish.git
cd Radish

# 2. 直接运行（使用默认 SQLite 配置）
dotnet run --project Radish.Api
dotnet run --project Radish.Auth
dotnet run --project Radish.Gateway
```

**方式二：自定义配置（可选）**
```bash
# 1. 克隆仓库
git clone https://github.com/your-org/Radish.git
cd Radish

# 2. 复制精简配置示例
cp Radish.Api/appsettings.Local.json.example Radish.Api/appsettings.Local.json
cp Radish.Auth/appsettings.Local.json.example Radish.Auth/appsettings.Local.json
cp Radish.Gateway/appsettings.Local.json.example Radish.Gateway/appsettings.Local.json

# 3. 编辑 appsettings.Local.json，取消注释并修改需要的配置项
#    - 数据库密码（切换到 PostgreSQL 时）
#    - Redis 密码（启用 Redis 时）
#    - 其他敏感信息
#
#    所有配置项都有详细注释说明

# 4. 启动项目
dotnet run --project Radish.Api
```

### Q5: 如果误提交了敏感数据怎么办？

```bash
# 1. 从 Git 历史中完全删除文件
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch Radish.Api/appsettings.Local.json" \
  --prune-empty --tag-name-filter cat -- --all

# 2. 强制推送（危险操作，需要团队协调）
git push origin --force --all

# 3. 立即更换泄露的密码/密钥
```

**注意**：这是最后的补救措施，预防才是关键。

## 相关文档

- [ASP.NET Core 配置系统](https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/configuration/)
- [开发规范](/architecture/specifications)
- [部署指南](/deployment/guide)

## 变更日志

- **2025-12-08**：
  - 优化配置策略：Local.json 只需包含需要覆盖的配置项，利用深度合并自动继承其他配置
  - 添加 `appsettings.Local.json.example` 精简配置示例，仅包含常见的敏感配置项
  - 补充配置优先级和合并机制说明
  - 简化配置文件结构，移除冗余的 `.Local.example.json`，`appsettings.json` 现作为完整配置模板
- **2025-11-27**：初始版本，引入 `appsettings.Local.json` 配置管理策略
