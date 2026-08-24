# Flutter Native P5-C3 Commerce Private 实施就绪与方案冻结

> 状态：`P5-C3 readiness` 已完成；其后已按冻结方案[实施完成](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-C2 Commerce Browse / Transaction 实现](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)

## 1. 本批结论

P5-C3 可以完全复用既有 Shop 私域读取契约、Coin 流水筛选和当前原生 route 完成，不需要新增后端 API、DTO、权限、数据库、migration 或移动端 BFF。现有 Flutter 已具备登录态订单页码列表、订单详情、权益 / 道具背包、订单—商品—背包—扣款流水来源返回，以及 Shop 主题权益 gateway；主要缺口是三个页面仍由 widget 局部字段直接拥有远端状态，订单 append 没有稳定去重和独立 issue，背包更把权益与道具用一次 `Future.wait` 绑定，任一路失败都会遮掉另一路成功。

本批固定为只读 Commerce Private 页面族：订单目录、当前订单详情，以及当前账号的权益 / 道具背包。订单、权益和道具只做读取、刷新、分页、来源核对与自适应呈现。主题激活 / 停用继续只归既有 `ShopThemeEntitlementGateway` 与主题选择任务；购买继续只归 P5-C2。P5-C3 不新增取消订单、退款、权益使用、道具使用、支付或履约写入。

本次只完成代码 / API / 导航事实审计、定向测试基线与方案冻结，没有修改 Dart、后端 API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与规模

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `shop_order_list_page.dart` | 订单目录 route、页码、refresh / append、详情 handoff 与全部列表 widget | `490` 行 |
| `shop_order_detail_page.dart` | 订单读取、refresh、商品 / Wallet / 背包 handoff 与全部详情 widget | `592` 行 |
| `shop_inventory_page.dart` | 权益和道具整批读取、refresh、来源订单 / 商品 handoff 与全部背包 widget | `621` 行 |
| `shop_repository.dart` | 商品、购买、订单、权益与背包统一 repository 契约 | `399` 行 |
| `shop_models.dart` | 商品、购买、订单、权益与背包模型 | `551` 行 |
| `shop_theme_entitlement_gateway.dart` | Shop 权益到主题状态机的读取与激活 / 停用适配 | `78` 行 |
| `shop_product_detail_page_test.dart` | 商品 / 购买以及订单、背包、Wallet 组合回流和 repository 契约 | `1428` 行 |
| `shop_theme_entitlement_gateway_test.dart` | 主题权益映射与激活 / 停用契约 | `177` 行 |

三个运行时页面都低于 `650` 行，但仍把 route 生命周期、远端状态和整页呈现集中在同一 owner。现有 `1428` 行组合测试接近 `1500` 行硬上限，不能继续承载 P5-C3 controller、局部失败和三档结构用例；实施前必须先按真实私域职责拆出既有订单 / 背包回归。

### 2.2 当前行为与缺口

- 订单目录使用 `pageIndex / pageSize=20` 经典页码，并用 request ID 拒绝旧响应；但列表直接维护可变数组，append 不去重，initial / refresh / append 共用字符串错误，加载更多失败会被呈现成刷新问题，dispose 也没有独立 owner 契约。
- 订单目录打开详情只检查非空 ID，没有像订单详情和来源动作一样先规范为正整数 LongId 字符串；异常服务端 ID 不应生成可请求 target。
- 订单详情已在请求前规范 orderId，同订单 refresh 会保留旧详情；但仍以页面字段和字符串错误表达状态，order / account / repository target 与迟到响应隔离没有形成可单测契约。
- 订单详情可打开来源商品、`CONSUME + Order + orderId` 扣款流水和背包。三类 handoff 都要求规范 LongId，并保留来源 label / 返回栈；这些导航是 P5-C3 必须保留的现有能力，不建立平行 route。
- 背包同时调用 `GetMyBenefits` 与 `GetMyInventory`，一次 `Future.wait` 决定整页成功 / 失败。权益成功而道具失败、或道具成功而权益失败时，成功资源也会被隐藏；刷新同样没有每类资源自己的 stale / recover。
- 背包中的权益可指向来源订单和商品，道具可指向来源商品；异常来源 ID 已会禁用 handoff。权益的 `canActivate / canDeactivate` 只供主题 gateway 判断，不代表背包页面可以执行写入。
- 三页仍使用 `PhaseScopeCard`、固定 `20px` 单列和通用 Card 堆叠，没有消费 P4 Shared / Adaptive 基座形成正式页面级 compact / medium / expanded 结构。
- Shell 已从“我的”打开订单与背包；购买结果、订单详情、背包和 Wallet 之间的真实 Navigator 返回栈已有 Smoke 覆盖。P5-C3 只重排 route 内部，不改变五入口 Shell 或来源返回语义。

### 2.3 改造前测试基线

执行：

```text
flutter test test/shop_product_detail_page_test.dart test/shop_theme_entitlement_gateway_test.dart
flutter test test/smoke_test.dart
```

结果分别为 `23 / 23` 与 `51 / 51`。现有私域相关覆盖包括订单详情 refresh 失败保留旧上下文、来源动作规范 LongId、背包加载失败仍可返回、背包来源动作规范 LongId、订单扣款流水筛选、私域 Shop repository 端点、主题权益 gateway，以及“我的”—订单—详情—Wallet / 背包—来源订单 / 商品的 Shell 返回栈。

当前没有直接覆盖：订单目录 standalone initial / empty / refresh / append、append issue 与稳定去重、订单目录异常 ID、订单 / 账号 / disposed 迟到响应；订单详情 initial unavailable / not found / recover 与跨 target 隔离；权益 / 道具单边失败、单边 stale / recover、跨账号隔离；`599 / 600 / 1024 / 1280` 页面级结构、四主题同构和长订单号 / 商品名 / 权益值 / 道具名无横向溢出。

## 3. 既有 API 与 target mapping

P5-C3 只消费以下既有契约：

| 既有能力 | P5-C3 用途 | 裁决 |
| --- | --- | --- |
| `GET Shop/GetMyOrders?pageIndex&pageSize` | 当前账号订单目录 | 保持经典页码；不新增状态筛选、搜索、日期筛选或取消入口 |
| `GET Shop/GetOrder/{orderId}` | 当前账号订单详情 | orderId 必须先通过正整数 LongId 字符串规范化；详情响应是订单权威快照 |
| `GET Shop/GetMyBenefits?includeExpired=false` | 当前账号有效权益 | 与道具独立裁决；不改过期过滤契约，不从背包激活 / 停用 |
| `GET Shop/GetMyInventory` | 当前账号道具库存 | 与权益独立裁决；数量只读，不新增使用、消耗或赠送 |
| `GET Coin/GetTransactions` 的 `CONSUME + Order + orderId` | 订单详情扣款流水 | 复用既有 Wallet route 和筛选，不把 Wallet 状态并入订单 owner |
| `ShopThemeEntitlementGateway` | 主题设置读取并切换已拥有主题权益 | 只作为回归边界；激活 / 停用继续由主题任务拥有 |
| Shell / 商品详情 / 背包 handoff | 打开订单、背包、来源订单或来源商品并返回原来源 | 保持真实 Navigator 栈；来源 label 与初始标题只作本地上下文 |

`accessToken` 只作为请求凭据，不应充当长期账号身份。实施时由已有 authenticated session 的 `userId` 作为首选 account target，并沿订单 / 背包子 route 传递；仅在仓库级测试或没有 session owner 的兼容入口下回退到规范化 token。账号 target 改变时必须清空旧私域快照，token 续期但 userId 不变时只更新凭据并重新读取，不把旧账号数据交给新账号。

## 4. 权威状态模型

### 4.1 订单目录 owner

新增独立订单目录 controller，维护 account target、`pageIndex / pageSize`、订单快照、服务端 `dataCount / pageCount`、请求 generation、initial / refresh / append busy 和三个独立结构化 issue。

- 首次读取清空旧账号目录并进入 loading；失败进入 unavailable / invalid response / request，不伪造空订单。
- 同账号 refresh 保留旧目录；失败只标记 refresh stale，成功由第一页权威结果替换。
- append 保留已加载订单；失败只显示 append issue，成功按服务端订单 ID 稳定去重并采用响应中的实际 page / pageCount。
- 订单 ID 只有通过正整数 LongId 规范化后才能生成详情 target。异常 ID 可保留为不可打开的服务端记录，但不能发起错误请求。
- account / repository / generation 变化或 dispose 使旧响应失效；换账号必须先清空旧订单，不允许跨账号 stale。

### 4.2 订单详情 owner

新增独立订单详情 controller，绑定规范 orderId 与 account target，维护订单权威快照、request generation、refreshing 和 `ShopIssue`。

- 新 order target 立即清空旧订单；首次失败按 not found、unavailable、invalid response 或 request 显示当前目标问题。
- 同一 order refresh 保留旧详情；失败保留旧订单并标记 stale，成功替换权威快照。
- account / order / repository / generation 变化或 dispose 使旧响应失效。列表 `initialTitle`、sourceLabel 与 returnLabel 都不是订单数据真相源。
- 商品、扣款流水和背包只消费当前成功订单快照中的规范 ID / 当前 account target；任一 handoff 失败不反写订单读取失败。

### 4.3 权益与道具 owner

新增两个独立只读 collection controller：一个只拥有 `GetMyBenefits`，一个只拥有 `GetMyInventory`。两者分别维护 account target、权威快照、request generation、loading / refreshing 与 `ShopIssue`，背包 route 只负责并列编排。

- 首次进入可并行启动两类读取，但分别提交结果；权益失败不遮挡道具，道具失败也不遮挡权益。
- 同账号刷新分别保留各自旧快照；任一路失败只在对应 section 标记 stale，另一路可独立成功、空或恢复。
- 全局“背包为空”只在两类资源都成功且都为空时出现；任一路未完成或失败时不得把整页描述为空。
- account / repository / generation 变化或 dispose 分别使旧响应失效；换账号立即清空两类旧快照。页面刷新可同时等待两个 controller，但不得重新建立整批成败裁决。
- 权益 / 道具的来源 handoff 只消费规范 LongId；异常来源显示不可导航的来源状态。主题权益 active / available 信息只读展示，不在背包 surface 暴露激活 / 停用动作。

四个 owner 统一复用既有 `shop_issue.dart`，保持 `notFound / unavailable / invalidResponse / request` 分类，不新建第二套 Shop 错误模型或全局状态框架。

## 5. Owner 与测试拆分

进入实现前按以下真实职责拆分：

| Owner | 冻结职责 |
| --- | --- |
| `shop_order_list_page.dart` | account / repository 编排、controller 生命周期、详情 handoff 与返回刷新 |
| `shop_order_catalog_controller.dart` | 页码订单快照、refresh / append、稳定去重、请求代际与结构化 issue |
| `shop_order_catalog_surface.dart` | 订单状态、订单卡和 compact / medium / expanded grid |
| `shop_order_detail_page.dart` | order / account 编排、controller 生命周期及商品 / Wallet / 背包 handoff |
| `shop_order_detail_controller.dart` | 订单详情 target、同 target stale、请求代际与结构化 issue |
| `shop_order_detail_surface.dart` | 订单身份、金额、状态、时间线、备注 / 失败原因和只读上下文动作 |
| `shop_inventory_page.dart` | account 编排、两个 collection owner 生命周期及来源 handoff |
| `shop_benefit_inventory_controller.dart` | 权益快照、独立 stale / recover、请求代际与结构化 issue |
| `shop_item_inventory_controller.dart` | 道具快照、独立 stale / recover、请求代际与结构化 issue |
| `shop_inventory_surface.dart` | 权益 / 道具 section、局部状态、来源动作和三档布局 |

继续复用 P5-C2 已建立的 `shop_issue.dart`、`shop_page_shared_widgets.dart`、`RadishContentFrame` 与 Shared 状态原语；仅在多个私域 surface 出现真实重复后再抽 Commerce Private 局部组件，不预建空泛抽象。

`shop_product_detail_page_test.dart` 先把既有订单详情、背包、Wallet 回流与私域 fixtures 机械迁移到独立 private routes / support 测试 owner，公开详情与购买用例继续留在原职责文件。随后新增订单目录、订单详情、权益、道具 controller 测试和 Commerce Private adaptive 测试。既有 `23 / 23` 组合回归与主题 gateway 用例不得减少；所有改动 Dart owner 必须低于 `1500` 行。

## 6. 三档结构

页面继续消费既有 `RadishWindowClass`、`RadishContentFrame`、`RadishSectionSurface`、`RadishStateSlot`、四主题 token 与 reduced-motion，不建立第二套断点、主题或 Navigator。

| 窗口 | 冻结结构 |
| --- | --- |
| compact `<600px` | 订单目录为单列连续订单卡；订单详情按订单身份 / 金额—状态与时间线—来源动作连续排列；背包依次呈现权益与道具两个独立 section，每类单列并保留自己的状态。 |
| medium `600–1023px` | 订单目录为受控两列；订单详情为订单主区 + `280–320px` 只读上下文动作区；背包仍按权益、道具顺序纵向组织，每个 section 使用最小卡宽约 `280px` 的一至两列内容网格。 |
| expanded `>=1024px` | 订单目录在 `1280px` content frame 内形成三列；订单详情采用约 `820px` 主轴 + `24px` 间距 + `360px` 上下文 rail；背包将权益与道具形成两个等宽独立资源 lane，各自保留 loading / empty / unavailable / stale 与来源动作。 |

订单卡只呈现真实订单号、商品、数量、金额、状态与时间；订单详情只呈现模型已有的商品、金额、状态、有效期、支付 / 完成 / 取消时间、原因、备注和扣款流水。背包只呈现模型已有的权益类型 / 值 / 状态 / 期限 / 来源和道具类型 / 值 / 数量 / 来源，不制造图片、推荐、用法或履约进度。窗口缩放、四主题切换和 reduced-motion 都不得重建 controller、丢失当前快照或改变返回栈。

## 7. 实施门禁

1. 保留现有 Shop 组合 `23 / 23` 与 Shell Smoke `51 / 51`，测试机械拆分不得减少既有覆盖。
2. 覆盖订单目录 initial unavailable、empty、refresh pending / stale / recover、append issue、稳定 ID 去重、异常 ID、跨 account / generation / disposed 隔离。
3. 覆盖订单详情 invalid target、initial not found / unavailable / retry、同订单 refresh pending / stale / recover、新订单清空旧详情和迟到响应隔离。
4. 覆盖权益与道具的双成功、双空、权益失败 / 道具成功、权益成功 / 道具失败、单边 stale / recover、跨账号与 disposed 隔离。
5. 覆盖订单—商品—Wallet—背包、背包—来源订单 / 商品的规范 LongId、来源 label、真实返回栈和返回后上下文保持。
6. 保持主题 gateway 的识别、激活 / 停用委托与 HTTP 契约回归；证明背包重构没有建立第二套主题权益状态机或写入口。
7. 覆盖 `599 / 600 / 1024 / 1280`、订单目录 `1 / 2 / 3` 列、详情主区 / rail、背包 sequential / 双 lane，以及长订单号 / 商品名 / 权益值 / 道具名无横向溢出。
8. 覆盖四主题同构、键盘 / 焦点 / Android Back、刷新与分页动作状态；reduced-motion 下不依赖自定义位移动效表达状态。
9. 运行 Shop 定向、Shell 静态 Smoke、`flutter analyze`、全量 `flutter test`、文件行数、文档检查、仓库卫生与 `git diff --check`。

## 8. 停止线

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖或 lockfile。
- 不新增订单搜索 / 筛选、取消订单、退款、支付、售后、履约动作、权益激活 / 停用、权益使用、道具使用 / 赠送或完整移动商城。
- 不修改 P5-C2 的公开目录、商品详情、支付草稿、购买幂等或服务端交易语义；只允许为既有私域 handoff 传递明确 account identity 和做机械测试拆分。
- 不把 Coin 流水、主题选择、Shell session 或来源返回状态并入 Commerce Private controller；各自继续由既有 owner 拥有。
- 不提前进入 Wallet / Experience、Leaderboard、Browse History、P5-E 成组门禁或其他派生只读面。
- 不读取或修改 Pen；P5-C3 直接继承 P1 Commerce 事实、P3 家族方向和 P4 Theme / Shared / Shell。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；运行态验收继续独立授权。

## 9. 下一顺位

P5-C3 readiness 已关闭，其后已按本记录先拆私域测试，再完成四个只读 owner、账号 / 请求隔离、局部状态与三档 surface，详见 [P5-C3 实现记录](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24)。后续 [P5-D1 Wallet / Experience](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24)也已完成，当前进入 P5-D2 Leaderboard readiness；P5-D2 实施、平台工程、服务启动与真实运行态 Smoke 不随之自动授权。
