# WOG-6 跨端幂等契约补齐方案评审

> 日期：`2026-06-20`（Asia/Shanghai）
>
> 状态：`待确认 / 进入代码前评审`
>
> 关联说明：[写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)、[支付与转账幂等治理](/guide/payment-idempotency-governance)、[WOG-1 写操作分级盘点记录](/records/wog-1-write-operation-inventory-2026-06-20)

## 目标

`WOG-6` 聚焦已具备服务端幂等能力、但客户端接入不完整的资产写入口。首批建议只补齐 Flutter 单商品购买的 `idempotencyKey` 契约，让移动原生购买与 Web 官方流程使用同一幂等语义。

本批不是重新设计商城购买链路，也不是把全部资产写入口改成强制幂等。目标是先让已承接到 Flutter 的单商品购买在重复点击、网络失败后重试、页面短时间内重复提交时，不绕过服务端已落地的 `OperationIdempotencyRecord` 保护。

## 当前代码事实

### 1. 服务端与 Web 已有基础

[支付与转账幂等治理](/guide/payment-idempotency-governance) 已完成首批代码：

- `CreateOrderDto.IdempotencyKey` 已存在，最大长度 `80`。
- `OrderService.PurchaseAsync` 已按 `ShopPurchase` 操作类型接入请求摘要、幂等记录、处理中提示、冲突拒绝和终态响应重放。
- 商城购买摘要字段为 `ProductId`、`Quantity`、`UserRemark`，不包含 `PaymentPassword`。
- Web 商城购买弹窗会生成 `shop:{uuid}`，同一次确认提交复用同一个 key，重新打开弹窗或变更数量后生成新 key。
- 服务端暂时兼容未传 key 的旧客户端；未传 key 时沿用现有写入流程，不承诺幂等。

### 2. Flutter 商城购买未传 key

当前 Flutter 商城已承接单商品购买：

- `ShopRepository.purchaseProduct` 只接收 `accessToken`、`productId`、`paymentPassword` 和 `quantity`。
- `HttpShopRepository.purchaseProduct` 请求体只提交 `productId`、`quantity`、`paymentPassword`。
- `ShopProductDetailPage._submitPurchase` 当前只校验支付口令、购买资格和商品 ID，再调用仓储购买。
- `shop_product_detail_page_test` 断言购买请求体不包含 `idempotencyKey`。

因此 Flutter 当前会命中服务端“未传 key 的兼容路径”，重复提交不具备已实现的幂等保护。

### 3. Flutter 目前没有转账写入口

Flutter 当前钱包仓储只包含余额和交易流水查询，未实现转账表单或 `TransferDto` 写入口。因此 `WOG-6` 首批不建议为了未来能力预先改 Flutter 转账；后续移动钱包进入实际开发时，再复用 `coin-transfer:{key}` 口径。

## 建议决策

### 决策 1：首批只补 Flutter 单商品购买

建议首批范围：

| 范围 | 动作 | 理由 |
| --- | --- | --- |
| Flutter 单商品购买 | 生成并提交 `shop:{key}` | 服务端已支持；Flutter 当前真实购买路径缺口明确 |
| Flutter 仓储契约 | `purchaseProduct` 增加 `idempotencyKey` 参数 | 调用方显式传入，测试能直接断言 |
| Flutter 商品详情页 | 一次购买尝试持有一个 key，成功、重新进入商品、登录态切换或用户重新确认新意图时重置 | 与 Web 弹窗口径一致 |
| Flutter 测试 | 仓储请求体和页面购买调用补断言 | 防止未来回退到无 key 兼容路径 |

首批不纳入：

- Flutter 转账，因为当前没有移动转账写入口。
- 服务端强制 `idempotencyKey` 必填，因为仍需确认旧客户端、测试工具和其他调用方接入情况。
- 购物车、批量购买、退款、取消订单、订单重试发放、完整钱包和资产风控平台。

### 决策 2：客户端 key 由 UI 意图生成，仓储只负责提交

建议 Flutter 口径：

1. 商品详情页在准备发起购买确认时生成 `shop:{random}`。
2. 同一次 `_submitPurchase` 请求重试应复用同一个 key。
3. 购买成功、切换商品、登录态变化、重新加载商品详情或用户清空 / 重新输入购买意图时生成新 key。
4. 仓储层不自行生成 key，只要求调用方传入非空 `idempotencyKey` 并按原字段提交。
5. 日志、错误提示和测试不输出支付口令；如未来记录 key，只记录是否存在或 key 前缀，不把 key 当作安全凭证。

Flutter 当前未引入 UUID 依赖。首批不建议为单个 key 生成新增依赖；可用 Dart 标准库生成符合服务端字符集的随机后缀，例如时间戳加安全随机十六进制片段。若后续已有稳定 UUID 依赖，再统一替换。

### 决策 3：服务端暂不强制 key

建议首批保持服务端兼容策略：

- `CreateOrderDto.IdempotencyKey` 继续可空。
- 已接入 key 的官方客户端享受幂等保护。
- 未接入 key 的旧客户端保持现有行为，但不承诺重复提交保护。
- 等 Web、Flutter 和后续主要资产客户端均完成接入后，再单独评审“资产写入口强制 key”。

这样可以避免 WOG-6 首批变成兼容性破坏批次，也避免把跨端客户端补漏和服务端强制策略混成同一风险面。

## 方案细节

### Flutter 仓储契约

建议调整：

- `ShopRepository.purchaseProduct` 增加必填 `String idempotencyKey`。
- `EmptyShopRepository` 和测试假仓储同步签名。
- `HttpShopRepository.purchaseProduct` 请求体增加：

```dart
'idempotencyKey': normalizedIdempotencyKey,
```

仓储层校验：

- `idempotencyKey.trim()` 不能为空。
- 不在仓储层校验支付口令以外的复杂服务端规则；长度和字符集继续由服务端兜底。

### Flutter 页面状态

建议在 `ShopProductDetailPage` 内维护一个购买 key：

- 新增 `_purchaseIdempotencyKey` 状态。
- `_submitPurchase` 进入实际购买前若不存在 key，则生成一个。
- 请求失败但未确认成功时保留该 key，允许用户直接重试同一购买意图。
- 购买成功并进入订单详情后清空 key；返回详情页后下一次购买生成新 key。
- 商品 ID 变化、仓储实例变化、会话退出或页面重新加载时清空 key。

如果支付口令错误，服务端当前不会占用 key；保留同一个 key 重试同一购买意图是可以接受的。若用户改变购买数量，本批单商品购买默认数量固定为 `1`，暂不需要数量变更重置逻辑。

### 测试入口

首批代码确认后建议补充：

1. `shop_product_detail_page_test`
   - `HttpShopRepository.purchaseProduct` 请求体包含 `idempotencyKey`。
   - 空白 `idempotencyKey` 拒绝提交。
   - 页面点击“确认购买 1 件”时假仓储收到 `shop:` 前缀 key。
   - 同一次失败后直接重试复用同一个 key；成功后下一次购买生成新 key。
2. `payment-idempotency-governance` 或 WOG-6 记录
   - 说明 Flutter 已接入后，再考虑服务端强制 key 的评审条件。

验证命令建议：

1. `flutter test Clients/radish.flutter/test/shop_product_detail_page_test.dart`
2. `flutter analyze Clients/radish.flutter`
3. `git diff --check`
4. `npm run check:repo-hygiene:changed`

如只改 Flutter 仓储和页面单元测试，不要求启动 Gateway 或做真实浏览器 smoke。若后续接入真实移动设备购买回归，再按移动端验收批次单独记录。

## 迁移边界

- 不涉及数据库结构变化。
- 不修改 `OperationIdempotencyRecord`、`CreateOrderDto` 或 `OrderService.PurchaseAsync`。
- 不改变 Web 商城购买和萝卜坑转账当前 key 生成口径。
- 不改变支付口令校验顺序：支付口令错误仍不占用幂等 key。
- 不改变服务端对未传 key 的兼容行为。

## 不做范围

- 不启动完整 Flutter 商城、购物车、退款、订单取消、权益使用或完整移动钱包。
- 不实现 Flutter 转账；等移动钱包转账进入实际产品开发时，再复用 `coin-transfer:{key}` 口径。
- 不把 `idempotencyKey` 当作安全签名、设备绑定、风控凭证或防攻击机制。
- 不新增 Redis 锁、Outbox、分布式事务、完整资产风控平台或开放 API 签名平台。
- 不强制所有客户端立即传 key，不在本批破坏旧客户端兼容。
- 不把前端按钮禁用写成可靠性方案；按钮禁用只作为体验层辅助，服务端幂等仍是最终保护。

## 待确认决策

1. WOG-6 首批是否只补 Flutter 单商品购买的 `idempotencyKey`，不纳入 Flutter 转账或服务端强制 key。
2. Flutter 是否由商品详情页生成并持有 `shop:{random}`，仓储层只负责校验非空和提交。
3. 购买失败后是否保留同一个 key 供用户直接重试，购买成功或购买意图重置后再生成新 key。
4. 首批是否继续保持服务端 `CreateOrderDto.IdempotencyKey` 可空，等主要客户端接入后再评审强制策略。
