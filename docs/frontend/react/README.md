# 前端（React + Vite）开发指南

React 子项目使用 Vite 与 TypeScript，已集成 ABP 的服务端本地化。以下为本地开发与联调指引。

## 本地运行

```bash
cd react
npm install
npm run dev
```

- 默认端口：`http://localhost:5173`（若启用 HTTPS，则为 `https://localhost:5173`）
- 与后端联调：在 `.env.local` 或启动环境中设置 `VITE_API_BASE_URL` 指向后端，例如：

```bash
VITE_API_BASE_URL=https://localhost:44342
```

## HTTPS 开发服务器

- 已在 `react/script/start.js` 中优先支持 HTTPS，并优先读取仓库根目录 `dev-certs/` 证书。
- 推荐使用统一脚本生成并信任证书（一次性）：
  - Bash: `./scripts/ssl-setup.sh`
  - PowerShell: `./scripts/ssl-setup.ps1`
- 启动后使用 `https://localhost:5173` 访问；首次访问若提示不安全，按浏览器提示接受一次即可（使用 mkcert 生成时通常会直接信任）。
- 若需关闭 HTTPS：在启动前设置 `DEV_HTTPS=0`。

与后端联调的 CORS 要点
- 确保后端 `.env` 已包含本前端来源：
  - `src/Radish.HttpApi.Host/.env` 中设置：`App__CorsOrigins=https://localhost:5173,https://localhost:4200`
- 后端启动时会在日志打印“CORS allowed origins: ...”用以确认。

## 构建与校验

```bash
npm run build    # 生产构建到 dist/
npm run preview  # 本地预览
npm run lint     # ESLint 校验
```

## ABP 集成与国际化（i18n）

- 运行时从后端获取本地化资源：`/api/abp/application-configuration?includeLocalizationResources=true`
- 在前端通过 `I18nProvider` 与 `useI18n()` 暴露 `t()`
- 建议使用 ABP 风格键：`::Key` 或 `Resource::Key`（默认资源 `Radish`）

详细指南：
- i18n（中文）：`docs/frontend/react/i18n-and-guidelines.zh-CN.md`
- i18n（English）：`docs/frontend/react/i18n-and-guidelines.en.md`
