# Internationalization Guide (Angular Admin)

This admin app ships a lightweight, custom i18n solution (no external libs) for frontend-only copies, separate from ABP server-side localization:

- Service: `src/app/i18n/i18n.service.ts`
- Pipe: `src/app/i18n/i18n.pipe.ts` (use `| t` in templates)
- Dictionaries and locale type: `src/app/i18n/messages.ts`
- Language switcher: `src/app/components/language-switcher/language-switcher.component.ts`

ABP `abpLocalization` remains for server resources (e.g., `::`/`AbpAccount::` keys). This setup targets custom UI texts.

## Quick Start
1. Read translations in templates via the `t` pipe:

```html
<h3>{{ 'app.welcome' | t }}</h3>
```

2. Or in TypeScript using the service:

```ts
import { I18nService } from 'src/app/i18n/i18n.service';
constructor(private i18n: I18nService) {}
const text = this.i18n.t('app.welcome');
```

3. Add the language switcher (already wired on Home):

```html
<app-language-switcher></app-language-switcher>
```

## Behavior
- Initial locale guessed from browser (`zh*` → `zh-CN`, otherwise `en`).
- Persisted in `localStorage('app.locale')`.
- Falls back to the key when missing.

## Maintain and Extend
In `src/app/i18n/messages.ts`:

```ts
export type Locale = 'en' | 'zh-CN'
export const messages = {
  en: { 'app.welcome': 'Welcome', /* ... */ },
  'zh-CN': { 'app.welcome': '欢迎', /* ... */ },
}
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN']
```

Recommended namespaces:
- `nav.*` navigation
- `actions.*` actions/buttons
- `aria.*` accessibility labels
- `app.*` page-level copies

### Add a New Locale
1. Extend `Locale` and add its dictionary;
2. Add it to `SUPPORTED_LOCALES`;
3. Update `options` in `language-switcher.component.ts` with a `labelKey` (e.g., `lang.ja`);
4. Add the label in dictionaries.

```ts
// Example: add Japanese
export type Locale = 'en' | 'zh-CN' | 'ja'
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN', 'ja']
// add { value: 'ja', labelKey: 'lang.ja' } to options
```

## Troubleshooting
- Key shown instead of text: define the key for the current locale in `messages.ts`.
- Switching has no effect: ensure the value exists in `SUPPORTED_LOCALES`.
- Locale lost after reload: verify `localStorage` availability/privacy settings.

## Alignment with React (Docs Only)
- The documentation structure mirrors `react/docs/i18n-and-guidelines.*.md` (Quick Start / Maintain / Troubleshooting);
- Implementation differs; only usage and maintenance conventions are aligned.
