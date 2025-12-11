# Radish.Gateway 配置说明

## 开发环境

直接运行即可，使用 `appsettings.json` 中的默认配置：

```bash
dotnet run --project Radish.Gateway
```

## 生产环境部署

Gateway 配置不包含敏感信息，建议通过**环境变量**覆盖配置，无需创建 `appsettings.Local.json`。

### 必须配置的环境变量

```bash
# Gateway 公开访问域名
GatewayService__PublicUrl=https://your-domain.com

# 前端 CORS 域名
Cors__AllowedOrigins__0=https://your-frontend-domain.com
```

### 可选配置的环境变量

如果使用 Docker/Kubernetes，内部服务地址可能需要修改：

```bash
# 下游服务地址（容器名或服务名）
DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200

# 前端服务地址
FrontendService__BaseUrl=https://your-frontend-domain.com

# YARP 反向代理集群地址
ReverseProxy__Clusters__api-cluster__Destinations__api__Address=http://radish-api:5100
ReverseProxy__Clusters__auth-cluster__Destinations__auth__Address=http://radish-auth:5200
ReverseProxy__Clusters__docs-cluster__Destinations__docs__Address=http://radish-docs:3001
```

### Docker Compose 示例

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
      - DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
      - DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200
      - FrontendService__BaseUrl=https://app.example.com
```

### Kubernetes ConfigMap 示例

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: gateway-config
data:
  GatewayService__PublicUrl: "https://radish.example.com"
  Cors__AllowedOrigins__0: "https://app.example.com"
  DownstreamServices__ApiService__BaseUrl: "http://radish-api:5100"
  DownstreamServices__AuthService__BaseUrl: "http://radish-auth:5200"
```

## 为什么不需要 appsettings.Local.json？

Gateway 配置特点：
- ✅ **无敏感信息**：没有数据库密码、API 密钥等
- ✅ **内部服务**：下游服务不对外公开，地址在容器化部署时相对固定
- ✅ **环境变量友好**：现代部署工具（Docker、K8s）都支持环境变量注入

因此，**推荐使用环境变量覆盖配置**，无需维护额外的配置文件。
