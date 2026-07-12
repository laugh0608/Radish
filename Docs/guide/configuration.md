# 配置管理指南

## 概述

Radish 项目只保留三种配置文件：共享 `appsettings.Shared.json`、宿主默认 `appsettings.json`、本地覆盖 `appsettings.Local.json`。敏感数据不进入 Git，部署差异统一通过环境变量覆盖。运营可调参数与 Console 系统设置的职责边界见 [运行时配置边界与系统设置](/guide/runtime-configuration-boundaries)。

## 配置文件结构

### 解决方案根目录（跨宿主共享）

```
Radish/
└── appsettings.Shared.json               ✅ 提交到 Git（API/Auth/Gateway 共享默认配置）
```

### Radish.Api、Radish.Auth、Radish.Gateway

```
Radish.Api/
├── appsettings.json                       ✅ 提交到 Git（宿主默认配置）
└── appsettings.Local.json                 ❌ 不提交（本地覆盖配置）
```

**重要说明**：

- `appsettings.Shared.json` - 根目录共享默认项，只允许保留这一份共享配置
- `appsettings.json` - 宿主默认配置，提交到 Git，作为稳定模板与默认值来源
- `appsettings.Local.json` - 宿主本地覆盖配置，只写需要覆盖的项，禁止提交到 Git
- **禁止新增或提交** `appsettings.Development.json`、`appsettings.Production.json`、`appsettings.*.json.example` 等额外变体

**配置策略**：
- ✅ **敏感信息放在 Local.json 或环境变量**：数据库密码、Redis 密码、API 密钥、加密密钥等
- ✅ **部署差异使用环境变量**：公开域名、内部服务地址、证书路径、生产密钥等
- ✅ **跨宿主非敏感配置放在 appsettings.Shared.json**：日志级别、`Snowflake.DataCenterId`、`MainDb/Databases`、Redis 开关与连接串默认值等
- ✅ **宿主私有非敏感配置放在各自 appsettings.json**：CORS、端口、服务路由、`Snowflake.WorkId`、`Redis.InstanceName` 等
- ✅ **利用深度合并**：Local.json 只写需要修改的配置项，其他自动继承

## 配置加载优先级

ASP.NET Core 按以下顺序加载配置（后面的会覆盖前面的）：

```
1. appsettings.Shared.json              (跨宿主共享默认值)
   ↓
2. appsettings.json                     (宿主基础配置)
   ↓
3. appsettings.Local.json               (本地覆盖配置，最高优先级)
   ↓
4. 环境变量                              (部署环境推荐，最终覆盖)
```

### 配置合并机制

ASP.NET Core 配置系统使用**深度合并**策略：

#### 1. 简单值的覆盖

后加载的配置会直接覆盖前加载的配置：

**appsettings.Shared.json**：
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
    "DataCenterId": 0    // ✅ 保留基础配置中的值
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
      "ConnectionString": "Radish.Log.db"
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

方式一：手动创建最小覆盖文件（推荐）

```bash
# 1. 创建本地覆盖文件
#    新建 Radish.Api/appsettings.Local.json
#    新建 Radish.Auth/appsettings.Local.json

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

**Radish.Gateway（默认配置 + 环境变量）**

Gateway 通常直接使用 `appsettings.json`；若本机需要临时覆盖，也应使用未提交的 `appsettings.Local.json`。测试部署与生产部署优先进入 `Deploy/` 目录，通过 `.env` 与默认 `docker-compose.yaml` 注入环境变量：

```bash
# Docker Compose 示例
services:
  radish-gateway:
    image: ${RADISH_IMAGE_REGISTRY}/radish-gateway:${RADISH_IMAGE_TAG:-${RADISH_IMAGE_TRACK:-test}-latest}
    environment:
      - RADISH_PUBLIC_URL=https://your-domain.com
      - GatewayService__PublicUrl=https://your-domain.com
      - DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
      - DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200
      - FrontendService__BaseUrl=https://your-domain.com
```

其中 `RADISH_IMAGE_REGISTRY / RADISH_IMAGE_TRACK` 应由 `Deploy/.env` 提供，默认按 `test-latest` / `release-latest` 拉取 `GHCR` 中已构建好的镜像；需要完全可复现部署时再启用固定 `RADISH_IMAGE_TAG`。测试 / 生产部署不再建议在部署机本地构建镜像。

部署态下，`Api / Auth / Gateway` 三个宿主都会优先根据 `RADISH_PUBLIC_URL` 推导统一的 CORS 允许来源；只有未提供该变量时，才回退到各自 `appsettings.json` 中的开发默认值。

补充说明：

- 开发运行下，`Radish.Api` 继续通过 `Authority=http://localhost:5200` 做 OIDC 元数据发现。
- 测试部署与生产部署下，`Radish.Api` 需要额外读取与 `Radish.Auth` 共享的 signing 证书路径和密码：
  - `OpenIddict__Encryption__SigningCertificatePath`
  - `OpenIddict__Encryption__SigningCertificatePassword`
- `Seed:DeveloperDefaultsEnabled` 控制 `DbMigrate` 是否创建 `system / admin / test` 开发账号和默认密码；只有 `RadishDeployment:Stage=local/test` 时允许开启，生产环境必须保持关闭
- 该证书应与 `Auth` 使用的 signing `.pfx` 保持同源，并以只读方式挂载给 `Api`，用于本地 JWT 验签。

详细配置说明请参考 `Radish.Gateway/README.md`

**重要**：
- `appsettings.Local.json` 已被 Git 忽略，不会被提交到仓库
- **只在 Local.json 中写需要覆盖的配置项**，利用深度合并机制自动继承其他配置
- **敏感信息**（密码、密钥）必须放在 Local.json，**非敏感配置**应保留在 `appsettings.Shared.json` 或宿主 `appsettings.json`
- `appsettings.Shared.json` + 宿主 `appsettings.json` 共同组成默认配置模板
- 禁止把 `appsettings.Development.json`、`appsettings.Production.json` 或其他 `appsettings.*.json` 变体重新引回仓库

## 配置项说明

### 1. Snowflake ID 配置

雪花 ID 算法用于生成分布式唯一 ID。当前推荐：`WorkId` 按宿主配置，`DataCenterId` 放共享配置统一维护。

```json
{
  "Snowflake": {
    "WorkId": 0 // 建议：本地开发=0, 生产服务器1=2, 生产服务器2=3
  }
}
```

**注意事项**：
- 不同环境的 `WorkId` 必须不同，否则可能生成重复 ID
- `DataCenterId` 建议在 `appsettings.Shared.json` 统一配置
- 取值范围：0-31
- 推荐约定：本地开发统一使用 0，生产环境从 2 开始递增

### 2. 数据库配置

**重要说明 - API 和 Auth 项目共享业务数据库**：
- **Radish.Api** 和 **Radish.Auth** 项目使用**相同的业务数据库**（`Radish.db` 和 `Radish.Log.db`）
- 这两个数据库存储用户、角色、权限、租户等业务数据，需要被两个项目共同访问
- **OpenIddict 使用独立的数据库**（`Radish.OpenIddict.db`），由 EF Core 管理，存储 OIDC 认证相关数据（客户端、授权码、令牌等）
- **Hangfire 使用独立的任务存储**（默认 `Radish.Hangfire.db`），由 `Radish.Api` 管理
- **所有数据库文件统一存放在解决方案根目录的 `DataBases/` 文件夹**
- `MainDb`、`Databases` 与 `OpenIddict:Database` 默认配置位于根目录 `appsettings.Shared.json`

`Databases`、`OpenIddict:Database` 与 `Hangfire` 都支持按配置切换 `DbType`：本地默认 `2=SQLite`，部署态通过环境变量覆盖为 `4=PostgreSQL`。OpenIddict 的配置由 `Radish.Api` 与 `Radish.Auth` 共享；Hangfire 配置只由 `Radish.Api` 使用。

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
- `Local.BaseUrl`：底层静态文件暴露前缀，不再作为附件业务真值
- `ImageProcessing.GenerateMultipleSizes`：是否生成多尺寸图片
- `Watermark`：水印配置（默认关闭）
- `Deduplication`：文件去重配置（默认启用）

补充口径：

- 当前附件业务统一以 `attachmentId` 为真值，对外展示统一推荐 `/_assets/attachments/{id}` 与 `/_assets/attachments/{id}/thumbnail`。
- 因此 `Local.BaseUrl=/uploads` 主要影响底层文件暴露与静态存储路径，不应被前端正文或业务表当作长期引用地址写回数据库。

### 2.2 Hangfire 定时任务配置

Hangfire 用于执行后台定时任务（如文件清理），配置位于 `Hangfire` 节点：

```json
{
  "Hangfire": {
    "DbType": 2,
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
- `DbType`：任务存储 provider，`2=SQLite`，`4=PostgreSQL`
- `ConnectionString`：SQLite 时可填写数据库文件名或 `Data Source=...`；PostgreSQL 时填写标准 Npgsql 连接串
- `Dashboard.RoutePrefix`：Dashboard 访问路径
- `FileCleanup.*.Schedule`：Cron 表达式
- `FileCleanup.*.Retention*`：保留周期（天/小时）
- 详细说明见：`/guide/hangfire-scheduled-jobs`

### 2.3 OpenIddict 存储配置

OpenIddict 使用 EF Core 存储，`Radish.Auth` 负责 OIDC Server 与种子数据，`Radish.Api` 复用同一数据库做客户端管理 API。

```json
{
  "OpenIddict": {
    "Database": {
      "DbType": 2,
      "ConnectionString": "Radish.OpenIddict.db"
    }
  }
}
```

**说明**：
- `DbType`：OpenIddict EF Core provider，`2=SQLite`，`4=PostgreSQL`
- `ConnectionString`：SQLite 时默认解析到解决方案根目录 `DataBases/`；PostgreSQL 时填写独立的 OpenIddict 数据库连接串
- 旧的 `ConnectionStrings:OpenIddict` 仍作为兼容入口；新配置应优先使用 `OpenIddict:Database:*`

### 2.4 数据库 provider 示例

#### SQLite（默认，适合本地开发）

`appsettings.Shared.json`：

```json
{
  "MainDb": "Main",
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
      "ConnectionString": "Radish.Log.db" // 日志数据库（API 和 Auth 共享）
    }
  ]
}
```

数据库文件会自动创建在解决方案根目录的 `DataBases/` 文件夹（例如：`/home/luobo/Code/Radish/DataBases/`）。

#### PostgreSQL（生产环境推荐）

`appsettings.Shared.json` 或环境变量覆盖：

```json
{
  "MainDb": "Main",
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

当前采用“部分共享”策略：
- `Redis.Enable` / `Redis.ConnectionString`：放在根目录 `appsettings.Shared.json`
- `Redis.InstanceName`：放在各宿主 `appsettings.json`

#### 方式一：使用内存缓存（默认，无需安装 Redis）

```json
{
  "Redis": {
    "Enable": false,
    "ConnectionString": "localhost:6379,allowAdmin=true"
  }
}
```

宿主差异（以 API 为例）：

```json
{
  "Redis": {
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
    "Enable": true,
    "ConnectionString": "localhost:6379,password=your_password,allowAdmin=true,ssl=true"
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

### 3.1 Time 时间配置（2026-02 新增）

共享配置 `appsettings.Shared.json` 中的 `Time` 配置段用于统一 API、DbMigrate 等宿主的系统业务时区与前端展示格式：

```json
{
  "Time": {
    "DefaultTimeZoneId": "Asia/Shanghai",
    "DisplayFormat": "yyyy-MM-dd HH:mm:ss"
  }
}
```

字段说明：
- `DefaultTimeZoneId`：系统默认时区，建议使用 IANA 时区 ID（如 `Asia/Shanghai`）。
- `DisplayFormat`：默认展示格式（前端按该约定渲染）。

相关接口：
- `GET /api/v1/User/GetTimeSettings`：公开接口，返回系统默认时区与展示格式。
- `GET /api/v1/User/GetMyTimePreference`：登录用户读取个人时区偏好。
- `POST /api/v1/User/UpdateMyTimePreference`：登录用户更新个人时区偏好（支持 IANA/Windows 时区 ID，后端会做规范化）。

实现约束：
- 数据库存储统一使用 UTC；PostgreSQL 写入前由独立的 SqlSugar 参数规范化契约处理 `DateTime` / `DateTimeOffset`，日志开关和测试执行顺序不得改变该行为。
- 前端按“用户偏好时区 > 浏览器时区 > 系统默认时区”回退策略进行展示。

### 3.2 Seed 与首次管理员初始化

`Seed:DeveloperDefaultsEnabled` 是部署安全相关开关：

```json
{
  "RadishDeployment": {
    "Stage": "production"
  },
  "Seed": {
    "DeveloperDefaultsEnabled": false
  }
}
```

规则：

- 默认值为 `false`，此时 `Radish.DbMigrate apply / seed` 只创建角色、权限、Console 授权、论坛 / 商城 / 等级等系统基础数据，不创建 `system / admin / test` 开发账号、默认密码、默认头像或用户角色绑定。
- `RadishDeployment:Stage` 用于区分种子安全边界，当前允许值为 `local / test / production`；未配置或未知阶段不允许创建开发默认账号。
- 当 `Seed:DeveloperDefaultsEnabled=true` 时，`DbMigrate` 只允许在 `RadishDeployment:Stage=local/test` 下继续执行；若阶段为 `production`、未配置或未知值，会直接失败退出，避免生产误开固定账号。
- 本地直接运行 `Radish.DbMigrate` 时，可在仓库根目录或 `Radish.DbMigrate/` 下创建未提交的 `appsettings.Local.json`，设置 `RadishDeployment:Stage=local` 与 `Seed:DeveloperDefaultsEnabled=true` 后再执行 `apply`。
- `DbMigrate` 控制台输出的 `Environment` 是 .NET 宿主环境名；未显式设置 `DOTNET_ENVIRONMENT` 时可能显示 `Production`，但开发默认账号是否允许创建以 `RadishDeployment:Stage` 与 `Seed:DeveloperDefaultsEnabled` 为准。
- 本地容器验证使用 `Deploy/docker-compose.local.yaml`，显式设置 `RadishDeployment__Stage=local` 与 `Seed__DeveloperDefaultsEnabled=true`，保留开发演示账号的开箱体验。
- 受控测试环境如确需固定开发账号，可在 `.env` 中显式设置 `RADISH_DEPLOYMENT_STAGE=test` 与 `RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED=true`；普通测试部署建议继续保持关闭，并通过首个管理员初始化或环境负责人创建测试账号。
- 生产部署必须设置或保持 `RADISH_DEPLOYMENT_STAGE=production` 与 `RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED=false`；生产环境严禁开启开发默认用户种子。
- Auth 登录页不再展示任何内置测试账号提示；即使本地或受控测试环境显式创建了开发演示账号，也应通过文档或人工约定获得账号信息。
- OpenIddict 官方客户端种子不受该开关影响；`radish-client / radish-console / radish-scalar` 与 `radish-api scope` 仍由 `Radish.Auth` 启动时维护，并跟随 `RADISH_PUBLIC_URL` / Issuer 更新回调地址。

新测试 / 生产环境首次部署后，当系统检测到还没有 `System / Admin` 管理员时，前端会进入首个管理员初始化页。`v26.7.1.1204-release` 当前由聊天、通知、圈子等已接入 `BootstrapGate` 的入口触发检查；公开根入口与 Workbench 的统一顶层门禁已进入发布后维护计划。初始化页要求部署人员输入展示名、邮箱和强密码，后端会做服务端校验和并发保护。已有管理员后，初始化接口不可再创建账号。

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

在本地开发环境下，Gateway 通过 YARP 暴露若干常用路由（默认规则位于 `Radish.Gateway/appsettings.json`，如需本机差异可放到未提交的 `appsettings.Local.json`）：

- `/` → 前端 webOS（转发到 `http://localhost:3000`）
- 固定文档源统一位于仓库 `Docs/`；API 启动后会自动同步到 WebOS“文档”应用
- `/api` → 后端 API（转发到 `http://localhost:5100`）
- `/scalar` → Scalar API 文档 UI（Gateway 直接代理到 Radish.Api 的 `/scalar`，`/api/docs` 仅作为旧路径重定向到 `/scalar`）
- `/console` → 管理控制台前端 `radish.console`（转发到 `http://localhost:3100`）

**配置说明**：
- `GatewayService.PublicUrl`：Gateway 自身的公开访问地址（用于门户页面展示）
- `DownstreamServices.ApiService.BaseUrl`：API 服务的访问地址
- `DownstreamServices.ApiService.HealthCheckPath`：API 健康检查端点路径
- `FrontendService.BaseUrl`：前端应用的访问地址；公开 head snapshot 细节见 [公开内容 SEO 与分享基线](/frontend/public-seo-sharing)

#### 生产环境配置

Gateway 在测试 / 生产部署中推荐复制 `Deploy/.env.example` 为 `Deploy/.env`，并在 `Deploy/` 目录直接执行 `docker compose`，而不是额外维护单独的 Gateway 生产示例 JSON。典型变量如下：

```bash
RADISH_PUBLIC_URL=https://radish.com
RADISH_DEPLOYMENT_STAGE=production
RADISH_SEED_DEVELOPER_DEFAULTS_ENABLED=false
GatewayService__PublicUrl=https://radish.com
FrontendService__BaseUrl=https://radish.com
DownstreamServices__ApiService__BaseUrl=http://api:5100
DownstreamServices__AuthService__BaseUrl=http://auth:5200
GatewayRuntime__EnableHttpsRedirection=false
```

**注意事项**：
- 生产环境 `DownstreamServices.ApiService.BaseUrl` 使用内网地址（如 Docker 容器名 `http://api:5100`）
- 公开地址（`GatewayService__PublicUrl` / `RADISH_PUBLIC_URL`）使用真实外部域名
- 不要再通过 `Cors__AllowedOrigins__0` 之类的单索引环境变量去覆盖部署态 CORS；.NET 会按索引合并数组，容易把旧的 `localhost` 端口残留进最终白名单
- 外部 `Nginx` 反向代理会将公网 HTTPS 请求转发到容器内 HTTP 端口
- 测试与生产使用同一套部署态 Compose，差异主要是 `RADISH_IMAGE_TRACK` / 可选固定 `RADISH_IMAGE_TAG` 与 `RADISH_PUBLIC_URL`
- 详细的反向代理配置请参考 [部署指南](/deployment/guide)

## 配置示例

### 示例 1：本地开发（SQLite + 内存缓存）

`appsettings.Local.json`：
```json
{
  "Snowflake": {
    "WorkId": 0
  }
}
```

这是最简单的配置，Redis 默认值直接继承 `appsettings.Shared.json`（内存缓存）。

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
  - OpenIddict__Database__DbType=4
  - OpenIddict__Database__ConnectionString=Host=postgres-prod;Port=5432;Database=radish_openiddict;Username=radish_user;Password=${DB_PASSWORD}
  - Hangfire__DbType=4
  - Hangfire__ConnectionString=Host=postgres-prod;Port=5432;Database=radish_hangfire;Username=radish_user;Password=${DB_PASSWORD}
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

Radish 宿主在 `ConfigureAppConfiguration` 中显式按顺序加载配置：

```csharp
config.Sources.Clear();
config.AddJsonFile("appsettings.Shared.json", optional: true, reloadOnChange: false);
config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
config.AddEnvironmentVariables();
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
   - `appsettings.Shared.json` 维护跨宿主共享默认项
   - 各宿主 `appsettings.json` 维护宿主私有默认项
   - 添加新配置项时，务必同步补充注释说明

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

### Q0: appsettings.Local.json 一定会覆盖 Shared/宿主配置吗？

**是的。** `appsettings.Local.json` 在 `appsettings.Shared.json`、`appsettings.json` 之后加载；若同名键存在，会覆盖前面的值（最终仍可被环境变量覆盖）。

**配置加载代码**（在 `Program.cs` 中）：
```csharp
config.AddJsonFile("appsettings.Shared.json", optional: true, reloadOnChange: false);
config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);  // 最后加载，最高优先级
```

**示例验证**：

1. **appsettings.Shared.json**（共享默认配置）：
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
- ✅ 对于对象，会进行深度合并（未指定的字段保留已加载配置中的值）
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
    image: ${RADISH_API_IMAGE}
    environment:
      - Snowflake__WorkId=2
      - Redis__Enable=true
      - Redis__ConnectionString=${REDIS_URL}
    env_file:
      - Deploy/.env  # 部署态敏感数据由 Compose 自动加载（不提交到 Git）
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

# 2. 创建本地覆盖文件
#    新建 Radish.Api/appsettings.Local.json
#    新建 Radish.Auth/appsettings.Local.json
#    新建 Radish.Gateway/appsettings.Local.json

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

- **2026-05-07**：
  - 正式收口为三种配置文件：`appsettings.Shared.json`、`appsettings.json`、`appsettings.Local.json`
  - 移除 `appsettings.{Environment}.json` 口径，部署差异统一改为环境变量
  - 明确禁止提交 `appsettings.Local.json` 与其他 `appsettings.*.json` 变体
- **2025-12-08**：
  - 优化配置策略：Local.json 只需包含需要覆盖的配置项，利用深度合并自动继承其他配置
  - 补充配置优先级和合并机制说明
  - 简化配置文件结构，`appsettings.json` 现作为完整配置模板
- **2025-11-27**：初始版本，引入 `appsettings.Local.json` 配置管理策略
