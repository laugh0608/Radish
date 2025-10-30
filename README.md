# Radish

![萝卜](./docs/images/RadishAcg.png)

基于 ABP Framework 的分层单体应用：后端 .NET 9 + OpenIddict，前端包含 Angular 管理端与 React（Vite）示例。

- 简体中文: docs/README_CN.md
- English: docs/README_EN.md
- 文档索引: docs/README.md

## 快速开始

- 先决条件
  - .NET 9 SDK、Node 18/20
  - 本地 MongoDB（更新连接串后再运行）
- 还原与构建
  - `dotnet restore`
  - `dotnet build Radish.sln -c Debug`
- 初始化数据（首次必须）
  - `cd src/Radish.DbMigrator && dotnet run`
- 启动后端（HTTPS）
  - `cd src/Radish.HttpApi.Host && dotnet run`
  - 首次请信任证书：`dotnet dev-certs https --trust`
  - API: https://localhost:44342
- 启动前端
  - Angular（HTTP）：`cd angular && yarn install && yarn start`（或 `npm install && npm run start`）
  - Angular（HTTPS）：`npm run start:https`（使用仓库根 `dev-certs/localhost.{key,crt}`）
  - React（HTTP）：`cd react && npm install && npm run dev`
  - React（HTTPS）：`DEV_HTTPS=1 npm run dev`（优先读取 `dev-certs/`）
- 默认账号
  - 用户名 `admin`，密码 `1q2w3E*`

提示
- CORS：可通过环境变量 `App__CorsOrigins` 配置，例如：
  - `http://localhost:4200,https://localhost:4200,http://localhost:5173,https://localhost:5173`
  - 启动日志会打印“CORS allowed origins: …”便于确认。
- OpenIddict：迁移种子默认为 Angular 与 React 同时登记 http/https 的回调与登出回调（Angular 还包含静默刷新页 `/assets/silent-refresh.html`），本地切换协议无需再改配置。

## 常用脚本

- 运行迁移：`cd src/Radish.DbMigrator && dotnet run`
- 启动 Host：`cd src/Radish.HttpApi.Host && dotnet run`
- 运行测试：`dotnet test`
- 代码格式：`dotnet format`；前端遵循各目录的 ESLint/Prettier 配置

## 目录结构

- 后端：`src/`（`Radish.Application*`、`Radish.Domain*`、`Radish.HttpApi*`、`Radish.HttpApi.Host`、`Radish.MongoDB`、`Radish.DbMigrator`）
- 前端：`angular/`（管理端）与 `react/`（Vite）
- 领域模块：`modules/`（例如 `comtohtri`）遵循 ABP 模块分层
- 测试：`test/`（`*.Tests` 与 `Radish.TestBase`）
- 更多详见 docs/DirectoryStructure.md

## 故障排查

- HTTPS 页面空白/跳转失败：
  - 先信任后端与前端证书（`dotnet dev-certs https --trust`；并导入 `dev-certs/localhost.crt`）。
  - F12 → Network/Console 查看是否有证书错误、跨域或 `redirect_uri` 不合法。种子已允许 http/https 回调，通常是证书或 CORS 导致。
- Mixed Content 警告：
  - Angular 环境已按 `window.location.origin` 设定 `redirectUri/requireHttps`，保持协议一致可避免该问题。

## 文档与参考

- 后端（.NET/ABP）：docs/backend/README.md
- Angular：docs/frontend/angular/README.md
- React：docs/frontend/react/README.md
- 首页/门户说明：docs/Host-Home-Features.md

## 许可证

见 LICENSE.txt。

