# Flutter Native P5-A 页面族成组重构拆批与首批就绪审计

> 状态：`P5-A readiness` 已完成；后续 [P5-B1 Forum Feed / Compose](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23) 已完成，当前等待 `P5-B2 Identity / Revisit`
>
> 日期：2026-08-23（Asia/Shanghai）
>
> 前置门禁：[P4-B5 成组静态门禁](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23)

## 1. 本批结论

P5-A 已反查 P1 全页面事实、P3 R3 继承路径、P4-B1–B5 代表实现和当前全部 Flutter 页面 owner。P5 不需要再次重做 Shell、Discover 或 Forum Detail：三档全局壳层、通知 stale、统一公开 Discover cursor 流和 Forum Detail 连续阅读已经完成并通过静态退出门禁，后续页面只消费这些继承来源。

剩余页面应按“Community → Docs / Commerce → 派生只读面”分批推进，且每批只包含一个高风险写入域。第一实施批建议固定为 `P5-B1 Forum Feed / Compose`；Profile / 我的与资料编辑另列 `P5-B2 Identity / Revisit`，避免发帖幂等与资料写入 / dirty 离开保护在同一批交叉。

本批只完成静态审计和文档拆批，没有修改 Dart、API、依赖、Pen 或平台工程，没有启动服务或执行真实 Smoke。

## 2. P4 继承基座

| 继承来源 | P5 裁决 |
| --- | --- |
| Adaptive Shell | compact 安全区胶囊底栏、medium / expanded 顶栏、五入口、快捷键、焦点、Android Back、OIDC 与来源返回直接复用；不建立第二套页面族 Shell |
| Notification | `idle / loading / available / empty / error / stale`、最近 20 条、标记已读与 Forum handoff 已在 Shell owner 内闭合；P5 不新增通知中心、分页、筛选或系统推送 |
| Discover | 连续内容密度、`904px` expanded 主轴、State Slot 和业务入口层级作为 Forum Feed、Shop 浏览等页面的呈现来源 |
| Forum Detail | 连续长内容、写入局部反馈、登录回流、幂等与来源返回作为 Forum Feed / Compose、Docs Reader 的交互来源 |
| Theme / Shared | 四主题、Noto typography、spacing / surface / motion、Lucide 映射、`RadishStateSlot / StateChip / SectionSurface` 继续作为唯一基础 |

P5 页面不得复制 Web DOM / CSS、P4 controller 或 handoff 状态，也不得以页面重构为理由新增 API、Flutter Chat、完整通知治理或平台工程。

## 3. 当前剩余 owner 与阻断

| 页面族 | 主要 owner | 当前规模 / 状态 | 实施前阻断 |
| --- | --- | --- | --- |
| Forum Feed / Compose | `forum_page.dart`、`forum_feed_controller.dart` | 页面 `1306` 行、测试 `1467` 行；20 个用例；列表 refresh 保留旧快照，发帖含分类、草稿、登录回流与幂等 key | 页面和测试接近硬上限；composer 常驻同一长页；仍使用 `PhaseScopeCard`、Card 堆叠和无页面级断点 |
| Identity / Revisit | `profile_page.dart`、`profile_controller.dart`、`profile_edit_dialog.dart` | 页面 `1904` 行、测试 `2598` 行；32 个用例 | 页面 / 测试超硬上限；资料、统计、帖子、评论仍由一次 `Future.wait` 整批裁决；编辑无 dirty / 离开保护，固定 `520px` Dialog |
| Docs Reader | `docs_page.dart` + feed / detail controller | 页面 `1294` 行、测试 `944` 行 | compact 内联详情与 handoff route 外壳并存；详情刷新未保留旧正文；缺目录—正文自适应结构 |
| Commerce Browse / Transaction | 商品列表、商品详情、购买 owner | 详情 `1341` 行、详情测试 `1428` 行 | 接近硬上限；敏感购买区和长商品内容未形成自适应分工；支付草稿无离开保护 |
| Commerce Private | 订单列表 / 详情、背包 | 单文件均低于 `650` 行 | 背包权益 / 道具仍整批失败；列表—详情和内容网格未按窗口级重排 |
| Wallet / Experience | 各自页面 | `646 / 562` 行 | 概要与流水仍整批成功 / 失败，局部 unavailable 未闭合 |
| Leaderboard | `leaderboard_page.dart` | `520` 行 | 只读首屏；业务 `themeColor` 与主题语义边界需要在页面复核，不扩分页 API |
| Browse History | `browse_history_page.dart` | `600` 行、2 个用例 | 需补 append 稳定 ID 去重、三档连续列表和与设备 recent shortcut 的文案区分 |

## 4. P5 实施拆批

| 批次 | 页面族 | 主要范围 | 高风险写入 |
| --- | --- | --- | --- |
| P5-B1 | Forum Feed / Compose | owner / 测试拆分、三档连续帖子流、独立 composer 任务、状态原语、分页 / handoff 回归 | `Post/Publish`，仅此一个 |
| P5-B2 | Identity / Revisit | Profile 独立权威快照、页面 / 测试拆分、公开主页 / 我的三档结构、资料编辑 dirty / busy / 离开保护 | `User/UpdateMyProfile`，仅此一个 |
| P5-C1 | Docs Reader | 共用详情 owner、compact 单任务、medium / expanded 目录—正文、旧正文刷新 stale | 无 |
| P5-C2 | Commerce Browse / Transaction | 商品列表、详情、资格 / 余额局部状态、购买敏感动作区和订单回流 | 单商品购买，仅此一个 |
| P5-C3 | Commerce Private | 订单列表 / 详情与背包 / 权益，局部状态和只读自适应结构 | 无 |
| P5-D1 | Wallet / Experience | 概要与流水独立权威状态、连续记录结构 | 无 |
| P5-D2 | Leaderboard | 紧凑排名、expanded 公开主页上下文、业务 accent 可读性 | 无 |
| P5-D3 | Browse History | 稳定去重、连续历史、原生 handoff 与设备 recent 边界 | 无 |
| P5-E | 成组静态门禁 | 四主题、compact / medium / expanded、关键状态、全量 analyze / test 和文件边界 | 无 |

每批独立授权、实现、测试和记录；后序批次可以根据前批事实缩小，但不得反向把多个高风险写入域合并。

## 5. P5-B1 推荐边界

### 5.1 Owner 拆分

进入布局实现前，先按真实职责拆分：

- `forum_page.dart`：生命周期、session / handoff、页面级编排；
- `forum_post_composer.dart`：分类、草稿、提交中、登录回流、成功 / 失败表面；
- `forum_feed_surface.dart`：排序、refresh、连续帖子、分页与作者 / 详情动作；
- `forum_feed_shared_widgets.dart`：页面内共享 meta、状态和轻量动作；
- `forum_page_test.dart`：保留 library 入口，按 repository contract、feed / states、composer、navigation / handoff 与 fixtures 拆分。

拆分只移动现有 owner；标题、分类、标签、正文 `TextEditingController` 和 `ForumSubmissionState` 继续由同一 `ForumPage` State 持有，确保 compact / medium / expanded 重排、登录回流和失败重试不会换草稿实例，不引入新的全局状态框架。

### 5.2 推荐三档结构

| 窗口 | 推荐结构 |
| --- | --- |
| compact `390px` | 单一连续帖子流；页面头只保留排序、刷新和“发布帖子”主动作；composer 使用可滚动的全高原生任务面，键盘出现时仍可完成分类、标签和长正文 |
| medium `800px` | 受控单主轴；composer 使用 bounded dialog / task surface，不常驻挤压帖子；不引入全局 rail |
| expanded `1440px` | 继承 Discover 的 `904px` 连续主轴，右侧只放发布入口、当前排序 / 页码和真实能力边界；不把 top categories 伪装成浏览筛选，不另取详情 API 做列表预览 |

帖子列表以 section、Divider 和留白形成连续扫描，不改为多列卡片瀑布；打开帖子继续走现有 `ForumDetailHandoffTarget`，打开作者继续走 Profile handoff。

### 5.3 必须保留

- `Post/GetList` 的 latest / hot、真实页码和 refresh 旧快照；
- `Category/GetTopCategories` 只服务发帖分类，不伪装论坛分类浏览；
- `Post/Publish` 的标题、分类、正文、`1–5` 标签校验；
- `forum-post:` submission fingerprint，同草稿失败重试复用同一 key；
- 匿名提交触发登录后回到同一草稿；成功刷新列表并以服务端返回的 PublicId 打开详情；
- 外部 Shell handoff 可消费、同一 target 清除后可再次打开；
- LongId / PublicId 保持字符串，不进入数值转换。

### 5.4 停止线

- 不新增搜索、分类浏览 API、富文本、附件、投票、抽奖、草稿箱、点赞或列表内详情读模型。
- 不改 Forum Detail、Discover、Shell 导航、后端接口、权限、数据库、依赖或 lockfile。
- 不读取或修改 Pen；P5-B1 按 P3 已确认的 Discover / Forum 继承路径实现。
- 不启动服务，不执行真实 Gateway / Android RC Smoke；阶段运行态验收继续独立授权。

## 6. P5-B1 门禁

1. 原 Forum repository / feed / publish / handoff `20` 个用例完整保留；拆测试不得减少覆盖。
2. 新增 `390 / 800 / 1440` 页面结构、compact 键盘、expanded `904px` 主轴、长标题 / 标签 / 正文与无横向溢出测试。
3. 覆盖 loading、empty、unavailable、refresh stale、分类失败、发布失败保留、同键重试、登录回流与成功详情 handoff。
4. 四主题至少通过同一代表结构参数化渲染；reduced-motion 下 composer 任务面不依赖自定义位移动效表达状态。
5. `dart format`、Forum 定向、Shell Smoke、`flutter analyze`、全量 `flutter test`、P5 改动 Dart owner `< 1500`、文档与差异卫生通过。

## 7. 后续进展

项目所有者已确认并完成 P5-B1，实施结果与本审计边界一致，详见 [P5-B1 实现记录](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23)。下一顺位为 P5-B2 Profile 独立快照；其他页面族、服务启动和真实 Smoke 仍不随之自动授权。
