# Flutter Native P5-E 成组静态门禁实现

> 状态：`P5-E` 已完成；P5 页面族首轮静态门禁已关闭
>
> 日期：2026-08-27（Asia/Shanghai）
>
> 前置记录：[P5-E 成组静态门禁 readiness](/records/f4-flutter-native-p5e-grouped-static-gate-readiness-2026-08-27)

## 1. 结论

P5-E 已按确认的 test-only 方案完成。Discover、Forum Detail 与 Commerce C2 的最后三处代表证据缺口已由 `13` 个独立 widget tests 闭合；没有发现需要修改运行时 surface 的布局问题，也没有新增业务 owner、API、依赖、设计源、平台工程或运行态兼容层。

P4 / P5 `29` 个可执行代表入口由 `383 / 383` 增至 `396 / 396`，Flutter 全量由 `406 / 406` 增至 `419 / 419`，Shell Smoke 保持 `51 / 51`，`flutter analyze` 零问题。P5 页面族的三档结构、四主题代表面、关键状态、长内容与跨页面 handoff 已形成首轮完整静态闭环。

## 2. 实现范围

### 2.1 Discover medium 四主题

`discover_page_test.dart` 的 test app 现在可显式选择 `RadishThemeId`，并为 `default / guofeng / theme-dark-night / theme-sakura` 各注册一个 `800px` medium 用例。每个用例验证：

- `discover-layout-medium` 保持同一页面结构；
- 连续公开流、焦点帖子与 Web-only channel 边界同时存在；
- medium 不错误出现 expanded 社区洞察 rail；
- 渲染过程没有 framework exception。

### 2.2 Forum Detail medium 四主题

`forum_detail_page_test.dart` 的共享 test app 增加主题参数，`forum_detail_page_reading_cases.dart` 为四主题各注册一个 `800px` medium 阅读用例。每个用例验证单一连续阅读轴、inline navigation、正文与评论上下文存在，且不出现 expanded context rail 或渲染异常。

### 2.3 Commerce C2 四主题与 compact 长内容

`shop_browse_transaction_responsive_test.dart` 新增：

- 四个 `600px` 主题用例，验证公开商品详情、medium 购买 rail 与单商品购买任务同构；
- 一个 `599px` 长内容用例，覆盖长商品名、长描述、长分类、长权益值、正 LongId `9223372036854775807` 公开路径和购买区优先顺序。

长内容用例通过既有 repository fixture 提供数据，不修改商品详情、购买状态机、LongId 规则或公开路由实现。

## 3. 验证结果

| 门禁 | 结果 |
| --- | --- |
| 新增涉及的三个测试入口 | `56 / 56`；原有 `43` + 新增 `13` |
| P4 / P5 `29` 个代表入口 | `396 / 396` |
| Shell Smoke | `51 / 51` |
| Flutter 全量 | `419 / 419` |
| `flutter analyze` | 零问题 |
| `dart format` | `4` 个涉及文件已格式化 |
| 运行时最大 owner | `radish_flutter_shell.dart` `1415` 行；`forum_detail_page.dart` `1342` 行 |
| 测试最大 owner | `shop_product_detail_page_test.dart` `1214` 行 |
| `>=1500` 行 Dart owner | `0` |

本批改动后的直接测试 owner 分别为：`discover_page_test.dart` `679` 行、`forum_detail_page_test.dart` `72` 行、`forum_detail_page_reading_cases.dart` `490` 行、`shop_browse_transaction_responsive_test.dart` `383` 行。

## 4. 边界与后续

- 本批只修改测试与文档；没有修改 `lib/` 运行时代码。
- 没有新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程。
- 没有启动 API / Auth / Gateway 或 Flutter 应用，没有执行真实 Gateway、浏览器、Android RC、iOS / desktop、签名、构建或分发。
- `part of` 支持文件继续不作为独立 `flutter test` entry；成组门禁固定使用 `29` 个可执行入口。
- P5 首轮静态门禁关闭不等于 Android UI RC 或多平台产品化完成。下一顺位建议进入 P6 Android UI RC readiness，先审计构建、设备、服务、证据与清理边界；是否实施、启动服务或执行真实设备 Smoke 仍需项目所有者独立确认。

P5-E 已关闭，后续不得以补静态覆盖为由继续扩张全页面笛卡尔矩阵。新的页面差异只在真实结构、行为或缺陷触发时按对应 owner 补证据。
