# Internationalization Guide and Dev Practices (Radish React)

This project ships a lightweight, custom i18n setup (no external lib) powered by `I18nProvider`, `useI18n`, and `messages.ts`. It supports translation, runtime locale switching, and persistence via LocalStorage.

- Provider: `src/lib/i18n/I18nProvider.tsx`
- Hook: `src/lib/i18n/useI18n.ts`
- Dictionaries and locale type: `src/lib/i18n/messages.ts`
- Context types: `src/lib/i18n/I18nContext.ts`
- Mounted at entry: `src/main.tsx`

## Quick Start
1. Ensure the app is wrapped with `I18nProvider` (already configured) in `src/main.tsx`.
2. Use `useI18n()` inside components to read translations or switch locale.
3. Maintain translations and supported locales in `messages.ts`.

Example (already present):

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

## Switching Locale
Use `locale` and `setLocale` from `useI18n()` to switch instantly. A ready-to-use component exists:

```tsx
// src/components/LanguageSwitcher.tsx
const { locale, setLocale, t } = useI18n()
<select aria-label={t('aria.langSwitcher')} value={locale} onChange={(e) => setLocale(e.target.value as Locale)}>
  ...
</select>
```

Provider behavior:
- Guesses initial locale from browser language (`zh*` → `zh-CN`, otherwise `en`).
- Persists selection in `localStorage('app.locale')`.
- Falls back to the key when a translation is missing (handy during development).

## Adding or Updating Messages
In `src/lib/i18n/messages.ts`:
- Use namespaced keys: `'module.subdomain.meaning'`, e.g., `app.welcome`, `nav.docs`, `actions.signIn`.
- Keep key sets identical across locales (provide values for all languages).

```ts
export const messages = {
  en: { 'feature.title': 'Feature', /* ... */ },
  'zh-CN': { 'feature.title': '功能', /* ... */ },
}
```

Recommended namespaces:
- `nav.*` navigation links
- `actions.*` buttons and actions
- `aria.*` accessibility texts
- `app.*` page-level copies

## Adding a New Locale
1. Extend the `Locale` union and add a dictionary in `messages.ts`:

```ts
export type Locale = 'en' | 'zh-CN' | 'ja'
export const messages = {
  en: { /* ... */ },
  'zh-CN': { /* ... */ },
  ja: { /* ... */ },
}
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-CN', 'ja']
```

2. Expose it in the switcher by updating options in `src/components/LanguageSwitcher.tsx`:

```ts
const options = [
  { value: 'zh-CN', labelKey: 'lang.zhCN' },
  { value: 'en', labelKey: 'lang.en' },
  { value: 'ja', labelKey: 'lang.ja' },
]
```

Also add `lang.*` labels in dictionaries:

```ts
// messages.ts
en: { 'lang.ja': 'Japanese', /* ... */ }
'zh-CN': { 'lang.ja': '日文', /* ... */ }
```

## Troubleshooting
- Key is shown instead of translated text: add the key for the current locale in `messages.ts`.
- Locale resets after reload: verify LocalStorage availability or privacy mode settings.
- Switching has no effect: ensure the value passed to `setLocale()` exists in `SUPPORTED_LOCALES`.

---

# Lightweight Development Guidelines

Tech & structure
- TypeScript + React function components, Vite workspace. Place features under `src/`; bundler-visible assets under `src/assets`; web-visible files in `public/`.
- Entrypoint/global styles: `src/main.tsx`, `src/index.css`. Shared components live in `src/components`.

Code style
- 2-space indentation, single quotes, trailing commas where practical. Run `npm run lint` and fix warnings pre-commit.
- Components in PascalCase (e.g., `UserBadge.tsx`), hooks in camelCase prefixed by `use` (`useFeature`).
- Utilities in `src/lib`; co-locate styles with features. Keep `index.css` for resets/tokens only.

I18n rules
- All user-facing strings must go through `t('key')`; avoid hardcoded strings (except temporary placeholders/logs).
- Namespaced keys such as `nav.*`, `actions.*`, `aria.*`, `app.*`, or module-specific prefixes.
- When adding a locale, update `Locale`, `messages`, `SUPPORTED_LOCALES`, and the language switcher options.

Commits & PRs
- Follow Conventional Commits: `feat:`, `fix:`, `docs:`; imperative mood; ≤ 72 chars.
- PRs should describe scope, link backlog IDs, and include screenshots/recordings for UI changes; ensure `npm run build` and `npm run lint` pass.

Testing
- Automated tests are optional for now. When introducing behavior, consider Vitest/RTL colocated `*.test.tsx`. Document manual test steps in the PR.

Local commands
- `npm run dev` start dev server (HMR).
- `npm run build` produce production bundle to `dist/`.
- `npm run preview` serve the latest build.
- `npm run lint` run ESLint and fix issues.

