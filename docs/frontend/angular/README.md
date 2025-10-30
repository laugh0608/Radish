# 前端（Angular 管理端）开发指南

Angular 管理端遵循 ABP 的 UI 集成规范，运行与打包流程如下。

## 本地运行

```bash
cd angular
yarn install && yarn start
# 或
npm install && npm run start
```

启动后访问 `http://localhost:4200/`（本地默认使用 HTTP，避免自签证书信任提示）。API 由 Host 提供的地址为 `https://localhost:44342`，首次使用建议信任本机开发证书：`dotnet dev-certs https --trust`。如需前端也使用 HTTPS，可运行：

```bash
yarn start:https
# 或
npm run start:https
```
对应证书优先使用仓库根目录 `dev-certs/localhost.{key,crt}`。

## 构建与测试

```bash
ng build            # 产物位于 dist/
ng test             # 单元测试（Karma）
ng e2e              # 端到端测试
```

可通过 `ng generate component component-name` 进行脚手架生成。

## 环境与配置

- 远程配置：默认通过 `dynamic-env.json` 与服务端 `getEnvConfig` 端点读取运行时环境。
- 与后端联调：确保后端 `Radish.HttpApi.Host` 已启动（例如 `https://localhost:44342`），并在 CORS 允许以下来源（含 http/https，Host 会自动补全缺失协议）：
  - 在 `src/Radish.HttpApi.Host/.env` 中设置 `App__CorsOrigins=http://localhost:4200,https://localhost:4200,http://localhost:5173,https://localhost:5173`
  - 后端启动日志会打印“CORS allowed origins: ...”用于确认。
  - 预检测试（期望 204 且包含 `Access-Control-Allow-Origin`）：
    - `curl -i -X OPTIONS "https://localhost:44342/api/abp/application-configuration" -H "Origin: http://localhost:4200" -H "Access-Control-Request-Method: GET"`

可选：本地 HTTPS 证书
- 若需在本地走 HTTPS，可使用统一脚本生成并信任（一次性）：
  - Bash: `./scripts/ssl-setup.sh`
  - PowerShell: `./scripts/ssl-setup.ps1`

本地 HTTPS 验证（dev-certs 检查）
- 文件存在：确认 `dev-certs/localhost.crt` 与 `dev-certs/localhost.key`
- 启动与绑定：`cd angular && npm run start:https`（或 `yarn start:https`）应成功启动并绑定 `../dev-certs/localhost.{key,crt}`
- 浏览器/命令行校验：
  - 访问 `https://localhost:4200`，查看证书受信
  - 或执行 `curl -vk https://localhost:4200`
- 可选证书信息：`openssl x509 -in dev-certs/localhost.crt -noout -subject -issuer -dates`
- 若不受信：先执行 `mkcert -install` 再按 `dev-certs/README.md` 重新生成。

相关任务看板：见 `docs/DevelopmentPlan.md` 中“执行看板（任务拆分）”的 W1-OPS-1。

更多参考 ABP 文档：Environment（Angular）
https://abp.io/docs/latest/framework/ui/angular/environment

## 国际化（i18n）

管理端直接使用 ABP 的服务端本地化（资源 `Radish`），不维护自定义前端字典：

- 模板使用 `abpLocalization` 管道，例如：`{{ '::Welcome' | abpLocalization }}`
- 语言切换由主题提供（右上角）
- 词条与语言在后端 `src/Radish.Domain.Shared/Localization/Radish/*.json` 与 `RadishDomainSharedModule` 统一维护

详细指南：
- i18n（中文）：`docs/frontend/angular/i18n-and-guidelines.zh-CN.md`
- i18n（English）：`docs/frontend/angular/i18n-and-guidelines.en.md`
