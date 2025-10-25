# Internationalization Guide (Angular Admin)

The admin app now relies solely on ABP server-side localization (Radish resource). No custom frontend i18n service/pipe/dictionaries are used.

Highlights
- Use the `abpLocalization` pipe in templates, e.g. `{{ '::Welcome' | abpLocalization }}`.
- The top-right language switcher is provided by the theme and applies ABP culture.
- Keys and languages live in the backend project: `src/Radish.Domain.Shared/Localization/Radish/*.json` and `RadishDomainSharedModule`.

## Frontend usage
- In HTML:

```html
<h4>{{ '::GetStarted' | abpLocalization }}</h4>
```

- In TypeScript (if needed): use ABP Angular `LocalizationService` to resolve strings.

## Backend maintenance
1. Add/modify keys in:
   - `src/Radish.Domain.Shared/Localization/Radish/en.json`
   - `src/Radish.Domain.Shared/Localization/Radish/zh-Hans.json`
2. Configure languages/default resource in `src/Radish.Domain.Shared/RadishDomainSharedModule.cs` (`Configure<AbpLocalizationOptions>`).

Example keys
```jsonc
// en.json Texts
"GetStarted": "Getting Started",
"Tutorial": "Web Application Development Tutorial",
"ExploreTutorial": "Explore Tutorial"
```

```jsonc
// zh-Hans.json texts
"GetStarted": "快速上手",
"Tutorial": "Web 应用开发教程",
"ExploreTutorial": "查看教程"
```

## Migration notes
- Removed: `src/app/i18n/*` and `src/app/components/language-switcher/*`.
- Home page replaces all `| t` with `| abpLocalization`.
- Add new UI copies directly to backend resource JSON.

## Troubleshooting
- Text not applied: ensure the key exists in the correct culture JSON and backend cache is refreshed.
- Unknown key: when using `::Key`, the default resource is `Radish`; include resource name if using another.
