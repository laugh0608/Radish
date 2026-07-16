# Radish.Gateway 配置入口

Gateway 的路由、端口、TLS、反向代理与部署口径统一维护在：

- [`Docs/guide/gateway.md`](../Docs/guide/gateway.md)
- [`Docs/deployment/guide.md`](../Docs/deployment/guide.md)

当前入口约束：

- 开发运行使用 `https://localhost:5000`；启动命令与授权要求见仓库协作指南。
- 本地容器验证使用 `Deploy/docker-compose.local.yaml`，Gateway 可直接使用开发证书提供 HTTPS。
- 测试与生产使用 `Deploy/docker-compose.yaml`；外部 `Nginx / Traefik / Caddy` 终止 TLS，Gateway 容器内监听 `http://+:5000`。
- `RADISH_PUBLIC_URL` 是部署态公开入口真相源，必须与 OIDC Issuer、回调地址、前端运行时入口和反向代理域名一致。

本文件只保留项目内导航，不复制完整配置和命令，避免与 `Docs/` 真相源漂移。
