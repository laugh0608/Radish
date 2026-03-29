# Gateway 服务网关

## 概述

Radish.Gateway 是 Radish 项目的统一服务入口,负责提供服务门户页面、路由转发、健康检查聚合等功能。Gateway 采用 ASP.NET Core + YARP 反向代理实现,与 Radish.Api、Radish.Auth 等服务协同工作,为用户提供统一的访问入口。

### 核心职责

1. **服务门户**:提供欢迎页面,展示服务状态、文档说明、API 可视化工具
2. **路由转发**:通过 YARP 反向代理,将请求路由到后端服务(API、Auth、Console 等)
3. **健康检查**:聚合各服务的健康状态,提供统一的监控入口
4. **统一入口**:为前端、控制台与 API 文档提供统一的访问地址

### 技术栈

- **ASP.NET Core 10**:Web 框架
- **YARP (Yet Another Reverse Proxy)**:反向代理和路由转发
- **Razor Pages**:服务门户页面渲染
- **Serilog**:结构化日志
- **HealthChecks**:健康检查

## 架构设计

### 服务拓扑

```text
开发运行：
客户端 (浏览器/前端应用)
    ↓
Radish.Gateway (https://localhost:5000，可选 http://localhost:5001 -> 5000)

测试部署：
客户端 (浏览器/前端应用)
    ↓
Radish.Gateway (https://IP:port 或测试域名，容器内直接提供 HTTPS)

生产部署：
客户端 (浏览器/前端应用)
    ↓
Nginx / Traefik / Caddy (https://radish.example.com)
    ↓
Radish.Gateway (http://gateway:5000，容器内仅提供 HTTP)

统一路由：
    ├─→ /api/** (业务 API → Radish.Api :5100)
    ├─→ /_assets/attachments/** (附件公开资源 → Radish.Api :5100)
    ├─→ /uploads/** (底层静态文件 / 存储层兼容路径 → Radish.Api :5100)
    ├─→ /scalar/** (API 文档 → Radish.Api :5100)
    ├─→ /openapi/** (OpenAPI 规范 → Radish.Api :5100)
    ├─→ /hangfire/** (定时任务面板 → Radish.Api :5100)
    ├─→ /console/** (管理控制台 → radish.console)
    ├─→ /Account/** (登录页面 → Radish.Auth :5200)
    ├─→ /connect/** (OIDC 端点 → Radish.Auth :5200)
    └─→ /** (前端应用 → radish.client，最低优先级)
```

### 端口配置

| 场景 | Gateway 公开入口 | Gateway 监听 | 说明 |
|------|------------------|--------------|------|
| **开发运行** | `https://localhost:5000` | `https://localhost:5000`，可选 `http://localhost:5001` 重定向 | IDE / `dotnet run` 默认口径 |
| **测试部署** | `https://IP:port` 或测试域名 | `https://+:5000` | Gateway 容器首次启动可自动生成并复用测试 TLS 证书 |
| **生产部署** | `https://radish.example.com` | `http://+:5000` | TLS 由外部 `Nginx / Traefik / Caddy` 终止，Gateway 容器内仅 HTTP |

补充约束：
- `Radish.Api` 与 `Radish.Auth` 在部署态默认保持内部 HTTP，由 Gateway / 反向代理统一对外暴露。
- `radish.client` 与 `radish.console` 在部署态统一通过 Gateway 对外访问，不再要求前端单独暴露独立公网端口。
- `RADISH_PUBLIC_URL` 是部署态公开入口的单一真相源，Gateway、前端运行时配置和 OIDC Issuer 都应围绕它对齐。

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

**注意**：Gateway 项目不需要 `appsettings.Local.json`，因为不包含数据库密码、API 密钥等敏感信息。开发运行直接使用默认配置；测试部署与生产部署优先通过 `Deploy/` 下的 `env-file + compose override` 覆盖配置。

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
| `/api/**` | Radish.Api (:5100) | 业务 API 接口 | - |
| `/_assets/attachments/**` | Radish.Api (:5100) | 附件公开资源口径 | - |
| `/uploads/**` | Radish.Api (:5100) | 底层静态文件暴露 / 兼容路径 | - |
| `/scalar/**` | Radish.Api (:5100) | Scalar API 文档 | - |
| `/openapi/**` | Radish.Api (:5100) | OpenAPI 规范 | - |
| `/hangfire/**` | Radish.Api (:5100) | Hangfire 定时任务面板 | - |
| `/console/**` | radish.console (:3100) | 管理控制台 | PathRemovePrefix, WebSocket |
| `/Account/**` | Radish.Auth (:5200) | OIDC 登录页面 | X-Forwarded-* 头 |
| `/connect/**` | Radish.Auth (:5200) | OIDC 协议端点 | X-Forwarded-* 头 |
| `/**` | radish.client (:3000) | 前端应用（最低优先级） | WebSocket, Order: 1000 |

补充说明：

- 附件业务公开访问口径已经切换到 `/_assets/attachments/{id}` 与 `/_assets/attachments/{id}/thumbnail`。
- `Radish.Gateway/appsettings.json` 当前已内建 `/_assets/attachments/** -> Radish.Api` 路由，避免该路径落到前端兜底路由。
- 如果系统前面还有 `Nginx / Traefik / Caddy`，仍需确保该路径被继续放行到 Gateway，而不是被前端静态站点或默认首页吞掉。
- `/uploads/**` 可以继续保留为底层静态目录或兼容路径，但不再是业务正文、商品、贴图、聊天图片等场景的推荐公开引用口径。

#### 完整配置示例

```json
{
  "ReverseProxy": {
    "Routes": {
      "docs-route": {
        "ClusterId": "docs-cluster",
      },
      "api-route": {
        "ClusterId": "apiCluster",
        "Match": { "Path": "/api/{**catch-all}" }
      },
      "console-route": {
        "ClusterId": "consoleCluster",
        "Match": { "Path": "/console/{**catch-all}" },
        "Transforms": [
          { "PathRemovePrefix": "/console" },
          { "RequestHeaderOriginalHost": "true" }
        ]
      },
      "auth-account-route": {
        "ClusterId": "authCluster",
        "Match": { "Path": "/Account/{**catch-all}" },
        "Transforms": [
          { "RequestHeader": "X-Forwarded-Host", "Set": "{host}" },
          { "RequestHeader": "X-Forwarded-Proto", "Set": "{scheme}" },
          { "RequestHeader": "X-Forwarded-For", "Append": "{RemoteIpAddress}" }
        ]
      },
      "frontend-root": {
        "ClusterId": "frontendCluster",
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
      "apiCluster": {
        "Destinations": {
          "api": { "Address": "http://localhost:5100" }
        }
      },
      "authCluster": {
        "Destinations": {
          "auth": { "Address": "http://localhost:5200" }
        }
      },
      "frontendCluster": {
        "Destinations": {
          "frontend": { "Address": "http://localhost:3000" }
        }
      },
      "consoleCluster": {
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

**WebSocket 配置说明**：
- Vite 开发服务器使用 WebSocket 进行热模块替换（HMR）
- `RequestHeaderOriginalHost` 转换确保 WebSocket 握手时 Host 头正确
- SignalR 实时通信也依赖 WebSocket 连接
- 生产环境需要确保反向代理支持 WebSocket 协议升级

**4. 路由优先级**

Frontend 根路由设置 `Order: 1000`（最低优先级），确保其他具体路由优先匹配。

### 部署环境口径

Gateway 当前已经收束为开发、测试、生产三种明确形态：

#### 开发运行

- 默认通过 IDE、`dotnet run` 或启动脚本运行。
- 使用本地开发证书，主入口为 `https://localhost:5000`。
- `http://localhost:5001` 仅用于本地 `https` Profile 的重定向辅助端口。

#### 测试部署

- 使用 `Deploy/docker-compose.yml + Deploy/docker-compose.test.yml`。
- 入口通常写成 `https://IP:port` 或测试域名，对应变量见 `Deploy/.env.test.example`。
- Gateway 容器内部直接监听 `https://+:5000`。
- 若 `RADISH_GATEWAY_CERT_AUTO_GENERATE=true` 且目标 `.pfx` 不存在，容器启动前会自动生成并复用测试 TLS 证书。
- 该证书通常为自签名证书，浏览器出现证书告警属于预期行为。

#### 生产部署

- 使用 `Deploy/docker-compose.yml + Deploy/docker-compose.prod.yml`。
- 外部 `Nginx / Traefik / Caddy` 提供真实域名的 HTTPS 入口。
- Gateway 容器内部仅监听 `http://+:5000`，并通过 `GatewayRuntime__EnableHttpsRedirection=false` 避免在反代链路里错误重定向。
- `RADISH_PUBLIC_URL` 必须与真实外部访问域名完全一致，否则 OIDC 回调、CORS 与门户跳转都会漂移。

#### 关键环境变量

| 变量 | 测试部署 | 生产部署 | 说明 |
|------|----------|----------|------|
| `RADISH_PUBLIC_URL` | 必填 | 必填 | 系统公开入口，Gateway / Frontend / OIDC 统一围绕它对齐 |
| `GatewayService__PublicUrl` | 由 Compose 从 `RADISH_PUBLIC_URL` 注入 | 由 Compose 从 `RADISH_PUBLIC_URL` 注入 | Gateway 门户显示与公开入口基准 |
| `GatewayRuntime__EnableHttpsRedirection` | `true` | `false` | 测试部署由 Gateway 直接提供 HTTPS；生产部署由外部反代终止 TLS |
| `Kestrel__Certificates__Default__Path` / `Password` | 需要 | 不需要 | 仅测试部署下 Gateway 容器内 HTTPS 使用 |
| `RADISH_GATEWAY_CERT_AUTO_GENERATE` | 可选，默认 `true` | 不使用 | 控制测试证书首次自动生成 |
| `DownstreamServices__*` / `ReverseProxy__Clusters__*` | 按需覆盖 | 按需覆盖 | 内部服务地址变更时再覆盖，默认 Compose 已给出最小值 |

如果你要看完整部署组合、证书持久化策略与 Nginx 示例，直接参考 [部署与容器指南](/deployment/guide)；如果你要看 Auth OIDC 证书策略，参考 [认证与授权指南](/guide/authentication)。

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
3. **Console 服务**：通过 Gateway 公开地址 + `/console` 检查

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
    }
  }
}
```

### 3. CORS 配置

Gateway 是默认公开入口，但 `Api / Auth / Gateway` 三个宿主都会注册同源的 CORS 策略。

- 开发运行：继续读取各自 `appsettings.json` 里的 `Cors:AllowedOrigins`
- 测试部署 / 生产部署：优先从 `RADISH_PUBLIC_URL` 推导单一公开入口 origin，并让三宿主保持一致
- 不建议再用 `Cors__AllowedOrigins__0` 这类单索引环境变量覆盖部署态 CORS，否则默认数组项可能残留旧端口

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

部署态下，`Gateway / Auth / Api` 的启动日志里，`CORS 允许来源` 应收敛成同一个公开入口，例如 `https://10.10.10.20:5000` 或 `https://radish.example.com`。

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

# 终端 5：启动控制台（可选）
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
| **管理控制台** | https://localhost:5000/console | 管理后台（通过 Gateway） |
| **健康检查** | https://localhost:5000/health | 健康检查端点 |

**直连下游服务（仅用于调试）**：

| 服务 | 地址 | 说明 |
|------|------|------|
| API 直连 | http://localhost:5100 | 绕过 Gateway 直连 API |
| Auth 直连 | http://localhost:5200 | 绕过 Gateway 直连 Auth |
| 前端 dev | http://localhost:3000 | 前端开发服务器 |
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

# 测试固定文档同步与文档应用
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

### Docker 与 Compose

当前仓库的 Gateway 镜像入口已经与测试/生产部署策略绑定：

- `Radish.Gateway/Dockerfile` 会在运行时镜像中安装 `openssl`
- 镜像会复制 `Scripts/docker/gateway-entrypoint.sh`
- 最终入口不是直接 `dotnet Radish.Gateway.dll`，而是先执行入口脚本，再按环境决定是否生成 / 复用测试 TLS 证书

只有在本地容器验证或手动验证镜像入口时，才需要显式构建本地镜像，例如：

```bash
docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml build gateway
```

测试部署与生产部署默认应直接拉取 `GHCR` 中已构建好的镜像，不再推荐在部署机单独 `docker build` 或 `docker run` Gateway 容器，而是直接使用仓库现成编排：

```bash
# 测试部署
docker compose --env-file Deploy/.env.test \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.test.yml config
docker compose --env-file Deploy/.env.test \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.test.yml pull
docker compose --env-file Deploy/.env.test \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.test.yml up -d

# 生产部署
docker compose --env-file Deploy/.env.prod \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.prod.yml config
docker compose --env-file Deploy/.env.prod \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.prod.yml pull
docker compose --env-file Deploy/.env.prod \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.prod.yml up -d
```

这样可以保证：

- Gateway 公开地址、CORS、前端运行时回退地址保持一致
- 测试环境的 Gateway TLS 证书可以自动生成并挂载复用
- 生产环境不会误把 Gateway 配成容器内 HTTPS
- Auth 的 OIDC 证书与 Gateway 的公开入口口径不会分叉

### 生产环境注意事项

1. **TLS / HTTPS 职责划分**：
   - 测试部署：Gateway 容器内直接提供 HTTPS，可自动生成自签名测试证书
   - 生产部署：TLS 由外部 `Nginx / Traefik / Caddy` 终止，Gateway 容器内仅提供 HTTP
   - 若生产反代层已经处理 HTTPS，不要再要求 Gateway 容器额外暴露 `5001` 或自行做 TLS 重定向

2. **性能优化**：
   - 启用 HTTP/2
   - 配置响应压缩（Gzip/Brotli）
   - 静态资源使用 CDN

3. **安全加固**：
   - 配置 CORS 白名单（通常与 `RADISH_PUBLIC_URL` 保持同源）
   - 在外部反代层补充速率限制（Rate Limiting）与 WAF 规则
   - 确保 `X-Forwarded-*` 头与 WebSocket 升级在反代层正确透传

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
    "ClusterId": "consoleCluster",
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

### Q6: Cookie SecurePolicy 配置问题

**A**: 如果遇到 Cookie 无法正确设置的问题（特别是 `SameSite=None` 的 Cookie），需要检查 Cookie SecurePolicy 配置。

**问题原因**：
- `SameSite=None` 要求 Cookie 必须设置 `Secure=true`
- 如果 `SecurePolicy` 设置为 `SameAsRequest`，在 HTTP 环境下会导致 Cookie 无法正确设置

**解决方案**：

在 Auth 服务的 `Program.cs` 中，将 Cookie SecurePolicy 设置为 `Always`：

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 改为 Always
});
```

**相关提交**：参见 commit `b91b035` - fix(auth): 修复 Cookie SecurePolicy 配置

### Q7: SignalR WebSocket 连接失败

**A**: 如果 SignalR 实时通知无法正常工作，检查以下配置：

**1. 检查 WebSocket 支持**

确保 Gateway 路由配置了 `RequestHeaderOriginalHost` 转换：

```json
{
  "frontend-route": {
    "Transforms": [
      { "RequestHeaderOriginalHost": "true" }
    ]
  }
}
```

**2. 检查认证状态**

SignalR 连接需要有效的 JWT Token：

```typescript
// 前端代码
import { hasAccessToken } from '@/services/auth';

// 仅在已登录时建立连接
if (hasAccessToken()) {
  const token = localStorage.getItem('access_token');
  const connection = new HubConnectionBuilder()
    .withUrl('https://localhost:5000/hubs/notification', {
      accessTokenFactory: () => token || '',
    })
    .build();
}
```

**3. 检查 CORS 配置**

确保 Gateway 的 CORS 配置允许前端域名：

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://localhost:3000"
    ]
  }
}
```

**4. 检查防火墙和代理**

- 确保防火墙允许 WebSocket 连接
- 如果使用反向代理（如 Nginx），确保配置了 WebSocket 支持

**相关文档**：
- [认证服务统一指南](./authentication-service.md)
- [前端设计文档](../frontend/design.md)

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
