# Flutter Native 产品化与 UI 重构

> 状态：`P6-A Android local RC candidate assembly` 已完成；P6-B AVD 运行态待单独授权
>
> 最后更新：2026-08-27（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [Radish UI 差异附录](/frontend/ui-addendum)
> - [F4-R 家族 UI 统一接入与产品视觉重构](/features/family-ui-convergence-design)
> - [Flutter 移动端 handoff 与回流说明](/guide/flutter-mobile-handoff)
> - [P1 全页面事实审计与代表分级](/records/f4-flutter-native-p1-full-page-fact-audit-2026-08-19)
> - [P3 Flutter 代表设计记录](/records/f4-flutter-native-p3-representative-design-2026-08-19)
> - [P4-A 实施就绪审计](/records/f4-flutter-native-p4a-readiness-2026-08-23)
> - [P4-B1 Theme / Shared 实现记录](/records/f4-flutter-native-p4b1-theme-shared-implementation-2026-08-23)
> - [P4-B2 Adaptive Shell 实现记录](/records/f4-flutter-native-p4b2-adaptive-shell-implementation-2026-08-23)
> - [P4-B3 Discover 实现记录](/records/f4-flutter-native-p4b3-discover-implementation-2026-08-23)
> - [P4-B4 Forum Detail 实现记录](/records/f4-flutter-native-p4b4-forum-detail-implementation-2026-08-23)
> - [P4-B5 成组静态门禁记录](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23)
> - [P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)
> - [P5-B1 Forum Feed / Compose 实现记录](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23)
> - [P5-B2 Identity / Revisit readiness](/records/f4-flutter-native-p5b2-identity-revisit-readiness-2026-08-23)
> - [P5-B2 Identity / Revisit 实现记录](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23)
> - [P5-C1 Docs Reader readiness](/records/f4-flutter-native-p5c1-docs-reader-readiness-2026-08-23)
> - [P5-C1 Docs Reader 实现记录](/records/f4-flutter-native-p5c1-docs-reader-implementation-2026-08-23)
> - [P5-C2 Commerce Browse / Transaction readiness](/records/f4-flutter-native-p5c2-commerce-browse-transaction-readiness-2026-08-24)
> - [P5-C2 Commerce Browse / Transaction 实现记录](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)
> - [P5-C3 Commerce Private readiness](/records/f4-flutter-native-p5c3-commerce-private-readiness-2026-08-24)
> - [P5-C3 Commerce Private 实现记录](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)
> - [P5-D1 Wallet / Experience readiness](/records/f4-flutter-native-p5d1-wallet-experience-readiness-2026-08-24)
> - [P5-D1 Wallet / Experience 实现记录](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)
> - [P5-D2 Leaderboard readiness](/records/f4-flutter-native-p5d2-leaderboard-readiness-2026-08-24)
> - [P5-D2 Leaderboard 实现记录](/records/f4-flutter-native-p5d2-leaderboard-implementation-2026-08-24)
> - [P5-D3 Browse History readiness](/records/f4-flutter-native-p5d3-browse-history-readiness-2026-08-24)
> - [P5-D3 Browse History 实现](/records/f4-flutter-native-p5d3-browse-history-implementation-2026-08-27)
> - [P5-E 成组静态门禁 readiness](/records/f4-flutter-native-p5e-grouped-static-gate-readiness-2026-08-27)
> - [P5-E 成组静态门禁实现](/records/f4-flutter-native-p5e-grouped-static-gate-implementation-2026-08-27)
> - [P6 Android UI RC readiness](/records/f4-flutter-native-p6-android-ui-rc-readiness-2026-08-27)
> - [P6-A Android 本地 RC 候选装配](/records/f4-flutter-native-p6a-android-local-rc-candidate-assembly-2026-08-27)

## 1. 结论摘要

Radish 长期只维护两条正式产品线：

1. **Web**：正式优先主线，覆盖 PC / mobile 浏览器、公开访问、SEO、分享、Author、Private 与 Console。
2. **Flutter Native**：次级原生安装包产品线，目标覆盖 Android、iOS、Windows、macOS 与 Linux；移动端优先，桌面端达到阶段门禁后再产品化。

`Frontend/radish.client` 的 WebOS `/desktop` 继续作为 Web 内的历史兼容入口，不构成第三条产品线。`Clients/radish-tauri` 正式弃用，只保留历史代码与验证资产，不进入当前开发、UI、CI、构建、发布或验收门禁。Flutter Web 不进入路线，避免维护第二套 Web 前端。

P1 启动时 Flutter 已不是功能空壳：Android MVP 具备认证、来源返回、发现、论坛、Docs、公开主页、通知、商城、订单、背包、钱包和经验等真实链路，但页面仍停留在早期 MVP / demo 级视觉，缺少可持续的主题、组件和宽屏交互系统。因此本专题采用“**保留业务 owner 与行为契约，重建视觉和自适应呈现**”，不从零重写数据层和状态机。当前 P4 与 P5 已完成首轮主题、壳层、高价值页面族、派生只读面和成组静态门禁，P6 readiness 已冻结 Android 新版 UI RC 的候选、AVD 与真机三段门禁，P6-A 也已完成固定候选装配；下一顺位为 P6-B AVD 运行态验收并等待单独授权。

## 2. 产品边界

| 产品线 | 正式设备 | 主要价值 | 不承担 |
| --- | --- | --- | --- |
| Web | PC / mobile 浏览器 | 公开分发、SEO、分享、完整 Web 主路径、Author、Private、Console | 原生安装包能力 |
| Flutter Native | Android / iOS / Windows / macOS / Linux | 高频阅读、参与、复访与原生安装体验；移动优先，桌面分阶段进入 | SEO、Console、完整 Web Author 复制、WebOS 工作台复制 |
| WebOS `/desktop` | 桌面浏览器历史入口 | 旧深链和既有工作台兼容 | 新功能、新视觉体系、新产品投入 |
| Tauri | 历史资产 | 仅供历史追溯 | 当前开发、发布、门禁和未来默认路线 |

Flutter 桌面端是正式长期目标，但不是立即把移动页面拉宽或一次性生成全部平台工程。桌面产品化必须先证明共享 Dart UI 能形成桌面级导航、键盘、鼠标、焦点、滚动和窗口体验，再逐个平台建立构建、签名、更新与分发门禁。

## 3. 目标与停止线

### 3.1 目标

- 建立与 Web 家族 UI 同源的 Dart 语义层，并完整承接四主题身份。
- 把现有默认 Material 页面升级为可识别、克制、现代的 Radish 原生产品。
- 为 compact、medium、expanded 三类窗口建立真实重排规则。
- 复用已验证的 Repository、Controller、认证、草稿、幂等、来源返回和错误边界。
- 按页面族成组设计、实现和验证，避免逐页堆局部样式。

### 3.2 明确停止线

- 不解析或运行时加载 Web CSS / `--rd-*` 文件；Flutter 使用显式 Dart 语义映射。
- 不建立第二套主题权益状态机；服务端权益和激活状态仍是权威来源。
- 不把 Web DOM、CSS 和运行时实现机械复制到 Flutter；代表设计可以用正式 Web 可编辑页面作为信息架构母版，但必须重新归一到 Flutter 的安全区、共享壳层、语义 token 与平台交互边界。
- 不把所有 Web 路由都搬到 Flutter；Console、SEO、完整 Author 与低频治理默认留在 Web。
- 不恢复 Tauri，不扩展 WebOS，不引入 Flutter Web。
- 不在 UI 重构中改后端接口、权限、业务状态机、提交幂等或来源返回契约。
- 不为“现代感”同时引入多套互相竞争的主题 / 组件框架。
- 不在缺少授权时安装依赖、生成平台目录或启动服务。

## 4. 初始 readiness 审计基线

### 4.1 当前 owner 与调用链

```text
lib/app/app.dart
  └─ MaterialApp(theme: buildRadishTheme())
       └─ lib/core/theme/radish_theme.dart
            ├─ ColorScheme.fromSeed(#B76536, light)
            ├─ scaffoldBackgroundColor #F6F1EA
            ├─ CardTheme radius 24 + outline
            └─ NavigationBar label weight

页面 / shared widgets
  └─ Theme.of(context)
       ├─ colorScheme
       └─ textTheme
```

`2026-08-15` 静态盘点显示，Flutter 只有一个固定亮色 `ThemeData` owner；没有主题 ID、`ThemeMode`、`ThemeExtension`、主题 Controller 或主题偏好持久化。`21` 个 Dart 文件约 `230` 处使用 `Theme.of(context)`，其中 `20` 个文件消费 `colorScheme`。这意味着现有页面虽视觉原始，但已经大体通过全局主题取色，先治理主题 owner 和共享组件可产生较高覆盖率。

### 4.2 已有业务能力

- `RadishApp` 已由 `SessionController` 驱动会话恢复，并向 `RadishFlutterShell` 注入各领域 Repository。
- Shell 已承接 Android Back、OIDC 回流、应用生命周期、底部主导航和通知入口。
- Discover、Forum、Docs、Profile、Leaderboard、Shop、Wallet、Experience 已有独立数据 / 展示 owner。
- 论坛写入已具备失败重试 key、局部更新、草稿保留和来源返回。
- 商品权益已能通过 `ShopRepository.getMyBenefits()` 读取，但当前 Repository 没有激活 / 停用方法。
- P2 已补齐 `ShopUserBenefit` 的 `voBenefitValue`、完整状态与可操作字段，并通过独立 gateway 复用既有 `GetMyBenefits / ActivateBenefit / DeactivateBenefit` 契约；Flutter 没有建立本地伪权益写入链。

### 4.3 与 Web 家族 UI 的差距及根因

| 差距 | 当前表现 | 根因 |
| --- | --- | --- |
| 四主题 | 只有固定亮色橙棕 seed | Theme owner 没有主题注册表、当前主题状态和服务端权益映射 |
| 语义 token | 页面主要消费 Material `ColorScheme`，缺少 Radish 扩展语义 | 尚未建立 Dart `ThemeExtension` 与 family-ui L1 对照 |
| 视觉识别 | 默认 Material 控件、单一字体、局部大卡片 | MVP 以行为打通为目标，没有共享视觉基座和代表设计 |
| 布局 | 以手机单列为主 | 没有窗口等级、导航变体、双栏 / 三栏和桌面输入模型 |
| 组件一致性 | 页面各自组合 Card、Button、Chip、状态区 | `shared/widgets` 只覆盖少数功能组件，没有薄 Radish 组件层 |
| 字体与密度 | 系统默认字体、层级和留白 | 没有跨平台字体资产、typography scale 和密度契约 |
| 动效与无障碍 | 加载动效依赖默认组件，缺少统一 motion 规则 | 没有 motion token、reduced-motion owner 和键鼠 / 焦点验收矩阵 |

根因不是“Material 天生不好看”，而是当前 `ThemeData` 只完成颜色 seed 与少量组件覆盖，页面没有共享语义、设计分级和自适应产品规则。直接逐页换控件会把问题扩散成新的局部样式债。

## 5. 自适应产品形态

断点最终由实现 spike 和代表设计确认，不在文档阶段冻结具体像素。产品形态先固定为：

| 窗口等级 | 典型设备 | 导航与布局 | 交互重点 |
| --- | --- | --- | --- |
| compact | 手机、窄窗口 | Web 家族品牌栏、安全区内悬浮胶囊底栏、单任务全屏、Bottom Sheet | 触控目标、安全区、系统返回、键盘避让 |
| medium | 平板、折叠屏、小桌面窗口 | Web 家族顶部栏、按需折叠页内栏、列表—详情双栏 | 横竖屏切换、鼠标悬停、焦点顺序、状态保留 |
| expanded | 桌面、大平板、宽窗口 | Web 家族顶部全局栏、页面级双栏 / 三栏、受控阅读宽度 | 键盘快捷键、右键 / hover、滚轮、窗口缩放和多任务效率 |

同一页面族共享任务和状态，但可以有不同组合：Forum 在 compact 是列表与详情分屏导航，在 expanded 可形成本页目录—正文—互动上下文三栏；Docs 在 compact 是目录 / 搜索 / 正文单任务切换，在 expanded 可并置目录与阅读面；Shop 与 Profile 不因宽屏自动变成 WebOS 多窗口工作台。Flutter 不复制 Web DOM，但品牌栏、一级导航、主题身份和信息层级必须与正式 Web 同属一个视觉家族。

## 6. 高价值承接路径

### 6.1 第一优先：共享壳层与主题入口

- 启动 / 会话恢复、登录、退出和认证错误。
- compact / medium / expanded 主导航。
- 当前主题、内建主题切换、权益主题激活 / 停用与失效回退。
- loading、empty、error、permission、stale 与离线保留状态。

这是所有页面的公共入口，也是四主题、字体、圆角、焦点和 reduced-motion 的最小独立验证面。

### 6.2 第二优先：社区高频闭环

- Discover 内容发现。
- Forum 列表、帖子详情、轻回应、评论 / 回复与纯文本发帖。
- Notification 到 forum 上下文回流。
- Profile / 公开主页与最近访问。

这些路径覆盖公开阅读、登录恢复、写入、长内容、分页、局部反馈和来源返回，最适合证明新设计系统是否能承载真实业务。

### 6.3 第三优先：知识与交易复访

- Docs 列表、搜索、目录与正文阅读。
- Shop 列表、商品详情、购买确认、订单与背包。
- Wallet、Experience、Leaderboard 的只读复访。

这批覆盖阅读密度、表单确认、敏感操作、数据摘要和多种空 / 错误状态，但不顺势扩购物车、退款、权益使用或资产治理。

### 6.4 不进入首轮

- Console、完整 Author 工作台、完整聊天平台、完整通知治理。
- 系统推送、后台任务、系统分享 SDK、自动更新和商店分发。
- iOS / 桌面平台目录生成、签名和发布流水线。

## 7. 代表类型与设计分级

Flutter 不把 Web 画板未经适配地直接当成原生实现。进入页面代码前，按移动原生与桌面原生的结构差异重新分级；R1 可复用正式 Web 可编辑母版保留信息完整性，但必须在 Flutter 独立设计源完成壳层、token、字体与交互边界适配：

| 等级 | 代表类型 | 设计要求 |
| --- | --- | --- |
| R1 | App Shell + 认证 / 通知；Discover；Forum 详情与互动；Profile / 我的 | 同时维护 compact 与 expanded 正式代表设计，必要时补 medium 关键差异 |
| R2 | Forum 列表 / 发帖；Docs 目录—正文；Shop 浏览 / 交易回流；共享状态与主题设置 | 维护关键区块、状态或响应式差异，不复制完整等价页面 |
| R3 | 订单 / 背包；Wallet、Experience、Leaderboard；最近访问 | 写明 R1 / R2 继承来源后实现，通过真实窗口截图复核 |

以上分级已经由 [P1 全页面事实审计](/records/f4-flutter-native-p1-full-page-fact-audit-2026-08-19)确认。Profile 编辑继承 Identity / Revisit，Docs 的内联详情与 handoff route 共享 Docs Reader owner，通知 sheet 归属 Shell / Auth / Notification；不能按文件名批量换皮。

## 8. 视觉系统方向

### 8.1 语义层

Flutter 目标分层：

```text
Radish 四主题身份与服务端权益
                 ↓
ThemeData / ColorScheme：Material 基础、原生控件和可访问性
                 ↓
Radish ThemeExtension：surface、text、border、brand、action、state、radius、spacing、motion
                 ↓
Radish 薄组件层：Button、Card、Field、Chip、State、Section、Navigation
                 ↓
页面族与领域布局
```

`ThemeExtension` 显式映射 family-ui L1 语义，但不读取 CSS。页面优先消费语义角色，禁止按主题 ID 分叉 UI。品牌识别与主操作继续区分 `brand` / `action`，状态色不得再建另一套红黄绿。

### 8.2 配色、字体与形态

- 四主题继续为 `default / guofeng / theme-dark-night / theme-sakura`，身份与 Web 一致，平台取值可以按原生控件和对比度校准。
- 方向为淡雅新中式与现代自然紧凑，不回到默认蓝紫 Material，也不做厚重国潮。
- 字体以清晰、跨平台一致和可离线交付为先；中文正文、拉丁正文与强调字族需在代表设计中实测。
- 卡片和按钮圆角以语义等级统一，常规目标约 `12–16`，阅读面和列表避免层层卡片；当前全局 `24` 圆角不作为长期默认。
- 阴影降为辅助层级，优先使用表面差、发丝边框和留白表达结构。
- 图标保持单一风格与稳定尺寸，不混用多套图标体系。

### 8.3 动效与可访问性

- 动效只表达层级变化、任务完成、展开收起和导航连续性，不给所有卡片 / 列表统一套入场动画。
- 建立 duration、curve、distance 和 emphasis motion token。
- 全部自定义动效必须尊重 `MediaQuery.disableAnimations`，关键状态在无动画时仍完整可见。
- compact 验收触控目标、安全区、屏幕阅读名称和系统返回；desktop 追加 Tab 焦点、方向键、Enter / Space、Escape、hover、滚轮和窗口缩放。

## 9. 技术选型裁决

### 9.1 推荐基础

| 候选 | 裁决 | 使用边界 |
| --- | --- | --- |
| Material 3 | 保留为可访问性、平台控件与输入系统底座 | 不直接把默认视觉当最终产品 UI |
| `flex_color_scheme` | 推荐作为 `ThemeData` 生成与组件主题引擎 | 使用 Radish 自定义四主题和语义映射，不以预设方案替代品牌决策 |
| 自有 `ThemeExtension` | 必须建立 | 承接 family-ui 中 Material `ColorScheme` 无法准确表达的语义 |
| Radish 薄组件层 | 必须建立 | 只抽取真实复用和稳定状态，不制造万能页面框架 |

### 9.2 有条件引入

- `google_fonts`：可用于字体 API 与本地资产接线；生产构建必须随包打入字体资产、关闭运行时网络获取，并保留字体许可证。具体字族在代表设计比较后确定。
- `flutter_animate`：等主题、组件和 motion token 稳定后再引入；只封装少量共享微动效，并完整支持 reduced-motion。
- `shadcn_ui`：P2 已裁决不引入。它与 Material / FlexColorScheme 并行会形成第二套主题和组件语义，当前没有真实复用价值。

### 9.3 不采用

- `macos_ui` 不作为共享产品框架。它只适合 macOS 风格与平台能力，无法支撑 Android / iOS / Windows / Linux 一致的 Radish 产品身份。
- 不同时引入 `shadcn_ui`、其他完整组件框架和自有 Material 主题三套全局体系。
- P2 已单独核对并获授权固定 `flex_color_scheme ^8.4.0` 与 `shared_preferences ^2.5.5`；版本、BSD-3-Clause 许可证、传递依赖、lockfile 影响和回滚面见 P2 实现记录。

官方参考：[`flex_color_scheme`](https://pub.dev/packages/flex_color_scheme)、[`google_fonts`](https://pub.dev/packages/google_fonts)、[`flutter_animate`](https://pub.dev/packages/flutter_animate)、[`shadcn_ui`](https://pub.dev/packages/shadcn_ui)、[`macos_ui`](https://pub.dev/packages/macos_ui)、[Flutter adaptive and responsive design](https://docs.flutter.dev/ui/adaptive-responsive)。

## 10. 四主题状态与持久化

主题状态必须遵循单一权威链：

```text
匿名 / 未登录
  └─ 内建主题本地偏好（default / guofeng）

已登录
  └─ GetMyBenefits / 当前有效权益
       └─ Theme Controller 解析有效主题
            ├─ ThemeData + ThemeExtension
            └─ 本地仅缓存最后有效显示，不能授予权益
```

规则：

- 本地可以保存内建主题偏好和最后一次有效显示，用于启动恢复；不能凭本地值激活付费主题。
- `theme-dark-night`、`theme-sakura` 的可用性、激活、停用和失效以服务端为准。
- 离线启动可展示最近有效主题，但重新获得服务端结果后必须按权益状态收敛；缓存不是第二真相源。
- Theme Controller 只编排主题状态，不复制 Shop 权益业务。权益 DTO 需完整保留主题解析所需字段。
- 主题失败回退、会话切换和退出登录必须有独立测试，不使用层层 fallback 掩盖契约缺失。

## 11. 实施拆批

### P0：产品路线与专题基线（本批）

- 固定 Web / Flutter 两条产品线。
- 正式弃用 Tauri，冻结 WebOS 新投入。
- 固定 Flutter mobile-first、desktop stage-gated 和 Flutter Web 排除边界。
- 裁决 UI 技术方向、阶段、停止线与验证方式。

退出条件：入口、路线、壳层、UI 附录和 Flutter README 口径一致。

### P1：全页面事实审计与代表分级（已完成，2026-08-19）

- 盘点每个页面的 owner、状态、调用链、关键交互、测试和技术债。
- 建立 compact / medium / expanded 结构表与 R1 / R2 / R3 继承表。
- 区分“保留行为、调整编排、重做呈现、后置功能”，不改代码和依赖。
- 确定 `P2` 的首批完整代表场景。

退出条件已满足：所有现有页面与非 Page 表面已有唯一归属，compact / medium / expanded 结构、R1 / R2 / R3 继承和首个 spike 输入已经形成，详见 [P1 审计记录](/records/f4-flutter-native-p1-full-page-fact-audit-2026-08-19)。

### P2：技术基座 spike 与依赖裁决（已完成，2026-08-19）

- 获授权加入 `flex_color_scheme ^8.4.0` 与 `shared_preferences ^2.5.5`；两者均为 BSD-3-Clause，无顺带直接依赖升级。
- 用自有 `RadishThemeTokens` 作为产品语义真相源，FlexColorScheme 只负责 Material 3 组件子主题；不引入 shadcn 或第二套组件系统。
- Theme Controller 复用 Shop 权益读取 / 激活 / 停用契约，内置偏好与服务端权益分属清晰 owner，账号切换与迟到响应已隔离。
- Shell 与 Discover / Forum Detail 覆盖 compact / medium / expanded、键盘切换、reduced-motion 与现有交互回归。
- 字体依赖未安装；P3 已把字族方向冻结为 `Noto Sans SC + Noto Serif SC`，P4-A 进一步完成本地资产、许可证、包体积、回退链与 `pubspec` 影响裁决。

退出条件已满足：只保留一套全局主题 / 自适应基础，包版本、许可证、权益 owner、持久化、回滚面和验证结论见 [P2 实现记录](/records/f4-flutter-native-p2-theme-adaptive-foundation-2026-08-19)。

### P3：Flutter 代表设计（已确认，2026-08-23）

- 新建并维护独立活动设计源 `Docs/frontend/design-sources/radish-flutter-native-ui-v1.pen`，不把 Flutter 画板追加进 Web / Console 活动源。
- 按 R1 / R2 维护 Flutter 专用 compact / expanded 代表设计；medium 只补真实结构差异。
- 以正式 Web 的品牌栏、导航语法、主题和信息层级为视觉基准，不复制 Web DOM；再按 Flutter 安全区、返回、触控、键鼠、焦点和窗口等级做原生适配。
- 确认 typography、四主题、圆角、阴影、状态、导航、宽屏布局和 motion。

当前已在独立设计源完成视觉基座、Discover / Forum Detail compact 与 expanded、主题选择器、medium 页内栏差分和权益关键状态，R3 继承路径也已形成。2026-08-23 三轮审阅后，Forum Expanded 的主题漏配已修复；第二轮按正式 Web 真实几何建立 Expanded Header、Mobile Header 与 Mobile Tab Bar 三个复用组件；第三轮进一步把 Web Discover / Forum Detail 的 PC / mobile 正式页面作为可编辑母版复制到 Flutter 源，保留完整信息架构与长内容，再映射回 Flutter 安全区、共享壳层、`theme` 轴、语义 token 和 Noto 字体层级。六张正式板最终按 Foundation、Discover Mobile / PC、Forum Mobile / PC、Theme / Medium 的顺序横向排布，便于连续审阅；Discover Expanded 继续采用 `904px` 主讨论区加社区洞察区，Forum Expanded 保留 `220 / 820 / 250` 社区导航—连续正文—线程索引并完整展开回帖，详见 [P3 代表设计记录](/records/f4-flutter-native-p3-representative-design-2026-08-19)。

退出条件已满足：项目所有者已确认第三轮横向审阅稿，后续代码实现不再临场决定视觉系统；`P4-A readiness` 也已完成，设计确认与 readiness 结论均不自动授权字体资产、依赖或 lockfile 变更。

### P4-A：实施就绪审计（已完成，2026-08-23）

- 反查 Theme、Shared、Shell、Theme Selector、Discover 与 Forum Detail 的 owner、规模、可继承契约和阻断项。
- 固定 Noto 官方简体中文区域子集变量 TTF 随包交付、平台默认回退与 OFL / SHA 留痕；不采用运行时网络字体或按当前文案裁字。
- 裁决以精确版本 `lucide_icons_flutter 3.1.15` 替换零运行时引用的 `cupertino_icons`；字体资产不改 lockfile，图标依赖替换会改 `pubspec.yaml` / `pubspec.lock`。
- 识别 Shell 与 Forum Detail 超过仓库文件硬上限，冻结先拆 owner 再改布局；Discover 改用既有 `PublicDiscover/GetFeed` 公开读模型，不新增后端 API 或 Flutter Chat。
- 将 P4 拆为 B1 Theme / Shared、B2 Shell、B3 Discover、B4 Forum Detail 与 B5 成组静态门禁，避免一次授权扩大为全页面改造。

退出条件已满足：设计到代码差分、字体 / 图标供应链裁决、包体积验证方法、实施拆批与停止线均已形成，详见 [P4-A 实施就绪审计](/records/f4-flutter-native-p4a-readiness-2026-08-23)。

### P4-B：主题、共享组件与代表实现

- `B1 Theme Foundation + Shared Primitives`（已完成，2026-08-23）：已接入经校验的 Noto 本地字体和精确版本 Lucide 图标；typography、density、surface、motion、焦点、共享状态原语与主题预览—确认均已落地，详见 [P4-B1 实现记录](/records/f4-flutter-native-p4b1-theme-shared-implementation-2026-08-23)。
- `B2 Web-Family Adaptive Shell`（已完成，2026-08-23）：三档壳层、compact 安全区胶囊底栏、medium / expanded 顶部全局栏与独立通知 / 账户动作已落地，五入口及既有行为契约保持，详见 [P4-B2 实现记录](/records/f4-flutter-native-p4b2-adaptive-shell-implementation-2026-08-23)。
- `B3 Discover`（已完成，2026-08-23）：已迁移既有公开发现 cursor 读模型，完成 compact 连续信息流与 expanded `904px` 主轴 + 社区洞察；Forum / Docs 保持原生 handoff，Messages 只读说明 Web 边界，详见 [P4-B3 实现记录](/records/f4-flutter-native-p4b3-discover-implementation-2026-08-23)。
- `B4 Forum Detail`（已完成，2026-08-23）：页面与测试已按真实职责拆分，compact 连续阅读、medium 单主轴与 expanded `220 / 820 / 250` 页面级三栏已落地；回答、评论、编辑、幂等、登录回流、定位和来源返回保持，详见 [P4-B4 实现记录](/records/f4-flutter-native-p4b4-forum-detail-implementation-2026-08-23)。
- `B5`（已完成，2026-08-23）：主题 / 权益、共享组件、Shell、Discover 与 Forum Detail 成组代表测试 `138 / 138`，全量 `241 / 241`、analyze 零问题；补拆 `5144` 行 Shell Smoke owner 后，P4 全部改动 Dart 文件低于 `1500` 行。真实 Gateway / Android RC Smoke 仍作为独立阶段验收并另行授权，详见 [P4-B5 门禁记录](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23)。

退出条件：主题与权益测试、组件测试、代表尺寸 widget tests、全量 analyze / test 和文件边界检查通过；B1–B4 分批获得授权、实现和验证。

### P5：页面族成组重构

- `P5-A readiness`（已完成，2026-08-23）：确认 P4 Shell / Notification / Discover / Forum Detail 作为继承基座，不重复实现；剩余页面拆为 B1 Forum Feed / Compose、B2 Identity / Revisit、C1 Docs、C2 Commerce Browse / Transaction、C3 Commerce Private、D1 Wallet / Experience、D2 Leaderboard、D3 Browse History 与 E 成组静态门禁，详见 [P5-A 审计记录](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)。
- `P5-B1`（已完成，2026-08-23）：Forum Feed / Compose 页面 / 测试 owner 已拆分，compact 全高 composer、medium bounded composer 与 expanded `904px` 连续主轴 + 社区洞察已落地；分页旧快照、结构化错误、分类、草稿、登录回流、`forum-post:` 幂等与 handoff 保持，详见 [P5-B1 实现记录](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23)。
- `P5-B2 readiness`（已完成，2026-08-23）：Identity / Revisit 固定复用既有 Profile API 与 Shell recent targets；公开身份、统计、帖子、评论和我的轻回应拆为独立权威快照，页面 / 测试按真实职责拆分，三档结构采用连续信息流、受控单主轴和 expanded `904px` 主轴 + 身份上下文，资料编辑只使用 `GetMyProfile + UpdateMyProfile` 并补 dirty / busy / 离开保护，详见 [P5-B2 readiness](/records/f4-flutter-native-p5b2-identity-revisit-readiness-2026-08-23)。
- `P5-B2 Identity / Revisit`（已完成，2026-08-23）：五类 Profile 资源已落地独立快照 / 代际 / issue、局部 unavailable / stale、三列表去重与跨 target 隔离；页面与测试拆分后均低于文件硬上限，三档结构与权威编辑保护完成。Profile `52 / 52`、Shell Smoke `51 / 51`、全量 `274 / 274` 通过，详见 [P5-B2 实现记录](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23)。
- `P5-C1 Docs Reader readiness`（已完成，2026-08-23）：冻结复用 `Wiki/GetList + Wiki/GetBySlug`、目录 query target 与正文 reader 独立权威快照、同 slug 刷新旧正文 stale、inline / handoff 共用 reader controller / surface、compact 单任务与 medium / expanded 目录—正文结构；改造前 Docs `16 / 16`，详见 [P5-C1 readiness](/records/f4-flutter-native-p5c1-docs-reader-readiness-2026-08-23)。
- `P5-C1 Docs Reader`（已完成，2026-08-23）：目录 query target、请求代际、结构化 issue 与旧页 stale 已落地；inline / handoff / linked-doc 共用 reader controller / surface，compact 单任务、medium 目录—正文与 expanded `280 / 904` 阅读结构完成。Docs `35 / 35`、Shell Smoke `51 / 51`、全量 `293 / 293` 与 analyze 零问题，详见 [P5-C1 实现记录](/records/f4-flutter-native-p5c1-docs-reader-implementation-2026-08-23)。
- `P5-C2 readiness`（已完成，2026-08-24）：冻结复用既有 Shop / Coin 契约、目录 / 详情 / 资格 / 余额独立权威状态、支付草稿 dirty / busy、单商品幂等生命周期、账号 / target 隔离、订单确认回流、三档结构与 owner / 测试拆分；改造前 Shop `25 / 25`，详见 [P5-C2 readiness](/records/f4-flutter-native-p5c2-commerce-browse-transaction-readiness-2026-08-24)。
- `P5-C2 Commerce Browse / Transaction`（已完成，2026-08-24）：目录、详情、购买三个独立 owner、结构化 issue、refresh / append / stale、支付草稿 dirty / busy、登录回流、同意图幂等、product / account / generation 隔离与订单确认回流已落地；目录三档 `1 / 2 / 3` 列，详情完成 compact 单任务、medium 双区与 expanded `820 + 24 + 360`。Shop `35 / 35`、Shell Smoke `51 / 51`、全量 `303 / 303` 与 analyze 零问题，详见 [P5-C2 实现记录](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)。
- `P5-C3 readiness`（已完成，2026-08-24）：冻结只复用订单列表 / 详情、权益、道具、Coin 订单流水与主题 gateway 既有契约；订单目录 / 详情、权益、道具拆为四个只读 owner，权益 / 道具不再整批失败，账号 / target / generation 隔离与测试拆分明确。三档采用订单 `1 / 2 / 3` 列、详情 `820 + 24 + 360` 和背包 sequential / 双 lane；改造前 Shop 组合 `23 / 23`、Shell Smoke `51 / 51`，详见 [P5-C3 readiness](/records/f4-flutter-native-p5c3-commerce-private-readiness-2026-08-24)。
- `P5-C3 Commerce Private`（已完成，2026-08-24）：订单目录 / 详情、权益、道具四个只读 owner 与账号 / target / generation / dispose 隔离已落地；订单目录 `1 / 2 / 3` 列、详情 compact 连续 / medium 双区 / expanded `820 + 24 + 360`、背包 sequential / 双 lane 完成，权益与道具单边失败 / stale 独立。Shop `50 / 50`、Shell Smoke `51 / 51`、全量 `318 / 318` 与 analyze 零问题，详见 [P5-C3 实现记录](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)。购买写入仍只属于 C2，C3 未扩取消、退款、权益激活 / 使用或道具使用。
- `P5-D1 readiness`（已完成，2026-08-24）：冻结只复用 Coin / Experience 四个私域读取 endpoint、Wallet 余额 / 流水和 Experience 等级 / 流水四个独立只读 owner、account / query / credential generation / dispose 隔离、单边 unavailable / stale、append 稳定去重和页面 / 测试拆分。三档采用 compact 连续任务、medium 概要双列 + 完整流水、expanded `280–300 + 24 + <=904`；改造前模型 `4 / 4`、Commerce / Wallet route `20 / 20`、Shell Smoke `51 / 51`，详见 [P5-D1 readiness](/records/f4-flutter-native-p5d1-wallet-experience-readiness-2026-08-24)。
- `P5-D1`（已完成，2026-08-24）：四个独立只读 owner、局部 unavailable / stale、空快照、refresh 替换、append issue / 去重、account / query / credential generation / dispose 隔离和三档 surface 已落地。P5-D1 定向 `113 / 113`、全量 `356 / 356`、analyze 零问题，详见 [P5-D1 实现记录](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)。
- `P5-D2 readiness`（已完成，2026-08-24）：冻结只复用匿名经验榜第一页 20 条和既有 Public Profile handoff，补映射公共身份字段，建立单一首屏 owner、结构化 empty / unavailable / stale 与 generation / dispose 隔离；compact 连续紧凑排名、medium 受控密集列表、expanded `<=904 + 24 + 280–300` 榜首身份上下文，以及“严格 `#RRGGBB` 只作无文字装饰 accent”已固定。改造前 Leaderboard `4 / 4`、Shell Smoke `51 / 51`，详见 [P5-D2 readiness](/records/f4-flutter-native-p5d2-leaderboard-readiness-2026-08-24)。
- `P5-D2`（已完成，2026-08-24）：单一首屏 owner、结构化状态、PublicId 优先公共身份、三档排名 surface 和业务色无文字 accent 已落地；P5-D2 定向 `22 / 22`、Shell `51 / 51`、全量 `374 / 374`、analyze 零问题，详见 [P5-D2 实现记录](/records/f4-flutter-native-p5d2-leaderboard-implementation-2026-08-24)。
- `P5-D3 readiness`（已完成，2026-08-24）：冻结只复用登录态 `User/GetMyBrowseHistory` 与现有 Forum / Docs / Shop handoff；服务端账号完整历史和本机 Forum / Docs 各最多 `5` 条 recent shortcut 保持不同 owner。分页 snapshot 使用 `VoId` 稳定去重，补 account / credential generation / dispose 隔离和 typed target；三档采用 compact 连续历史、medium 时间顺序密集列表与 expanded `<=904 + 24 + 280–300` 数据来源上下文。改造前 Browse History `2 / 2`、Shell `51 / 51`，Flutter 合计 `53 / 53`；服务端契约 `3 / 3`，详见 [P5-D3 readiness](/records/f4-flutter-native-p5d3-browse-history-readiness-2026-08-24)。
- `P5-D3 implementation`（已完成，2026-08-27）：账号完整历史已拆为独立分页 owner，完成 ready / empty / unavailable / stale、append issue / retry、`VoId` 稳定去重、account / credential / repository generation / dispose 隔离，以及 Post / Wiki / Product typed target；compact 连续历史、medium 时间顺序密集列表与 expanded `<=904 + 24 + 280–300` 数据来源说明已落地，设备 recent shortcut 保持独立 owner。P5-D3 定向 `34 / 34`、Shell `51 / 51`、Flutter 全量 `406 / 406`、analyze 零问题，服务端既有契约 `3 / 3`，详见 [P5-D3 实现记录](/records/f4-flutter-native-p5d3-browse-history-implementation-2026-08-27)。
- `P5-E readiness`（已完成，2026-08-27）：P4 / P5 `29` 个代表入口 `383 / 383`、Shell `51 / 51`、Flutter 全量 `406 / 406`、analyze 零问题，全 Flutter Dart owner 均低于 `1500` 行。审计确认只剩 Discover medium + 四主题、Forum Detail 四主题、Commerce C2 四主题 + compact 长商品信息三处直接证据；实施固定只在三个既有测试 owner 补 `13` 个 widget tests，预期成组 `396 / 396`、全量 `419 / 419`，默认不改运行时代码，详见 [P5-E readiness](/records/f4-flutter-native-p5e-grouped-static-gate-readiness-2026-08-27)。
- `P5-E grouped static gate`（已完成，2026-08-27）：按确认方案补齐 Discover medium 四主题、Forum Detail medium 四主题、Commerce C2 medium 四主题与 compact 长商品信息 `13` 个独立 widget tests，没有修改 `lib/` 运行时代码。三个涉及入口 `56 / 56`、P4 / P5 `29` 个代表入口 `396 / 396`、Shell `51 / 51`、Flutter 全量 `419 / 419`、analyze 零问题，全 Flutter Dart owner 均低于 `1500` 行；P5 首轮静态门禁关闭，详见 [P5-E 实现记录](/records/f4-flutter-native-p5e-grouped-static-gate-implementation-2026-08-27)。
- `P6 Android UI RC readiness`（已完成，2026-08-27）：平台工程、旧 MVP RC 证据、构建 / Gateway / OIDC / 签名、本机工具链、设备、证据与清理边界已审计；Android JVM 单测 `7 / 7`。当前有 compact AVD 和完整构建链，但没有在线 Android 目标、medium AVD、正式签名或候选 APK。实施固定拆为 P6-A 本地候选装配、P6-B compact / medium AVD 运行态、P6-C 同哈希真机验收；只有三段通过才可给出 Android 新版 UI 本地 / 内部 RC Go，详见 [P6 readiness](/records/f4-flutter-native-p6-android-ui-rc-readiness-2026-08-27)。
- `P6-A Android local RC candidate assembly`（已完成，2026-08-27）：Flutter analyze 零问题、全量 `419 / 419`、Android JVM `7 / 7` 与 release 构建通过；候选固定为 SHA-256 `d7b1b9d1f12e5943bae7ddffe3daffcf6071d63ddb79a186ae16e05946234200`、`26.8.2+1`、三 ABI、`development + https://localhost:5000`。包身份、权限、OIDC、SDK、AOT define 与 debug-signing 性质均已冻结；下一顺位为 P6-B，详见 [P6-A 记录](/records/f4-flutter-native-p6a-android-local-rc-candidate-assembly-2026-08-27)。

每批保留业务状态、幂等、来源返回和原生 handoff，按继承关系改呈现；单批不跨越多个高风险写入领域，不顺手扩新功能。

退出条件：页面族的 compact / expanded、四主题、关键状态和交互回归通过。

### P6：平台产品化与发布门禁

- Android 先形成新版 UI RC；iOS 再进入独立平台验收。
- desktop 在共享 UI 通过宽屏和输入门禁后，按 Windows、macOS、Linux 分别生成 / 补齐平台工程、构建、签名、更新和分发。
- 平台工程与分发要求独立授权和记录，不因 Dart UI 可运行自动宣称产品完成。

## 12. P2 技术基座结果

P2 已按 **Flutter Theme Foundation + Adaptive Shell + Discover + Forum Detail** 落地可退出技术基座，不等于 P3 代表设计或 P4–P5 全量实现。

已实现：

- `default / guofeng / theme-dark-night / theme-sakura` 四主题和自有 `ThemeExtension`；
- `SharedPreferencesAsync` 内置偏好与 Shop 权益 gateway；
- `<600 / 600–1023 / >=1024` 三档 Shell 与 `Ctrl/Cmd + 1..5`；
- compact Bottom Sheet 与 medium / expanded Dialog 主题入口；
- Discover 单列 / 双列结构和 Forum Detail 宽屏阅读 rail；现有全局 `NavigationBar` / `NavigationRail` 外观仍是 P4 待按修订稿收口的实现差分；
- 评论定位 reduced-motion 和通知刷新 stale 保留。

已验证：

1. `flutter analyze`：零问题；
2. `flutter test`：`228 / 228` 通过；
3. `390 / 800 / 1200` 结构、键盘切换、四主题、权益失效 / stale / 账号隔离与代表页回归通过；
4. Android debug 构建因本机 Gradle daemon 无任务输出而中止，未记为通过；本批未启动服务或执行真实 smoke。

## 13. 当前动作（2026-08-27）

`P6-A Android local RC candidate assembly` 已完成：固定 SHA-256 `d7b1b9d1…34200` 的 `26.8.2+1` 三 ABI debug-signing release APK 已通过静态、Android JVM、构建与制品契约门禁。下一顺位是 P6-B：单独授权服务、compact / medium AVD、APK 安装、`adb reverse` 与运行态取证后，使用同一哈希完成默认主题完整矩阵和四主题代表矩阵；P6-C 真机仍独立确认。正式签名、外部分发、iOS 和 desktop 继续后置。
