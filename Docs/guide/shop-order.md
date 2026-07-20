# 4. 商城订单系统

> 版本：v2.1 | 最后更新：2026-07-15
>
> 入口页：[商城系统设计方案](/guide/shop-system)

订单是购买、支付和履约意图的不可变快照。商品后续改名、改价、下架或软删除，不得改变历史已支付订单的发放内容。

## 4.1 一步购买流程

用户侧购买入口：

```http
GET  /api/v1/Shop/CheckCanBuy/{productId}?quantity=1
POST /api/v1/Shop/Purchase
```

`Purchase` 请求至少包含：

```json
{
  "productId": "2060964941900283904",
  "quantity": 1,
  "paymentPassword": "******",
  "idempotencyKey": "shop:550e8400-e29b-41d4-a716-446655440000"
}
```

服务端顺序为：

1. 校验商品可售能力、库存、限购、等级和余额。
2. 校验 6 位数字支付口令。
3. 绑定购买幂等键与请求摘要。
4. 扣减限量库存并创建订单快照。
5. 扣减胡萝卜并记录 `CoinTransactionId`。
6. 只根据订单快照发放持续权益或消耗品。
7. 写明履约资源 ID、完成状态和可靠通知任务。

`CheckCanBuy` 只提供资格预检，不创建订单或改变资产；最终写入仍由 `Purchase` 重新校验。

## 4.2 订单快照

`Order` 需要独立保存下列购买时事实：

| 分组 | 字段 |
|------|------|
| 商品 | `ProductId`、`ProductName`、`ProductIconAttachmentId`、`ProductType` |
| 效果 | `BenefitType`、`ConsumableType`、`BenefitValue` |
| 有效期 | `DurationType`、`DurationDays`、`FixedExpiresAt` |
| 库存与金额 | `StockType`、`Quantity`、`UnitPrice`、`TotalPrice` |
| 支付 | `PaidTime`、`CoinTransactionId` |
| 结果 | `GrantedBenefitId`、`GrantedInventoryId`、`BenefitExpiresAt` |

`UserBenefitId` 是兼容旧数据的历史字段，新代码不再写入。持续权益和消耗品必须分别写入 `GrantedBenefitId` 与 `GrantedInventoryId`。

## 4.3 状态机

```text
Pending ──支付成功──> Paid ──履约成功──> Completed
   │                    │
   ├──取消────────────> Cancelled
   ├──支付失败────────> Failed(Payment)
   └────────────────────└──履约失败────> Failed(Fulfillment)

Failed(Fulfillment) ──支付证据校验 + 幂等发放──> Completed
```

`Completed` 表示支付和发放完成，不表示持续权益当前一定处于启用状态。权益可能随后被停用、到期或撤销。

## 4.4 购买幂等

正式 Web 和 Flutter 为每次用户购买意图生成 `shop:{uuid}`：

- 同一用户、同一键、同一请求摘要返回首次终态结果。
- 同一键绑定不同商品、数量或备注时拒绝执行。
- 支付口令校验失败等尚未进入资产写入的失败不占用幂等键。
- 已开始资产写入的终态失败会被记录，避免重试造成重复扣款或发放。

购买请求幂等和履约资源防重是两层保护：

- `OperationIdempotencyRecord` 防止同一购买动作重复执行。
- `UserBenefit.SourceOrderId` 防止一张订单产生多份持续权益。
- `UserInventoryGrantRecord.SourceOrderId` 防止一张订单重复增加消耗品数量。

## 4.5 支付失败与履约失败

支付失败必须记录为：

```text
Status = Failed
FailureStage = Payment
```

履约失败必须记录为：

```text
Status = Failed
FailureStage = Fulfillment
PaidTime != null
CoinTransactionId != null
```

两者不能互换。支付失败订单没有发放资格；履约失败订单也不能仅凭字段组合直接发放，管理员重试前还必须验证真实扣款流水。

## 4.6 管理员重试发放

`POST /api/v1/Shop/RetryGrantBenefit/{orderId}` 需要 `console.orders.retry` 权限。服务端只接受同时满足以下条件的订单：

1. `Status=Failed`。
2. `FailureStage=Fulfillment`。
3. `PaidTime` 和 `CoinTransactionId` 均存在。
4. 扣款流水与订单属于同一租户、同一用户。
5. 流水为成功消费，`BusinessType=Order`、`BusinessId=OrderId`。
6. 流水金额等于订单 `TotalPrice`。

重试继续使用原订单快照，不重新读取当前 `Product`。商品被编辑、下架或软删除都不能改变历史履约内容。

发放服务通过来源订单唯一键返回既有结果，因此管理员重复点击不会创建第二份权益或重复增加背包数量。

## 4.7 固定日期和天数有效期

- `Permanent`：履约结果没有到期时间。
- `Days`：从履约 UTC 时间加订单快照中的天数。
- `FixedDate`：直接使用订单快照中的固定到期时间。
- 固定到期时间已经过去时拒绝自动发放，进入人工处理，不擅自延长。

历史订单禁止从当前商品重新读取 `DurationDays` 或固定到期时间。

## 4.8 用户与 Console 展示

用户侧：

- `/shop/orders` 查看订单列表。
- `/shop/order/:orderId` 查看订单详情和商品快照。
- 失败状态必须区分“支付失败”和“履约失败”。
- 所有订单、商品和交易 ID 按字符串 LongId 处理。

Console 订单详情展示：

- 支付时间、扣款流水 ID、失败阶段和失败原因。
- `GrantedBenefitId` 或 `GrantedInventoryId`。
- 只有 `VoCanRetryFulfillment=true` 时显示重试资格；服务端仍会再次校验支付证据。
- 订单到用户、商品和胡萝卜流水的治理回跳。

## 4.9 取消、通知与备注

- 用户只能取消自己的 `Pending` 订单。
- 系统超时任务也只处理 `Pending` 订单。
- 购买成功通知通过可靠 Outbox 请求，不因实时推送失败回退订单完成状态。
- 管理员备注不改变支付、履约或资产事实。
- F1 不提供用户退款入口；人工补偿不能通过直接改余额或软删订单掩盖问题。

### 4.9.1 管理端错误契约

订单治理接口使用结构化失败响应，至少保留 HTTP status、`Code`、`MessageKey` 与安全 `MessageInfo`：

| 场景 | HTTP | Code | MessageKey |
| --- | ---: | --- | --- |
| 订单不存在 | `404` | `Order.NotFound` | `error.order.not_found` |
| 支付证据或状态不允许重试履约 | `409` | `Order.RetryRejected` | `error.order.retry_rejected` |
| 管理员备注保存失败或发生状态冲突 | `409` | `Order.RemarkFailed` | `error.order.remark_failed` |

Console 必须通过 status、`Code` 和 `voCanRetryFulfillment` 决定 not-found、冲突与动作资格；`MessageKey` 用于宿主本地化，服务端按 `Accept-Language` 返回的 `MessageInfo` 只作安全回退。管理员备注、订单失败原因和商品快照名称属于人工或历史内容，保持原文。

## 4.10 数据库迁移

`20260713_001_shop_order_fulfillment_safety` 通过 schema ledger 完成：

- 增加 `FailureStage`、`FixedExpiresAt`、`GrantedBenefitId`、`GrantedInventoryId`。
- 按历史支付和履约信息回填失败阶段及资源类型。
- 保留无法可靠判断的数据供 doctor 报告，不猜测修复高风险异常。
- verify 检查字段、历史映射和履约资源一致性。

禁止使用运行时 Code First 静默补列或绕过 ledger 修改正式数据库。

## 4.11 排障顺序

遇到订单异常时按以下顺序确认：

1. 查看订单 `Status / FailureStage`。
2. 对照 `PaidTime / CoinTransactionId / TotalPrice`。
3. 核对胡萝卜流水的用户、租户、业务类型、业务 ID 和金额。
4. 检查 `GrantedBenefitId / GrantedInventoryId`。
5. 检查来源订单唯一记录，确认是否已经发放。
6. 只有满足重试条件时使用受权重试入口。

不要直接编辑订单状态、背包数量或权益记录来“修好”页面。

---

> 下一篇：[5. 权益与背包](/guide/shop-inventory)
