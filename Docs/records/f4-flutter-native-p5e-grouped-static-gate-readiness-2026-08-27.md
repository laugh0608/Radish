# Flutter Native P5-E 成组静态门禁实施就绪审计

> 状态：`P5-E readiness` 已完成；后续已按确认方案实施并关闭 P5 首轮静态门禁
>
> 日期：2026-08-27（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P4-B5 成组静态门禁](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23)、[P5-D3 Browse History 实现](/records/f4-flutter-native-p5d3-browse-history-implementation-2026-08-27)

## 1. 结论

P4 Theme / Shared / Shell / Discover / Forum Detail 与 P5-B1–D3 页面族的现有静态基线健康：P4 / P5 成组入口 `383 / 383`、Flutter 全量 `406 / 406`、`flutter analyze` 零问题，全 Flutter `lib / test` Dart 文件均低于 `1500` 行。

P5-E 不需要新增业务 owner、运行时功能、API、依赖、聚合测试入口或第二套断点。现有 controller / repository / handoff / source return / dirty / busy / idempotency 测试已经覆盖各批冻结的关键行为；重新为每个页面制造四主题 × 四宽度 × 全状态的笛卡尔矩阵只会重复证据并增加维护成本。

当前只剩三处真实代表缺口：Discover 缺 medium 四主题同构，Forum Detail 缺页面级四主题同构，Commerce C2 的公开商品—敏感购买面缺四主题与 compact 长商品信息直接证据。P5-E 实施固定为在三个既有测试 owner 中补 `13` 个 widget tests，再运行成组、Smoke、全量、analyze、文件与仓库门禁；默认不修改运行时代码。

## 2. 继承范围与现有覆盖

| 范围 | 既有覆盖 | 审计结论 |
| --- | --- | --- |
| P4-B1 Theme / Shared | 四主题完整 token、对比度、Noto typography、density、surface、状态原语、主题预览 / 确认与权益 owner | 直接继承，不重复造主题系统测试 |
| P4-B2 Shell | `599 / 600 / 1023 / 1024` 分类器、`390 / 800 / 1440` 壳层、安全区、五入口、键盘 / 焦点、reduced-motion、Android Back 与 Shell Smoke | 直接继承；四主题由 foundation + 页面代表共同裁决 |
| P4-B3 Discover | cursor 读模型、generation、refresh stale、append 去重、compact 连续流、expanded `904px` 与 handoff | 缺 medium 页面结构在四主题下的直接证据 |
| P4-B4 Forum Detail | `390 / 800 / 1440`、`220 / 820 / 250`、长正文、定位 / 分页、空 / 错误、登录回流、编辑、幂等与 reduced-motion | 缺页面级四主题同构直接证据 |
| P5-B1 Forum Feed / Compose | 三档结构、四主题、loading / empty / category unavailable、refresh stale、长内容、键盘安全、草稿 / 登录 / 幂等 / handoff | 覆盖闭合 |
| P5-B2 Identity / Revisit | 三档结构、四主题、独立资源 unavailable / stale、append、跨 target / dispose、长身份、资料编辑 dirty / busy / 离开保护 | 覆盖闭合 |
| P5-C1 Docs Reader | compact 单任务、medium / expanded 目录—正文、四主题、旧正文 stale、空正文、长 slug、内链 / handoff / 返回 | 覆盖闭合 |
| P5-C2 Commerce Browse / Transaction | `599 / 600 / 1024 / 1280`、目录 `1 / 2 / 3` 列、详情 `820 + 24 + 360`、controller 隔离、dirty / busy、购买幂等与订单回流 | 缺商品 / 购买页面四主题和 compact 长标题 / 描述 / 路径直接证据 |
| P5-C3 Commerce Private | 订单 / 详情 / 背包精确断点、四主题、长订单、权益 / 道具局部失败、账号 / target / dispose 隔离 | 覆盖闭合 |
| P5-D1 Wallet / Experience | `599 / 600 / 1024 / 1280`、四主题、四个 owner、局部 unavailable / stale、append、长值与账号 / query 隔离 | 覆盖闭合 |
| P5-D2 Leaderboard | 精确断点、四主题、ready / empty / unavailable / stale、长身份、PublicId fallback 与业务 accent | 覆盖闭合 |
| P5-D3 Browse History | 精确断点、四主题、ready / empty / unavailable / stale、append issue、长内容、typed target 与账号 / credential 隔离 | 覆盖闭合 |
| 跨页面 Shell | `51 / 51`，覆盖登录、OIDC、五入口、Forum / Docs / Profile / Shop / Wallet / Leaderboard / Browse History handoff 与 Android Back | 继续作为独立固定门禁 |

共享 `RadishWindowClass` 已精确覆盖断点两侧；各页面只需覆盖代表结构或自身有特殊几何的精确边界，不要求每个页面重复四个边界值。四主题同样采用 foundation 全量语义 + 每个页面族至少一个代表结构，不在每个状态和宽度重复渲染。

## 3. 当前基线

### 3.1 成组测试

现有 P4 / P5 代表入口共 `29` 个可执行测试文件，结果为 `383 / 383`。该组合包含 Theme / Shared、Shell、Discover、Forum Detail、P5-B1–D3 全页面族与 Shell Smoke。

`part of` 支持文件不是独立 test entry，不能直接传给 `flutter test`；例如 `shop_private_route_cases.dart` 由 `shop_product_detail_page_test.dart` 注册，不在成组命令中重复列出。

### 3.2 全量与文件边界

| 门禁 | 当前结果 |
| --- | --- |
| P4 / P5 成组入口 | `383 / 383` |
| Shell Smoke | `51 / 51`，已包含在成组入口 |
| `flutter test` | `406 / 406` |
| `flutter analyze` | 零问题 |
| 运行时最大 owner | `radish_flutter_shell.dart` `1415` 行；`forum_detail_page.dart` `1342` 行 |
| 测试最大 owner | `shop_product_detail_page_test.dart` `1214` 行 |
| `>=1500` 行 Dart owner | `0` |

Shell 与 Forum Detail 接近建议线但仍承担真实页面编排职责，且低于硬上限；P5-E 不做机械拆分。商品详情组合测试已由 C2 / C3 从 `1428` 行回落到 `1214` 行，也不为追求更小数字继续切片。

## 4. 最小实施方案

### 4.1 Discover medium + 四主题

只修改 `discover_page_test.dart`：让现有 test app 接受 `RadishThemeId`，在 `800px` 对四主题分别验证 `discover-layout-medium`、连续公开流、Web 能力边界和无渲染异常。新增 `4` 个独立用例，不改 Discover repository、controller、surface 或 handoff。

### 4.2 Forum Detail 四主题

只修改 `forum_detail_page_test.dart` 与既有 reading cases：让测试 app 接受 `RadishThemeId`，在 `800px` 对四主题分别验证 medium 单阅读轴、正文 / 评论 / 线程上下文与无渲染异常。新增 `4` 个独立用例，不重跑写入交互的主题笛卡尔矩阵，也不改 Forum Detail 运行时 owner。

### 4.3 Commerce C2 四主题与长内容

只修改 `shop_browse_transaction_responsive_test.dart`：

- 在 `600px` 对四主题分别验证公开商品详情、购买区、语义 surface 与同一页面结构，新增 `4` 个用例；
- 在 compact `599px` 使用长商品名、描述、类别 / 权益值和正 LongId 公开路径，验证购买区优先、长内容可换行且无横向溢出，新增 `1` 个用例。

不修改支付草稿、购买 controller、幂等、订单 route 或主题权益 owner。若新增测试暴露真实布局问题，只允许在对应现有 surface 内做最小语义布局修正，并在实施前说明实际影响；不得扩展业务或兼容层。

### 4.4 预期计数

P5-E 计划新增 `13` 个 widget tests：Discover `4`、Forum Detail `4`、Commerce C2 `5`。在当前基线不变的前提下：

- P4 / P5 成组入口由 `383 / 383` 增至 `396 / 396`；
- Flutter 全量由 `406 / 406` 增至 `419 / 419`；
- Shell Smoke 保持 `51 / 51`，不得减少或改名规避失败。

## 5. 实施与退出门禁

1. 只在上述三个既有测试 owner 补 `13` 个代表用例；不新建聚合 test library，不复制 fixtures 或业务状态机。
2. 新增用例先独立运行，再执行 P4 / P5 `29` 个入口的成组命令；`shop_private_route_cases.dart` 等 `part` 文件不得作为独立入口。
3. 单独执行 Shell Smoke `51 / 51`，确认跨页面来源返回没有因测试调整丢失。
4. 执行 `flutter analyze` 与全量 `flutter test`；预期分别为零问题与 `419 / 419`。
5. 执行 `dart format`、全 `lib / test` Dart owner `<1500`、`npm run check:long-id-safety`、`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`。
6. 形成 P5-E 实现记录后关闭 P5 页面族首轮静态门禁；真实运行态、平台工程与下一阶段顺位必须重新裁决。

## 6. 停止线

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、移动端 BFF 或平台通道。
- 不修改页面业务 owner、请求状态、写入语义、幂等、导航或来源返回；测试若发现真实问题，修复仅限对应 surface 的根因且不得顺手重构。
- 不为 P5-E 新增截图 golden、像素基线、coverage 阈值或全页面笛卡尔矩阵；现阶段继续使用语义 key、几何、可见状态和 `takeException()` 作为稳定静态证据。
- 不读取或修改 Pen；P5-E 只验证已确认设计与既有原生实现的一致性。
- 不启动 API / Auth / Gateway 或 Flutter 应用，不执行真实 Gateway、浏览器、Android RC、iOS / desktop、签名、构建或分发。
- 不提前进入 P6 或恢复 Tauri / WebOS 新功能。

项目所有者已确认上述 test-only 方案，后续已由 [P5-E 实现](/records/f4-flutter-native-p5e-grouped-static-gate-implementation-2026-08-27)按边界落地：新增 `13` 个独立 widget tests，成组 `396 / 396`、Shell `51 / 51`、Flutter 全量 `419 / 419`、analyze 零问题，且没有修改运行时代码。P5 首轮静态门禁已关闭；本记录仍不自动授权服务启动、真实运行态 Smoke、平台工程或下一阶段工作。
