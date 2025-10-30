# 国际化使用说明（Angular 管理端）

本项目统一采用 ABP 的服务端本地化机制（Radish 资源）。前端不再维护自定义 i18n 服务/管道/词典。

关键点
- 模板使用 `abpLocalization` 管道，如：`{{ '::Welcome' | abpLocalization }}`。
- 右上角语言切换由主题提供，切换后会重新应用 ABP 语言资源。
- 词条和语言均在后端项目维护：`src/Radish.Domain.Shared/Localization/Radish/*.json` 与 `RadishDomainSharedModule`。

## 前端用法
- 在 HTML 模板：

```html
<h4>{{ '::GetStarted' | abpLocalization }}</h4>
```

- 在 TypeScript（如需要）：通过 ABP Angular 提供的 `LocalizationService` 获取。

## 后端维护
1. 词条：在以下文件中新增/修改键值：
   - `src/Radish.Domain.Shared/Localization/Radish/en.json`
   - `src/Radish.Domain.Shared/Localization/Radish/zh-Hans.json`
2. 语言清单与默认资源：`src/Radish.Domain.Shared/RadishDomainSharedModule.cs` 的 `Configure<AbpLocalizationOptions>`。

示例键
```jsonc
// en.json 的 Texts 里
"GetStarted": "Getting Started",
"Tutorial": "Web Application Development Tutorial",
"ExploreTutorial": "Explore Tutorial"
```

```jsonc
// zh-Hans.json 的 texts 里
"GetStarted": "快速上手",
"Tutorial": "Web 应用开发教程",
"ExploreTutorial": "查看教程"
```

## 迁移说明（从旧方案到 ABP）
- 已移除：`src/app/i18n/*` 与 `src/app/components/language-switcher/*`。
- 首页模板已将 `| t` 全部替换为 `| abpLocalization`。
- 如需新增界面文案，请直接在后端资源 JSON 中添加键值。

## 常见问题
- 文案未生效：确认键已写入对应文化 JSON，并重启后端或清缓存。
- 找不到键：模板中使用 `::Key` 形式时，默认资源为 `Radish`；如引用其它资源需带资源名。

