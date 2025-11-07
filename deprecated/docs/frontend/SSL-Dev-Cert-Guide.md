# 前端本地 HTTPS 证书指南（Angular/React）

本文档介绍如何为两个前端子项目（Angular 管理端与 React + Vite）启用本地 HTTPS，依赖仓库根目录 `scripts/` 下的证书生成脚本与统一证书目录 `dev-certs/`。

## 统一目录与命名
- 证书统一存放于仓库根目录：`dev-certs/`
- 固定文件名：`localhost.key`、`localhost.crt`

## 一次性准备（生成并信任证书）
推荐使用 mkcert 生成受本机信任的开发证书。确保已安装 mkcert：

- macOS: `brew install mkcert nss`
- Windows: `choco install mkcert` 或 `scoop install mkcert`
- Linux: 参考 https://github.com/FiloSottile/mkcert（常见：Debian/Ubuntu 可安装 `mkcert` 与 `libnss3-tools`）

使用仓库脚本一键生成证书到 `dev-certs/`：

- Bash（macOS/Linux/WSL）：`./scripts/ssl-setup.sh`
- PowerShell（Windows）：`./scripts/ssl-setup.ps1`

脚本会执行：
1) `mkcert -install` 安装/更新本地根证书（若已安装会跳过）
2) 生成 `dev-certs/localhost.key` 与 `dev-certs/localhost.crt`

完成后即可被两个前端子项目识别与复用。

## 在 Angular 中启用 HTTPS

- 默认开发命令（HTTP）：
  - `cd angular && yarn start` 或 `npm run start`
- 启用 HTTPS（读取 `../dev-certs/localhost.{key,crt}`）：
  - `cd angular && yarn start:https` 或 `npm run start:https`
- Angular CLI `serve` 已在 `angular/angular.json` 中配置：
  - `sslKey: "../dev-certs/localhost.key"`
  - `sslCert: "../dev-certs/localhost.crt"`

验证：访问 `https://localhost:4200` 或执行 `curl -vk https://localhost:4200`。

## 在 React（Vite）中启用 HTTPS

- 默认开发命令（HTTP）：
  - `cd react && npm run dev`
- 启用 HTTPS：设置环境变量 `DEV_HTTPS=1` 再启动：
  - macOS/Linux: `cd react && DEV_HTTPS=1 npm run dev`
  - Windows PowerShell: `cd react; $env:DEV_HTTPS='1'; npm run dev`

React 的启动脚本 `react/script/start.js` 会优先从上级 `../dev-certs/` 读取 `localhost.{key,crt}`；若未找到，会回退到 `react/script/certs/` 或 `@vitejs/plugin-basic-ssl`。

验证：访问 `https://localhost:5173` 或执行 `curl -vk https://localhost:5173`。

## 常见问题排查

- 提示未安装 mkcert：请先按“一次性准备”安装 mkcert 后重试脚本。
- 浏览器不信任证书：先执行 `mkcert -install`，再重新生成证书；或在系统证书管理器中确认已信任 mkcert 根证书。
- Windows 执行策略限制（脚本被阻止）：使用临时绕过执行策略运行 PowerShell 脚本：
  - `powershell -ExecutionPolicy Bypass -File ./scripts/ssl-setup.ps1`
- 确认文件是否存在：`dev-certs/localhost.crt` 与 `dev-certs/localhost.key`
- 查看证书信息：`openssl x509 -in dev-certs/localhost.crt -noout -subject -issuer -dates`

更多背景与说明：见仓库根目录 `dev-certs/README.md`。

