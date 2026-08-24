# Flutter Native P5-C2 Commerce Browse / Transaction 实现记录

> 状态：`P5-C2` 已完成；后续 P5-C3 与 [P5-D1 Wallet / Experience](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)也已完成，当前进入 P5-D2 Leaderboard readiness
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-C2 实施就绪与方案冻结](/records/f4-flutter-native-p5c2-commerce-browse-transaction-readiness-2026-08-24)

## 1. 结论

P5-C2 已按冻结边界完成。公开商品目录、公开详情与登录态单商品购买继续只复用既有 Shop / Coin 契约，没有新增后端 API、DTO、权限、数据库、migration 或移动端 BFF。目录、详情和购买已从页面局部字段拆为三个独立 owner；公开详情不会被资格、余额或交易失败覆盖，商品 / 账号 / 请求代际变化会隔离迟到响应。

购买仍固定为当前商品 `1` 件。支付口令只存在当前 route 的内存表单，草稿具备 dirty 丢弃确认，购买 busy 时禁止离开；同一未完成意图失败重试复用原 `shop:` 幂等键，成功、放弃、换商品或换账号后清除。购买成功与订单确认继续分层：规范订单 ID 打开既有订单详情，非规范 ID 留在商品详情提示核对，不把成功反写成失败。

compact / medium / expanded 已形成目录 `1 / 2 / 3` 列，以及详情单任务流、公开主区 + 购买区双区和 expanded `820 + 24 + 360` 结构。精确断点测试覆盖 `599 / 600 / 1024 / 1280`，并在实施中修正了最窄两列 / 三列卡片高度与 `600px` 双区公开路径溢出。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `shop_product_list_page.dart` | 目录 route、controller 生命周期与详情 handoff | `158` |
| `shop_catalog_controller.dart` | 页码快照、refresh / append、稳定去重与请求代际 | `221` |
| `shop_product_catalog_surface.dart` | 目录状态、商品卡与三档 grid | `223` |
| `shop_product_detail_page.dart` | product / account 编排、登录、PopScope 与订单结果 handoff | `389` |
| `shop_product_detail_controller.dart` | 公开详情 target、同 target stale 与请求代际 | `167` |
| `shop_product_detail_surface.dart` | 公开商品信息、链接、元数据与三档结构 | `417` |
| `shop_purchase_controller.dart` | 资格、余额、登录意图、交易、幂等与 target 隔离 | `604` |
| `shop_purchase_surface.dart` | 敏感购买表单及资格 / 余额 / 交易局部状态 | `228` |
| `shop_page_shared_widgets.dart` | Commerce 主题边界、局部状态与格式化组件 | `108` |
| `shop_issue.dart` | Shop 结构化 issue | `56` |

原 `571` 行目录页和 `1341` 行详情页已按真实职责拆分。既有 `1428` 行组合回归没有继续增长；新增 controller 与 adaptive 测试分别落在 `475` 行和 `277` 行独立 owner。本批新增 / 修改 Dart owner 最大 `604` 行，全部低于 `1500` 行硬上限。

## 3. 权威状态与交易契约

- `ShopCatalogController` 区分 initial、refresh 与 append；同目录刷新保留旧商品，append issue 不冒充刷新失败，成功按规范 LongId 稳定去重，新 generation 与 dispose 会使旧响应失效。
- `ShopProductDetailController` 统一表达 idle / loading / ready / unavailable / stale；打开新商品立即清除旧详情，同商品刷新才保留旧快照。
- `ShopPurchaseController` 将资格、余额和交易反馈分开保存；任一局部刷新不会擦除另一资源或交易结论，token 更新会在同账号内重新读取私域状态。
- 商品或账号变化会同时清除旧资格、余额、交易结果、幂等键与支付草稿；购买响应只有在 product / account / generation 仍匹配时才能提交 UI。
- 登录意图只绑定当前商品 route；登录成功回到原详情并读取资格 / 余额，取消、失败或缺少登录入口显示局部终态。
- 订单详情仍是既有独立 route；返回商品详情后重新读取资格与余额。P5-C2 没有修改订单、背包、权益或 Wallet 私域页面 owner。

## 4. 三档结构

| 窗口 | 实施结果 |
| --- | --- |
| compact `<600px` | 单列目录；详情按公开身份 / 价格、购买任务、说明连续排列，购买区先于长说明 |
| medium `600–1023px` | 两列目录；公开详情主区 + `300px` 购买区，共用同一滚动与购买 owner |
| expanded `>=1024px` | 三列目录；详情使用受控公开主区 + `360px` 购买 rail，空间足够时固定 `820 + 24 + 360` |

页面继续消费 `RadishWindowClass`、`RadishContentFrame`、Theme token 与 Shared 状态原语；没有建立第二套主题、断点或购买实例，也没有制造商品图、评价、推荐或筛选数据。

## 5. 验证

- Shop 定向：`35 / 35`（改造前 `25 / 25`，新增 `10` 条 controller / adaptive / dirty 边界用例）。
- Shell 静态 Smoke：`51 / 51`；覆盖 Discover 目录 / 详情 handoff、登录态购买、来源返回与 Android Back。
- Flutter 全量：`303 / 303`。
- `flutter analyze`：零问题。
- `dart format`、文件行数与 `git diff --check` 通过；文档与仓库卫生门禁在本记录同步后执行。

本批未新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程，未启动服务，未执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke。

## 6. 下一顺位

P5-C2 关闭。后续 [P5-C3 Commerce Private](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24) 与 [P5-D1 Wallet / Experience](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)也已完成，当前进入 P5-D2 Leaderboard readiness。购买写入仍只归 P5-C2；P5-D2 实施、后续派生面、平台工程、服务启动与真实运行态 Smoke 不随之自动授权。
