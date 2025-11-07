本目录用于存放本地开发 HTTPS 证书，供 React 与 Angular 开发服务器统一引用。

文件命名（固定）：
- `localhost.key`
- `localhost.crt`

推荐使用 mkcert 生成并安装本机信任的开发证书：

1) 安装 mkcert
- macOS: `brew install mkcert nss`
- Windows: `choco install mkcert` 或 `scoop install mkcert`
- Linux: 参考 https://github.com/FiloSottile/mkcert

2) 生成并安装本地根证书（只需一次）
```
mkcert -install
```

3) 生成 localhost 证书（包含常见回环地址）
```
mkcert -key-file localhost.key -cert-file localhost.crt localhost 127.0.0.1 ::1
```

快捷脚本：
- Bash: `./scripts/ssl-setup.sh`
- PowerShell: `./scripts/ssl-setup.ps1`

完成后：
- React: `npm run dev` 将优先使用此处证书（见 `react/script/start.js`）。
- Angular: `yarn start` 或 `npm start`（`angular.json` 已配置 dev server 使用此处证书）。

注意：证书仅用于本地开发，请勿提交到版本库。此目录下仅保留说明文件。
