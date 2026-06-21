# P3-12-B1 账户资产与商城交易 Web 化方案

> 日期：2026-06-21（Asia/Shanghai）
>
> 状态：方案已梳理，路由 / 登录回流契约、商城私域正式 Web 入口、资产正式入口、公开购买回流与公开商城 `/desktop` 回跳替换已完成；当前先推进 B1 直接相关残留清理判断，Gateway PC / mobile 真实复核后置到小阶段验收，统一 UI 设计后置到页面迁移完成后的 P3-12-D
>
> 结论：B1 首批应补齐 **正式 Web 资产入口、商城购买、订单、库存 / 权益和交易回流**，并在替代路径可用后替换 `/me` 完整钱包与公开商城购买中的 `/desktop` 回跳。

## 本轮核对

Git 状态：

- `git status --short --branch`：`dev...origin/dev [ahead 9]`，工作区无未提交改动。
- `git log -1 --oneline`：`0beb6ee9 docs(planning): 完成 P3-12-A 功能资产盘点`。
- `git rev-list --count master..dev`：`53`。

判断：

- 最新提交与 P3-12-A 盘点结论一致，未发现本轮开始前新增代码影响面。
- `dev` 相对 `origin/dev` 领先数量从 P3-12-A 记录中的 8 变为 9，是 P3-12-A 文档提交本身造成；本轮仍需在后续恢复 PR 前重新刷新 `master..dev` 范围。

已读代码范围：

- `/me`：`Frontend/radish.client/src/me/*`
- 公开商城：`Frontend/radish.client/src/public/shop/*`、`shopRouteState.ts`
- WebOS 商城：`Frontend/radish.client/src/apps/shop/*`
- WebOS 萝卜坑：`Frontend/radish.client/src/apps/radish-pit/*`
- API client：`Frontend/radish.client/src/api/shop.ts`、`coin.ts`
- 登录回流与路由入口：`entryRoute.ts`、`authReturnPath.ts`、`desktopEntryNavigation.ts`
- 相关契约测试：`authReturnPath.test.ts`、`publicRouteState.test.ts`、`entryRoute.test.ts`、`realUsagePathContracts.test.ts`、`publicSeoStatic.test.ts`

未执行：

- 未启动 Gateway / API / Auth / Vite。
- 未做 PC / mobile 真实页面复核。
- 未进入统一 UI 设计；本轮按功能迁移节奏推进路由 / 登录回流契约，后续页面接入继续复用既有组件和主题，不做跨页面视觉重塑。

## 首批实现进展

已完成：

- `PublicShopRoute` 支持 `/shop/product/:productId?intent=purchase`，用于保留公开商品购买意图。
- 公开商城 head canonical 继续归一到 `/shop/product/:productId`，不把购买意图写入公开 canonical。
- `entryRoute.isPublicContentPathname` 已收窄 `/shop/*` 判断，`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 不再进入公开内容壳层。
- `authReturnPath` 新增正式 Web 资产和商城交易返回路径：
  - `/me/assets`
  - `/me/assets/transactions`
  - `/shop/product/:productId?intent=purchase`
  - `/shop/orders`
  - `/shop/order/:orderId`
  - `/shop/inventory`
- 保留 `buildDesktopShop*ReturnPath` 作为 `/desktop` 历史入口维护线，未删除 WebOS 深链能力。
- 新增 `src/shop/ShopEntry.tsx`、`ShopWebApp.tsx` 和 `shopRouteState.ts`，以 `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 承接正式 Web 私域商城入口。
- `main.tsx` 与 `entryRoute` 已把私域商城路由接入 `ShopEntry`，公开 `/shop`、`/shop/products`、`/shop/product/:productId` 继续由公开商城壳层承接。
- `OrderList`、`OrderDetail`、`Inventory` 已补可选 `href` 能力，正式 Web 入口可为订单详情、返回、来源订单和来源商品提供真实 URL，WebOS 窗口版保持原回调导航。
- `meRouteState` 已把 `/me/assets`、`/me/assets/transactions` 纳入正式 Web 私域路由，`MeApp` 按 route 分支接入 `MeAssetsPage`。
- `MeAssetsPage` 覆盖余额、冻结余额、累计获得、累计支出、最近流水和完整分页流水，支持交易类型 / 状态基础筛选；未搬迁转账、支付口令、安全日志、统计导出或完整资产风控。
- `/me` 近期资产面板的“完整钱包”已从 `/desktop?app=radish-pit` 替换为 `/me/assets/transactions`。
- `PublicShopApp` 已把公开商品详情购买入口从 `buildDesktopShopProductReturnPath` 替换为 `buildShopProductPurchaseReturnPath`，保留真实 `href` 到 `/shop/product/:productId?intent=purchase`。
- 公开商品详情已接入登录态初始化、购买资格检查、`PurchaseModal`、支付口令确认、旧口令升级提示和 `purchaseProduct`；未登录时保存正式 Web return path 并跳登录。
- 购买成功后优先跳 `/shop/order/:orderId`，订单 ID 缺失时回 `/shop/orders`；公开详情 canonical 和分享链接继续归一到 `/shop/product/:productId`。
- 公开商城与公开发现的商城入口文案已从“购买留在 WebOS / 只读详情”调整为“公开浏览 + 登录购买 + 订单 / 背包私域 Web”。
- 纯 Web `/notifications` 的订单通知目标已从 `/desktop?app=shop&orderId=...` 切到 `/shop/order/:orderId`，缺失或非法订单 ID 时回 `/shop/orders`；WebOS 通知中心窗口内仍保留打开 WebOS `shop` app 的历史行为。
- `P3-12-C1` 首轮残留判断已开始，记录见 [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)：公开商品榜单 / 发现页榜单文案不再把商品详情购买能力描述为“不可购买”，而是明确“榜单只读、购买从商品详情登录后继续、订单 / 背包在私域 Web 路由”。

已验证：

- `node --test --test-isolation=none ./tests/authReturnPath.test.ts ./tests/publicRouteState.test.ts ./tests/entryRoute.test.ts ./tests/realUsagePathContracts.test.ts ./tests/publicHead.test.ts`
- `node --test --test-isolation=none ./tests/shopRouteState.test.ts ./tests/entryRoute.test.ts ./tests/realUsagePathContracts.test.ts ./tests/publicSeoStatic.test.ts`
- `node --test --test-isolation=none ./tests/meRouteState.test.ts ./tests/entryRoute.test.ts ./tests/authReturnPath.test.ts ./tests/realUsagePathContracts.test.ts ./tests/publicSeoStatic.test.ts`
- `node --test --test-isolation=none ./tests/publicSeoStatic.test.ts ./tests/publicRouteState.test.ts ./tests/authReturnPath.test.ts ./tests/realUsagePathContracts.test.ts ./tests/publicHead.test.ts`
- `node --test --test-isolation=none ./tests/notificationNavigation.test.ts ./tests/realUsagePathContracts.test.ts ./tests/authReturnPath.test.ts`
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `git diff --check`

## 现状判断

### `/me`

- 当前 `/me` 是登录态状态看板，只识别 `/me` 单一路由。
- 页面聚合公开资料、成长、资产摘要、最近访问和宠物摘要。
- 资产区只拉取余额和最近 5 条流水。
- “完整钱包”已改指 `/me/assets/transactions`，继续保留 `/desktop?app=radish-pit` 作为 WebOS 历史入口。

### 公开 `/shop`

- 公开路由只覆盖：
  - `/shop`
  - `/shop/products`
  - `/shop/product/:productId`
- `PublicShopApp` 商品详情仍承载公开浏览、head、分享和 JSON-LD；购买动作通过 `/shop/product/:productId?intent=purchase` 进入登录回流，登录后自动恢复购买确认。
- 购买确认复用 `PurchaseModal` 与 `shopApi.purchaseProduct`，成功后进入 `/shop/order/:orderId`；订单、库存和权益入口仍由正式 Web 私域路由承接。
- 公开壳层会应用 public head 和结构化数据，不适合作为订单 / 库存等私域页面的长期容器。

### WebOS `ShopApp`

- 已具备商品首页、列表、详情、购买弹窗、订单列表、订单详情、库存 / 权益、权益激活、道具使用和改名卡使用。
- 业务组件基本可复用，但顶层 `ShopApp` 与 `useCurrentWindow()`、窗口参数和内存状态导航耦合。
- `OrderList`、`OrderDetail`、`Inventory`、`PurchaseModal`、`useShopData`、`useShopActions` 是 B1 可复用资产；不应直接把整个窗口应用搬到正式 Web。

### WebOS `radish-pit`

- 覆盖账户总览、转移、记录、安全、统计。
- `TransactionHistory`、余额 / 统计 hook 可复用一部分。
- 转移、支付口令、安全日志和统计属于更高风险资产能力；不进入 B1 首批默认实现。

### API client / 后端能力

- `coinApi` 已覆盖余额、分页流水、流水详情、转账和统计。
- `shopApi` 已覆盖分类、商品、购买资格检查、购买、订单、权益、背包、权益激活 / 取消、道具使用和改名卡。
- 购买和转账已接入 `idempotencyKey`，B1 不需要新增后端交易契约。

## B1 范围

首批纳入：

- 资产正式 Web 入口：余额、冻结余额、累计收入 / 支出摘要、最近流水、完整流水列表。
- 商城正式 Web 交易入口：商品详情登录购买、购买确认、订单列表、订单详情。
- 库存 / 权益正式 Web 入口：权益列表、消耗品列表、来源订单 / 商品回流、现有可用动作。
- 交易回流：购买成功后进入订单详情，订单 / 库存可返回商品详情和资产流水。
- `/desktop` 回跳替换：`/me` 完整钱包、公开商品详情购买入口。

首批不纳入：

- WebOS Dock、窗口系统、桌面 app 外壳、窗口几何记忆。
- 完整钱包全量迁移：转账、支付口令、安全日志、统计导出和完整资产风控。
- 完整移动商城、Flutter 商城、Tauri 壳层、PWA。
- 新经济玩法、商品体系扩展、发放规则调整、后端幂等契约改造。
- Console 商品治理、订单治理或资产管理员入口。

## 路由边界

推荐正式 Web 路由：

| 路由 | 类型 | 说明 |
| --- | --- | --- |
| `/me` | 登录态私域 | 保持我的状态看板，不承载完整交易页。 |
| `/me/assets` | 登录态私域 | 新增资产正式入口，展示余额、统计摘要和最近流水。 |
| `/me/assets/transactions` | 登录态私域 | 新增完整资产流水列表，保留分页和基础筛选。 |
| `/shop` | 公开 | 保持公开商城首页。 |
| `/shop/products` | 公开 | 保持公开商品列表。 |
| `/shop/product/:productId` | 公开 + 登录态购买动作 | 商品详情仍可公开阅读；登录后可直接购买。 |
| `/shop/product/:productId?intent=purchase` | 公开 + 登录回流 | 用于购买按钮和登录后自动恢复购买意图。 |
| `/shop/orders` | 登录态私域 | 新增我的订单列表。 |
| `/shop/order/:orderId` | 登录态私域 | 新增订单详情。 |
| `/shop/inventory` | 登录态私域 | 新增库存 / 权益入口。 |

路由处理原则：

- `/shop` 公开浏览和 `/shop/orders`、`/shop/order/:id`、`/shop/inventory` 私域交易必须在入口识别上区分，避免私域页面继续套用 public head / JSON-LD。
- `authReturnPath` 应新增正式 Web 商城和资产返回路径白名单，拒绝未知参数、非法 LongId、外部 URL 和反斜杠路径。
- 保留 `buildDesktopShop*ReturnPath` 作为 `/desktop` 维护线；正式 Web 新增 `buildShopProductPurchaseReturnPath`、`buildShopOrderReturnPath`、`buildShopInventoryReturnPath` 等独立 helper。
- 购买成功后优先跳 `/shop/order/:orderId`；库存来源动作跳 `/shop/order/:orderId` 或 `/shop/product/:productId`。

## 可复用组件

可直接复用或轻改：

- `ShopHome`、`ProductList`：继续服务公开商城。
- `ProductDetail`：继续保留给 WebOS `ShopApp`；公开商品详情已在 `PublicShopApp` 中独立接入正式 Web 购买回流，避免把 WebOS 窗口状态机带入公开壳层。
- `PurchaseModal`：保留支付口令校验和 `shop:` 幂等键生成。
- `OrderList`、`OrderDetail`、`Inventory`：作为私域交易页主内容复用，补真实 `href` 和路由回调。
- `useShopData`、`useShopActions`：可复用 API 编排，但正式 Web 入口应避免依赖窗口状态。
- `coinApi.getBalance`、`coinApi.getTransactions`、`TransactionHistory`：承接资产入口和完整流水。

需要隔离或避免复用：

- `ShopApp` 顶层窗口状态机：保留给 WebOS，不直接进入正式 Web。
- `RadishPitApp` 顶层 tab 壳：保留给 WebOS，不搬迁窗口式导航。
- WebOS CSS 中大量固定窗口风格和硬编码视觉：不能作为 P3-12-D 正式 Web 视觉基线。

## UI / Pencil 口径

- B1 当前按“功能迁移优先、统一视觉后置”推进：先补正式 Web 资产、购买、订单、库存 / 权益和交易回流路径，页面接入阶段复用既有组件、主题 token 和成熟交互。
- 迁移期间只允许必要布局适配和路由级承接，不引入新的跨页面视觉体系，不照搬 WebOS Dock、窗口壳层、窗口几何记忆或桌面化导航。
- 页面迁移齐后再进入 `P3-12-D` 统一 UI 设计与美化专题；届时使用 Pencil 做 PC / mobile 设计稿，更新设计 / 说明文档，再进入视觉实现。
- 若 B1 过程中确实出现跨页面交易壳层或端点级视觉重塑需求，应先把该部分切到 `P3-12-D`，不阻塞 B1 功能迁移主线。

## 验证口径

代码批次内建议：

- `npm run test --workspace=radish.client -- authReturnPath publicRouteState entryRoute realUsagePathContracts publicSeoStatic`
- `npm run type-check --workspace=@radish/http`（若调整共享 HTTP 类型或 client）
- `npm run build --workspace=radish.client`
- `git diff --check`

涉及购买、订单、库存或资产写入时补：

- 后端定向测试覆盖 `ShopController` / `OrderService` / `CoinService` 相关路径。
- 必要时执行 `dotnet test Radish.Api.Tests`。

阶段性验收或准备合并前：

- 在用户明确确认 API / Auth / Gateway / 前端已启动后，再执行 Gateway PC / mobile 真实页面复核。
- 覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/product/:id?intent=purchase`、`/shop/orders`、`/shop/order/:id`、`/shop/inventory`、购买成功回流和库存来源回流。

## 风险点

- `entryRoute.isPublicContentPathname` 当前把所有 `/shop/` 都视为公开内容；新增私域交易路由前必须先收窄判断或增加更高优先级入口。
- `authReturnPath` 目前拒绝 `/shop/product/:id` 普通公开路径；购买登录回流需要新增精确白名单，不能放开整个 `/shop/*`。
- 公共商城详情有 SEO / JSON-LD 契约；购买动作接入不能破坏公开详情 canonical 和分享链接。
- WebOS `ProductDetail` 当前未登录购买仍构造桌面 return path，保留给 `/desktop` 维护线；正式 Web 公开详情不要复用这段未登录回流逻辑。
- `Inventory` 和 `OrderList` 当前多处使用按钮导航；正式 Web 路由需要补真实 `href`，保持新开标签 / 复制链接可用。
- 资产流水导出当前 `TransactionHistory` 会拉取多页数据并生成 CSV；若搬到移动 Web，需确认性能和移动端交互，不应作为 B1 首屏必要动作。
- 支付口令升级提示已有，但支付口令设置页仍在 WebOS `radish-pit`；B1 可提示后续治理，不应临时引回 `/desktop` 作为主要完成路径。

## C1 残留清理口径

- 开发中先清理与 B1 直接冲突的默认产品路径残留：仍误回 `/desktop` 的链接、购买 / 订单 / 库存文案和 route helper 假设。
- WebOS `ShopApp`、`radish-pit`、`buildDesktopShop*ReturnPath`、`desktopEntryNavigation` 和对应测试继续作为 `/desktop` 历史入口维护线保留。
- Gateway PC / mobile 真实页面复核不作为本地连续提交的默认步骤，放到 B1 + C1 小阶段准备验收时，在用户明确确认 API / Auth / Gateway / 前端已启动后集中执行。
- 页面级 UI 设计、跨页面视觉重塑和统一美化继续后置到 `P3-12-D`。

## 后续执行顺序

1. 继续 `P3-12-C1` 与 B1 直接相关的 WebOS 残留入口清理判断，只处理默认产品路径仍误回 `/desktop` 的链接、文案和路由假设。
2. B1 + C1 小阶段准备验收时，在用户明确确认 API / Auth / Gateway / 前端已启动后做 PC / mobile Gateway 复核，覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/product/:id?intent=purchase`、`/shop/orders`、`/shop/order/:id`、`/shop/inventory` 和购买成功回流。
3. 页面迁移齐后进入 `P3-12-D` 统一 UI 设计与美化专题。
