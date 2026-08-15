# Flutter Native 产品化与 UI 重构

> 状态：`P0` 产品路线与技术方向已确认；下一顺位为 `P1` 全页面事实审计与代表类型分级，尚未修改 Flutter 代码或依赖
>
> 最后更新：2026-08-15（Asia/Shanghai）
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [Radish UI 差异附录](/frontend/ui-addendum)
> - [F4-R 家族 UI 统一接入与产品视觉重构](/features/family-ui-convergence-design)
> - [Flutter 移动端 handoff 与回流说明](/guide/flutter-mobile-handoff)

## 1. 结论摘要

Radish 长期只维护两条正式产品线：

1. **Web**：正式优先主线，覆盖 PC / mobile 浏览器、公开访问、SEO、分享、Author、Private 与 Console。
2. **Flutter Native**：次级原生安装包产品线，目标覆盖 Android、iOS、Windows、macOS 与 Linux；移动端优先，桌面端达到阶段门禁后再产品化。

`Frontend/radish.client` 的 WebOS `/desktop` 继续作为 Web 内的历史兼容入口，不构成第三条产品线。`Clients/radish-tauri` 正式弃用，只保留历史代码与验证资产，不进入当前开发、UI、CI、构建、发布或验收门禁。Flutter Web 不进入路线，避免维护第二套 Web 前端。

Flutter 当前不是功能空壳。Android MVP 已具备认证、来源返回、发现、论坛、Docs、公开主页、通知、商城、订单、背包、钱包和经验等真实链路；主要问题是页面仍停留在早期 MVP / demo 级视觉，缺少可持续的主题、组件和宽屏交互系统。因此本专题采用“**保留业务 owner 与行为契约，重建视觉和自适应呈现**”，不从零重写数据层和状态机。

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
- 不把 Web 页面、DOM 结构或像素值机械复制到 Flutter。
- 不把所有 Web 路由都搬到 Flutter；Console、SEO、完整 Author 与低频治理默认留在 Web。
- 不恢复 Tauri，不扩展 WebOS，不引入 Flutter Web。
- 不在 UI 重构中改后端接口、权限、业务状态机、提交幂等或来源返回契约。
- 不为“现代感”同时引入多套互相竞争的主题 / 组件框架。
- 不在缺少授权时安装依赖、生成平台目录或启动服务。

## 4. 当前 readiness 审计

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
- 商品权益已能通过 `ShopRepository.getMyBenefits()` 读取，并具备激活 / 停用 API 调用入口。
- 当前 `ShopUserBenefit` 尚未完整保留主题映射所需的 `voBenefitValue`、状态等字段，Theme owner 也未消费权益结果；这是四主题不能承接的直接断点。

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
| compact | 手机、窄窗口 | 底部主导航、单任务全屏、Bottom Sheet | 触控目标、安全区、系统返回、键盘避让 |
| medium | 平板、折叠屏、小桌面窗口 | Navigation Rail、列表—详情双栏 | 横竖屏切换、鼠标悬停、焦点顺序、状态保留 |
| expanded | 桌面、大平板、宽窗口 | 侧栏 + 双栏 / 三栏，内容阅读宽度受控 | 键盘快捷键、右键 / hover、滚轮、窗口缩放和多任务效率 |

同一页面族共享任务和状态，但可以有不同组合：Forum 在 compact 是列表与详情分屏导航，在 expanded 可形成列表—正文—上下文三栏；Docs 在 compact 是目录 / 搜索 / 正文单任务切换，在 expanded 可并置目录与阅读面；Shop 与 Profile 不因宽屏自动变成 WebOS 多窗口工作台。

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

Flutter 不复刻 Web 已有画板。进入页面代码前，按移动原生与桌面原生的结构差异重新分级：

| 等级 | 代表类型 | 设计要求 |
| --- | --- | --- |
| R1 | App Shell + 认证 / 通知；Discover；Forum 详情与互动；Profile / 我的 | 同时维护 compact 与 expanded 正式代表设计，必要时补 medium 关键差异 |
| R2 | Forum 列表 / 发帖；Docs 目录—正文；Shop 交易回流；共享状态与主题设置 | 维护关键区块、状态或响应式差异，不复制完整等价页面 |
| R3 | Leaderboard、Wallet、Experience、订单 / 背包等派生面 | 写明继承来源后实现，通过真实窗口截图复核 |

`P1` 必须先逐个审计现有页面 owner、业务状态、窗口结构、共享候选与风险，再最终确认 R1 / R2 / R3。不能仅凭文件名批量换皮。

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
- `shadcn_ui`：不作为全局基础。它与 Material / FlexColorScheme 并行时容易形成第二套主题和组件语义；如确有价值，只允许在 `P2` 对单个隔离代表面做 spike，失败即退出。

### 9.3 不采用

- `macos_ui` 不作为共享产品框架。它只适合 macOS 风格与平台能力，无法支撑 Android / iOS / Windows / Linux 一致的 Radish 产品身份。
- 不同时引入 `shadcn_ui`、其他完整组件框架和自有 Material 主题三套全局体系。
- 本文只裁决方向，不固定包版本；版本、许可证、维护活跃度、传递依赖和锁文件影响须在 `P2` 重新核对，安装前另行说明命令并取得授权。

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

### P1：全页面事实审计与代表分级（下一批）

- 盘点每个页面的 owner、状态、调用链、关键交互、测试和技术债。
- 建立 compact / medium / expanded 结构表与 R1 / R2 / R3 继承表。
- 区分“保留行为、调整编排、重做呈现、后置功能”，不改代码和依赖。
- 确定 `P2` 的首批完整代表场景。

退出条件：所有现有页面有唯一归属，首个 spike 不依赖猜测。

### P2：技术基座 spike 与依赖裁决

- 在取得依赖授权后，验证 FlexColorScheme、自有 ThemeExtension、字体本地资产和共享组件边界。
- 用 Shell + Discover + Forum detail 的完整真实状态组合比较纯 Material 主题、推荐组合和可选 shadcn 隔离样例。
- 验证四主题、reduced-motion、compact / expanded、键盘 / 触控与现有测试可维护性。

退出条件：只保留一套全局主题 / 组件基础，包版本、许可证、资产和回滚方式明确。

### P3：Flutter 代表设计

- 按 R1 / R2 维护 Flutter 专用 compact / expanded 代表设计；medium 只补真实结构差异。
- 吸收 Web 家族 UI 的语义和气质，不复制 Web 画板。
- 确认 typography、四主题、圆角、阴影、状态、导航、宽屏布局和 motion。

退出条件：代表设计经确认，R3 继承路径完整，代码实现不再临场决定视觉系统。

### P4：主题与共享组件实现

- 落地 Theme Controller、四主题映射、ThemeExtension 和主题持久化。
- 落地 typography、radius、surface、state、motion 与共享导航 / 状态 / 表单组件。
- 接入首批完整代表页，先关闭公共 owner 风险。

退出条件：主题与权益测试、组件测试、analyze 和代表尺寸 widget tests 通过。

### P5：页面族成组重构

- 先 Shell / Community，再 Docs / Commerce，最后派生只读面。
- 每批保留业务状态和来源返回，按继承关系改呈现。
- 单批不跨越多个高风险写入领域，不顺手扩新功能。

退出条件：页面族的 compact / expanded、四主题、关键状态和交互回归通过。

### P6：平台产品化与发布门禁

- Android 先形成新版 UI RC；iOS 再进入独立平台验收。
- desktop 在共享 UI 通过宽屏和输入门禁后，按 Windows、macOS、Linux 分别生成 / 补齐平台工程、构建、签名、更新和分发。
- 平台工程与分发要求独立授权和记录，不因 Dart UI 可运行自动宣称产品完成。

## 12. 推荐第一实现批次

在 `P1–P3` 完成并获实现确认后，第一实现批次推荐为 **Flutter Theme Foundation + Adaptive Shell**，暂不整页重做全部业务。

预计影响：

- `Clients/radish.flutter/pubspec.yaml`：经授权加入最终确认的主题 / 字体依赖与本地字体资产。
- `lib/core/theme/`：四主题定义、Radish `ThemeExtension`、typography、Theme Controller 与持久化 owner。
- `lib/features/shop/data/shop_models.dart`、`shop_repository.dart`：完整保留并读取主题权益映射字段，不改服务端状态机。
- `lib/app/app.dart`、`lib/app/bootstrap.dart`：注入主题 owner，让 `MaterialApp` 消费权威主题状态。
- `lib/features/shell/presentation/radish_flutter_shell.dart`：compact / medium / expanded 导航骨架和主题入口。
- `lib/shared/widgets/`：只增加首批真实复用的状态、表面、按钮与导航组件。
- `test/`：主题映射、权益 / 本地偏好、失效回退、窗口等级、reduced-motion 和键盘焦点测试。

验证方式：

1. `flutter analyze`
2. `flutter test` 与主题 / Shell 定向 widget tests
3. 对 compact、medium、expanded 固定 surface 尺寸执行 golden 或结构快照；golden 策略需先解决跨平台字体稳定性
4. 覆盖四主题、未登录 / 已登录、无权益 / 有权益 / 失效、light / dark、reduced-motion
5. desktop 追加键盘焦点和窗口缩放测试，mobile 追加安全区、系统返回和键盘避让
6. 只有专题准备验收且服务启动另获授权后，才执行真实 Android / desktop smoke；不重启生产证据采集

## 13. 下一动作

当前下一顺位是 `P1`：只做 Flutter 全页面事实审计、页面族分级和首个 spike 场景裁决。该批不修改 Dart、`pubspec.yaml`、平台目录、Pencil 或服务状态；完成后先汇报并等待确认，再进入依赖与设计基座验证。
