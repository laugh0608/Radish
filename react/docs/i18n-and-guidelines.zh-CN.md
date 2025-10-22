# 国际化使用说明与开发规范（Radish React）

本项目内置一套轻量的国际化实现（非第三方库），通过 `I18nProvider`、`useI18n` 和 `messages.ts` 提供翻译、语言切换与持久化（LocalStorage）。

- Provider：`src/lib/i18n/I18nProvider.tsx`
- Hook：`src/lib/i18n/useI18n.ts`
- 词典与语言枚举：`src/lib/i18n/messages.ts`
- 上下文类型：`src/lib/i18n/I18nContext.ts`
- 应用入口注入：`src/main.tsx`

## 快速开始
1. 确认在应用入口已包裹 Provider（已配置）：`src/main.tsx`。
2. 在组件中使用 `useI18n()` 读取翻译或切换语言。
3. 在 `messages.ts` 中维护多语言文案与支持的语言列表。

示例（已存在）：

```tsx
// src/App.tsx
import { useI18n } from './lib/i18n/useI18n'

function App() {
  const { t } = useI18n()
  return (
    <>
      <h1>{t('app.welcome')}</h1>
      <p>{t('app.getStarted')}</p>
    </>
  )
}
```

## 语言切换
使用 `useI18n()` 提供的 `locale` 与 `setLocale` 实现即时切换，已提供示例组件：

```tsx
// src/components/LanguageSwitcher.tsx
const { locale, setLocale, t } = useI18n()
<select aria-label={t('aria.langSwitcher')} value={locale} onChange={(e) => setLocale(e.target.value as Locale)}>
  ...
</select>
```

Provider 会：
- 首次根据浏览器语言猜测（`zh` → `zh-CN`，否则 `en`）。
- 将选择持久化到 `localStorage('app.locale')`。
- 翻译查不到时回退为键名（便于开发阶段暴露缺失键）。

## 新增/维护文案
在 `src/lib/i18n/messages.ts`：
- 文案键名请使用命名空间前缀分组：`'模块.子域.含义'`，如 `app.welcome`、`nav.docs`、`actions.signIn`。
- 文案需同时为每种语言提供值（保持键集合一致）。

```ts
export const messages = {
  en: { 'feature.title': 'Feature', /* ... */ },
  'zh-CN': { 'feature.title': '功能', /* ... */ },
}
```

建议命名空间约定：
- `nav.*` 导航链接
- `actions.*` 按钮与操作
- `aria.*` 无障碍/辅助文本
- `app.*` 页面级文案

## 新增语言
1. 在 `messages.ts` 中扩展 `Locale` 联合类型，并新增该语言的词典：

```ts
export type Locale = 'en' | 'zh-CN' | 'ja'
export const messages = {
  en: { /* ... */ },
  'zh-CN': { /* ... */ },
  ja: { /* ... */ },
}
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN', 'ja']
```

2. 如需在语言切换器中展示，更新 `src/components/LanguageSwitcher.tsx` 的选项数组：

```ts
const options = [
  { value: 'zh-CN', labelKey: 'lang.zhCN' },
  { value: 'en', labelKey: 'lang.en' },
  { value: 'ja', labelKey: 'lang.ja' },
]
```

并在词典中补充对应 `lang.*` 标签：

```ts
// messages.ts
en: { 'lang.ja': 'Japanese', /* ... */ }
'zh-CN': { 'lang.ja': '日文', /* ... */ }
```

## 常见问题
- 文案显示为键名：该键未在当前语言字典中定义，请在 `messages.ts` 补齐。
- 重启后语言丢失：检查浏览器是否阻止本地存储或隐私模式下 `localStorage` 不可用。
- 切换不生效：确认 `setLocale()` 的传入值在 `SUPPORTED_LOCALES` 列表中。

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
- 所有用户可见文案必须走 `t('key')`，禁止硬编码字符串（除纯占位符、临时日志）。
- 键名以命名空间分组：`nav.*`、`actions.*`、`aria.*`、`app.*`、或按模块自定义前缀。
- 新增语言需同步更新 `Locale`、`messages`、`SUPPORTED_LOCALES`，并补充语言切换器选项。

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

