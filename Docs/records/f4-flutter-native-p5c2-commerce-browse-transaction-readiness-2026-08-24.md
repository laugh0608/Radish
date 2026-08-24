# Flutter Native P5-C2 Commerce Browse / Transaction 实施就绪与方案冻结

> 状态：`P5-C2 readiness` 已完成；其后已按冻结方案[实施完成](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)
>
> 日期：2026-08-24（Asia/Shanghai）
>
> 前置记录：[P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23)、[P5-C1 Docs Reader 实现](/records/f4-flutter-native-p5c1-docs-reader-implementation-2026-08-23)

## 1. 本批结论

P5-C2 可以完全复用既有 Shop / Coin 契约完成，不需要新增后端 API、DTO、权限、数据库、migration 或移动端 BFF。现有 Flutter 已具备公开商品页码列表、公开详情、规范 LongId、登录回流、购买资格、余额、六位支付口令、单商品购买、失败重试同幂等键、购买成功余额刷新与订单详情确认；主要缺口是列表和详情仍停留在早期单列 MVP，商品详情把四类远端资源与敏感购买草稿集中在 `1341` 行 widget，支付草稿没有 dirty / busy 离开保护，账号 / target 变化和购买迟到响应也未形成显式隔离契约。

本批固定为同一 Commerce Browse / Transaction 页面族下的两个任务：公开商品目录，以及当前商品的公开详情—敏感购买—订单确认回流。目录和详情是公开读任务；唯一写入仍是购买当前商品 `1` 件。订单详情只作为既有购买结果确认 route 被复用，不在 P5-C2 重构；订单列表、订单详情、背包与权益的私域页面继续属于 P5-C3。

本次只完成代码 / API 事实审计、Shop 定向基线与方案冻结，没有修改 Dart、后端 API、依赖、lockfile、Pen 或平台工程，没有启动服务或执行真实 Gateway / 设备 Smoke。

## 2. 当前事实与基线

### 2.1 Owner 与规模

| Owner | 当前职责 | 当前规模 |
| --- | --- | ---: |
| `shop_product_list_page.dart` | 目录页生命周期、经典页码 append、刷新、详情 route 与全部列表 widget | `571` 行 |
| `shop_product_detail_page.dart` | 详情、资格、余额、登录回流、支付草稿、购买、幂等键、订单 route 与全部详情 widget | `1341` 行 |
| `shop_repository.dart` | 商品、购买、订单、权益与背包的统一 repository 契约 | `399` 行 |
| `shop_models.dart` | 商品、购买结果、订单、权益与背包模型 | `551` 行 |
| `shop_long_id.dart` | 正整数 LongId 字符串规范化 | `10` 行 |
| `wallet_repository.dart` | 余额与胡萝卜流水 | `114` 行 |
| `shop_product_detail_page_test.dart` | 商品详情 / 购买以及订单、背包、Wallet 组合回流和 repository 契约 | `1428` 行 |
| `shop_product_list_page_test.dart` | 目录成功 / 失败与详情打开 | `274` 行 |
| `shop_theme_entitlement_gateway_test.dart` | Shop 主题权益映射与激活 / 停用契约 | `177` 行 |

商品详情和详情测试尚未超过 `1500` 行，但已接近硬上限。P5-C2 必须先按真实职责拆分再增加三档结构与状态测试，不能在原文件继续堆叠。主题权益 gateway 只作为 Shop repository 回归基线保留，不迁入购买 owner。

### 2.2 当前行为与缺口

- 商品目录使用 `pageIndex / pageSize` 经典页码，刷新保留旧列表并有单一 request ID；但页面直接维护可变列表，append 不按稳定商品 ID 去重，首次 / refresh / append 共用字符串错误，加载更多失败会被渲染成刷新问题，也没有 disposed owner 的显式隔离。
- 商品详情已有同 target 刷新旧快照和请求 ID；但详情、资格、余额和购买分别用页面字段表达，只有详情 / 资格 / 余额有独立 request ID，没有统一 target 或结构化 issue，换商品和换账号依赖 widget 回调零散清理。
- 匿名用户可先读详情并从购买区发起 OIDC；登录成功后会回到仍存活的商品 route，并重新读取资格和余额。登录取消 / 失败尚未形成独立购买意图终态，pending 标记可能继续停留。
- 支付口令只存在页面内 `TextEditingController`，不会持久化或上送日志；但输入后离开没有确认，退出登录或账号切换也未显式清除草稿。
- 购买在提交前重新确认资格，固定数量 `1`，六位数字支付口令本地校验；首次真实提交才生成 `shop:` 幂等键。接口失败或业务失败保留原键，成功后清除，现有测试已证明失败重试同键。
- 购买提交只有 `_isPurchasing` 防重复，没有绑定 product / account / generation 的响应裁决；购买进行中发生会话变化或 route 离开时，迟到成功不应再替其他账号 / 商品导航。
- 成功结果会清空支付口令、刷新余额，并在规范订单 ID 存在时打开既有订单详情；订单 ID 非规范时会留在商品页提示到订单列表核对。购买成功、余额刷新失败和订单确认不可导航必须保持三个不同结论。
- 目录与详情仍使用 `PhaseScopeCard`、通用 Card 堆叠和固定 `20px` 单列；没有消费 P4 Theme / Shared / Adaptive 基座形成正式三档商品目录和敏感购买侧栏。

### 2.3 改造前测试基线

执行：

```text
flutter test test/shop_product_list_page_test.dart test/shop_product_detail_page_test.dart test/shop_theme_entitlement_gateway_test.dart
```

结果为 `25 / 25`。现有覆盖包括目录成功 / 失败、详情打开与返回、详情成功 / 失败、非规范 LongId 拒绝、登录回流、购买成功与订单确认、购买失败、失败重试同幂等键、非规范订单 ID、Shop repository 的公开 / 私有端点和请求体，以及既有订单、背包、Wallet 来源回流与主题权益 gateway。

当前没有直接覆盖：目录 refresh / append stale 分离与稳定 ID 去重、详情同 target refresh stale / recover、跨 product / disposed 迟到响应、资格与余额局部 stale、跨账号私域状态隔离、登录取消、支付草稿 dirty / busy 离开保护、购买中会话变化、`390 / 800 / 1440` 页面级结构、四主题同构和长商品信息无横向溢出。

## 3. 既有 API 与 target mapping

P5-C2 只消费以下既有契约：

| 既有能力 | P5-C2 用途 | 裁决 |
| --- | --- | --- |
| `GET Shop/GetProducts?pageIndex&pageSize` | 匿名公开商品目录 | 保持经典页码；Flutter 不新增分类、搜索、类型筛选或 capabilities owner |
| `GET Shop/GetProduct/{productId}` | 公开商品详情权威快照 | productId 必须先通过正整数 LongId 字符串规范化；登录读取仍由服务端记录 Browse History |
| `GET Shop/CheckCanBuy/{productId}?quantity=1` | 当前账号购买资格 | 登录后读取；资格只约束购买区，不影响公开详情继续阅读 |
| `GET Coin/GetBalance` | 当前账号胡萝卜余额 | 与资格独立裁决；失败不覆盖商品详情或资格结论 |
| `POST Shop/Purchase` | 当前商品购买 `1` 件 | body 保持 `productId / quantity / paymentPassword / idempotencyKey`；不增加备注或多数量 UI |
| `GET Shop/GetOrder/{orderId}` | 购买成功后的既有订单确认 route | 只复用，不在 P5-C2 改订单详情 owner 或布局 |
| Shell / Discover / Browse History handoff | 打开目录或指定商品详情并返回原来源 | 来源 label 与初始标题只作本地上下文；商品响应才是权威详情 |
| `PublicLinkCopyPanel` | Gateway Base URL + `/shop/product/:productId` | 继续使用规范 LongId 字符串，不引入 Flutter deep link 或分享 SDK |

后端虽已有分类、关键词、商品评价、举报和 capability 元数据，但 Flutter 当前没有这些业务 owner，P5-C2 不借 UI 重构机械追平 Web。主题权益读取 / 激活 / 停用继续由既有 Theme gateway 拥有，也不并入购买 controller。

## 4. 权威状态模型

### 4.1 商品目录 owner

新增独立目录 controller，显式维护 `pageIndex / pageSize` target、已加载商品快照、请求 generation、initial / refresh / append busy 与结构化 issue。

- 首次读取进入 loading；失败进入 unavailable / error，不伪造空目录。
- 同一目录 refresh 保留旧商品并标记 refreshing；失败保留旧目录并标记 stale，成功用第一页权威结果替换。
- append 保留既有目录；失败只显示 append issue，不冒充 refresh 失败；成功按规范商品 ID 稳定去重并更新服务端实际 page / pageCount。
- 新 generation、repository 替换或 dispose 使旧响应失效；无效商品 ID 不生成可打开 target。
- issue 至少保留 `kind / message / code / statusCode`，`FormatException` 明确归为 invalid response。

### 4.2 商品详情 owner

新增独立详情 controller，持有规范 product target、商品权威快照、request generation、refreshing 与结构化 issue。

- 打开新 productId 时立即切换 target 并清空旧商品；首次失败按 `404 not found`、unavailable、invalid response 或 request 语义显示当前目标问题。
- 同一 productId 刷新保留旧详情；失败保留旧详情并标记 stale，成功替换权威商品快照。
- target / repository 变化或 dispose 使旧响应失效。`initialTitle`、来源和设备 recent 只提供请求前上下文，不覆盖成功响应。
- 公开详情始终可独立阅读；购买资格、余额或交易问题不得把详情降级为整页失败。

### 4.3 购买 owner

新增页面级购买 controller，绑定规范 productId 与当前账号 identity，分别持有资格快照 / issue、余额快照 / issue、购买 busy、交易结果 / issue、登录返回意图和幂等键。支付口令本身继续只存于当前 route 的内存表单，不持久化、不记录日志；controller 只跟踪是否有敏感草稿以支持离开保护。

- 匿名态不请求资格 / 余额；发起登录时记录当前商品购买意图。登录成功只在原 route、原 product target 仍有效时恢复并读取资格 / 余额；取消或失败清除 pending intent 并显示局部反馈。
- product、账号或 repository target 变化时递增 generation，清空旧资格、余额、交易反馈、幂等键和支付草稿；退出登录同样清除所有账号私域状态。
- 资格和余额独立读取、独立刷新、独立 unavailable / stale；旧余额可在刷新期间保留，但不得用余额失败替代资格失败或商品失败。
- 提交固定购买 `1` 件：先验证六位支付口令与规范商品 ID，再取得当前资格；只有进入真实 `Purchase` 请求时才建立幂等键。
- 同一个未完成购买意图的接口异常、业务失败、支付口令失败或处理中重试继续复用原幂等键；购买成功、明确放弃、route 关闭、切换商品或切换账号后清除。修改支付口令不自动生成新的业务意图。
- busy 时阻止重复提交和 route 离开；非 busy 但存在支付草稿时离开需确认丢弃。窗口重排不得重建购买 controller 或丢失当前草稿。
- 购买响应只有在 route 未 dispose、product 与账号 target 均匹配当前 generation 时才能提交 UI。成功先清除口令和幂等键、保留交易收据，再刷新 `Coin/GetBalance`；`remainingBalance` 只属于交易结果，不替代余额接口的长期权威快照。
- 规范 orderId 存在时打开既有订单详情并带 `sourceLabel=购买结果 / returnLabel=返回商品详情`；订单 ID 无效或确认 route 失败只表示“购买成功但确认不可用”，不能反写成购买失败。订单返回后重新检查资格和余额。

## 5. Owner 与测试拆分

进入实现前按以下真实职责拆分：

| Owner | 冻结职责 |
| --- | --- |
| `shop_product_list_page.dart` | 公开目录 route、controller 生命周期、详情 handoff 与返回编排 |
| `shop_catalog_controller.dart` | 页码目录快照、refresh / append、稳定去重、请求代际与结构化 issue |
| `shop_product_catalog_surface.dart` | 商品卡、目录状态和 compact / medium / expanded grid |
| `shop_product_detail_page.dart` | product / account target 编排、dirty / busy PopScope、订单结果 handoff |
| `shop_product_detail_controller.dart` | 商品详情快照、同 target refresh stale、请求代际与结构化 issue |
| `shop_purchase_controller.dart` | 资格、余额、登录意图、交易、幂等与账号 / target 隔离 |
| `shop_product_detail_surface.dart` | 公开商品信息、价格、库存、限购、有效期、说明与链接 |
| `shop_purchase_surface.dart` | 敏感购买表单、资格 / 余额局部状态、提交与交易反馈 |
| `shop_page_shared_widgets.dart` | Commerce 页面族局部 heading、价格、状态与元数据组件 |
| `shop_issue.dart` | 目录、详情、资格与余额共用结构化 issue |

`shop_product_detail_page_test.dart` 按 public detail、controller states、purchase transaction、adaptive、private handoff 与 support / fixtures 拆分。现有 `25 / 25` 用例不得减少；其中订单、背包、Wallet 组合用例只做机械迁移和回归，不借测试拆分修改 P5-C3 运行时代码。所有改动 Dart owner 必须低于 `1500` 行。

## 6. 三档结构

页面继续消费既有 `RadishWindowClass`、`RadishContentFrame`、`RadishSectionSurface`、`RadishStateSlot`、四主题 token 与 reduced-motion，不建立第二套断点或页面专属主题。

| 窗口 | 冻结结构 |
| --- | --- |
| compact `390px` | 目录为单列连续商品卡；详情为“商品身份 / 价格—库存与限购—购买资格与敏感购买—商品说明”单任务流。购买区不脱离当前商品，不使用遮挡底部导航的悬浮主按钮。 |
| medium `800px` | 目录为自适应两列卡；详情使用公开商品主区 + `280–320px` 固定宽度购买摘要双区，共用单一页面滚动和同一购买 owner。 |
| expanded `1440px` | 目录在既有 `1280px` content frame 内形成受控三列目录，不增加筛选 rail；详情为约 `820px` 公开详情主轴 + `24px` 间距 + `360px` 敏感购买侧栏。 |

三档都先呈现真实商品、价格和可用性，再呈现购买任务；匿名用户继续完整阅读公开详情。商品描述、类别、权益值、库存、限购、有效期和公开路径使用实际模型字段，不制造商品图、评价、推荐或筛选数据。medium / expanded 侧栏不建立独立 Navigator 或第二个购买实例，窗口缩放、四主题切换和 reduced-motion 都必须保留同一草稿、幂等键与焦点语义。

## 7. 实施门禁

1. 保留现有 `25 / 25` Shop 用例，并新增 catalog / detail / purchase controller 定向状态测试。
2. 覆盖目录 initial unavailable、refresh pending / stale / recover、append issue、稳定 ID 去重、迟到 / disposed 隔离。
3. 覆盖详情 initial loading / not found / unavailable / retry、同 product 旧详情 refresh pending / stale / recover、新 product 清空旧详情和迟到隔离。
4. 覆盖资格 / 余额独立 unavailable / stale / recover、登录成功 / 取消、退出登录、跨账号与跨 product 私域隔离。
5. 覆盖支付口令格式、dirty 丢弃确认、busy 禁止离开、同意图失败重试同幂等键、成功 / 放弃 / 换 target 清键，以及购买迟到响应隔离。
6. 覆盖购买成功、余额刷新失败、规范 / 非规范订单 ID、订单返回后资格 / 余额刷新和来源返回。
7. 覆盖 `390 / 800 / 1440`、目录 `1 / 2 / 3` 列、expanded `820 / 360` 主区、长标题 / 描述 / 路径、四主题同构和无横向溢出。
8. 运行 Shop 定向、Shell 静态 Smoke、`flutter analyze`、全量 `flutter test`、文件行数、文档检查、仓库卫生与 `git diff --check`。

## 8. 停止线

- 不新增或修改后端 API、DTO、权限、数据库、migration、依赖或 lockfile。
- 不新增分类 / 搜索 / 类型筛选、商品 capability、推荐、商品图伪数据、评价、举报、购物车、多数量购买、备注、退款、售后或完整移动商城。
- 不修改购买服务端幂等、支付口令、资产事务、库存、订单履约或错误码契约；Flutter 只保持和强化现有客户端意图生命周期。
- 不修改 P5-C3 的订单列表 / 详情、背包、权益激活 / 停用运行时代码；订单详情只作为既有购买确认 route 复用。
- 不提前进入 Wallet / Experience、Leaderboard、Browse History 或其他派生只读面。
- 不读取或修改 Pen；P5-C2 直接继承 P1 已冻结的 Commerce 结构、P3 家族方向和 P4 Theme / Shared / Shell。
- 不启动服务，不执行真实 Gateway、浏览器、Android RC 或其他设备 Smoke；运行态验收继续独立授权。

P5-C2 readiness 已关闭，其后已按本记录完成商品目录、详情和购买 owner / 测试拆分与三档结构，详见 [P5-C2 实现记录](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24)。下一顺位进入 P5-C3 Commerce Private readiness；派生只读面、平台工程与运行态 Smoke 不随之自动授权。
