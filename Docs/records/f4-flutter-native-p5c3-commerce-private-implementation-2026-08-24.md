# Flutter Native P5-C3 Commerce Private 实现记录

> 状态：`P5-C3` 已完成；后续 [P5-D1 Wallet / Experience readiness](/records/f4-flutter-native-p5d1-wallet-experience-readiness-2026-08-24)也已完成，当前等待 P5-D1 实施确认
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-C3 实施就绪与方案冻结](/records/f4-flutter-native-p5c3-commerce-private-readiness-2026-08-24)

## 1. 结论

P5-C3 已按冻结边界完成。订单目录、订单详情、权益与道具继续只复用既有 Shop / Coin 契约，没有新增后端 API、DTO、权限、数据库、migration 或移动端 BFF。三个早期 MVP 页面已拆为 route 编排、四个独立只读状态 owner 与三个自适应 surface；订单、账号、请求代际或 dispose 变化会隔离迟到响应。

背包不再用一次 `Future.wait` 裁决整页。权益和道具分别提交 loading / ready / empty / unavailable / stale：任一路失败不会遮掉另一路成功，已知空快照刷新失败也不会被误写为首次不可用。主题激活 / 停用继续只归既有 Theme gateway；背包没有新增写入口。

compact / medium / expanded 已形成订单目录 `1 / 2 / 3` 列、订单详情连续任务 / 主区 + rail / `820 + 24 + 360`，以及背包 sequential section / 独立双 lane。精确断点覆盖 `599 / 600 / 1024 / 1280`，长订单号、商品、权益和值、道具和值均完成无横向溢出回归。

## 2. Owner 与规模

| Owner | 实施后职责 | 行数 |
| --- | --- | ---: |
| `shop_order_list_page.dart` | account / repository 编排、订单详情 handoff 与返回 | `163` |
| `shop_order_catalog_controller.dart` | 页码订单快照、refresh / append、稳定去重、账号与请求代际 | `284` |
| `shop_order_catalog_surface.dart` | 订单状态、订单卡与三档 grid | `232` |
| `shop_order_detail_page.dart` | order / account 编排及商品 / Wallet / 背包 handoff | `238` |
| `shop_order_detail_controller.dart` | 订单 target、同订单 stale 与请求代际 | `217` |
| `shop_order_detail_surface.dart` | 订单信息、时间线、上下文动作与三档结构 | `360` |
| `shop_inventory_page.dart` | account 编排、两个 collection owner 与来源 handoff | `239` |
| `shop_private_collection_controller.dart` | 私域只读 collection 的 account / generation / stale 共用契约 | `194` |
| `shop_benefit_inventory_controller.dart` | 权益读取 owner | `13` |
| `shop_item_inventory_controller.dart` | 道具读取 owner | `13` |
| `shop_inventory_surface.dart` | 权益 / 道具局部状态、来源动作与 sequential / 双 lane | `421` |

原 `490 / 592 / 621` 行订单列表、订单详情和背包页面已按真实职责拆分。本批运行时 Dart owner 最大 `421` 行。原 `1428` 行商品 / 私域组合测试先把六组订单、背包和 Wallet route 用例机械迁入 `220` 行 `shop_private_route_cases.dart`，入口文件降至 `1214` 行；新增 controller 与 adaptive 测试分别为 `445 / 329` 行，全部低于 `1500` 行硬上限。

## 3. 权威状态与账号边界

- `ShopOrderCatalogController` 区分 initial、refresh 与 append；同账号 refresh 保留旧订单，append issue 不冒充刷新失败，成功按规范 LongId / 服务端原始 ID 稳定去重。
- `ShopOrderDetailController` 只接受规范正整数 LongId；打开新订单立即清除旧详情，同订单刷新才保留旧快照并进入 stale。
- `ShopBenefitInventoryController` 与 `ShopItemInventoryController` 是两个独立实例和请求代际。页面可并行启动或等待刷新，但不再建立整批成败裁决。
- 已成功的空权益 / 空道具也是权威快照；后续刷新失败显示 stale issue 并保留空结论，不退回首次 unavailable。
- Shell 将 authenticated session `userId` 传入订单 / 背包，订单—商品—背包子 route 继续传递 account identity；`accessToken` 只作请求凭据，没有 session owner 的兼容测试入口才回退到 token。
- account、order、repository、credential generation 或 dispose 变化会使旧响应失效；换账号先清空旧私域快照，同账号 token 续期重新读取但不改变账号 identity。

## 4. 导航与主题边界

- “我的”继续打开订单目录和背包；订单目录只为规范 orderId 打开详情。
- 订单详情继续打开来源商品、`CONSUME + Order + orderId` 扣款流水和背包发放，并保留来源 label 与真实 Navigator 返回栈。
- 背包权益继续支持来源订单 / 商品，道具继续支持来源商品；异常来源 ID 不生成请求 target。
- `ShopProductDetailPage` 新增可选 account identity 传递，购买结果进入订单详情和背包来源回流时不会把 token 当作跨 route 账号真相。
- `ShopThemeEntitlementGateway` 的识别、激活 / 停用委托与 HTTP 契约保持；P5-C3 只把它作为回归边界，没有复制主题状态机或在背包暴露写入。

## 5. 三档结构

| 窗口 | 实施结果 |
| --- | --- |
| compact `<600px` | 单列连续订单卡；订单详情主内容与只读上下文动作连续排列；背包按权益、道具两个独立 section 顺序呈现 |
| medium `600–1023px` | 两列订单目录；订单详情主区 + `300px` 上下文动作区；背包 section 纵向组织并按真实宽度使用一至两列内容卡 |
| expanded `>=1024px` | 三列订单目录；订单详情使用受控主轴 + `360px` rail，空间足够时为 `820 + 24 + 360`；背包形成权益 / 道具两个等宽独立 lane |

页面继续消费 `RadishWindowClass`、`RadishContentFrame`、Theme token 与 Shared 状态原语；没有建立第二套断点、主题、Navigator 或权益状态机，也没有制造图片、推荐、用法或履约进度数据。

## 6. 验证

- Shop 页面族：`50 / 50`（P5-C2 完成时为 `35 / 35`，本批净增 `15` 条 controller、局部失败、空快照、断点、四主题和长内容用例）。
- Shell 静态 Smoke：`51 / 51`；覆盖“我的”—订单—详情—Wallet / 背包—来源订单 / 商品、购买结果和 Android Back。
- Flutter 全量：`318 / 318`。
- `flutter analyze`：零问题。
- `dart format`、文件行数、`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check` 通过。

本批未新增或修改后端 API、DTO、权限、数据库、migration、依赖、lockfile、Pen 或平台工程，未启动服务，未执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke。

## 7. 下一顺位

P5-C3 关闭。后续 [P5-D1 Wallet / Experience readiness](/records/f4-flutter-native-p5d1-wallet-experience-readiness-2026-08-24)已完成，四个只读 owner、局部状态、三档结构和测试边界已经冻结，当前等待独立实施确认。P5-C3 不扩取消订单、退款、权益 / 道具使用；P5-D1 也不随之自动获得资产或经验写入授权。Leaderboard、Browse History、P5-E、平台工程、服务启动与真实运行态 Smoke 继续独立授权。
