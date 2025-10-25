# Internationalization Guide (Radish React)

The React app now aligns with ABP server-side localization. It fetches resources from `/api/abp/application-configuration?includeLocalizationResources=true` and exposes them via `I18nProvider`/`useI18n`. The local `messages.ts` serves as a fallback only.

- Provider: `src/lib/i18n/I18nProvider.tsx` (fetches ABP resources)
- Hook: `src/lib/i18n/useI18n.ts`
- Context types: `src/lib/i18n/I18nContext.ts` (locales: `en`, `zh-Hans`)
- Fallback dictionary: `src/lib/i18n/messages.ts`
- Mounted at entry: `src/main.tsx`

## Quick Start
1. Set backend base URL via Vite env: `VITE_API_BASE_URL=https://localhost:44342`.
2. Use `useI18n()` inside components to read translations or switch locale.

Example (already present):

```tsx
// src/App.tsx
import { useI18n } from './lib/i18n/useI18n'

function App() {
  const { t } = useI18n()
  return (
    <>
      <h1>{t('::Welcome')}</h1>
      <p>{t('::GetStarted')}</p>
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
- Guesses initial locale from browser (`zh*` → `zh-Hans`, else `en`).
- Persists selection in `localStorage('app.locale')`.
- First tries ABP resources; if missing, falls back to local dictionary; else returns the key (to expose gaps).

## Adding or Updating Messages
Prefer adding UI copies in backend resources:
- `src/Radish.Domain.Shared/Localization/Radish/en.json`
- `src/Radish.Domain.Shared/Localization/Radish/zh-Hans.json`

For temporary UI-only strings, you may still use the fallback dictionary `src/lib/i18n/messages.ts`.

```ts
// Temporary fallback dictionary example (if needed)
export const messages = {
  en: { 'feature.title': 'Feature' },
  'zh-Hans': { 'feature.title': '功能' },
}
```

Recommended namespaces:
- `nav.*` navigation links
- `actions.*` buttons and actions
- `aria.*` accessibility texts
- `app.*` page-level copies

## Adding a New Locale
1. Register new language in backend `RadishDomainSharedModule` and add JSON resources.

```ts
export type Locale = 'en' | 'zh-Hans' | 'ja'
export const messages = {
  en: { /* ... */ },
  'zh-CN': { /* ... */ },
  ja: { /* ... */ },
}
export const SUPPORTED_LOCALES: Locale[] = ['en', 'zh-Hans', 'ja']
```

2. Expose it in the switcher by updating options in `src/components/LanguageSwitcher.tsx`:

```ts
const options = [
  { value: 'zh-Hans', labelKey: 'lang.zhCN' },
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
