# 6-8. 精度、对账与数据库设计

> 入口页：[萝卜币系统设计方案](/guide/radish-coin-system)

## 6. 精度处理与计算规范

### 6.1 核心设计原则

**五大关键原则** 🎯：

1. **强制整数运算**
   - 所有金额字段使用 `long` 类型（64位整数）
   - **禁止**使用 `decimal`、`float`、`double` 存储余额
   - 白萝卜仅用于前端展示，后端统一使用胡萝卜

2. **统一舍入规则**
   - 所有比例计算**向下取整（Floor）**
   - 手续费不足最小值（1 胡萝卜）时免收
   - 文档化并在代码中集中管理（`CoinCalculator` 工具类）

3. **透明化差额**
   - `CoinTransaction` 表新增字段：
     - `theoretical_amount` (DECIMAL(18,6))：理论金额（精确计算结果）
     - `rounding_diff` (DECIMAL(18,6))：舍入差额（理论 - 实际）
   - 记录每次舍入产生的差额，支持审计

4. **业务规则保底**
   - 设置最小金额阈值（1 胡萝卜）
   - 避免 1 胡萝卜以下的交易
   - 手续费不足最小值时直接免收

5. **定期对账**
   - 每日/每周汇总舍入差额
   - 确保账务平衡：`Σ理论金额 - Σ实际金额 = Σ舍入差额`
   - 差额超过阈值时触发告警

### 6.2 CoinCalculator 工具类

**位置**：`Radish.Common/Utils/CoinCalculator.cs`

**功能概览**：
```csharp
// 1. 单位转换（展示用）
decimal whiteRadish = CoinCalculator.ToWhiteRadish(12345);  // 123.45 白萝卜
long carrot = CoinCalculator.ToCarrot(123.45m);             // 12345 胡萝卜

// 2. 比例计算（向下取整）
var result = CoinCalculator.CalculateByRate(100, 0.055m);  // 5.5% 手续费
// result.TheoreticalAmount = 5.5
// result.ActualAmount = 5
// result.RoundingDiff = 0.5

// 3. 手续费计算（不足最小值免收）
var feeResult = CoinCalculator.CalculateFee(50, 0.01m);    // 1% 手续费
// feeResult.ActualAmount = 0（理论 0.5，不足 1 免收）

// 4. 批量均分（余额分配）
var shares = CoinCalculator.DistributeEqually(100, 3);
// shares = [34, 33, 33]（总和 100，精确分配）

// 5. 按权重分配
var weights = new List<int> { 3, 2, 1 };
var allocations = CoinCalculator.DistributeByWeight(100, weights);
// weights 3:2:1 → 分配 [50, 33, 17]（总和 100）

// 6. 累积计算器（利息、分红等）
var calculator = new CoinCalculator.AccumulativeCalculator();
var settled1 = calculator.Add(0.3m);  // 返回 0（未满 1）
var settled2 = calculator.Add(0.8m);  // 返回 1（累积 1.1，结算 1，剩余 0.1）
```

### 6.3 使用场景示例

**场景 1：手续费扣除（避免小数）**
```csharp
// ❌ 错误：直接计算可能产生小数
long transferAmount = 100;
long fee = (long)(transferAmount * 0.055m);  // 可能是 5 或 6，不确定

// ✅ 正确：使用 CoinCalculator
var feeResult = CoinCalculator.CalculateFee(transferAmount, 0.055m);
long actualFee = feeResult.ActualAmount;  // 5 胡萝卜（向下取整）

// 创建交易记录（含差额追踪）
var transaction = new CoinTransaction
{
    Amount = transferAmount - actualFee,
    Fee = actualFee,
    TheoreticalAmount = feeResult.TheoreticalAmount,  // 5.5
    RoundingDiff = feeResult.RoundingDiff              // 0.5
};
```

**场景 2：按比例分配奖励（神评奖励池）**
```csharp
// 场景：100 胡萝卜奖励池，按点赞数分配给 3 个神评作者
long totalReward = 100;
var likeWeights = new List<int> { 50, 30, 20 };  // 点赞数

var allocations = CoinCalculator.DistributeByWeight(totalReward, likeWeights);

// 结果：[50, 30, 20]（精确分配，无差额）
foreach (var allocation in allocations)
{
    await _coinService.GrantCoinAsync(
        userId: userIds[allocation.Index],
        amount: allocation.ActualAmount,
        transactionType: "HIGHLIGHT_REWARD"
    );
}
```

**场景 3：每日利息累积（避免频繁小额发放）**
```csharp
// 场景：存款利息每日 0.05%，累积到满 1 胡萝卜再发放
var interestCalculator = new CoinCalculator.AccumulativeCalculator();

for (int day = 1; day <= 365; day++)
{
    long principal = 1000;  // 1000 胡萝卜本金
    decimal dailyInterest = principal * 0.0005m;  // 0.5 胡萝卜/天

    long toSettle = interestCalculator.Add(dailyInterest);

    if (toSettle > 0)
    {
        // 累积满 1 胡萝卜时发放
        await _coinService.GrantCoinAsync(userId, toSettle, "INTEREST");
    }
}
// 累积策略：第1天 0.5 未发，第2天 1.0 发放 1，剩余 0，第3天 0.5 未发...
```

### 6.4 数据库字段设计

**user_balance 表**：
```sql
CREATE TABLE user_balance (
    user_id BIGINT PRIMARY KEY,
    balance BIGINT NOT NULL DEFAULT 0,           -- 可用余额（胡萝卜）
    frozen_balance BIGINT NOT NULL DEFAULT 0,    -- 冻结余额
    version INT NOT NULL DEFAULT 0,              -- 乐观锁版本号
    updated_at TIMESTAMP NOT NULL
);
```

**coin_transaction 表**（新增差额追踪字段）：
```sql
CREATE TABLE coin_transaction (
    id BIGINT PRIMARY KEY,
    transaction_no VARCHAR(64) UNIQUE NOT NULL,
    amount BIGINT NOT NULL,                      -- 实际交易金额（胡萝卜）
    fee BIGINT NOT NULL DEFAULT 0,               -- 实际手续费（胡萝卜）
    theoretical_amount DECIMAL(18,6),            -- 理论金额（精确计算）
    rounding_diff DECIMAL(18,6),                 -- 舍入差额
    -- 其他字段...
    created_at TIMESTAMP NOT NULL,
    INDEX idx_created_at (created_at)
);
```

### 6.5 对账与差额汇总

**差额汇总视图**：
```sql
CREATE VIEW v_rounding_summary AS
SELECT
    DATE(created_at) as date,
    transaction_type,
    SUM(rounding_diff) as total_rounding_diff,
    COUNT(*) as transaction_count,
    SUM(amount) as total_actual_amount,
    SUM(theoretical_amount) as total_theoretical_amount
FROM coin_transaction
WHERE rounding_diff > 0
GROUP BY DATE(created_at), transaction_type;
```

**对账任务示例**：
```csharp
public class DailyRoundingReportJob
{
    [AutomaticRetry(Attempts = 3)]
    public async Task GenerateDailyRoundingReportAsync()
    {
        var yesterday = DateTime.Today.AddDays(-1);

        // 汇总昨日舍入差额
        var roundingSummary = await _db.Queryable<CoinTransaction>()
            .Where(t => t.CreateTime.Date == yesterday && t.RoundingDiff.HasValue)
            .GroupBy(t => t.TransactionType)
            .Select(g => new {
                TransactionType = g.Key,
                TotalRoundingDiff = g.Sum(t => t.RoundingDiff.Value),
                TransactionCount = g.Count()
            })
            .ToListAsync();

        // 记录日报
        foreach (var summary in roundingSummary)
        {
            Log.Information("舍入差额日报：{Type} - 差额 {Diff}，交易数 {Count}",
                summary.TransactionType, summary.TotalRoundingDiff, summary.TransactionCount);
        }

        // 差额超过阈值时告警（如每日差额超过 100 胡萝卜）
        var totalDiff = roundingSummary.Sum(s => s.TotalRoundingDiff);
        if (totalDiff > 100)
        {
            await _alertService.SendAlertAsync(
                $"萝卜币舍入差额异常：昨日总差额 {totalDiff:F2}（超过阈值 100）"
            );
        }
    }
}
```

### 6.6 计算规则最佳实践

**DO✅**：
```csharp
// 1. 使用 CoinCalculator 处理所有比例计算
var feeResult = CoinCalculator.CalculateFee(amount, feeRate);

// 2. 记录差额到数据库
transaction.TheoreticalAmount = feeResult.TheoreticalAmount;
transaction.RoundingDiff = feeResult.RoundingDiff;

// 3. 所有金额使用 long 类型
long balance = 12345;  // 12345 胡萝卜

// 4. 展示时根据金额大小智能切换
string display = CoinCalculator.FormatDisplay(balance);
// balance < 1000: "999 胡萝卜"
// balance >= 1000: "123 白萝卜 45 胡萝卜"
```

**DON'T❌**：
```csharp
// 1. 直接使用浮点运算
decimal balance = 123.45m;  // ❌ 余额不应使用 decimal

// 2. 手动向上取整
long fee = (long)Math.Ceiling(amount * 0.05m);  // ❌ 应使用 Floor

// 3. 忽略差额记录
transaction.Amount = calculatedAmount;  // ❌ 缺少 TheoreticalAmount 和 RoundingDiff

// 4. 在内存中累加小数
decimal totalInterest = 0.3m + 0.5m + 0.8m;  // ❌ 应使用 AccumulativeCalculator

// 5. 硬编码显示规则
string display = balance < 1000
    ? $"{balance} 胡萝卜"
    : $"{balance/100} 白萝卜 {balance%100} 胡萝卜";  // ❌ 应使用 FormatDisplay
```

---

## 7. 对账机制

### 7.1 对账原则

**资金守恒定律**：
```
系统总发行量 = 所有用户余额总和 + 平台账户余额 + 已消耗金额
```

**每日对账任务**：
1. 统计所有用户余额总和
2. 统计平台账户余额（手续费收入）
3. 统计当日发行量（注册奖励、活动奖励）
4. 统计当日消耗量（系统回收、惩罚扣除）
5. 验证公式：`昨日总量 + 今日发行 - 今日消耗 = 今日总量`

### 7.2 对账表设计

```sql
CREATE TABLE daily_balance_report (
    report_date DATE PRIMARY KEY,
    total_user_balance BIGINT NOT NULL,      -- 用户余额总和
    platform_balance BIGINT NOT NULL,        -- 平台账户余额
    daily_issued BIGINT NOT NULL,            -- 当日发行量
    daily_consumed BIGINT NOT NULL,          -- 当日消耗量
    expected_total BIGINT NOT NULL,          -- 预期总量
    actual_total BIGINT NOT NULL,            -- 实际总量
    difference BIGINT NOT NULL,              -- 差异（应为0）
    status VARCHAR(20) NOT NULL,             -- BALANCED / UNBALANCED
    created_at TIMESTAMP NOT NULL
);
```

**异常处理**：
- 差异不为 0 时，触发告警通知管理员
- 记录详细日志，便于排查（可能是并发问题、代码bug等）
- 冻结相关交易，直到问题解决

---

## 8. 数据库设计

### 8.1 用户余额表

```sql
CREATE TABLE user_balance (
    user_id BIGINT PRIMARY KEY,
    balance BIGINT NOT NULL DEFAULT 0,           -- 可用余额（胡萝卜）
    frozen_balance BIGINT NOT NULL DEFAULT 0,    -- 冻结余额
    total_earned BIGINT NOT NULL DEFAULT 0,      -- 累计获得
    total_spent BIGINT NOT NULL DEFAULT 0,       -- 累计消费
    total_transferred_in BIGINT NOT NULL DEFAULT 0,   -- 累计转入
    total_transferred_out BIGINT NOT NULL DEFAULT 0,  -- 累计转出
    version INT NOT NULL DEFAULT 0,              -- 乐观锁版本号
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    INDEX idx_balance (balance),
    INDEX idx_updated_at (updated_at)
);
```

### 8.2 交易记录表

```sql
CREATE TABLE coin_transaction (
    id BIGINT PRIMARY KEY,                       -- 雪花ID
    transaction_no VARCHAR(64) UNIQUE NOT NULL,  -- 交易流水号
    from_user_id BIGINT,                         -- 发起方（NULL表示系统）
    to_user_id BIGINT,                           -- 接收方（NULL表示系统）
    amount BIGINT NOT NULL,                      -- 交易金额（胡萝卜）
    fee BIGINT NOT NULL DEFAULT 0,               -- 手续费
    transaction_type VARCHAR(50) NOT NULL,       -- 交易类型（见8.3）
    status VARCHAR(20) NOT NULL,                 -- SUCCESS / FAILED / PENDING
    business_type VARCHAR(50),                   -- 业务类型（点赞/打赏/置顶等）
    business_id BIGINT,                          -- 关联业务ID
    remark VARCHAR(500),                         -- 备注
    created_at TIMESTAMP NOT NULL,
    INDEX idx_from_user (from_user_id, created_at),
    INDEX idx_to_user (to_user_id, created_at),
    INDEX idx_transaction_no (transaction_no),
    INDEX idx_created_at (created_at)
);
```

### 8.3 交易类型枚举

| 类型代码 | 说明 | from_user_id | to_user_id |
|---------|------|--------------|------------|
| `SYSTEM_GRANT` | 系统赠送 | NULL | 用户ID |
| `LIKE_REWARD` | 点赞奖励 | NULL | 用户ID |
| `COMMENT_REWARD` | 评论奖励 | NULL | 用户ID |
| `TRANSFER` | 用户转账 | 用户ID | 用户ID |
| `TIP` | 打赏 | 用户ID | 用户ID |
| `CONSUME` | 消费 | 用户ID | NULL |
| `REFUND` | 退款 | NULL | 用户ID |
| `PENALTY` | 惩罚扣除 | 用户ID | NULL |
| `ADMIN_ADJUST` | 管理员调整 | NULL/用户ID | NULL/用户ID |

### 8.4 余额变动日志表

```sql
CREATE TABLE balance_change_log (
    id BIGINT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    transaction_id BIGINT NOT NULL,              -- 关联交易记录
    change_amount BIGINT NOT NULL,               -- 变动金额（正数=增加，负数=减少）
    balance_before BIGINT NOT NULL,              -- 变动前余额
    balance_after BIGINT NOT NULL,               -- 变动后余额
    change_type VARCHAR(50) NOT NULL,            -- EARN / SPEND / TRANSFER_IN / TRANSFER_OUT
    created_at TIMESTAMP NOT NULL,
    INDEX idx_user_time (user_id, created_at),
    INDEX idx_transaction (transaction_id)
);
```

---

