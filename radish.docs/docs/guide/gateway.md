# Gateway 服务网关

## 概述

Radish.Gateway 是 Radish 项目的统一服务入口,负责提供服务门户页面、路由转发、健康检查聚合等功能。Gateway 采用 ASP.NET Core + YARP 反向代理实现,与 Radish.Api、Radish.Auth 等服务协同工作,为用户提供统一的访问入口。

### 核心职责

1. **服务门户**:提供欢迎页面,展示服务状态、文档入口、API 可视化工具
2. **路由转发**:通过 YARP 反向代理,将请求路由到后端服务(API、Auth、Docs 等)
3. **健康检查**:聚合各服务的健康状态,提供统一的监控入口
4. **统一入口**:为前端、文档站、管理控制台提供统一的访问地址

### 技术栈

- **ASP.NET Core 10**:Web 框架
- **YARP (Yet Another Reverse Proxy)**:反向代理和路由转发
- **Razor Pages**:服务门户页面渲染
- **Serilog**:结构化日志
- **HealthChecks**:健康检查

## 架构设计

### 服务拓扑

```
客户端 (浏览器/前端应用)
    ↓
Radish.Gateway (https://localhost:5000)
    ├─→ /docs/** (文档站 → radish.docs dev 服务 :4000)
    ├─→ /api/** (业务 API → Radish.Api :5100)
    ├─→ /uploads/** (上传文件 → Radish.Api :5100)
    ├─→ /scalar/** (API 文档 → Radish.Api :5100)
    ├─→ /openapi/** (OpenAPI 规范 → Radish.Api :5100)
    ├─→ /hangfire/** (定时任务面板 → Radish.Api :5100)
    ├─→ /console/** (管理控制台 → radish.console :3100)
    ├─→ /Account/** (登录页面 → Radish.Auth :5200)
    ├─→ /connect/** (OIDC 端点 → Radish.Auth :5200)
    └─→ /** (前端应用 → radish.client :3000，最低优先级)
```

### 端口配置

| 服务 | 开发环境端口 | 说明 |
|------|------------|------|
| **Radish.Gateway** | https://localhost:5000<br>http://localhost:5001 | 统一入口,对外暴露 |
| **Radish.Api** | http://localhost:5100 | 内部服务,仅 Gateway 访问 |
| **Radish.Auth** | http://localhost:5200 | 内部服务,仅 Gateway 访问 |
| **radish.client** | http://localhost:3000 | 前端开发服务器 |
| **radish.docs** | http://localhost:4000 | 文档站开发服务器 |
| **radish.console** | http://localhost:3100 | 管理控制台开发服务器 |

**设计原则**:
- Gateway 作为唯一对外入口,使用标准 HTTPS 端口(5000)
- 后端服务(API、Auth)使用内部端口,不直接暴露给客户端
- 前端应用统一通过 Gateway 访问后端服务

## 项目结构

```
Radish.Gateway/
├── Pages/                      # Razor Pages 页面
│   ├── Index.cshtml           # 服务门户首页
│   └── _ViewImports.cshtml    # 视图导入配置
├── wwwroot/                   # 静态资源
│   ├── css/                   # 样式文件
│   │   └── site.css
│   └── js/                    # JavaScript 文件
│       └── health-check.js    # 健康检查脚本
├── appsettings.json           # 基础配置（包含 YARP 路由）
├── Program.cs                 # 应用入口
├── README.md                  # 部署说明
└── Radish.Gateway.csproj      # 项目文件
```

**注意**：Gateway 项目不需要 `appsettings.Local.json`，因为不包含敏感信息。生产环境通过环境变量覆盖配置。

## 配置说明

### 核心配置项

Gateway 的配置分为以下几个部分：

1. **CORS 配置** - 允许的前端域名
2. **GatewayService** - Gateway 公开访问地址
3. **DownstreamServices** - 下游服务地址（API、Auth）
4. **FrontendService** - 前端服务地址
5. **ReverseProxy** - YARP 路由和集群配置

### 基础配置 (appsettings.json)

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://localhost:3000",
      "http://localhost:3100",
      "https://localhost:3100"
    ]
  },

  "GatewayService": {
    "PublicUrl": "https://localhost:5000"
  },

  "DownstreamServices": {
    "ApiService": {
      "BaseUrl": "http://localhost:5100",
      "HealthCheckPath": "/health"
    },
    "AuthService": {
      "BaseUrl": "http://localhost:5200",
      "HealthCheckPath": "/health"
    }
  },

  "FrontendService": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

### YARP 路由配置

Gateway 使用 YARP 进行路由转发，配置在 `appsettings.json` 的 `ReverseProxy` 节点。

#### 路由规则

| 路径模式 | 目标服务 | 说明 | 特殊配置 |
|---------|---------|------|---------|
| `/docs/**` | radish.docs (:4000) | 项目文档站 | - |
| `/api/**` | Radish.Api (:5100) | 业务 API 接口 | - |
| `/uploads/**` | Radish.Api (:5100) | 上传文件静态资源 | - |
| `/scalar/**` | Radish.Api (:5100) | Scalar API 文档 | - |
| `/openapi/**` | Radish.Api (:5100) | OpenAPI 规范 | - |
| `/hangfire/**` | Radish.Api (:5100) | Hangfire 定时任务面板 | - |
| `/console/**` | radish.console (:3100) | 管理控制台 | PathRemovePrefix, WebSocket |
| `/Account/**` | Radish.Auth (:5200) | OIDC 登录页面 | X-Forwarded-* 头 |
| `/connect/**` | Radish.Auth (:5200) | OIDC 协议端点 | X-Forwarded-* 头 |
| `/**` | radish.client (:3000) | 前端应用（最低优先级） | WebSocket, Order: 1000 |

#### 完整配置示例

```json
{
  "ReverseProxy": {
    "Routes": {
      "docs-route": {
        "ClusterId": "docs-cluster",
        "Match": { "Path": "/docs/{**catch-all}" }
      },
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": { "Path": "/api/{**catch-all}" }
      },
      "console-route": {
        "ClusterId": "console-cluster",
        "Match": { "Path": "/console/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/console" },
          { "RequestHeaderOriginalHost": "true" }
        ]
      },
      "auth-account-route": {
        "ClusterId": "auth-cluster",
        "Match": { "Path": "/Account/{**catch-all}" },
        "Transforms": [
          { "RequestHeader": "X-Forwarded-Host", "Set": "{host}" },
          { "RequestHeader": "X-Forwarded-Proto", "Set": "{scheme}" },
          { "RequestHeader": "X-Forwarded-For", "Append": "{RemoteIpAddress}" }
        ]
      },
      "frontend-root": {
        "ClusterId": "frontend-cluster",
        "Match": { "Path": "/{**catch-all}" },
        "Order": 1000,
        "Transforms": [
          { "RequestHeaderOriginalHost": "true" }
        ]
      }
    },
    "Clusters": {
      "docs-cluster": {
        "Destinations": {
          "docs": { "Address": "http://localhost:4000" }
        }
      },
      "api-cluster": {
        "Destinations": {
          "api": { "Address": "http://localhost:5100" }
        }
      },
      "auth-cluster": {
        "Destinations": {
          "auth": { "Address": "http://localhost:5200" }
        }
      },
      "frontend-cluster": {
        "Destinations": {
          "frontend": { "Address": "http://localhost:3000" }
        }
      },
      "console-cluster": {
        "Destinations": {
          "console": { "Address": "http://localhost:3100" }
        }
      }
    }
  }
}
```

#### 路由特性说明

**1. Console 路由的路径转换**

Console 路由使用 `PathRemovePrefix` 转换，将 `/console/*` 转发到下游的 `/`：

```
客户端请求: https://localhost:5000/console/dashboard
    ↓
Gateway 转发: http://localhost:3100/dashboard
```

这样配合 Console 的 Vite `base: '/console/'` 配置，确保资源路径正确。

**2. Auth 路由的代理头**

Auth 路由添加 `X-Forwarded-*` 头，确保 OIDC 重定向 URL 正确：

- `X-Forwarded-Host`: 原始请求的 Host
- `X-Forwarded-Proto`: 原始请求的协议（http/https）
- `X-Forwarded-For`: 客户端真实 IP

**3. WebSocket 支持**

Console 和 Frontend 路由启用 `RequestHeaderOriginalHost`，支持 Vite HMR 的 WebSocket 连接。

**4. 路由优先级**

Frontend 根路由设置 `Order: 1000`（最低优先级），确保其他具体路由优先匹配。

### 生产环境配置

**重要**：Gateway 不包含敏感信息（无数据库密码、API 密钥等），推荐使用**环境变量**覆盖配置，无需创建 `appsettings.Local.json`。

#### 必须配置的环境变量

```bash
# Gateway 公开访问域名
GatewayService__PublicUrl=https://radish.example.com

# 前端 CORS 域名
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://console.example.com
```

#### 可选配置的环境变量

如果使用 Docker/Kubernetes，内部服务地址可能需要修改：

```bash
# 下游服务地址（容器名或服务名）
DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200

# 前端服务地址
FrontendService__BaseUrl=https://app.example.com

# YARP 反向代理集群地址
ReverseProxy__Clusters__api-cluster__Destinations__api__Address=http://radish-api:5100
ReverseProxy__Clusters__auth-cluster__Destinations__auth__Address=http://radish-auth:5200
ReverseProxy__Clusters__docs-cluster__Destinations__docs__Address=http://radish-docs:4000
ReverseProxy__Clusters__frontend-cluster__Destinations__frontend__Address=http://radish-frontend:3000
ReverseProxy__Clusters__console-cluster__Destinations__console__Address=http://radish-console:3100
```

#### Docker Compose 示例

```yaml
services:
  radish-gateway:
    image: radish-gateway:latest
    ports:
      - "5000:5000"
      - "5001:5001"
    environment:
      - GatewayService__PublicUrl=https://radish.example.com
      - Cors__AllowedOrigins__0=https://app.example.com
      - Cors__AllowedOrigins__1=https://console.example.com
      - DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
      - DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200
      - FrontendService__BaseUrl=https://app.example.com
      - ReverseProxy__Clusters__api-cluster__Destinations__api__Address=http://radish-api:5100
      - ReverseProxy__Clusters__auth-cluster__Destinations__auth__Address=http://radish-auth:5200
    depends_on:
      - radish-api
      - radish-auth
    networks:
      - radish-network

  radish-api:
    image: radish-api:latest
    expose:
      - "5100"
    networks:
      - radish-network

  radish-auth:
    image: radish-auth:latest
    expose:
      - "5200"
    networks:
      - radish-network

networks:
  radish-network:
    driver: bridge
```

#### Kubernetes ConfigMap 示例

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: gateway-config
data:
  GatewayService__PublicUrl: "https://radish.example.com"
  Cors__AllowedOrigins__0: "https://app.example.com"
  Cors__AllowedOrigins__1: "https://console.example.com"
  DownstreamServices__ApiService__BaseUrl: "http://radish-api-service:5100"
  DownstreamServices__AuthService__BaseUrl: "http://radish-auth-service:5200"
```

## 核心功能

### 1. 反向代理路由

Gateway 使用 YARP 实现反向代理，支持以下功能：

**路由匹配**：
- 基于路径模式匹配（支持通配符 `{**catch-all}`）
- 路由优先级控制（`Order` 参数）
- 自动转发请求头和查询参数

**路径转换**：
- `PathRemovePrefix`：移除路径前缀（如 Console 路由）
- 保留原始路径（默认行为）

**请求头转换**：
- `X-Forwarded-*` 头注入（Host、Proto、For）
- `RequestHeaderOriginalHost`：保留原始 Host（WebSocket 支持）

**协议支持**：
- HTTP/HTTPS
- WebSocket（用于 Vite HMR）

### 2. 健康检查

Gateway 提供健康检查端点，监控自身和下游服务状态。

**端点**：
- `/health` - 标准健康检查端点
- `/healthz` - Kubernetes 风格健康检查端点

**检查项**：
1. **Gateway 自身**：进程运行状态
2. **API 服务**：通过 `DownstreamServices:ApiService:BaseUrl` + `HealthCheckPath` 检查
3. **Docs 服务**：通过 Gateway 公开地址 + `/docs` 检查（如果配置了 `GatewayService:PublicUrl`）
4. **Console 服务**：通过 Gateway 公开地址 + `/console` 检查

**配置示例**：

```csharp
// Program.cs 中的健康检查配置
var healthChecksBuilder = builder.Services.AddHealthChecks();

// 添加下游 API 服务健康检查
if (!string.IsNullOrEmpty(apiBaseUrl) && !string.IsNullOrEmpty(apiHealthPath))
{
    var apiHealthUrl = $"{apiBaseUrl.TrimEnd('/')}{apiHealthPath}";
    healthChecksBuilder.AddUrlGroup(
        new Uri(apiHealthUrl),
        name: "api-service",
        tags: ["downstream", "api"]);
}
```

**响应格式**：

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "api-service": {
      "status": "Healthy",
      "description": "OK"
    },
    "docs-service": {
      "status": "Healthy",
      "description": "OK"
    }
  }
}
```

### 3. CORS 配置

Gateway 统一处理 CORS，下游服务无需配置 CORS。

**配置**：

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://localhost:3000",
      "http://localhost:3100",
      "https://localhost:3100"
    ]
  }
}
```

**策略**：
- 允许指定的源（Origins）
- 允许所有方法（GET、POST、PUT、DELETE 等）
- 允许所有请求头
- 允许携带凭据（Credentials）

### 4. 日志记录

Gateway 使用 Serilog 记录结构化日志。

**日志输出**：
- **控制台**：彩色格式化输出，包含启动信息
- **文件**：`Log/Gateway/Gateway.txt`（按天滚动）

**日志内容**：
- 启动信息（环境、监听地址、CORS 配置、下游服务地址）
- 请求日志（通过 Serilog Request Logging 中间件）
- 错误日志（异常堆栈、错误上下文）

**启动日志示例**：

```
====================================
   ____           _ _     _
  |  _ \ __ _  __| (_)___| |__
  | |_) / _` |/ _` | / __| '_ \
  |  _ < (_| | (_| | \__ \ | | |
  |_| \_\__,_|\__,_|_|___/_| |_|
        Radish.Gateway --by luobo
====================================
环境: Development
监听地址: https://localhost:5000, http://localhost:5001
CORS 允许来源: http://localhost:3000, https://localhost:3000, http://localhost:3100, https://localhost:3100
下游 API 服务: http://localhost:5100
```

## 开发指南

### 本地启动

**方式 1：使用启动脚本（推荐）**

```bash
# Windows PowerShell
pwsh ./start.ps1
# 选择选项 9: Gateway + Auth + API

# Linux/macOS
./start.sh
# 选择选项 9: Gateway + Auth + API
```

**方式 2：手动启动**

```bash
# 终端 1：启动 API
dotnet run --project Radish.Api/Radish.Api.csproj

# 终端 2：启动 Auth
dotnet run --project Radish.Auth/Radish.Auth.csproj

# 终端 3：启动 Gateway
dotnet run --project Radish.Gateway/Radish.Gateway.csproj

# 终端 4：启动前端（可选）
npm run dev --workspace=radish.client

# 终端 5：启动文档站（可选）
npm run dev --workspace=radish.docs

# 终端 6：启动控制台（可选）
npm run dev --workspace=radish.console
```

**方式 3：使用 dotnet watch（热重载）**

```bash
dotnet watch --project Radish.Gateway/Radish.Gateway.csproj
```

### 访问地址

启动后，访问以下地址：

| 服务 | 地址 | 说明 |
|------|------|------|
| **Gateway 门户** | https://localhost:5000/ | 服务门户首页 |
| **前端应用** | https://localhost:5000/ | WebOS 桌面（通过 Gateway） |
| **API 接口** | https://localhost:5000/api/... | 业务 API（通过 Gateway） |
| **Scalar 文档** | https://localhost:5000/scalar | API 文档（通过 Gateway） |
| **Hangfire 面板** | https://localhost:5000/hangfire | 定时任务面板（通过 Gateway） |
| **文档站** | https://localhost:5000/docs | 项目文档（通过 Gateway） |
| **管理控制台** | https://localhost:5000/console | 管理后台（通过 Gateway） |
| **健康检查** | https://localhost:5000/health | 健康检查端点 |

**直连下游服务（仅用于调试）**：

| 服务 | 地址 | 说明 |
|------|------|------|
| API 直连 | http://localhost:5100 | 绕过 Gateway 直连 API |
| Auth 直连 | http://localhost:5200 | 绕过 Gateway 直连 Auth |
| 前端 dev | http://localhost:3000 | 前端开发服务器 |
| Docs dev | http://localhost:4000 | 文档站开发服务器 |
| Console dev | http://localhost:3100 | 控制台开发服务器 |

### 添加新路由

如果需要添加新的路由规则，编辑 `Radish.Gateway/appsettings.json`：

```json
{
  "ReverseProxy": {
    "Routes": {
      "new-service-route": {
        "ClusterId": "new-service-cluster",
        "Match": {
          "Path": "/new-service/{**catch-all}"
        },
        "Transforms": [
          // 可选：添加请求头转换
          { "RequestHeader": "X-Custom-Header", "Set": "value" }
        ]
      }
    },
    "Clusters": {
      "new-service-cluster": {
        "Destinations": {
          "new-service": {
            "Address": "http://localhost:5300"
          }
        }
      }
    }
  }
}
```

**重启 Gateway** 使配置生效：

```bash
# 如果使用 dotnet watch，会自动重启
# 否则手动重启
dotnet run --project Radish.Gateway/Radish.Gateway.csproj
```

### 调试技巧

**1. 查看 YARP 路由日志**

在 `appsettings.json` 中启用详细日志：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Yarp": "Debug"
    }
  }
}
```

**2. 测试路由转发**

```bash
# 测试 API 路由
curl -X GET https://localhost:5000/api/v2/Test/Hello

# 测试健康检查
curl https://localhost:5000/health

# 测试 OIDC 登录页面
curl https://localhost:5000/Account/Login

# 测试文档站
curl https://localhost:5000/docs/
```

**3. 验证 CORS 配置**

```bash
# 使用浏览器开发者工具查看 CORS 响应头
# 或使用 curl 测试
curl -H "Origin: http://localhost:3000" \
     -H "Access-Control-Request-Method: POST" \
     -H "Access-Control-Request-Headers: Content-Type" \
     -X OPTIONS \
     https://localhost:5000/api/v2/Test/Hello
```

**4. 检查健康状态**

```bash
# 查看所有服务健康状态
curl https://localhost:5000/health | jq

# 输出示例
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "api-service": {
      "status": "Healthy"
    }
  }
}
```

## 部署指南

### Docker 部署

**Dockerfile 示例**：

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Radish.Gateway/Radish.Gateway.csproj", "Radish.Gateway/"]
COPY ["Radish.Common/Radish.Common.csproj", "Radish.Common/"]
COPY ["Radish.Extension/Radish.Extension.csproj", "Radish.Extension/"]
RUN dotnet restore "Radish.Gateway/Radish.Gateway.csproj"
COPY . .
WORKDIR "/src/Radish.Gateway"
RUN dotnet build "Radish.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Radish.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Radish.Gateway.dll"]
```

**构建镜像**：

```bash
docker build -t radish-gateway:latest -f Radish.Gateway/Dockerfile .
```

**运行容器**：

```bash
docker run -d \
  --name radish-gateway \
  -p 5000:5000 \
  -p 5001:5001 \
  -e GatewayService__PublicUrl=https://radish.example.com \
  -e Cors__AllowedOrigins__0=https://app.example.com \
  radish-gateway:latest
```

### 生产环境注意事项

1. **HTTPS 配置**：
   - 使用 Let's Encrypt 或其他 CA 签发的证书
   - 配置 HSTS（HTTP Strict Transport Security）
   - 强制 HTTPS 重定向

2. **性能优化**：
   - 启用 HTTP/2
   - 配置响应压缩（Gzip/Brotli）
   - 静态资源使用 CDN

3. **安全加固**：
   - 配置 CORS 白名单（生产域名）
   - 启用速率限制（Rate Limiting）
   - 添加 WAF（Web Application Firewall）规则

4. **高可用**：
   - 部署至少 2 个 Gateway 实例
   - 使用负载均衡器（Nginx/HAProxy/云负载均衡）
   - 配置健康检查和自动重启

5. **监控告警**：
   - 集成 Prometheus + Grafana 监控
   - 配置日志聚合（ELK/Loki）
   - 设置关键指标告警（QPS、错误率、延迟）

## 常见问题

### Q1: Gateway 启动失败，提示端口被占用

**A**: 检查端口 5000/5001 是否被其他进程占用：

```bash
# Windows
netstat -ano | findstr :5000

# Linux/macOS
lsof -i :5000
```

**解决方案**：
1. 关闭占用端口的进程
2. 或修改 Gateway 监听端口（在 `Program.cs` 或环境变量中配置）

### Q2: 通过 Gateway 访问 API 返回 502 Bad Gateway

**A**: 检查后端服务是否正常运行：

```bash
# 测试 API 服务
curl http://localhost:5100/health

# 测试 Auth 服务
curl http://localhost:5200/health
```

**常见原因**：
- 后端服务未启动
- 后端服务地址配置错误
- 网络连接问题（防火墙、Docker 网络）

**解决方案**：
1. 确保后端服务已启动
2. 检查 `DownstreamServices` 配置是否正确
3. 检查 `ReverseProxy.Clusters` 配置是否正确

### Q3: OIDC 登录后重定向失败

**A**: 检查以下配置：

1. **Auth 服务的 Client 配置**：确保 `RedirectUris` 包含 Gateway 地址
2. **前端 OIDC 配置**：确保 `authority` 和 `redirect_uri` 使用 Gateway 地址
3. **X-Forwarded-* 头**：确保 Auth 路由配置了正确的请求头转换

**示例配置**：

```json
{
  "ReverseProxy": {
    "Routes": {
      "auth-account-route": {
        "Transforms": [
          { "RequestHeader": "X-Forwarded-Host", "Set": "{host}" },
          { "RequestHeader": "X-Forwarded-Proto", "Set": "{scheme}" },
          { "RequestHeader": "X-Forwarded-For", "Append": "{RemoteIpAddress}" }
        ]
      }
    }
  }
}
```

### Q4: Console 路由无法正常工作

**A**: 检查以下配置：

1. **路径转换**：确保配置了 `PathRemovePrefix`
2. **Vite base 配置**：确保 Console 的 `vite.config.ts` 中 `base: '/console/'`
3. **WebSocket 支持**：确保配置了 `RequestHeaderOriginalHost`

**正确配置**：

```json
{
  "console-route": {
    "ClusterId": "console-cluster",
    "Match": { "Path": "/console/{**catch-all}" },
    "Transforms": [
      { "PathRemovePrefix": "/console" },
      { "RequestHeaderOriginalHost": "true" }
    ]
  }
}
```

### Q5: 如何在开发时绕过 Gateway 直连 API？

**A**: 修改前端环境变量：

```bash
# .env.development.local
VITE_API_BASE_URL=http://localhost:5100
```

**注意**：直连模式下需要在 API 服务中配置 CORS，允许前端域名访问。

### Q6: Gateway 日志在哪里查看？

**A**: 日志输出位置：

- **控制台**：启动 Gateway 的终端窗口
- **文件**：`Log/Gateway/Gateway.txt`（按天滚动）
- **结构化日志**：可配置输出到 Elasticsearch、Seq 等日志平台

### Q7: 如何添加新的后端服务？

**A**: 按以下步骤操作：

1. 在 `appsettings.json` 的 `ReverseProxy.Routes` 中添加路由规则
2. 在 `ReverseProxy.Clusters` 中添加目标集群
3. 重启 Gateway 服务

**示例**：

```json
{
  "ReverseProxy": {
    "Routes": {
      "new-service-route": {
        "ClusterId": "new-service-cluster",
        "Match": { "Path": "/new-service/{**catch-all}" }
      }
    },
    "Clusters": {
      "new-service-cluster": {
        "Destinations": {
          "new-service": { "Address": "http://localhost:5300" }
        }
      }
    }
  }
}
```

### Q8: Gateway 会成为性能瓶颈吗？

**A**: 理论上会增加一跳延迟（通常 <5ms），但通过以下优化可以忽略：

- 使用 Kestrel 高性能服务器
- 启用 HTTP/2
- 静态资源直接走 CDN，不经过 Gateway
- 热点接口可以缓存在 Gateway 层

### Q9: 如何避免 Gateway 单点故障？

**A**：
- 部署至少 2 个 Gateway 副本
- 前置 Nginx/HAProxy 做负载均衡和健康检查
- 使用容器编排（Docker Swarm / Kubernetes）自动重启

## 参考资料

- [YARP 官方文档](https://microsoft.github.io/reverse-proxy/)
- [ASP.NET Core 健康检查](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Serilog 文档](https://serilog.net/)
- [Radish 认证指南](./authentication.md)
- [Radish 配置指南](./configuration.md)
- [Radish 部署指南](/deployment/guide)
