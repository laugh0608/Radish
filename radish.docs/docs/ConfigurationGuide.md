# 配置管理指南

## 概述

Radish 项目采用多层配置管理策略，以实现开发环境与生产环境的配置隔离，确保敏感数据不会被提交到 Git 仓库。

## 配置文件结构

```
Radish.Api/
├── appsettings.json                    ✅ 提交到 Git（基础配置，占位符）
├── appsettings.Development.json        ✅ 提交到 Git（开发环境特定配置）
├── appsettings.Production.json         ✅ 提交到 Git（生产环境特定配置）
├── appsettings.Local.json              ❌ 不提交（本地敏感数据）
└── appsettings.Local.example.json      ✅ 提交到 Git（配置模板）
```

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

## 快速开始

### 新开发者配置步骤

```bash
# 1. 复制配置模板
cp Radish.Api/appsettings.Local.example.json Radish.Api/appsettings.Local.json

# 2. 编辑 appsettings.Local.json（使用任何文本编辑器）
# 根据你的本地环境修改以下配置项：
#   - Snowflake.WorkId (建议使用 0)
#   - Databases 连接字符串（默认 SQLite 可直接使用）
#   - Redis 配置（默认使用内存缓存，无需 Redis）
#   - AutoMapper.LicenseKey（如有商业许可证）

# 3. 启动项目
dotnet run --project Radish.Api
```

**重要**：`appsettings.Local.json` 已被 Git 忽略，不会被提交到仓库。

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

#### SQLite（默认，适合本地开发）

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 2,                    // 2 = SQLite
      "Enabled": true,
      "ConnectionString": "Radish.db" // 数据库文件名
    },
    {
      "ConnId": "Log",
      "DbType": 2,
      "Enabled": true,
      "HitRate": 50,
      "ConnectionString": "RadishLog.db"
    }
  ]
}
```

数据库文件会自动创建在项目根目录（`/mnt/d/Code/Radish/`）。

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
      "https://localhost:3000",      // Vite 默认端口
      "http://localhost:3000",
      "https://localhost:3000",     // 项目自定义端口
      "http://localhost:3000"
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
- `/docs` → 文档站点（转发到 radish.docs dev 根路径 `http://localhost:3001`，内部 `base=/docs/`）
- `/api` → 后端 API（转发到 `http://localhost:5100`）
- `/scalar` → Scalar API 文档 UI（通过 Gateway 暴露在 `/api/docs`）
- `/console` → 管理控制台前端 `radish.console`（转发到 `http://localhost:3002`）

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
- 详细的反向代理配置请参考 [部署指南](./DeploymentGuide.md)

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
   - 更新 `appsettings.Local.example.json` 让新人快速上手

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

```bash
# 1. 克隆仓库
git clone https://github.com/your-org/Radish.git
cd Radish

# 2. 复制配置模板
cp Radish.Api/appsettings.Local.example.json Radish.Api/appsettings.Local.json

# 3. 如需修改，编辑 appsettings.Local.json
# （默认配置通常可以直接使用）

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
- [开发规范文档](./DevelopmentSpecifications.md)
- [部署指南](./DeploymentGuide.md)

## 变更日志

- **2025-11-27**：初始版本，引入 `appsettings.Local.json` 配置管理策略
