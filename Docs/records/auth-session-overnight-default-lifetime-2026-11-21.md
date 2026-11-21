# 2026-11-21 社区 Web 默认 Token 期限隔离复现

`v26.8.1-release` 的默认 `5 / 25` 分钟配置下，7 分钟组恢复为同一账号，27 分钟组因 refresh token 到期回到游客态。

## 环境与证据范围

- 使用 OrbStack 独立 Compose 项目及本机缓存的 `v26.8.1-release` ARM64 五镜像。Auth 镜像摘要为 `sha256:c3c9e922c02a7f3dc5e2d350755cb38dd6ee1a1d2bff75674f708aa5c3ea137b`。PostgreSQL 16.15、Redis 7.4.12、测试账户、证书与存储均为本轮隔离资源。
- 启动前确认固定摘要在本机缓存中，启动后对照运行镜像 ID。唯一宿主绑定是 `127.0.0.1:35443`，浏览器仅访问 `https://localhost:35443`。Auth 使用镜像默认期限和不活跃判断，检查运行容器无相关环境变量或 `appsettings.Local.json` 覆盖。两组测试使用独立 Chromium 资料目录及同一隔离账号，实际关闭浏览器，按各自最后一次签发计时。
- 两组均以 `radish-client` 在社区 Web 完成授权码交换，HTTP `200`，返回 refresh token。关闭后对隔离库作只读聚合，两条 access token 的数据库创建至过期差值均为 `300s`，两条 refresh token 均为 `1500s`。初次签发响应观测到 `expires_in=299`，数据库有效期仍为 `300s`。

## 关页重开对照

表中时间为北京时间（UTC+08:00），重开间隔按浏览器收到最后一次签发响应至再次开始导航计算。身份通过受保护的 `GET /api/v1/User/GetUserByHttpContext` 验证，仅保留状态码和同一账号判断。

| 组别 | 签发、关闭与重开 | `/connect/token` 刷新 | 重开后的登录态 |
| --- | --- | --- | --- |
| 7 分钟 | 19:14:23.583 签发，19:14:23.849 关闭，19:21:23.583 重开，间隔 `420s` | 两次 `refresh_token` grant 分别在 19:21:24.207 和 19:21:24.218 返回 `200`，均发放新 refresh token | 受保护接口 `200`，账号与登录时一致。本地 access 和 refresh token 均存在，登录入口隐藏。无新 OIDC 授权请求。 |
| 27 分钟 | 19:14:25.604 签发，19:14:25.711 关闭，19:41:25.605 重开，间隔 `1620.001s` | 两次 `refresh_token` grant 分别在 19:41:26.200 和 19:41:26.214 返回 `400 invalid_grant`。Auth 日志两次记录 `ID2018`。 | 受保护接口 `401`。本地 access 和 refresh token 均清除，登录入口可见。公开首页 `200`，无新 OIDC 授权请求。 |

27 分钟组的 refresh token 于 19:39:25 到期。两次拒绝均命中 OpenIddict 7.2.0 的 refresh token 到期分支 `ID2018`，未观测到 `session_idle_expired` 或 Radish 的不活跃拒绝日志。两组重开各观察到两次刷新请求，记录未采集请求来源及所持凭据的比对信息，具体并发路径待另查。[OpenIddict 到期分支](https://github.com/openiddict/openiddict-core/blob/7.2.0/src/OpenIddict.Server/OpenIddictServerHandlers.Protection.cs#L852-L876)

## 清理与适用范围

两组 Chromium 均已关闭。执行 `docker compose down --volumes --remove-orphans` 后，按项目标签核验剩余容器、网络、卷均为 `0`。Docker 无 `35443` 发布记录，`lsof` 无监听，连接 `127.0.0.1:35443` 返回 `ECONNREFUSED`。浏览器资料、临时配置、测试证书及凭据文件已删除，保留本脱敏记录。

本轮范围为社区 Web 桌面视图、默认期限和两组实时时间对照。用户遇到掉线的实际实例与入口、Console、原生客户端、网络故障及并发归因仍需各自的证据。未改动 Token 配置或实施修复。[排查与验收方案](../features/auth-session-overnight-recovery-plan.md)
