# 萝卜坑应用核心概念

> 版本：v1.1 | 最后更新：2026-07-15 | 状态：已实现，持续维护

本文定义萝卜资产域当前实际使用的数据语义和跨层稳定契约。具体阈值与业务实现以 Service 和测试为准，前端不得从展示文案反推规则。

## 1. 单位与精度

- **胡萝卜**：存储、计算和 API 业务语义中的最小整数单位。
- **白萝卜**：展示单位，`1 白萝卜 = 1000 胡萝卜`。
- **CoinAmount**：后端为 `long`，client 类型为十进制整数字符串。
- **LongId**：用户、流水实体和业务对象的 `long` 标识在 client 同样按字符串处理。

client 不得先将完整 `long` 转为 `number` 再比较、累加或格式化。胡萝卜 / 白萝卜换算使用整数商和三位余数；图表库需要 `number` 时，只能在图表适配边界显式投影。

## 2. 账户与流水

### 2.1 账户字段

```ts
type CoinAmount = string;
type LongId = string;

interface UserBalance {
  voUserId: LongId;
  voBalance: CoinAmount;
  voFrozenBalance: CoinAmount;
  voTotalEarned: CoinAmount;
  voTotalSpent: CoinAmount;
  voTotalTransferredIn: CoinAmount;
  voTotalTransferredOut: CoinAmount;
  voCreateTime: string;
  voModifyTime?: string | null;
}
```

`voBalanceDisplay / voFrozenBalanceDisplay` 是历史兼容展示字段。当前 client 使用数值字段和 locale formatter，不依赖服务端固定格式。

### 2.2 流水字段

```ts
interface CoinTransaction {
  voId: LongId;
  voTransactionNo: string;
  voFromUserId: LongId | null;
  voFromUserName: string | null;
  voToUserId: LongId | null;
  voToUserName: string | null;
  voAmount: CoinAmount;
  voFee: CoinAmount;
  voTransactionType: string;
  voStatus: string;
  voBusinessType: string | null;
  voBusinessId: LongId | null;
  voRemark: string | null;
  voCreateTime: string;
}
```

- `voTransactionNo` 是完整流水号字符串，不得以数值 ID 替代。
- `voFromUserId / voToUserId` 用于判断当前用户的收支方向；空参与方表示系统。
- 用户名和备注属于人工内容，原样展示。
- `voTransactionTypeDisplay / voStatusDisplay / voAmountDisplay / voFeeDisplay` 仅保留旧消费者兼容。

## 3. 稳定系统词元

### 3.1 交易类型

当前已知类型包括：

```text
SYSTEM_GRANT
LIKE_REWARD
COMMENT_REWARD
HIGHLIGHT_REWARD
GODCOMMENT_REWARD
GODLIKE_REWARD
SOFA_REWARD
TRANSFER
TRANSFER_IN
TRANSFER_OUT
TIP
CONSUME
PURCHASE
REFUND
PENALTY
ADMIN_ADJUST
```

client 只按 `voTransactionType` 解析宿主词元。服务端新增未知类型时，client 显示稳定原值，不猜测或映射为“其他类型”。

### 3.2 交易状态

```text
PENDING
SUCCESS
FAILED
CANCELLED
```

控制流、颜色和筛选都使用 `voStatus`；未知状态保留原值并使用中性样式。

### 3.3 统计分类

统计接口按方向返回稳定分类：

```text
IN_SYSTEM_GRANT  IN_LIKE_REWARD  IN_COMMENT_REWARD
IN_GOD_COMMENT_REWARD  IN_SOFA_REWARD  IN_TRANSFER
IN_REFUND  IN_OTHER
OUT_TRANSFER  OUT_CONSUME  OUT_PENALTY  OUT_OTHER
```

`voCategory` 是系统词元，不是运营分类名称。client 按 `pit.statistics.category.*` 解析，未知分类直接显示原值。

### 3.4 安全日志

- `voType`：`password_verify / password_change / password_set / account_unlock / payment_password` 等稳定类型。
- `voResult`：`success / failed`。
- `voAction`：历史中文动作说明，仅保留兼容。
- IP、User-Agent 和时间是审计内容，分别按原文和当前 locale 展示。

## 4. 业务规则边界

### 4.1 用户间转移

服务端权威规则包括：

- 发起方和接收方不能相同；
- 金额必须是正整数；
- 发起方可用余额必须充足，双方账户必须可用；
- 支付口令必须已配置、格式有效、验证成功且未锁定；
- 写入使用事务与既有并发重试；
- 可选幂等键按同键同请求摘要重放，同键不同摘要返回冲突。

前端校验只用于即时反馈，不能替代服务端规则。本文不新增单笔上限、每日上限、冷却或手续费规则。

### 4.2 余额关系

```text
总余额 = 可用余额 + 冻结余额
累计净收入 = 累计获得 - 累计消费
累计净转移 = 累计转入 - 累计转出
```

这些关系用于展示计算，不改变 Service 的记账语义。

### 4.3 支付口令

- 当前口令是六位数字，六位完全相同不被接受；
- 当前哈希版本为 Argon2id；可识别的 SHA-256 v1 在成功验证后自动升级，缺失或超出支持范围的版本必须重置；
- 连续失败五次进入三十分钟锁定；
- 修改口令前验证当前口令；
- 口令明文不得进入日志或持久化响应。

## 5. 错误契约

### 5.1 萝卜流水与转移

| 场景 | HTTP | Code | MessageKey |
| --- | ---: | --- | --- |
| 流水不存在 | 404 | `Coin.TransactionNotFound` | `error.coin.transaction_not_found` |
| 自转移 | 400 | `Coin.TransferSelfRejected` | `error.coin.transfer_self_rejected` |
| 金额无效 | 400 | `Coin.TransferAmountInvalid` | `error.coin.transfer_amount_invalid` |
| 余额不足 | 409 | `Coin.TransferInsufficientBalance` | `error.coin.transfer_insufficient_balance` |
| 账户不可用 | 409 | `Coin.TransferAccountUnavailable` | `error.coin.transfer_account_unavailable` |
| 并发冲突 | 409 | `Coin.TransferConcurrencyConflict` | `error.coin.transfer_concurrency_conflict` |
| 同请求处理中 | 409 | `Coin.TransferProcessing` | `error.coin.transfer_processing` |
| 幂等键冲突 / 无效 | 409 / 400 | `Coin.TransferIdempotencyConflict / Invalid` | 对应 `error.coin.*` |
| 终态重放缺失 | 409 | `Coin.TransferReplayUnavailable` | `error.coin.transfer_replay_unavailable` |

### 5.2 支付口令

| 场景 | HTTP | Code | MessageKey |
| --- | ---: | --- | --- |
| 缺少、格式错误、重复数字、确认不一致 | 400 | `PaymentPassword.*` | 对应 `error.payment_password.*` |
| 已配置 / 未配置 | 409 | `PaymentPassword.AlreadyConfigured / NotConfigured` | 对应 `error.payment_password.*` |
| 验证失败 | 400 | `PaymentPassword.Invalid` | `error.payment_password.invalid` |
| 暂时锁定 | 429 | `PaymentPassword.Locked` | `error.payment_password.locked` |
| 旧口令升级 | 409 | `PAYMENT_PASSCODE_UPGRADE_REQUIRED` | `error.payment_password.upgrade_required` |

`MessageInfo` 是按 `Accept-Language` 生成的安全兜底；client 控制流只使用 HTTP status 和 `Code`，本地提示优先使用 `MessageKey`。

## 相关文档

- [萝卜坑应用总体设计](/guide/radish-pit-system)
- [萝卜坑功能模块](/guide/radish-pit-game-mechanics)
- [萝卜币系统](/guide/radish-coin-system)
