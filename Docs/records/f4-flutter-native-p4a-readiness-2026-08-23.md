# Flutter Native P4-A 实施就绪审计

> 状态：`P4-A readiness` 已完成；后续 `P4-B1 / P4-B2` 已实施，当前等待 `P4-B3 Discover` 授权
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 设计基线：[P3 代表设计记录](/records/f4-flutter-native-p3-representative-design-2026-08-19)

## 1. 本批结论

P2 已提供可继承的四主题状态机、语义颜色、三档窗口等级、键盘切换和代表页行为，不需要重写业务 owner；P3 已冻结 Web 家族壳层、排版、密度和代表页面。P4 可以进入实现，但必须按真实职责分批，不能继续把视觉逻辑堆入现有巨型页面。

本批形成以下裁决：

1. 字体使用 Noto 官方仓库的 `NotoSansSC-VF.ttf` 与 `NotoSerifSC-VF.ttf` **简体中文区域子集变量 TTF**；不按仓库当前文案二次裁字，避免论坛、评论、文档和用户资料出现缺字。
2. 字体通过 Flutter `pubspec.yaml` 原生 `fonts` 声明接入，不新增 `google_fonts`；字体声明和资产本身不改变 `pubspec.lock`。
3. P3 已明确采用 Lucide 线性图标；当前 `cupertino_icons` 只存在于依赖清单、运行时代码零引用。实施时建议以固定版本 `lucide_icons_flutter 3.1.15` 替换 `cupertino_icons`，该操作会改变 `pubspec.yaml` 与 `pubspec.lock`，必须单独授权并执行依赖门禁。
4. Shell 保留 Flutter 当前五个真实主入口“发现 / 论坛 / 文档 / 榜单 / 我的”，只继承 Web 的几何、层级和图标语法；不会为对齐母版伪造尚未进入 Flutter 范围的聊天入口。
5. Discover 若只给现有 Forum / Docs / Shop 摘要卡换皮，无法达到已确认母版。P4 应改为消费后端既有 `/api/v1/PublicDiscover/GetFeed` 公开发现读模型；Forum / Docs 目标保持原生跳转，`Messages` 目标只读表达 Web 能力边界，不扩建 Flutter 聊天。
6. `radish_flutter_shell.dart` 与 `forum_detail_page.dart` 已越过仓库文件上限。Shell、通知面、Forum 详情编排、回答、轻回应和评论必须先按 owner 拆分，再实施布局。

本批只修改文档，没有下载字体、安装或更新包、修改 Dart 运行时代码、修改平台工程、启动服务或执行真实 Smoke。

## 2. 当前代码事实

| 领域 | 当前 owner 与规模 | 可继承事实 | 主要阻断 |
| --- | --- | --- | --- |
| App / Theme | `lib/app/app.dart`；`lib/core/theme/radish_theme.dart` `341` 行 | `RadishThemeController`、四主题 ID、Shop entitlement、持久化、`ThemeExtension`、主题恢复 | `ThemeData` 没有 Noto 字体、正式 `TextTheme`、密度、控件状态和表面层级；主题动画仍为单一 `180ms` |
| Motion | `lib/core/theme/radish_motion.dart` `7` 行 | 可按 `MediaQuery.disableAnimations` 归零 | 没有 `120 / 200 / 280ms` token；页面仍散落 `80 / 280ms` 常量 |
| Shared | `lib/shared/widgets/` 只有 `PhaseScopeCard`、`PublicLinkCopyPanel`、`ReadOnlyMarkdownView` | 公开链接和 Markdown owner 可继续复用 | 没有 P3 的 Button、State Chip、State Slot、连续 section surface 或壳层组件；Discover / Forum 重复实现 loading / error / stale 表面 |
| Shell | `radish_flutter_shell.dart` `1918` 行；`radish_adaptive_navigation.dart` `143` 行 | 五 tab `IndexedStack`、Android Back、OIDC、通知、来源返回、`Ctrl/Cmd + 1..5` | Compact 仍为全宽 Material `NavigationBar`；medium / expanded 仍为全局 `NavigationRail`；常驻环境 / 会话 / 通知 chip 带不属于正式产品壳层；通知 sheet 混在 Shell 巨型文件 |
| Theme Selector | `radish_theme_selector.dart` `228` 行 | compact sheet / expanded dialog、权益状态读取 | 点击即持久化或激活，没有预览—确认；未拥有主题没有 Shop 引导 |
| Discover | `discover_page.dart` `945` 行；`DiscoverFeedController` + `DiscoverRepository` | 旧快照刷新保留、Forum / Docs / Shop 局部失败、原生 handoff | 仍是通用 Card 两列；数据 owner 聚合三个旧接口，缺少 Web 正式发现流的 cursor、pulse、actor、target 与连续内容结构 |
| Forum Detail | `forum_detail_page.dart` `3768` 行；测试 `2232` 行 | 详情、回答、轻回应、评论 / 子评论、幂等提交、登录回流和来源返回完整 | 单文件混合编排和全部视图；expanded 仅“阅读面 + `304px` rail”，不是 `220 / 820 / 250`；自身 AppBar 与全局壳层职责重叠；连续阅读面仍层层 Card |

目标文件未发现页面级硬编码主题颜色；颜色主要通过 `Theme.of(context).colorScheme` 消费。这一基础应保留，P4 不做按主题 ID 分叉的页面代码。

## 3. P3 设计到代码差分

| 差分 | P2 当前事实 | P3 / P4 目标 | 实施 owner |
| --- | --- | --- | --- |
| Typography | 系统默认字体与 Material 默认 scale | UI / 正文 / 数据用 `Noto Sans SC`；页面主标题和少量品牌强调用 `Noto Serif SC`；正文 `15–16px`、常规标题 `18–24px`、展示标题约 `32px` | `core/theme/radish_typography.dart` + `ThemeData.textTheme` |
| 语义 token | `RadishThemeTokens` 只有颜色和 `8 / 12 / 18` 圆角 | 增加 spacing、density、surface、focus、最小触控目标和组件状态；颜色继续保持四主题集中映射 | `core/theme/` |
| Motion | `180ms` 主题过渡；页面散落常量 | `120 / 200 / 280ms` 三档并统一 reduced-motion 归零 | `RadishMotion` |
| 共享状态 | 各页私有 loading / error / stale Card | `RadishStateSlot` 统一状态骨架，业务文案、动作和旧快照仍由页面 owner 提供 | `shared/widgets/` |
| 图标 | Material Icons；`cupertino_icons` 未消费 | P3 Lucide 线性图标，导航 `18px`；不保留第二套未使用图标依赖 | `lucide_icons_flutter` + shared icon mapping |
| 主题选择 | 点击即提交 | 本地预览、取消恢复、显式确认；权益主题继续由 controller 激活；未拥有只解释并引导 Shop | Theme Selector + Shell callback |
| Compact Shell | 默认 AppBar + 全宽 `NavigationBar` | `64px` Web 家族品牌栏；安全区内约 `358 × 64` 悬浮胶囊，单 tab `52px`、标签 `11px`、触控目标不少于 `48px` | `radish_adaptive_navigation.dart` 拆为 Shell frame / mobile tab bar |
| Expanded Shell | AppBar + 永久全局 `NavigationRail` | `68px` Web 家族顶部全局栏；品牌、一级导航、弹性动作区、通知 / 主题 / 账号保持同一层级；不保留全局侧栏 | Shell frame / expanded header |
| Medium Shell | 全局窄 rail | 全局仍使用 Web 家族顶栏；`72px` rail 只在确有列表—详情关系的页面内出现 | 各页面 context owner，不放入全局 Shell |
| Shell 状态 | 环境、会话、最近阅读、通知等常驻 chip 带 | 环境标识退出产品 UI；recent、通知、登录错误只在顶栏动作或任务上下文中出现 | Shell coordinator + Notification surface |
| 导航语义 | 发现 / 论坛 / 文档 / 榜单 / 我的 | 保留五个已实现入口；仅复用 Web 几何和视觉语法，不把母版“聊天 / 更多”当成新功能授权 | Shell destinations |
| Discover 内容 | Forum / Docs / Shop 三段摘要卡 | 使用既有 Public Discover cursor feed 形成焦点流、actor / metric、community pulse 和上下文入口；Shop / Leaderboard 仍是从属入口 | Discover models / repository / controller / page |
| Discover target | internal forum ID、docs slug、product ID | `ForumPost` 与 `Docs` 原生打开；`Messages` 项只读展示“Web 提供”，不扩 Flutter Chat | Discover handoff mapping |
| Forum compact | 自身 AppBar + 大 Card 单轴 | 壳层品牌栏之下保持正文—回答—轻回应—评论连续滚动；减少嵌套 Card，保留系统返回、键盘避让和定位滚动 | Forum detail page + section widgets |
| Forum expanded | reading pane + `304px` context rail | 页面级 `220 / 820 / 250` 社区导航—连续正文—线程索引；左右栏不承担全局应用导航 | Forum detail layout / context rails |
| 文件边界 | Shell、Forum Detail 及其测试超限 | Shell 通知面独立；Forum 编排、正文 / 回答、轻回应、评论 / 子评论和 rail 分文件；测试按同一职责拆分 | 对应 feature presentation / test |

## 4. 字体资产裁决

### 4.1 来源与格式

固定从 Noto 官方仓库的已发布 tag 取文件，不跟随 `main` 漂移：

| 字族 | 固定版本与文件 | 官方页面标注大小 | 用途 |
| --- | --- | --- | --- |
| Noto Sans SC | `Sans2.004` · `Sans/Variable/TTF/Subset/NotoSansSC-VF.ttf` | `16.9 MB` | 全局 UI、正文、表单、导航和数据 |
| Noto Serif SC | `Serif2.003` · `Serif/Variable/TTF/Subset/NotoSerifSC-VF.ttf` | `24 MB` | 页面主标题和少量品牌强调 |

选择 TTF 而非 variable OTF，是因为官方 `Sans2.004` 发布说明明确警告 Windows 10 / 11 对 CFF2 variable fonts 存在文本损坏风险；Flutter desktop 是长期正式目标，TTF 可以避免现在埋入已知跨平台风险。Flutter 官方支持 `.ttf / .otf / .ttc`，当前锁定的 Flutter `3.44.0` 也已包含 `3.41` 稳定生效的 variable font `FontWeight` 行为，因此每个字族只需一个变量字体文件，不需要复制七个静态字重。

这里的“Subset”是 Noto 上游提供的 **Simplified Chinese region-specific subset**，不是根据 Radish 当前字符串生成的项目子集。禁止用 `pyftsubset` 只保留现有 UI 文案；Radish 有公开帖子、评论、文档、昵称和多语言用户内容，固定字符清单无法保证运行时覆盖。

### 4.2 许可证与可追溯性

两套字体均为 SIL Open Font License 1.1。实施时必须同时提交：

- 两个原始文件名不变的字体资产；
- Sans / Serif 各自的官方 `LICENSE` 文本，并通过 `pubspec.yaml assets` 打入应用；
- `assets/fonts/noto/README.md`，记录 tag、上游 URL、下载日期、字节数和本地 `SHA-256`；
- 下载后校验官方 manifest。当前官方 manifest 记录的 `SHA-256` 为：
  - `NotoSansSC-VF.ttf`：`d68bafcb48a2707749396aa12bbbd833cb70401f3a9a689fd2902c7e0d295964`
  - `NotoSerifSC-VF.ttf`：`5326cfb097e3ab26fcb39329752b5c0a439bf8d5c4649520e4b492939c352a09`

若固定 tag 下载结果与记录不一致，停止接入并重新核对 release / manifest，不以“文件能打开”为理由跳过供应链校验。

### 4.3 Flutter 声明与回退链

计划在 `pubspec.yaml` 中直接声明两个 family：

```yaml
flutter:
  uses-material-design: true
  assets:
    - assets/fonts/noto/licenses/
  fonts:
    - family: NotoSansSC
      fonts:
        - asset: assets/fonts/noto/NotoSansSC-VF.ttf
    - family: NotoSerifSC
      fonts:
        - asset: assets/fonts/noto/NotoSerifSC-VF.ttf
```

回退链固定为：

1. 常规文字：`NotoSansSC` → Flutter 平台默认字体；
2. 展示 / 主标题：`NotoSerifSC` → `NotoSansSC` → Flutter 平台默认字体；
3. Emoji、非简体中文区域字形和两套资产均未覆盖的脚本，最终由 Android、Apple、Windows 或 Linux 的平台默认字体承接。

Flutter 的 `fontFamilyFallback` 会按顺序查找，全部未命中后自动使用平台默认字体，因此不在共享 Dart Theme 中硬编码 `PingFang SC`、`Microsoft YaHei UI` 或发行版特定 Linux 字体名。这样既保持离线主字族一致，又不把尚未生成的平台工程绑定到脆弱的系统字体字符串。

### 4.4 包体积与依赖影响

- 两个原始字体资产合计约 `40.9 MB`；这是源码资产规模，不等同于最终 APK、AAB 或各桌面产物增量。
- P4-B1 必须在接入前后各构建一次 Android release APK / AAB，记录文件大小和 bundle analyzer 结果。若实际增量异常或影响既定分发预算，先停下讨论交付策略，不回退到运行时网络字体，也不擅自裁掉用户内容字形。
- 原生 `fonts` / `assets` 声明不引入 Dart package，因此不会改变 `pubspec.lock`。
- 不采用 `google_fonts`：它会增加 package 及传递依赖，并保留运行时 HTTP 获取能力；Radish 已明确生产离线随包交付，直接 `pubspec` 声明更简单、可审计。

### 4.5 上游依据

- Flutter 官方：[使用自定义字体](https://docs.flutter.dev/cookbook/design/fonts)、[`fontFamilyFallback`](https://api.flutter.dev/flutter/painting/TextStyle/fontFamilyFallback.html) 与 [variable font weight 迁移说明](https://docs.flutter.dev/release/breaking-changes/font-weight-variation)。
- Noto 官方：[Noto CJK 仓库](https://github.com/notofonts/noto-cjk)、[Sans `2.004` release](https://github.com/notofonts/noto-cjk/releases/tag/Sans2.004)、[Serif `2.003` release](https://github.com/notofonts/noto-cjk/releases/tag/Serif2.003)、[Sans OFL](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE) 与 [Serif OFL](https://github.com/notofonts/noto-cjk/blob/main/Serif/LICENSE)。
- 未采用方案的官方说明：[Google Fonts for Flutter](https://pub.dev/packages/google_fonts)。

## 5. 图标依赖裁决

P3 活动设计源和代表记录均冻结 Lucide 线性图标；当前 `cupertino_icons ^1.0.8` 没有任何 Dart 引用。为避免 Material、Cupertino 与 Lucide 三套图标长期混用，建议在 P4-B1：

1. 删除未使用的 `cupertino_icons` 直接依赖；
2. 新增精确版本 `lucide_icons_flutter: 3.1.15`，不使用 caret 自动漂移；
3. 只通过一个 Radish icon mapping 暴露当前产品需要的图标，不让页面直接形成任意图标依赖；
4. 保留包 MIT license 记录，并在依赖更新后执行 package security / license 与构建门禁。

候选依赖以 [`lucide_icons_flutter 3.1.15`](https://pub.dev/packages/lucide_icons_flutter/versions/3.1.15) 的发布页与包内 license 为供应链依据。该包只有 Flutter 直接依赖，没有平台 plugin，但会修改 `pubspec.yaml` 与 `pubspec.lock`，也需要运行 `flutter pub get`。这部分与字体二进制下载一并等待项目所有者授权。

## 6. P4 实施拆批

### P4-B1：Theme Foundation + Shared Primitives

- 接入并校验两套字体与许可证；替换未使用图标依赖。
- 将颜色、typography、spacing / density、surface、motion 从单一 `radish_theme.dart` 按职责拆分。
- 完成四主题 `TextTheme`、控件主题、焦点和 `120 / 200 / 280ms`。
- 新增 `RadishStateSlot`、`RadishStateChip`、连续 section surface 和受控 icon mapping。
- 主题选择器改为预览—确认—取消；未拥有主题只引导 Shop。
- 门禁：`dart format`、`flutter analyze`、Theme / Selector / shared widget 定向测试、四主题 typography / contrast 契约、release 包体积基线。

### P4-B2：Web-Family Adaptive Shell

- 先把通知 sheet / tile / 状态从 `radish_flutter_shell.dart` 拆出。
- Compact 落地品牌栏与安全区胶囊底栏；expanded 落地顶部全局栏；medium 不再使用全局 rail。
- 保留五个真实 destination、`IndexedStack`、Android Back、OIDC、通知、主题、来源返回和键盘快捷键。
- 删除常驻环境 / 会话 chip 带；recent、通知和登录问题进入对应动作或上下文状态。
- 门禁：`390 / 800 / 1440` widget tests、安全区、键盘、焦点、reduced-motion、五入口行为回归。

### P4-B3：Discover 正式读模型与代表页

- 将 Flutter Discover repository / models / controller 迁移到既有 `PublicDiscover/GetFeed` cursor 读模型，不新增后端 API。
- 保留旧快照刷新、过期响应丢弃、分页去重和结构化错误；Forum / Docs 进入现有原生 handoff，Messages 目标只读表达边界。
- 按 P3 形成 compact 连续信息流和 expanded `904px` 主轴 + 社区洞察；Shop / Leaderboard 为从属上下文入口。
- 门禁：解析、cursor、去重、target mapping、loading / empty / unavailable / stale 与 compact / expanded 测试。

### P4-B4：Forum Detail 拆分与代表页

- 先按“页面编排 / 正文与回答 / 轻回应 / 评论与子评论 / 左右 context rail”拆分 `3768` 行页面和 `2232` 行测试。
- Compact 保持连续阅读、定位滚动、登录回流和键盘避让；expanded 落地 `220 / 820 / 250` 三栏。
- 复用 P4-B1 状态与表面组件，减少层层 Card；不改变回答、评论、编辑、幂等和来源返回契约。
- 门禁：现有 Forum Detail 全回归、三档布局、长正文 / 长评论、定位、分页、登录回流和 reduced-motion。

### P4-B5：代表范围成组静态门禁

- 全量 `flutter analyze`、`flutter test`；基线为 P2 已验证的 `228 / 228`，拆测试不得减少行为覆盖。
- 检查所有改动 Dart 文件不超过仓库 `1500` 行硬上限；Shell / Forum 巨型文件必须实质降到职责边界内。
- 真实 Gateway PC / mobile、四主题与 Android RC Smoke 继续作为阶段验收，必须另行取得服务启动和运行态授权，不混入 B1–B4 日常静态实现。

## 7. 停止线

- 未获得授权前，不下载字体、不执行 `flutter pub get`、不改依赖或 lockfile。
- 不以视觉母版为理由新增 Flutter Chat、完整通知中心、搜索 API、平台工程或 WebOS 工作台。
- 不按当前 UI 字符串裁剪字体，不依赖运行时网络字体。
- 不把 Web DOM、CSS 或 React 状态机复制到 Flutter；只复用现有公开 API、产品语义、几何和信息层级。
- 不在 Shell / Forum Detail 巨型文件上继续叠加新组件。
- Discover 切换公开读模型前必须确认 `Messages` target 的只读停止线；不得把不可承接目标伪装成可点击入口。

## 8. 后续进展

项目所有者已授权并完成 `P4-B1` 的明确变更面：

- 下载并提交约 `40.9 MB` 的两套固定 Noto 字体、OFL 文本和供应链记录；
- 删除 `cupertino_icons`，新增精确版本 `lucide_icons_flutter 3.1.15`，执行 `flutter pub get` 并更新 lockfile；
- 修改 Flutter Theme / shared widgets / theme selector 及对应测试；
- 执行 Android release 包体积前后对比，但不启动应用服务或真实 Smoke。

实现与验证结论见 [P4-B1 Theme / Shared 实现记录](/records/f4-flutter-native-p4b1-theme-shared-implementation-2026-08-23)与 [P4-B2 Adaptive Shell 实现记录](/records/f4-flutter-native-p4b2-adaptive-shell-implementation-2026-08-23)。两个批次均按独立授权和可审阅边界完成；当前等待 P4-B3 明确授权，后续 B3 / B4 仍按本记录逐批推进。
