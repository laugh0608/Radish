# 商城 Web 回流与工作台深链

本文说明纯 Web 公开商城与 `/desktop` WebOS 保留入口之间的迁移期回流契约。完整商城前端设计仍见 [商城前端设计](/guide/shop-frontend)。

## 适用范围

- 公开商城：`/shop`、`/shop/products`、`/shop/product/:productId`
- WebOS 保留入口：`/desktop`
- OIDC 回调：`/oidc/callback`

公开商城继续承担匿名浏览、分享、搜索索引和只读商品详情；购买、订单、背包仍由登录后工作台商城承接。该设计是迁移期桥接，不表示 WebOS 重新成为新增功能主线。

## 公开详情到工作台

公开商品详情需要继续购买时，进入：

```text
/desktop?app=shop&productId=:productId
```

规则：

- `productId` 只接受正整数形式的字符串，前端不得转成不安全 `number`。
- 进入后打开商城窗口的商品详情。
- 商品详情可以匿名读取；点击购买时仍要求登录。
- 公开详情的 canonical、分享链接和 sitemap 仍保持 `/shop/product/:productId`，不写成 `/desktop` 深链。
- 从公开商品榜单或商品列表进入详情时，公开详情返回按钮应按来源显示精确文案，例如“返回榜单”或“返回商品列表”；该来源状态仍保存在 `history.state`，不写入公开分享 URL。

## 工作台入口消费

`/desktop` 是迁移期承接点，不是公开 URL 契约。入口解析规则集中在 `parseDesktopExternalEntry()`：

- `productId` 可匿名消费，用于打开商品详情；若用户继续购买，再由商品详情内的购买动作触发登录。
- `orderId`、`view=orders` 和 `view=inventory` 都要求已登录后消费；匿名态会先发起登录，不能把未登录状态渲染成空订单或空背包。
- 外部入口被消费后，桌面壳层会清理 `app / productId / orderId / view` 等一次性 query，避免深链参数长期停留在地址栏。

## 登录后继续购买

未登录用户在工作台商品详情点击“登录后继续购买”时：

1. 前端构造 `/desktop?app=shop&productId=...`。
2. `redirectToLogin({ returnPath })` 将该路径写入 sessionStorage 的一次性认证返回路径。
3. OIDC 回调成功后先写入 token 并预热当前用户资料。
4. 回调页消费返回路径并跳回原商品详情。
5. 用户继续点击购买，进入登录态购买流程。

购买入口仍需先调用 `Shop/CheckCanBuy/{productId}`。资格不通过时只展示原因，不打开支付口令弹窗，也不提交 `Shop/Purchase`。

安全边界：

- 返回路径只允许同源相对路径。
- 当前只允许 `/desktop`，避免开放重定向。
- 返回路径一次性消费，不能作为长期跳转状态。

## 订单与背包深链

当前支持：

```text
/desktop?app=shop&orderId=:orderId
/desktop?app=shop&view=orders
/desktop?app=shop&view=inventory
```

规则：

- `orderId` 只接受正整数形式的字符串。
- `view` 只接受 `orders` 或 `inventory`。
- 订单详情、订单列表和背包要求登录后消费。
- 匿名态不得误显空订单或空背包；应先保存原 `/desktop` 深链，完成登录态恢复，再打开目标视图。
- ShopApp 内部私有视图也会显示明确登录门禁状态；该门禁是用户意图保留，不是空数据兜底。

## Console 排障回流

Console 商品、订单和胡萝卜流水之间存在管理端排障回流，但它不是公开 URL，也不是 `/desktop` 工作台深链。

当前规则：

- `ProductList` 与 `OrderList` 使用各自 URL 状态 helper 构造查询参数，避免页面内临时拼接跳转字符串。
- 商品详情进入相关订单时，应把当前商品详情上下文作为 `returnTo` 传给订单页；订单页若本身来自其他合法来源，也要继续保留原来源。
- 订单详情关闭、分页、筛选和重置应保留合法 `returnTo`，避免从胡萝卜流水或商品详情定位订单后丢失排障上下文。
- `orderId / productId / businessId / userId` 等外部 LongId 查询参数都按字符串读取和写回，不进入 JavaScript `number`。
- `returnTo` 只接受同源相对路径；外部 URL、协议相对 URL 和无法解析的来源必须丢弃。

## 验证要点

- 从 `/shop/product/:productId` 进入 `/desktop?app=shop&productId=:productId` 后，应打开同一商品详情。
- 未登录时点击“登录后继续购买”，回调成功后应回到原商品详情。
- `/desktop?app=shop&orderId=:orderId` 应打开订单详情。
- `/desktop?app=shop&view=orders` 应打开订单列表。
- `/desktop?app=shop&view=inventory` 应打开背包。
- 复制公开商品链接时仍应复制 `/shop/product/:productId` canonical，而不是 `/desktop` 深链。
- 从 Console 商品详情进入相关订单、从订单进入扣款流水、从流水回看订单时，应保留合法来源返回，并保持所有 LongId 查询参数为字符串。
