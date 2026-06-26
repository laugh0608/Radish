# 支付与转账幂等治理

> 状态：`首批代码已实现 / 待发布候选回归`
>
> 记录日期：`2026-06-19`（Asia/Shanghai）
>
> 适用范围：商城购买、萝卜币转账，以及后续明确进入资产写入链路的同类操作。

## 背景

前端敏感日志脱敏和支付口令哈希升级已经完成，支付口令负责确认高风险操作意图，但它不解决重复点击、网络重试、客户端超时后重发或短窗口重放带来的资产一致性问题。

商城购买和萝卜币转账都属于资产写操作：

- 商城购买会验证支付口令、扣减库存、创建订单、扣除萝卜币并发放权益或物品。
- 萝卜币转账会验证支付口令、扣减转出方余额、增加转入方余额并写入交易流水。

本专题只治理这些写操作的幂等和重放边界，不改变支付口令、权限、余额、库存、事务和审计的职责。

## 目标

1. 同一个用户对同一写操作使用同一个 `idempotencyKey` 重试时，服务端能识别这是同一请求。
2. 同一个 `idempotencyKey` 搭配不同请求参数时，服务端拒绝执行，避免客户端误复用 key。
3. 已成功的请求被重复提交时，返回同一订单号或交易流水号，不重复扣款、扣库存、发放权益或转账。
4. 未进入资产写入的失败请求，例如未登录、参数格式错误、支付口令错误，不占用幂等 key。
5. 幂等记录不保存支付口令，不把 `idempotencyKey` 当作安全密钥或签名凭证。

## 首批范围

| 业务 | 入口 | 首批结果 |
| --- | --- | --- |
| 商城购买 | `POST /api/v1/Shop/Purchase`、`OrderService.PurchaseAsync` | 成功重复提交返回同一订单号和购买结果 |
| 萝卜币转账 | `POST /api/v1/Coin/Transfer`、`CoinService.TransferAsync` | 成功重复提交返回同一交易流水号 |

首批不覆盖订单取消、权益激活、管理员调账、注册奖励、经验奖励、宠物照顾、轻回应或论坛互动。这些操作已有各自的业务去重或暂不属于本批资产链路。

## 首批实现状态

截至 `2026-06-19`，首批代码已落地：

- 新增 `OperationIdempotencyRecord` 实体、通用幂等记录服务和 `Radish.DbMigrate` 结构入口；2026-06-22 后按上线前数据库口径不再维护历史发布脚本。
- `CreateOrderDto` 与 `TransferDto` 已增加 `IdempotencyKey`，未传 key 时仍保留现有行为。
- `OrderService.PurchaseAsync` 与 `CoinService.TransferAsync` 已在支付口令验证通过后接入请求摘要、幂等记录创建 / 读取、冲突拒绝、处理中提示和终态响应重放。
- Web 官方商城购买与萝卜坑转账流程已生成并传入 `shop:{uuid}` / `coin-transfer:{uuid}`。

实现口径：

- `Succeeded` 表示该 key 已记录终态响应，不等同于业务一定成功；如果请求已经进入订单、库存、扣款、交易流水或转账内部处理等资产写入边界，即使最终业务响应为失败，也会记录终态响应，避免同 key 重放导致重复写资产。
- `Failed` 只用于未形成终态资产响应的失败；同 key、同摘要可重新进入处理，不同摘要仍拒绝，避免客户端误复用 key。

## 接口契约

### 商城购买

`CreateOrderDto` 增加：

```csharp
public string? IdempotencyKey { get; set; }
```

请求摘要字段：

- `ProductId`
- `Quantity`
- `UserRemark`（空白按空字符串处理）

不纳入摘要：

- `PaymentPassword`
- 登录 token
- 客户端本地状态

### 萝卜币转账

`TransferDto` 增加：

```csharp
public string? IdempotencyKey { get; set; }
```

请求摘要字段：

- `ToUserId`
- `Amount`
- `Remark`（空白按空字符串处理）

不纳入摘要：

- `PaymentPassword`
- 登录 token
- 客户端本地状态

### 兼容策略

Web 与 Flutter 官方购买流程必须生成并传入 `idempotencyKey`。服务端暂不强制旧客户端必须传入 key；未传入时保留现有行为，不提供幂等保护。

后续其他客户端承接同一工作流时，应复用同一接口字段和请求摘要口径。等主要客户端都完成接入后，再评审是否将 `idempotencyKey` 调整为强制字段。

## Key 生成口径

客户端生成 key，只用于标识一次用户确认后的提交尝试，不具备鉴权或签名意义。

建议格式：

```text
shop:{uuid}
coin-transfer:{uuid}
```

其中 `uuid` 使用客户端可用的随机 UUID。服务端只校验长度、字符集和业务上下文绑定，不信任 key 中携带的业务含义。

首批限制：

- 最大长度：`80`
- 允许字符：英文字母、数字、`-`、`_`、`:`
- 空白字符串视为未传入

## 服务端记录

新增通用幂等记录实体命名为 `OperationIdempotencyRecord`。

建议字段：

| 字段 | 说明 |
| --- | --- |
| `Id` | 雪花 ID |
| `TenantId` | 租户 ID |
| `UserId` | 发起用户 ID |
| `OperationType` | `ShopPurchase`、`CoinTransfer` |
| `IdempotencyKey` | 客户端提交的 key |
| `RequestHash` | 规范化请求摘要的 SHA-256 |
| `RequestSummary` | 不含敏感信息的摘要 JSON |
| `Status` | `Processing`、`Succeeded`、`Failed` |
| `ResourceType` | `Order`、`CoinTransaction` |
| `ResourceId` | 成功后资源 ID |
| `ResourceNo` | 成功后订单号或交易流水号 |
| `ResponsePayload` | 可安全返回的结果 JSON |
| `ErrorCode` | 可选错误码 |
| `ErrorMessage` | 可选错误信息，不写支付口令 |
| `ExpiresAt` | 过期时间 |
| `CreateTime` | 创建时间 |
| `CompleteTime` | 完成时间 |

唯一约束：

```text
TenantId + UserId + OperationType + IdempotencyKey
```

索引：

- `UserId + OperationType + CreateTime`
- `ExpiresAt`

保留时间：

- 首批默认 `24` 小时。
- 过期清理可先保留为后续维护任务；首批不要求立即新增后台 job。

## 执行顺序

服务端入口按以下顺序处理：

1. 校验登录态和基础 DTO。
2. 校验支付口令格式。
3. 验证支付口令。
4. 若未传入 `idempotencyKey`，沿用现有写入流程。
5. 规范化请求摘要并计算 `RequestHash`。
6. 尝试创建或读取幂等记录。
7. 如果同一 key 已存在且 `RequestHash` 不同，拒绝执行。
8. 如果同一 key 已成功，返回已记录的订单号或交易流水号。
9. 如果同一 key 正在处理中，返回“请求处理中，请稍后查询结果或重试”。
10. 如果是新 key，进入资产写入流程。
11. 资产写入形成终态响应后记录资源 ID、资源编号和可安全返回的结果。

支付口令验证放在幂等记录之前，原因是支付口令错误不应占用 key，也不应让攻击者通过幂等记录枚举操作状态。

## 重放处理

| 场景 | 处理 |
| --- | --- |
| 同一 key、同一摘要、首次形成终态响应 | 正常执行并记录终态响应 |
| 同一 key、同一摘要、终态后重复提交 | 返回同一终态响应 |
| 同一 key、不同摘要 | 拒绝执行，提示 key 已被不同请求使用 |
| 同一 key、同一摘要、处理中 | 返回处理中提示，不重复进入资产写入 |
| 未传 key | 保持当前行为，不承诺幂等 |
| 支付口令错误 | 返回支付口令错误，不记录幂等结果 |
| 前置检查失败且未进入资产流程 | 返回业务失败；同 key、同摘要可重试 |
| 已进入资产写入后业务失败 | 返回同一终态失败响应，不重复扣款、扣库存、发放权益或转账 |

## 前端接入

### 商城

购买弹窗在用户确认购买时生成 `shop:{uuid}`，同一次请求重试使用同一个 key；购买成功、关闭弹窗或重新打开弹窗后生成新 key。

前端仍应禁用提交中的按钮，避免把重复点击放大成并发请求。按钮禁用只是体验层保护，服务端幂等是最终保护。

### 萝卜坑转账

转账流程进入确认页时生成 `coin-transfer:{uuid}`，确认提交时随 `TransferDto` 发送。用户返回表单修改收款人、金额或备注后必须生成新 key。

前端日志不得输出支付口令；如需记录幂等 key，只能记录业务类型和 key 是否存在，避免把 key 当作审计凭证。

## 验证入口

后端测试：

- `OrderServiceTest`：成功购买后同 key 重放返回同一订单号，不重复扣库存、扣币或发放权益。
- `OrderServiceTest`：同 key 但商品、数量或备注变化时拒绝执行。
- `CoinServiceTest`：成功转账后同 key 重放返回同一交易流水号，不重复扣转出方余额或增加转入方余额。
- `CoinServiceTest`：同 key 但收款人、金额或备注变化时拒绝执行。
- 幂等记录服务测试：key 规范化、请求摘要 hash、处理中 / 成功 / 失败状态读取。

前端验证：

- `radish.client` type-check。
- 商城购买请求类型包含 `idempotencyKey`。
- 萝卜坑转账请求类型包含 `idempotencyKey`。

数据库验证：

- 本地 `Radish.DbMigrate` 能创建幂等记录表。
- 正式数据库阶段的发布 SQL 覆盖新表、唯一约束和必要索引。

## 不做范围

- 不做浏览器通用 `sign`。
- 不做字段级 RSA / AES 加密。
- 不把 `idempotencyKey` 当作安全密钥。
- 不把支付口令 hash 后作为协议值发送。
- 不启动安全会话、设备绑定、WebAuthn / OTP 或资产风控平台。
- 不扩展完整钱包、经济系统、商城活动、购物车、优惠券或转账手续费。
- 不把 Redis / 分布式锁作为首批前置依赖；首批优先依赖数据库唯一约束和事务。

## 后续评审项

- 是否把 `idempotencyKey` 调整为资产写操作强制字段。
- 是否新增过期清理 job 和 Console 幂等记录查询入口。
- 是否把管理员调账、奖励发放、订单取消等其他写操作纳入同一治理。
- 多实例高并发后，是否引入 Redis 分布式锁或数据库 advisory lock。
- 与未来开放 API / 服务端到服务端签名的关系：签名负责来源可信和请求完整性，幂等负责重复提交治理，两者不能互相替代。
