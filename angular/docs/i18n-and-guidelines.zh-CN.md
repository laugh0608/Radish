# 国际化使用说明与开发规范（Angular 管理端）

本管理端提供一套轻量级前端国际化方案（不依赖第三方库），用于自定义文案的翻译与运行时切换，并与 ABP 自带的服务端资源本地化互不干扰：

- 服务：`src/app/i18n/i18n.service.ts`
- 管道：`src/app/i18n/i18n.pipe.ts`（模板中使用 `| t`）
- 词典与语言类型：`src/app/i18n/messages.ts`
- 语言切换组件：`src/app/components/language-switcher/language-switcher.component.ts`

ABP 的 `abpLocalization` 仍然用于 `::`/`AbpAccount::` 等后端资源键，本方案专注于前端自定义文案。

## 快速开始
1. 在需要使用的模板中，通过 `| t` 管道读取翻译：

```html
<h3>{{ 'app.welcome' | t }}</h3>
```

2. TypeScript 中使用服务读取：

```ts
import { I18nService } from 'src/app/i18n/i18n.service';
constructor(private i18n: I18nService) {}
const text = this.i18n.t('app.welcome');
```

3. 页面引入语言切换组件（已接入首页示例）：

```html
<app-language-switcher></app-language-switcher>
```

## 行为说明
- 首次加载根据浏览器语言猜测：`zh*` → `zh-CN`，否则 `en`。
- 选择的语言持久化到 `localStorage('app.locale')`。
- 未命中键时回退为键名，便于开发阶段发现缺失文案。

## 维护与扩展
在 `src/app/i18n/messages.ts`：

```ts
export type Locale = 'en' | 'zh-CN'
export const messages = {
  en: { 'app.welcome': 'Welcome', /* ... */ },
  'zh-CN': { 'app.welcome': '欢迎', /* ... */ },
}
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN']
```

键名建议采用命名空间分组：
- `nav.*` 导航链接
- `actions.*` 按钮与操作
- `aria.*` 无障碍/辅助文本
- `app.*` 页面级文案

### 新增语言
1. 扩展 `Locale` 联合类型并新增该语言词典；
2. 将语言加入 `SUPPORTED_LOCALES`；
3. 在 `language-switcher.component.ts` 的 `options` 中补充对应 `labelKey`（如 `lang.ja`）；
4. 在词典中补充 `lang.*` 标签。

```ts
// 例：加入日文
export type Locale = 'en' | 'zh-CN' | 'ja'
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN', 'ja']
// options 增加 { value: 'ja', labelKey: 'lang.ja' }
```

## 常见问题
- 文案显示为键名：该键未在当前语言字典中定义，请在 `messages.ts` 补齐。
- 切换不生效：确认传入值位于 `SUPPORTED_LOCALES` 列表中。
- 重启后语言丢失：检查是否启用隐私模式或禁用了 `localStorage`。

## 与 React 项目的一致性（仅文档层面）
- 文档组织与说明方法参考 `react/docs/i18n-and-guidelines.*.md`，保持“快速开始 / 维护与扩展 / 常见问题”的结构；
- 实现细节不共享，仅对齐使用方式与维护约定。
