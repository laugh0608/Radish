# Certs

该目录用于存放本地开发与测试所需的证书文件。

## 目录说明

- `dev-auth-cert.pfx`：本地认证服务的开发证书。
- `dev-gateway-cert.pfx`：本地 Docker / Compose 联调时 Gateway 对外 `https://localhost:5000` 的开发证书。

## 使用建议

- 仅用于本地/测试环境，不建议直接用于生产。
- 当前仓库内置开发证书密码统一为 `RadishDevCert123!`，仅供本地联调使用。
- 浏览器若提示证书不受信任，需在本机信任该开发证书后再访问 `https://localhost:5000`。
- 若证书更新，请同步检查 `appsettings.Local.json` 或相关环境变量配置。
