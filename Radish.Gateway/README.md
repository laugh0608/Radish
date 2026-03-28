# Radish.Gateway 配置说明

## 开发运行

Gateway 开发运行默认直接使用 `appsettings.json` 与本地开发证书：

```bash
dotnet run --project Radish.Gateway
```

常见本地入口：

- `https://localhost:5000`
- `http://localhost:5001`（仅本地 `https` Profile 的辅助重定向端口）

## 测试部署

测试部署使用仓库现成编排：

```bash
docker compose --env-file Deploy/.env.test \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.test.yml up -d
```

测试部署口径：

- Gateway 容器内直接监听 `https://+:5000`
- `RADISH_PUBLIC_URL` 通常是 `https://IP:port` 或测试域名
- 若 `RADISH_GATEWAY_CERT_AUTO_GENERATE=true` 且目标 `.pfx` 不存在，入口脚本会自动生成并复用测试 TLS 证书
- 浏览器对自签名证书的告警属于预期行为

## 生产部署

生产部署同样使用仓库现成编排：

```bash
docker compose --env-file Deploy/.env.prod \
  -f Deploy/docker-compose.yml \
  -f Deploy/docker-compose.prod.yml up -d
```

生产部署口径：

- 外部 `Nginx / Traefik / Caddy` 提供 HTTPS
- Gateway 容器内部仅监听 `http://+:5000`
- `GatewayRuntime__EnableHttpsRedirection=false`
- `RADISH_PUBLIC_URL` 必须与真实外部域名一致

## 关键变量

常见变量如下：

```bash
RADISH_PUBLIC_URL=https://radish.example.com
GatewayService__PublicUrl=https://radish.example.com
Cors__AllowedOrigins__0=https://radish.example.com
FrontendService__BaseUrl=https://radish.example.com
DownstreamServices__ApiService__BaseUrl=http://radish-api:5100
DownstreamServices__AuthService__BaseUrl=http://radish-auth:5200
```

测试部署额外需要：

```bash
Kestrel__Certificates__Default__Path=/app/certs/gateway-test-cert.pfx
Kestrel__Certificates__Default__Password=ChangeMeGateway123!
RADISH_GATEWAY_CERT_AUTO_GENERATE=true
```

## 为什么不需要 appsettings.Local.json？

Gateway 配置特点：

- 无数据库密码、API 密钥等敏感信息
- 下游服务地址在容器部署时通常可以通过环境变量或 Compose 统一注入
- 测试/生产部署都已经通过 `Deploy/` 下的 env-file 和 Compose override 收口

因此，Gateway 一般不需要再维护单独的 `appsettings.Local.json`。
