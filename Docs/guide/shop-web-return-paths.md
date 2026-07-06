# 商城正式 Web 回流与 WebOS 深链保留

本文说明公开商城、正式 Web 私域商城路径与 `/desktop` WebOS 保留入口之间的当前契约。完整商城前端设计仍见 [商城前端设计](/guide/shop-frontend)。

## 适用范围

- 公开商城：`/shop`、`/shop/products`、`/shop/product/:productId`
- 正式 Web 私域商城：`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`
- WebOS 保留入口：`/desktop`
- OIDC 回调：`/oidc/callback`

公开商城继续承担匿名浏览、分享、搜索索引和商品详情阅读。登录后购买、订单详情、订单列表和背包已经进入正式 Web 私域路径；WebOS 商城窗口继续作为 `/desktop` 历史工作台能力保留，不再作为浏览器默认购买回流目标。

## 公开详情到正式 Web 购买

公开商品详情需要继续购买时，进入：

```text
/shop/product/:productId?intent=purchase
```

规则：

- `productId` 只接受正整数形式的字符串，前端不得转成不安全 `number`。
- `/shop/product/:productId` 是公开 canonical；`intent=purchase` 只表示登录后购买意图，不写入 canonical、OpenGraph、JSON-LD 或 sitemap。
- 未登录用户触发购买时，保存 `/shop/product/:productId?intent=purchase` 作为一次性认证返回路径。
- 登录成功后回到同一商品详情，并在正式 Web 详情上下文内继续购买。
- 购买入口仍需先调用 `Shop/CheckCanBuy/{productId}`。资格不通过时只展示原因，不打开支付口令弹窗，也不提交 `Shop/Purchase`。
- 购买成功后进入 `/shop/order/:orderId`，让用户确认订单状态、商品快照和支付结果。

公开详情的复制链接、分享链接和搜索索引仍保持 `/shop/product/:productId`。从公开商品榜单或商品列表进入详情时，返回按钮应按来源显示精确文案，例如“返回榜单”或“返回商品列表”；该来源状态保存在 `history.state`，不写入公开分享 URL。

## 公开商城移动展示

`/shop`、`/shop/products` 和 `/shop/product/:productId` 在移动浏览器下仍属于正式 Web 公开浏览路径，不回退 WebOS 商城窗口。

移动展示规则：

- 商品详情按“信息优先、图片辅助、购买回流明确”组织，首屏优先展示商品名、类型、价格、库存 / 售出和购买状态。
- 商品列表和商城首页的商品记录也必须展示状态 / 库存信号；列表可用既有 `voInStock` 表达“可购买 / 不足”，不展示伪造库存数量，详情页继续以真实库存字段为准。
- 前端展示库存类型时必须兼容数字枚举和字符串枚举；`voStockType` 为无限库存时，详情页展示“无限库存”，不得因为 `voStock` 为空或 `0` 而显示成 `0 件商品`。
- 商品图使用紧凑预览，不把关键购买信息推到浮动底栏之后。
- 公开商城说明、登录购买提示和规则说明属于辅助区，应位于商品信息、商品列表或分页之后；除加载、错误或空态外，移动首屏不应用说明卡替代真实商品内容。
- 购买仍使用 `/shop/product/:productId?intent=purchase` 登录回流和购买弹窗，不新增移动专用载荷。
- 商品列表和商城首页移动端优先展示可点击商品记录、分类和筛选，不通过大图、营销说明或规则卡撑满首屏。

## 正式 Web 私域商城

当前支持：

```text
/shop/orders
/shop/order/:orderId
/shop/inventory
```

规则：

- `orderId` 只接受正整数形式的字符串。
- 三个路径都要求登录后访问；匿名态先进入 OIDC 登录并保存合法返回路径。
- `/shop/orders` 展示当前用户订单列表。
- `/shop/order/:orderId` 展示当前用户订单详情；无权限或订单不存在时给出明确错误态。
- `/shop/inventory` 展示当前用户背包和权益 / 消耗品。
- 订单通知目标优先进入 `/shop/order/:orderId`；缺少或非法订单 ID 时回 `/shop/orders`。
- 订单、背包和来源商品之间的回访保持正式 Web URL，不再把普通浏览器用户带回 `/desktop`。

这些路径属于登录态私域页面，不进入公开 sitemap，不输出 public canonical，也不作为公开分享 URL。

## 正式 Web 私域商城加载失败恢复

`/shop/orders`、`/shop/order/:orderId` 和 `/shop/inventory` 是购买后高频回看路径，加载失败时不能退化成空列表或静默失败。

当前恢复规则：

- 订单列表加载失败时保留订单页语义，展示失败说明、重试动作和复制诊断动作。
- 订单详情加载失败时保留目标 `orderId`，展示失败说明、重试、返回订单列表和复制诊断。
- 背包加载失败时展示失败说明、重试、返回公开商城或上一级，以及复制诊断。
- 复制诊断只包含模块、阶段、当前路径、错误摘要和有限目标上下文，例如 `orderId`、筛选状态、分页或当前 route kind；不得包含支付口令、token、完整请求体或用户隐私正文。

这些恢复动作只用于前端可恢复错误和人工支持定位，不改变订单、库存、权益发放或支付幂等后端契约。

## WebOS 深链保留

`/desktop` 仍可保留以下商城深链，供历史工作台、旧入口或 WebOS 内部窗口恢复使用：

```text
/desktop?app=shop&productId=:productId
/desktop?app=shop&orderId=:orderId
/desktop?app=shop&view=orders
/desktop?app=shop&view=inventory
```

规则：

- `/desktop` 是历史工作台入口，不是公开 URL 契约，也不是正式 Web 默认回流目标。
- `productId` 可匿名消费，用于打开 WebOS 商城商品详情；若用户继续购买，再由 WebOS 商品详情内购买动作触发登录。
- `orderId`、`view=orders` 和 `view=inventory` 都要求已登录后消费；匿名态应保存原深链并登录，不能把未登录状态渲染成空订单或空背包。
- 外部入口被消费后，桌面壳层会清理 `app / productId / orderId / view` 等一次性 query，避免深链参数长期停留在地址栏。

新浏览器主路径应优先使用正式 Web 路由。只有维护 WebOS 历史能力、旧通知载荷兼容或 `/desktop` 内部窗口恢复时，才继续使用这些深链。

## 登录后继续购买

未登录用户在公开商品详情点击“登录后继续购买”时：

1. 前端构造 `/shop/product/:productId?intent=purchase`。
2. `redirectToLogin({ returnPath })` 将该路径写入 sessionStorage 的一次性认证返回路径。
3. OIDC 回调成功后先写入 token 并预热当前用户资料。
4. 回调页消费返回路径并跳回原商品详情。
5. 商品详情继续执行购买资格检查、支付口令确认和购买提交。
6. 购买成功后跳转 `/shop/order/:orderId`。

安全边界：

- 返回路径只允许同源相对路径。
- 商城购买回流使用 `/shop/product/:productId?intent=purchase`；统一认证返回路径白名单还包括 `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`、`/circle`、`/me/*` 和公开 forum 受控参与 / 作者态路径，详见 [认证服务统一指南](./authentication-service.md)。
- 返回路径一次性消费，不能作为长期跳转状态。

## Console 排障回流

Console 商品、订单和胡萝卜流水之间存在管理端排障回流，但它不是公开 URL，也不是 WebOS 工作台深链。

当前规则：

- `ProductList` 与 `OrderList` 使用各自 URL 状态 helper 构造查询参数，避免页面内临时拼接跳转字符串。
- 商品详情进入相关订单时，应把当前商品详情上下文作为 `returnTo` 传给订单页；订单页若本身来自其他合法来源，也要继续保留原来源。
- 订单详情关闭、分页、筛选和重置应保留合法 `returnTo`，避免从胡萝卜流水或商品详情定位订单后丢失排障上下文。
- `orderId / productId / businessId / userId` 等外部 LongId 查询参数都按字符串读取和写回，不进入 JavaScript `number`。
- `returnTo` 只接受同源相对路径；外部 URL、协议相对 URL 和无法解析的来源必须丢弃。

## 验证要点

- `/shop/product/:productId` 公开详情复制链接时仍应复制 canonical，不携带 `intent=purchase`。
- 未登录时从公开商品详情触发购买，应保存 `/shop/product/:productId?intent=purchase` 并在登录后回到同一商品详情。
- 购买资格失败时只展示原因，不打开支付口令弹窗，不提交购买请求。
- 购买成功后应进入 `/shop/order/:orderId`。
- `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 匿名访问时应进入登录并恢复原路径。
- 订单通知应优先进入 `/shop/order/:orderId`，缺少合法订单 ID 时回 `/shop/orders`。
- `/desktop?app=shop&productId=:productId`、`/desktop?app=shop&orderId=:orderId`、`view=orders`、`view=inventory` 仍应在 WebOS 工作台中保持历史兼容。
- 从 Console 商品详情进入相关订单、从订单进入扣款流水、从流水回看订单时，应保留合法来源返回，并保持所有 LongId 查询参数为字符串。
