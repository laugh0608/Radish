# 国际化使用说明（Radish React）

React 子项目已对齐 ABP 本地化：通过调用后端 `/api/abp/application-configuration?includeLocalizationResources=true` 获取资源，并在前端提供 `I18nProvider`/`useI18n`。自定义字典 `messages.ts` 仅作为兜底，不再是主数据源。

- Provider：`src/lib/i18n/I18nProvider.tsx`（从 ABP 拉取）
- Hook：`src/lib/i18n/useI18n.ts`
- 上下文类型：`src/lib/i18n/I18nContext.ts`（语言为 `en` / `zh-Hans`）
- 兜底字典：`src/lib/i18n/messages.ts`（可逐步减少直至移除）
- 入口注入：`src/main.tsx`

## 快速开始
1. 设置后端地址环境变量（Vite）：`VITE_API_BASE_URL=https://localhost:44342`。
2. 在组件中使用 `useI18n().t()` 读取文案，建议使用 ABP 风格键：

```tsx
<h1>{t('::Welcome')}</h1>
<p>{t('::GetStarted')}</p>
```

- `Resource::Key` 或 `::Key`（默认资源 `Radish`）均可。
- 如服务端无该键，将回退到本地 `messages.ts`，再回退为键名（便于识别缺失）。

## 语言切换
`LanguageSwitcher` 切换为 `en` 与 `zh-Hans`，切换后重新从服务端获取资源。也会将选择写入 `localStorage('app.locale')`。

## 在后端新增/维护词条
与 Angular 一致：在
- `src/Radish.Domain.Shared/Localization/Radish/en.json`
- `src/Radish.Domain.Shared/Localization/Radish/zh-Hans.json`
添加键值，如：

```jsonc
// en.json
"GetStarted": "Getting Started"
```

```jsonc
// zh-Hans.json
"GetStarted": "快速上手"
```

并在 `RadishDomainSharedModule` 的 `Configure<AbpLocalizationOptions>` 中确保已注册 `en` 与 `zh-Hans`。

## 常见问题
- 403/跨域：确保后端 CORS 允许 React 开发端口；必要时使用同域或代理。
- 未生效：确认键已写入后端 JSON 并重启后端；或清空浏览器缓存。
- 键显示为原文：说明后端未提供该键且本地兜底也无，补齐资源后即可。

---

# 精简开发规范（与仓库约定一致）

**技术栈与结构**
- 使用 TypeScript 与 React 函数组件，Vite 工程；功能模块置于 `src/`，静态资源到 `src/assets`，对外可见文件放 `public/`。
- 入口、全局样式：`src/main.tsx`、`src/index.css`，全局布局与共享组件放置在 `src/components`。

**代码风格**
- 缩进 2 空格、单引号、尽量保留结尾逗号；提交前运行 `npm run lint` 并修复告警。
- 组件命名 PascalCase（如 `UserBadge.tsx`），Hook 以 `use` 开头的 camelCase（如 `useFeature`）。
- 工具函数放 `src/lib`；样式就近存放于特性目录，`index.css` 仅承载重置与设计令牌。

**国际化约定**
- 用户可见文案优先走 ABP 资源键：`::Key` 或 `Resource::Key`；对未迁移部分可临时使用本地兜底键。
- 逐步将本地兜底文案迁移至后端资源，保持与 Angular 一致。

**提交与 PR**
- 提交信息遵循 Conventional Commits：`feat:`、`fix:`、`docs:` 等，英文祈使句，72 字符内。
- PR 描述范围、引用任务 ID；若涉及 UI，附带截图或录屏；保证 `npm run build` 与 `npm run lint` 通过。

**测试**
- 当前未强制自动化测试；引入新逻辑时可使用 Vitest/RTL 在同目录新增 `*.test.tsx`；PR 说明手工验证步骤。

**本地命令**
- `npm run dev` 启动开发服务器（HMR）。
- `npm run build` 生成生产构建至 `dist/`。
- `npm run preview` 本地预览最新构建。
- `npm run lint` 运行 ESLint 并修复问题。
