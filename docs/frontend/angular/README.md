# 前端（Angular 管理端）开发指南

Angular 管理端遵循 ABP 的 UI 集成规范，运行与打包流程如下。

## 本地运行

```bash
cd angular
yarn install && yarn start
# 或
npm install && npm run start
```

启动后访问 `https://localhost:4200/`（已在 `angular.json` 中启用本地 HTTPS 并统一使用仓库根 `dev-certs/` 证书）。

## 构建与测试

```bash
ng build            # 产物位于 dist/
ng test             # 单元测试（Karma）
ng e2e              # 端到端测试
```

可通过 `ng generate component component-name` 进行脚手架生成。

## 环境与配置

- 远程配置：默认通过 `dynamic-env.json` 与服务端 `getEnvConfig` 端点读取运行时环境。
- 与后端联调：确保后端 `Radish.HttpApi.Host` 已启动（例如 `https://localhost:44342`），并在 CORS 允许以下来源：
  - 在 `src/Radish.HttpApi.Host/.env` 中设置 `App__CorsOrigins=https://localhost:4200,https://localhost:5173`
  - 后端启动日志会打印“CORS allowed origins: ...”用于确认。

本地 HTTPS 证书
- 推荐使用统一脚本生成并信任（一次性）：
  - Bash: `./scripts/ssl-setup.sh`
  - PowerShell: `./scripts/ssl-setup.ps1`

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
