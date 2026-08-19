# Flutter Native P2 主题与自适应技术基座实现

> 日期：2026-08-19（Asia/Shanghai）  
> 结论：P2 技术基座 spike 已完成。Flutter Native 现在只有一套四主题语义基础、一个主题状态 owner 和一套 compact / medium / expanded 壳层；Discover 与 Forum Detail 已作为首批代表页接入。下一顺位进入 P3 Flutter 代表设计，不在本批扩展全页面视觉重构或平台分发。

## 1. 本批目标

P2 只验证 P1 审计选出的四个共同根部：

1. Theme Foundation 是否能承接 Web 已冻结的四主题标识与语义颜色；
2. 主题权益能否复用既有 Shop 服务端状态机，而不建立 Flutter 本地伪权益；
3. Shell 能否在同一页面状态下覆盖 compact / medium / expanded；
4. Discover 与 Forum Detail 能否证明列表聚合页、长阅读互动页都可使用同一自适应基础。

本批没有修改后端接口、数据库、权限、OIDC、论坛写入、LongId、来源返回或 Android 平台桥接代码。

## 2. 依赖裁决

经单独说明并取得授权后执行：

```text
flutter pub add flex_color_scheme:^8.4.0 shared_preferences:^2.5.5
```

最终直接依赖：

| 包 | 固定范围 | 许可证 | 用途 | 回滚面 |
| --- | --- | --- | --- | --- |
| `flex_color_scheme` | `^8.4.0` | BSD-3-Clause | 生成 Material 3 组件子主题；Radish 语义色仍由自有 token 决定 | 删除依赖并让 `buildRadishTheme` 直接返回 `ThemeData` |
| `shared_preferences` | `^2.5.5` | BSD-3-Clause | 只保存非敏感的内置主题偏好 | 替换 `RadishThemePreferenceStore` 实现；服务端权益不受影响 |

lockfile 新增的传递依赖包括 `flex_seed_scheme`、`ffi`、`file`、`path_provider_*`、`platform`、`plugin_platform_interface`、`web`、`xdg_directories` 与各平台 `shared_preferences_*` 实现，共 `19` 个新增解析项。现有直接依赖与 lint 版本没有顺带升级。

`google_fonts` 本批没有安装：P2 不应增加运行时字体网络获取和额外平台依赖面；字族、字体文件与许可证留到 P3 代表设计比较后，以本地资产方式裁决。

当前 Flutter `3.44.0 stable` / Dart `3.12.0` 满足两个直接依赖的 SDK 要求。`shared_preferences` 只承载用户可重新选择的外观偏好，不承载 token、权益、支付或其他关键数据。

## 3. 唯一主题基础

`lib/core/theme/radish_theme.dart` 现在定义四个正式标识：

| Flutter 标识 | 契约值 | 明暗 | 权限来源 |
| --- | --- | --- | --- |
| `defaultTheme` | `default` | light | 内置 |
| `guofeng` | `guofeng` | light | 内置，默认 |
| `darkNight` | `theme-dark-night` | dark | Shop 权益 |
| `sakura` | `theme-sakura` | light | Shop 权益 |

这些值与正式 Web 主题契约一致。Flutter 不解析 Web CSS，也不维护另一套公开主题 ID。

`RadishThemeTokens` 作为唯一 `ThemeExtension` 暴露：

- app / surface / muted surface；
- primary text / muted text / border；
- brand / on-brand / brand-soft；
- action / on-action / action-soft；
- success / warning / error / info；
- `8 / 12 / 18` 三档圆角。

FlexColorScheme 只负责 Material 3 组件主题生成。Radish 的 app background、surface、text、border、brand 与 action 最终都会显式回填到 `ThemeData`，避免包的派生算法成为产品颜色真相源。

## 4. 主题状态与权益边界

`RadishThemeController` 是应用唯一主题 owner：

- 首次恢复只读取 `RadishThemePreferenceStore` 中的内置主题；
- 匿名态只允许 `default / guofeng`；
- 登录后读取 `GetMyBenefits`，只识别 `BenefitType.Theme` 且值为已知主题 ID 的权益；
- `theme-dark-night / theme-sakura` 只有在 `ActivateBenefit` 成功后才切换；
- 返回内置主题时先保存本机偏好，再调用 `DeactivateBenefit` 停用当前服务端权益；
- 同一账号刷新失败时保留上次可用权益并标记 stale；
- 切换账号或退出时立即清空旧权益并回到内置主题；
- 迟到的激活响应受 generation 隔离，不能在退出后重新套用旧账号主题。

Shop Flutter 模型补齐 `voBenefitValue / voStatus / voCanActivate / voCanDeactivate / voUnavailableReason` 等既有服务端字段。`HttpShopRepository` 实现独立的 `ShopBenefitActionRepository`，没有扩大 `ShopRepository` 的既有测试 fake 契约；主题 gateway 只做 Shop DTO 到主题 entitlement 的映射。

## 5. 自适应壳层

窗口等级固定为：

```text
compact  < 600
medium   600..1023
expanded >= 1024
```

同一个 `IndexedStack` 和同一组页面 owner 在三档窗口中复用：

- compact：Material `NavigationBar`；
- medium：窄 `NavigationRail`；
- expanded：extended `NavigationRail`；
- desktop 键盘：`Ctrl/Cmd + 1..5` 切换五个主入口；
- AppBar 提供唯一主题入口；compact 使用 Bottom Sheet，medium / expanded 使用受宽度约束的 Dialog。

`RadishContentFrame` 统一内容最大宽度与横向留白。它只负责布局约束，不持有页面状态、请求或导航。

## 6. 首批代表页面

### 6.1 Discover

- 删除面向开发过程的 `PhaseScopeCard` 和“当前不支持购买”的过期口径；
- compact 保持单列任务流；
- medium / expanded 使用论坛主区与 Docs / Shop 次区双列布局；
- 页面仍只持有一个 `DiscoverFeedController` 和一个 snapshot；
- 继续保留首次 loading / error、刷新保留旧数据和分区失败边界；
- 商品说明改为真实衔接原生详情、购买、订单与背包，不再把已实现能力描述为后置。

### 6.2 Forum Detail

- compact / medium 使用受控阅读宽度的单列页面；
- expanded 增加独立阅读导航 rail，提供刷新、轻回应、回答和评论区跳转；
- rail 只引用当前详情与既有 section key，不复制正文、评论 owner 或提交状态；
- 既有回答、轻回应、评论分页、回复、作者编辑、登录回流和来源返回全部保留；
- section 定位动画尊重系统 `disableAnimations`，reduced-motion 下时长为零。

### 6.3 Shell 通知刷新

P1 识别的局部状态债同时关闭：已有通知刷新失败时不再清空旧列表，而是保留上次可用数据并显示“上次”状态；首次读取失败仍显示明确错误和重试入口。

## 7. 验证结果

已完成：

```text
flutter analyze
No issues found

flutter test
228 / 228 passed
```

新增或扩展的自动化覆盖：

- 四主题标识、brightness 与语义 token；
- 内置偏好恢复、登录权益同步、同账号 stale、账号切换、退出回落；
- 激活失败不切主题、停用权益后回内置主题、迟到激活响应隔离；
- Shop 权益字段映射与 `ActivateBenefit / DeactivateBenefit` HTTP 路径；
- `390 / 800 / 1200` 三档 Shell 结构与键盘切换；
- compact Bottom Sheet / medium Dialog 主题选择；
- Discover compact / expanded 结构；
- Forum Detail compact / expanded context rail；
- Forum Detail 既有 `35` 个交互回归；
- 全量 shell、OIDC、来源返回、商城、Docs、Profile 与通知 smoke。

Android debug APK 构建已尝试，但本机 Gradle daemon 接单后长期停在 `assembleDebug` 且没有进入任务输出，有限等待后中止；旧 APK 时间未变化，因此不记为构建通过，也不视为代码失败。本批未启动 Gateway、API、Auth、前端服务、模拟器或浏览器，符合开发轮次静态验证边界。

## 8. P2 停止线与剩余事项

P2 到此停止：

- 不为所有页面机械套新色；
- 不在没有代表设计确认前批量重排 Community / Docs / Commerce 页面族；
- 不安装字体包，不引入 shadcn 或第二套组件系统；
- 不新增主题商品、权益或服务端状态机；
- 不生成 iOS / desktop 平台工程，不进入签名、分发或部署。

下一顺位为 P3 Flutter 代表设计：基于已经可运行的 Theme Foundation 和 Adaptive Shell，确认 typography、本地字体资产、组件密度、四主题视觉、Discover 与 Forum Detail 的 compact / expanded 代表稿，并冻结 R3 页面继承规则。Android debug 构建问题在下一次需要平台编译门禁时独立诊断，不阻断 P3 设计推进。
