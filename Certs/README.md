# Certs

该目录用于存放仓库内置的本地开发证书文件。

## 目录说明

- `dev-auth-cert.pfx`：本地认证服务的开发证书。
- `dev-gateway-cert.pfx`：本地 Docker / Compose 联调时 Gateway 对外 `https://localhost:5000` 的开发证书。

## 使用建议

- 仓库内置证书仅用于本地开发或本地容器联调，不建议直接用于测试部署或生产。
- 当前仓库内置开发证书密码统一为 `RadishDevCert123!`，仅供本地联调使用。
- 浏览器若提示证书不受信任，需在本机信任该开发证书后再访问 `https://localhost:5000`。
- 若证书更新，请同步检查 `appsettings.Local.json` 或相关环境变量配置。
- 测试部署与生产部署所需证书不再写回本目录，而是由容器在挂载目录中按需自动生成并持久化。
