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

- 已在 `script/start.js` 中优先支持 HTTPS。
- 方式 A（推荐）：安装基础 SSL 插件
  - `npm i -D @vitejs/plugin-basic-ssl`
  - `npm run dev` 后即可通过 `https://localhost:5173` 访问
- 方式 B：自备证书
  - 将 `localhost.crt` 与 `localhost.key` 放到 `react/script/certs/`
  - `npm run dev` 即可
- 若需关闭 HTTPS：在启动前设置 `DEV_HTTPS=0`

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

