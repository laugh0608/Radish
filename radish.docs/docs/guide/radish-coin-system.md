# 萝卜币系统设计方案

## 1. 系统概述

萝卜币（Radish Coin）是 Radish 社区的虚拟积分系统，采用类似货币的设计理念，支持用户间流转、消费和奖励机制。系统设计遵循严格的财务规范，确保每一笔交易可追溯、可对账。

**核心设计原则**：
- **精度优先**：避免浮点运算，使用整数存储，确保分毫不差
- **可追溯性**：每笔交易完整记录，支持审计和对账
- **安全性**：防刷机制、余额锁定、并发控制
- **合规性**：避免使用"货币"等敏感词汇，定位为社区积分

---

## 2. 核心概念

### 2.1 萝卜币种类

| 币种 | 英文名 | 换算关系 | 说明 |
|------|--------|----------|------|
| **胡萝卜** | Carrot | 基础单位 | 最小交易单位，不可再分 |
| **白萝卜** | Radish | 1 白萝卜 = 1000 胡萝卜 | 高级单位，便于大额显示 |

**换算规则**：
- 系统内部统一使用**胡萝卜（Carrot）**作为存储单位（整数）
- 白萝卜仅用于显示，换算时向下取整（避免精度丢失）
- 示例：
  - 用户余额：`12345 胡萝卜` = `12 白萝卜 345 胡萝卜`
  - 显示模式：
    - 仅胡萝卜：`12345 胡萝卜`
    - 仅白萝卜：`12.345 白萝卜`（小数点后3位）
    - 混合显示：`12 白萝卜 345 胡萝卜`

### 2.2 最小交易单位

- **最小消费金额**：`1 胡萝卜`
- **最小转账金额**：`1 胡萝卜`
- **手续费最小值**：`1 胡萝卜`（手续费计算结果向上取整）

---

## 3. 获取机制

### 3.1 初始赠送

- **新用户注册**：赠送 `50 胡萝卜`
- 记录类型：`系统赠送`，备注：`新用户注册奖励`

#### 3.1.1 用户注册奖励集成指南

**集成位置**：在用户注册流程中（`Radish.Auth/AccountController.Register` 或 `Radish.Api/UserController.Register`）

**实现步骤**：

1. **注入 ICoinService**：
   ```csharp
   private readonly ICoinService _coinService;

   public AccountController(ICoinService coinService, ...)
   {
       _coinService = coinService;
   }
   ```

2. **在注册成功后发放奖励**：
   ```csharp
   [HttpPost]
   [AllowAnonymous]
   public async Task<IActionResult> Register([FromBody] RegisterRequest request)
   {
       // 1. 创建用户
       var userId = await _userService.AddAsync(newUser);

       // 2. 发放注册奖励（50 胡萝卜）
       try
       {
           await _coinService.GrantCoinAsync(
               userId: userId,
               amount: 50,
               transactionType: "SYSTEM_GRANT",
               businessType: "UserRegistration",
               businessId: userId,
               remark: "新用户注册奖励"
           );

           Log.Information("用户 {UserId} 注册成功，已发放 50 胡萝卜奖励", userId);
       }
       catch (Exception ex)
       {
           // 注册奖励发放失败不应影响注册流程
           Log.Error(ex, "用户 {UserId} 注册奖励发放失败", userId);
       }

       return Success("注册成功！已赠送 50 胡萝卜");
   }
   ```

3. **错误处理策略**：
   - **推荐**：注册奖励发放失败 **不应影响** 用户注册流程
   - **原因**：用户注册是核心功能，币奖励是附加功能
   - **处理**：记录日志，后续可通过对账任务或管理员手动补发

4. **幂等性保证**：
   ```csharp
   // 在发放奖励前检查是否已发放过（可选）
   var existingGrant = await _coinService.GetTransactionsAsync(
       userId: userId,
       pageIndex: 1,
       pageSize: 1,
       transactionType: "SYSTEM_GRANT"
   );

   if (existingGrant.Data.Any(t => t.BusinessType == "UserRegistration"))
   {
       Log.Warning("用户 {UserId} 已领取过注册奖励，跳过发放", userId);
       return; // 跳过发放
   }
   ```

### 3.2 互动奖励

| 行为 | 获得方 | 奖励金额 | 说明 |
|------|--------|----------|------|
| **发布帖子** | 作者 | 待定 | 鼓励内容创作 |
| **被点赞** | 作者 | `+2 胡萝卜/次` | 内容质量认可 |
| **点赞他人** | 点赞者 | `+1 胡萝卜/次` | 鼓励互动（每日上限 50） |
| **评论被回复** | 评论者 | `+1 胡萝卜/次` | 促进讨论 |
| **神评** | 评论者 | 基础 `+8` + 点赞加成 `+5/点赞` + 保留奖励 `+15/周` | 详见 Section 16.2 |
| **沙发** | 评论者 | 基础 `+5` + 点赞加成 `+3/点赞` + 保留奖励 `+10/周` | 详见 Section 16.2 |

**防刷机制**：
- 同一用户对同一内容的点赞/评论奖励，每日仅计算一次
- 点赞奖励每日上限：`50 胡萝卜`（防止刷赞）
- 系统检测异常行为（短时间大量操作），自动冻结账户

### 3.3 活动奖励

- **签到奖励**：连续签到递增（1-7天：1/2/3/5/8/13/21 胡萝卜）
- **任务完成**：完善资料、首次发帖等（10-50 胡萝卜）
- **官方活动**：节日活动、周年庆等（由管理员手动发放）

---

## 4. 流转机制

### 4.1 用户间转账

**转账流程**：
1. 发起方选择接收方和金额
2. 系统计算手续费（见 4.2）
3. 检查发起方余额是否充足（金额 + 手续费）
4. 扣除发起方余额
5. 增加接收方余额
6. 手续费归入平台账户
7. 记录交易日志

**转账限制**：
- 最小转账金额：`10 胡萝卜`
- 单笔最大金额：`10000 胡萝卜`（10 白萝卜）
- 每日转账次数上限：`20 次`
- 每日转账总额上限：`50000 胡萝卜`（50 白萝卜）

**多租户配额控制**：

不同租户可根据业务规模和付费等级设置不同的配额限制：

1. **租户级配额表设计**：
   ```sql
   CREATE TABLE tenant_coin_quota (
       tenant_id BIGINT PRIMARY KEY,
       -- 转账限制
       min_transfer_amount BIGINT DEFAULT 10,
       max_single_transfer BIGINT DEFAULT 10000,
       daily_transfer_count_limit INT DEFAULT 20,
       daily_transfer_amount_limit BIGINT DEFAULT 50000,

       -- 获取限制
       daily_reward_cap BIGINT DEFAULT 500,           -- 每日奖励总上限
       daily_like_reward_cap BIGINT DEFAULT 50,       -- 每日点赞奖励上限

       -- 消费限制
       daily_consume_limit BIGINT DEFAULT 100000,     -- 每日消费上限

       -- 余额限制
       max_user_balance BIGINT DEFAULT 1000000,       -- 单用户余额上限（100 白萝卜）
       platform_balance_alert BIGINT DEFAULT 10000000,-- 平台账户余额告警阈值

       -- 发行控制
       daily_issuance_limit BIGINT DEFAULT 100000,    -- 每日系统发行上限
       total_issuance_cap BIGINT,                     -- 总发行量上限（NULL=无限制）

       -- 配置元数据
       quota_level VARCHAR(20) DEFAULT 'STANDARD',    -- FREE / STANDARD / PREMIUM / ENTERPRISE
       effective_from TIMESTAMP NOT NULL,
       created_at TIMESTAMP NOT NULL,
       updated_at TIMESTAMP NOT NULL,

       INDEX idx_level (quota_level)
   );
   ```

2. **配额等级说明**：
   | 等级 | 日转账限额 | 日奖励上限 | 单用户余额上限 | 适用场景 |
   |-----|-----------|-----------|--------------|---------|
   | **FREE** | 10000 胡萝卜 | 100 胡萝卜 | 50000 胡萝卜 | 测试租户、小型社区 |
   | **STANDARD** | 50000 胡萝卜 | 500 胡萝卜 | 100000 胡萝卜 | 普通社区 |
   | **PREMIUM** | 200000 胡萝卜 | 2000 胡萝卜 | 500000 胡萝卜 | 活跃社区 |
   | **ENTERPRISE** | 无限制 | 无限制 | 无限制 | 大型平台、企业客户 |

3. **配额检查实现**：
   ```csharp
   public class TenantQuotaValidator
   {
       private readonly ICaching _cache;
       private readonly IBaseRepository<TenantCoinQuota> _quotaRepository;

       public async Task<QuotaCheckResult> CheckTransferQuotaAsync(
           long tenantId, long userId, long amount)
       {
           // 1. 获取租户配额（优先从缓存）
           var quota = await GetTenantQuotaAsync(tenantId);

           // 2. 检查单笔限额
           if (amount < quota.MinTransferAmount)
               return QuotaCheckResult.Fail($"最小转账金额为 {quota.MinTransferAmount} 胡萝卜");

           if (amount > quota.MaxSingleTransfer)
               return QuotaCheckResult.Fail($"单笔转账不得超过 {quota.MaxSingleTransfer} 胡萝卜");

           // 3. 检查每日转账次数（基于 Redis 计数器）
           var todayCountKey = $"quota:transfer:count:{tenantId}:{userId}:{DateTime.Today:yyyyMMdd}";
           var todayCount = await _cache.GetAsync<int>(todayCountKey);

           if (todayCount >= quota.DailyTransferCountLimit)
               return QuotaCheckResult.Fail($"今日转账次数已达上限（{quota.DailyTransferCountLimit} 次）");

           // 4. 检查每日转账总额（基于 Redis 累加）
           var todayAmountKey = $"quota:transfer:amount:{tenantId}:{userId}:{DateTime.Today:yyyyMMdd}";
           var todayAmount = await _cache.GetAsync<long>(todayAmountKey);

           if (todayAmount + amount > quota.DailyTransferAmountLimit)
               return QuotaCheckResult.Fail($"今日转账总额已达上限（{quota.DailyTransferAmountLimit} 胡萝卜）");

           return QuotaCheckResult.Success();
       }

       public async Task IncrementTransferCountAsync(long tenantId, long userId, long amount)
       {
           var today = DateTime.Today.ToString("yyyyMMdd");
           var countKey = $"quota:transfer:count:{tenantId}:{userId}:{today}";
           var amountKey = $"quota:transfer:amount:{tenantId}:{userId}:{today}";

           // 增加计数器（过期时间：次日凌晨）
           var expiry = DateTime.Today.AddDays(1) - DateTime.Now;

           await _cache.IncrementAsync(countKey, 1, expiry);
           await _cache.IncrementAsync(amountKey, amount, expiry);
       }
   }
   ```

4. **租户配额告警机制**：
   ```csharp
   public class TenantQuotaMonitor
   {
       [AutomaticRetry(Attempts = 3)]
       public async Task MonitorQuotaUsageAsync()
       {
           var tenants = await _quotaRepository.QueryAsync();

           foreach (var quota in tenants)
           {
               // 1. 检查今日发行量
               var todayIssuance = await GetTodayIssuanceAsync(quota.TenantId);
               if (todayIssuance > quota.DailyIssuanceLimit * 0.9m)
               {
                   await SendAlertAsync(quota.TenantId,
                       $"租户 {quota.TenantId} 今日发行量已达 90%（{todayIssuance}/{quota.DailyIssuanceLimit}）");
               }

               // 2. 检查平台账户余额
               var platformBalance = await GetPlatformBalanceAsync(quota.TenantId);
               if (platformBalance > quota.PlatformBalanceAlert)
               {
                   await SendAlertAsync(quota.TenantId,
                       $"租户 {quota.TenantId} 平台账户余额过高（{platformBalance}），建议进行活动回馈");
               }

               // 3. 检查总发行量上限（如果配置了）
               if (quota.TotalIssuanceCap.HasValue)
               {
                   var totalIssuance = await GetTotalIssuanceAsync(quota.TenantId);
                   if (totalIssuance > quota.TotalIssuanceCap.Value * 0.95m)
                   {
                       await SendAlertAsync(quota.TenantId,
                           $"租户 {quota.TenantId} 总发行量已达 95%，即将触及上限");
                   }
               }
           }
       }
   }
   ```

5. **配额管理 API（管理员）**：
   ```csharp
   [Authorize(Roles = "System,Admin")]
   [HttpPost("api/v2/Coin/Admin/UpdateTenantQuota")]
   public async Task<MessageModel> UpdateTenantQuota([FromBody] UpdateTenantQuotaDto dto)
   {
       var quota = await _quotaRepository.QueryByIdAsync(dto.TenantId);
       if (quota == null)
           return Failed("租户配额配置不存在");

       // 更新配额配置
       await _quotaRepository.UpdateAsync(new TenantCoinQuota
       {
           TenantId = dto.TenantId,
           QuotaLevel = dto.QuotaLevel,
           MaxSingleTransfer = dto.MaxSingleTransfer,
           DailyTransferCountLimit = dto.DailyTransferCountLimit,
           // ... 其他字段
       });

       // 清除缓存
       await _cache.DelAsync($"tenant:quota:{dto.TenantId}");

       return Success("配额更新成功");
   }
   ```

### 4.2 手续费规则

**计算公式**：
```
手续费 = max(转账金额 × 费率, 最低手续费)
```

**基础费率表**：
| 转账金额（胡萝卜） | 费率 | 最低手续费 |
|-------------------|------|-----------|
| 10 - 99 | 10% | 1 胡萝卜 |
| 100 - 999 | 5% | 10 胡萝卜 |
| 1000 - 49999 | 3% | 50 胡萝卜 |
| 50000+ | 1% | 500 胡萝卜 |

**示例**：
- 转账 `50 胡萝卜`：手续费 = `max(50 × 10%, 1)` = `5 胡萝卜`
- 转账 `500 胡萝卜`：手续费 = `max(500 × 5%, 10)` = `25 胡萝卜`
- 转账 `5000 胡萝卜`：手续费 = `max(5000 × 3%, 50)` = `150 胡萝卜`
- 转账 `100000 胡萝卜`：手续费 = `max(100000 × 1%, 500)` = `1000 胡萝卜`

**手续费向上取整**，确保平台收入不因精度问题损失。

**动态调整策略**：

1. **用户等级优惠**
   - VIP 用户：手续费 7 折
   - 普通用户：全价
   - 示例：VIP 用户转账 500 胡萝卜，手续费 = `25 × 0.7 = 18 胡萝卜`（向上取整）

2. **活动期间减免**
   - 节日活动期间：手续费减半
   - 新用户首次转账：免手续费
   - 周年庆等特殊活动：手续费全免

3. **实现示例**
   ```csharp
   public class DynamicFeeCalculator
   {
       public long CalculateFee(long amount, UserLevel level, bool isEventPeriod)
       {
           // 1. 计算基础费率
           var baseRate = GetBaseRate(amount); // 10% / 5% / 3% / 1%
           var minFee = GetMinFee(amount);     // 1 / 10 / 50 / 500

           // 2. 应用折扣
           var levelDiscount = level == UserLevel.VIP ? 0.7m : 1.0m;
           var eventDiscount = isEventPeriod ? 0.5m : 1.0m;

           // 3. 计算最终手续费
           var finalRate = baseRate * levelDiscount * eventDiscount;
           var calculatedFee = (long)Math.Ceiling(amount * finalRate);

           // 4. 确保不低于最低手续费（折扣后）
           var adjustedMinFee = (long)Math.Ceiling(minFee * levelDiscount * eventDiscount);
           return Math.Max(calculatedFee, adjustedMinFee);
       }

       private decimal GetBaseRate(long amount)
       {
           if (amount < 100) return 0.10m;
           if (amount < 1000) return 0.05m;
           if (amount < 50000) return 0.03m;
           return 0.01m;
       }
   }
   ```

4. **配置文件**
   ```json
   {
       "CoinSystem": {
           "FeeRates": [
               { "Max": 99, "Rate": 0.10, "MinFee": 1 },
               { "Max": 999, "Rate": 0.05, "MinFee": 10 },
               { "Max": 49999, "Rate": 0.03, "MinFee": 50 },
               { "Max": null, "Rate": 0.01, "MinFee": 500 }
           ],
           "VipDiscount": 0.7,
           "EventDiscount": 0.5
       }
   }
   ```

---

## 5. 消费机制

### 5.1 消费场景

| 场景 | 金额 | 说明 |
|------|------|------|
| **帖子置顶** | `100 胡萝卜/天` | 首页置顶24小时 |
| **评论高亮** | `20 胡萝卜/次` | 评论显示特殊样式 |
| **打赏作者** | 自定义 | 直接转账给作者（含手续费） |
| **购买勋章** | `500-5000 胡萝卜` | 个性化展示 |
| **解锁付费内容** | 作者设定 | 付费阅读/下载 |

### 5.2 消费流程

1. 用户发起消费请求
2. 系统检查余额是否充足
3. **余额锁定**（防止并发重复扣款）
4. 执行业务逻辑（置顶、高亮等）
5. 扣除用户余额
6. 释放余额锁定
7. 记录消费日志

**失败回滚**：
- 业务逻辑失败时，自动释放锁定并退还金额
- 记录失败原因，便于排查

### 5.3 退款机制

**自动退款场景**：

1. **帖子置顶失败**
   - 场景：帖子在置顶期间被删除或违规下架
   - 退款规则：按剩余时长比例退款
   - 示例：置顶 3 天（300 胡萝卜），使用 1 天后被删除，退款 = `300 × (2/3) = 200 胡萝卜`

2. **付费内容解锁失败**
   - 场景：支付后发现内容已删除或不可访问
   - 退款规则：全额退款
   - 时限：支付后 24 小时内

3. **系统故障导致的消费失败**
   - 场景：扣款成功但服务未生效（如高亮未应用）
   - 退款规则：自动检测并全额退款

**手动退款流程**：

1. **用户发起退款申请**
   - 申请入口：消费记录详情页
   - 时限：消费后 7 天内
   - 必填信息：退款原因、凭证截图

2. **管理员审核**
   - 查看消费记录和业务日志
   - 验证退款原因的合理性
   - 审核时限：48 小时内

3. **退款执行**
   - 审核通过：退还金额 - 10% 手续费
   - 审核拒绝：通知用户拒绝原因
   - 特殊情况（如系统故障）：全额退款

**退款记录追溯**：

```sql
CREATE TABLE coin_refund (
    id BIGINT PRIMARY KEY,
    original_transaction_id BIGINT NOT NULL,  -- 原交易ID
    refund_transaction_id BIGINT NOT NULL,    -- 退款交易ID
    refund_amount BIGINT NOT NULL,            -- 退款金额
    refund_type VARCHAR(50) NOT NULL,         -- AUTO/MANUAL
    refund_reason VARCHAR(500),               -- 退款原因
    auditor_id BIGINT,                        -- 审核人（手动退款）
    created_at TIMESTAMP NOT NULL,
    INDEX idx_original_tx (original_transaction_id),
    INDEX idx_refund_tx (refund_transaction_id)
);
```

**实现示例**：

```csharp
public async Task<long> RefundAsync(long originalTransactionId, string reason, RefundType type)
{
    var original = await GetTransactionAsync(originalTransactionId);

    // 检查: 是否已退款
    var existingRefund = await GetRefundByOriginalTxAsync(originalTransactionId);
    if (existingRefund != null)
        throw new BusinessException("该交易已退款");

    // 检查: 7天内（手动退款）
    if (type == RefundType.Manual && (DateTime.Now - original.CreatedAt).TotalDays > 7)
        throw new BusinessException("超过退款期限");

    // 计算退款金额
    var refundAmount = type == RefundType.Auto
        ? original.Amount  // 自动退款全额
        : (long)(original.Amount * 0.9m); // 手动退款扣10%

    // 创建退款交易
    var refundTx = new CoinTransaction
    {
        TransactionType = "REFUND",
        FromUserId = null,
        ToUserId = original.FromUserId,
        Amount = refundAmount,
        Remark = $"退款: {reason} (原交易: {originalTransactionId})"
    };

    var refundTxId = await CreateTransactionAsync(refundTx);

    // 记录退款关联
    await InsertRefundRecordAsync(new CoinRefund
    {
        OriginalTransactionId = originalTransactionId,
        RefundTransactionId = refundTxId,
        RefundAmount = refundAmount,
        RefundType = type.ToString(),
        RefundReason = reason
    });

    return refundTxId;
}
```

---

## 6. 精度处理方案

### 6.1 存储设计

**数据库字段类型**：
- 使用 `BIGINT`（64位整数）存储胡萝卜数量
- **禁止使用** `DECIMAL`、`FLOAT`、`DOUBLE`（避免浮点误差）

**示例**：
```sql
CREATE TABLE user_balance (
    user_id BIGINT PRIMARY KEY,
    balance BIGINT NOT NULL DEFAULT 0,  -- 胡萝卜数量（整数）
    frozen_balance BIGINT NOT NULL DEFAULT 0,  -- 冻结金额
    updated_at TIMESTAMP NOT NULL
);
```

### 6.2 计算规则

**加减运算**：
```csharp
// ✅ 正确：整数运算
long balance = 12345;  // 12345 胡萝卜
long amount = 100;
long newBalance = balance - amount;  // 12245 胡萝卜

// ❌ 错误：浮点运算
decimal balance = 12.345m;  // 12.345 白萝卜
decimal amount = 0.1m;
decimal newBalance = balance - amount;  // 可能产生精度误差
```

**换算显示**：
```csharp
// 胡萝卜 → 白萝卜（仅用于显示）
long carrotBalance = 12345;
decimal radishBalance = carrotBalance / 1000m;  // 12.345 白萝卜
string display = radishBalance.ToString("F3");  // "12.345"

// 白萝卜 → 胡萝卜（用户输入时）
decimal inputRadish = 12.345m;
long carrotAmount = (long)(inputRadish * 1000);  // 12345 胡萝卜
```

**手续费计算**：
```csharp
// 手续费向上取整
long amount = 50;
decimal feeRate = 0.1m;  // 10%
long fee = (long)Math.Ceiling(amount * feeRate);  // 5 胡萝卜
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

## 9. 安全性设计

### 9.1 并发控制

**乐观锁方案**：
```csharp
// 更新余额时检查版本号
UPDATE user_balance
SET balance = balance - @amount,
    version = version + 1,
    updated_at = NOW()
WHERE user_id = @userId
  AND version = @currentVersion
  AND balance >= @amount;

// 影响行数为0表示并发冲突，需重试
```

**悲观锁方案**（高并发场景）：
```csharp
// 使用行锁
SELECT balance FROM user_balance
WHERE user_id = @userId
FOR UPDATE;

// 执行扣款操作
UPDATE user_balance SET balance = balance - @amount WHERE user_id = @userId;
```

### 9.2 防刷机制

**限流规则**：
- 同一用户每秒最多发起 `5` 笔交易
- 同一IP每分钟最多发起 `50` 笔交易
- 异常行为检测：短时间内大量小额转账（可能是洗钱）

**风控策略**：
- 新用户转账限额更低（注册7天内）
- 检测到异常行为自动冻结账户，需人工审核
- 记录设备指纹，防止批量注册刷币

**智能风控升级**：

1. **设备指纹 + IP 关联分析**
   - 检测同一设备或 IP 下的多个账号互刷行为
   - 记录设备特征（浏览器指纹、操作系统、屏幕分辨率等）
   - 关联分析：同一设备下多账号频繁互相转账或点赞

2. **行为模式识别**
   - **正常用户特征**：
     - 点赞/评论分散在不同帖子和时间段
     - 行为间隔不规律（符合人类随机性）
     - 浏览时长与互动频率成正比
   - **刷子特征**：
     - 点赞/评论集中在少数几个帖子（前3个帖子占80%以上）
     - 时间间隔规律（标准差小于5秒，机器行为）
     - 无浏览行为直接点赞（秒点）

3. **信用分系统**
   - 新用户初始信用分：`100`
   - 正常行为加分：连续登录、发布优质内容、获得点赞
   - 异常行为扣分：
     - 被检测到刷币行为：-20 分
     - 短时间大量小额转账：-10 分
     - 设备指纹关联多账号：-30 分
   - 信用分等级：
     - 90-100：正常用户，无限制
     - 60-89：观察用户，转账限额减半
     - 0-59：高风险用户，禁止转账，仅保留查询功能

4. **实现示例**
   ```csharp
   public class AntiFraudDetector
   {
       public async Task<FraudRisk> DetectAsync(long userId)
       {
           var recentActions = await GetRecentActionsAsync(userId, hours: 24);

           // 检测1: 点赞集中度（前3个帖子点赞数占总数80%以上）
           var topPostsRatio = recentActions.GroupBy(a => a.PostId)
               .OrderByDescending(g => g.Count())
               .Take(3)
               .Sum(g => g.Count()) / (double)recentActions.Count;

           // 检测2: 时间间隔规律性（标准差小于5秒）
           var intervals = recentActions.Zip(recentActions.Skip(1),
               (a, b) => (b.CreatedAt - a.CreatedAt).TotalSeconds);
           var stdDev = CalculateStdDev(intervals);

           // 检测3: 设备指纹关联
           var deviceAccounts = await GetDeviceFingerprintAccountsAsync(userId);

           if (topPostsRatio > 0.8 && stdDev < 5)
               return FraudRisk.High; // 高风险

           if (deviceAccounts.Count > 5)
               return FraudRisk.Medium; // 中风险

           return FraudRisk.Low;
       }
   }
   ```

### 9.3 审计日志

**关键操作记录**：
- 所有余额变动（包括失败的操作）
- 管理员手动调整余额
- 异常交易（金额异常、频率异常）
- 对账差异记录

**日志保留**：
- 交易记录永久保留
- 审计日志保留 `3` 年
- 对账报告永久保留

---

## 10. 未来扩展

### 10.1 等级系统关联

- 用户等级影响手续费率（VIP用户手续费折扣）
- 高等级用户转账限额更高
- 等级提升奖励萝卜币

### 10.2 投资理财

- **定期存款**：锁定萝卜币一段时间，到期获得利息
- **活动基金**：用户众筹支持社区活动，成功后分红

### 10.3 商城系统

- 使用萝卜币兑换实物商品（需对接物流）
- 虚拟商品（会员、主题、表情包等）

### 10.4 税收系统

- 大额交易征收"税收"（类似手续费，但归入公共基金）
- 公共基金用于社区建设、活动奖励

### 10.5 慈善捐赠

- 用户可将萝卜币捐赠给公益项目
- 捐赠记录公开展示，提升用户声誉

---

## 11. 技术实现要点

### 11.1 事务管理

**关键原则**：
- 所有涉及余额变动的操作必须在事务中执行
- 事务失败时自动回滚，确保数据一致性
- 使用分布式事务（如需跨服务）

**示例**：
```csharp
using var transaction = await _db.BeginTransactionAsync();
try
{
    // 1. 扣除发起方余额
    await DeductBalanceAsync(fromUserId, amount + fee);

    // 2. 增加接收方余额
    await AddBalanceAsync(toUserId, amount);

    // 3. 增加平台手续费
    await AddBalanceAsync(platformUserId, fee);

    // 4. 记录交易日志
    await InsertTransactionLogAsync(...);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 11.2 幂等性保证

**交易流水号机制**：
- 每笔交易生成唯一流水号（雪花ID + 业务前缀）
- 数据库唯一索引防止重复提交
- 客户端重试时使用相同流水号

**示例**：
```csharp
string transactionNo = $"TXN_{SnowFlakeSingle.Instance.NextId()}";
// 插入时如果流水号重复，返回已有记录（幂等）
```

### 11.3 性能优化

**缓存策略**：
- 用户余额缓存（Redis），过期时间 `5` 分钟
- 缓存更新策略：先更新数据库，再删除缓存（Cache-Aside）
- 高并发场景使用分布式锁

**分库分表**：
- 交易记录按月分表（`coin_transaction_202501`）
- 用户余额按用户ID哈希分库（未来百万用户时）

---

## 12. 开发计划

### Phase 1: 核心功能（MVP）
- [ ] 数据库表设计与创建
- [ ] 用户余额管理（查询、增加、扣除）
- [ ] 系统赠送（注册奖励）
- [ ] 交易记录查询
- [ ] 基础对账功能

### Phase 2: 流转机制
- [ ] 用户间转账
- [ ] 手续费计算
- [ ] 并发控制（乐观锁）
- [ ] 防刷限流

### Phase 3: 消费场景
- [ ] 帖子置顶
- [ ] 打赏功能
- [ ] 评论高亮
- [ ] 付费内容解锁

### Phase 4: 奖励机制
- [ ] 点赞奖励
- [ ] 评论奖励
- [ ] 签到奖励
- [ ] 任务系统

### Phase 5: 运营工具
- [ ] 管理后台（余额查询、手动调整）
- [ ] 对账报表
- [ ] 异常交易监控
- [ ] 数据统计分析

---

## 13. 风险与合规

### 13.1 法律风险

**避免触碰红线**：
- ❌ 不称为"货币"、"代币"、"虚拟货币"
- ❌ 不支持与法定货币兑换（充值/提现）
- ❌ 不承诺保值增值
- ✅ 定位为"社区积分"、"虚拟道具"
- ✅ 用户协议明确说明无现金价值
- ✅ 平台有权回收、调整规则

### 13.2 用户协议条款

**必须包含**：
- 萝卜币无现金价值，不可兑换法定货币
- 平台有权调整获取/消费规则
- 违规行为（刷币、作弊）将被扣除或封号
- 平台有权回收长期不活跃用户的萝卜币
- 服务终止时，萝卜币自动失效

### 13.3 反洗钱措施

- 限制单笔/单日转账金额
- 监控异常交易模式（频繁小额转账）
- 实名认证用户才能转账（可选）
- 保留交易记录至少 `3` 年

---

## 14. 落地实现清单（建议按优先级推进）

> 本节用于把“设计方案”落到可直接开发的任务列表，避免实现时反复补洞。

### 14.1 数据模型与约束（必须先定）

1. **平台账户模型（Platform Account）**
   - 明确“平台账户”是全局唯一还是按租户隔离（建议：按 `TenantId` 一套平台账户）。
   - 在业务实现中，平台手续费收入统一归集到平台账户（`to_user_id = platformUserId`）。

2. **交易流水号（transaction_no）与幂等性**
   - 明确流水号生成方：
     - 建议：**服务端生成** `transaction_no`（对外提供 `Idempotency-Key`），客户端只负责传入幂等键。
   - 幂等键存储与约束：
     - `idempotency_key`（建议新增字段）+ `business_type` + `business_id` + `from_user_id` 组合唯一。
     - 同一请求重试必须返回同一笔交易结果（SUCCESS/FAILED/PENDING）。

3. **交易表与账本表职责边界（coin_transaction vs balance_change_log）**
   - `coin_transaction`：记录“业务交易意图与结果”（谁给谁、多少、手续费、业务关联、状态）。
   - `balance_change_log`：记录“账户分录”（每个 user 的余额变动明细），可用于对账与追溯。
   - 建议口径：
     - 每笔 `coin_transaction` 至少对应 1 条或多条 `balance_change_log`（转账通常是 2~3 条：转出、转入、平台手续费）。

4. **去重/防刷的落地键（Daily Once Rule）**
   - 需要明确去重维度：
     - `user_id + action_type + target_type + target_id + date`。
   - 建议新增“奖励去重表”（或在 `coin_transaction` 上用唯一键实现）：
     - 例如：`LIKE_REWARD` 以 `business_type=POST_LIKE`、`business_id=postId`、`from_user_id=NULL`、`to_user_id=authorId`、`reward_date` 唯一。

5. **金额字段的非负与溢出边界**
   - 统一约束：余额不得为负；手续费/金额不得为负。
   - 所有金额运算使用 `checked`（C#）或在写库前做上限校验，防止极端输入造成溢出。

### 14.2 事务与并发策略（必须写进实现规范）

1. **单库事务原则**
   - 一次余额变动（扣款/转账/奖励）必须在同一个数据库事务内完成：
     - 写 `coin_transaction`（PENDING）→ 更新余额 → 写 `balance_change_log` → 更新 `coin_transaction`（SUCCESS/FAILED）。

2. **乐观锁 vs 悲观锁：明确默认方案**
   - 默认建议：乐观锁（`version` 字段），冲突重试 N 次。
   - 高并发热点场景（例如点赞奖励）可评估：悲观锁或“按用户维度串行化”（队列/锁）。

3. **冻结余额的使用边界**
   - 明确哪些场景需要 `frozen_balance`：
     - 典型：支付/消费类需要先冻结再确认扣除。
     - 纯奖励类通常不需要冻结。

### 14.3 接口草案（MVP 先把最小闭环跑通）

> 先实现“余额查询 + 系统发放 + 转账 + 账单查询”，再逐步接入奖励/消费。

1. `GET /api/v1/Coin/Balance`
   - 返回：可用余额、冻结余额、累计获得/消费等。

2. `GET /api/v1/Coin/Transactions`
   - 参数：pageIndex/pageSize、transactionType、status、dateRange
   - 返回：`coin_transaction` 列表（分页）。

3. `POST /api/v1/Coin/Transfer`
   - 入参：toUserId、amount、idempotencyKey
   - 规则：校验限额、手续费、余额充足；写交易与分录。

4. `POST /api/v1/Coin/AdminAdjust`（管理员）
   - 入参：userId、deltaAmount（可正可负）、reason、idempotencyKey
   - 必须写审计日志与分录。

### 14.4 与论坛/神评沙发的对接点（建议明确触发时机）

1. **点赞奖励（LIKE_REWARD）**
   - 触发点建议：点赞成功后异步发放（避免接口被奖励逻辑拖慢）。
   - 去重：同一用户对同一内容每日仅奖励一次（见 14.1）。

2. **神评/沙发奖励**
   - 当前你已经有定时统计：建议以统计任务为发放入口（每日结算），避免实时刷赞套利。
   - 奖励上限：每日/每帖/每用户上限建议写清楚。

**异步奖励机制的健壮性保障**：

3. **死信队列（DLQ）**
   - 异步发放失败的奖励进入死信队列
   - 定期重试（每小时一次，最多重试 3 次）
   - 超过重试次数后人工介入

4. **补偿机制**
   - 每日对账时检测"已点赞但未发放奖励"的记录
   - 自动补发缺失的奖励
   - 记录补偿日志便于审计

5. **幂等性保证**
   - 使用 `业务类型 + 业务ID + 用户ID + 日期` 作为唯一键
   - 防止消息队列重复消费导致重复发放
   - 数据库唯一索引约束

6. **实现示例**
   ```csharp
   public interface ICoinRewardQueue
   {
       // 入队奖励消息
       Task EnqueueRewardAsync(RewardMessage message);

       // 重试失败的奖励（定时任务调用）
       Task RetryFailedRewardsAsync();

       // 对账补偿（每日对账时调用）
       Task CompensateMissingRewardsAsync(DateTime date);
   }

   public class RewardMessage
   {
       public string BusinessType { get; set; }  // POST_LIKE / COMMENT_REPLY
       public long BusinessId { get; set; }      // PostId / CommentId
       public long ToUserId { get; set; }        // 奖励接收者
       public long Amount { get; set; }          // 奖励金额
       public string IdempotencyKey { get; set; } // 幂等键: {BusinessType}_{BusinessId}_{ToUserId}_{Date}
   }

   // 定时任务：每小时重试死信队列
   [AutomaticRetry(Attempts = 0)]
   public async Task RetryDeadLetterQueueAsync()
   {
       await _rewardQueue.RetryFailedRewardsAsync();
   }

   // 定时任务：每日凌晨3点补偿昨日缺失奖励
   [AutomaticRetry(Attempts = 0)]
   public async Task CompensateMissingRewardsAsync()
   {
       await _rewardQueue.CompensateMissingRewardsAsync(DateTime.Today.AddDays(-1));
   }
   ```

### 14.5 对账口径与定时任务（上线前必须有）

1. **资金守恒公式落地**
   - 需要明确"发行量/消耗量"的口径来自哪些 `transaction_type`。

2. **每日对账任务**
   - 建议实现一个 Hangfire Job：
     - 汇总用户余额、平台余额、当日发行/消耗，写入 `daily_balance_report`。
     - 差异非 0：告警 + 标记 UNBALANCED。

**实时监控与告警增强**：

3. **实时账本校验**
   - 每笔交易后立即校验：`sum(balance_change_log.change_amount) = 交易金额 + 手续费`
   - 校验失败立即记录异常日志并告警
   - 示例实现：
     ```csharp
     public class RealTimeBalanceMonitor
     {
         public async Task<bool> VerifyTransactionAsync(long transactionId)
         {
             var transaction = await GetTransactionAsync(transactionId);
             var logs = await GetBalanceChangeLogsAsync(transactionId);

             // 校验: 交易金额 + 手续费 = 变动日志总和
             var expectedTotal = transaction.Amount + transaction.Fee;
             var actualTotal = logs.Sum(l => Math.Abs(l.ChangeAmount));

             if (expectedTotal != actualTotal)
             {
                 await _alertService.SendAlertAsync(
                     $"交易 {transactionId} 账本不一致: 预期 {expectedTotal}, 实际 {actualTotal}"
                 );
                 return false;
             }
             return true;
         }
     }
     ```

4. **异常交易即时告警**
   - **大额交易告警**：单笔交易超过 10 万胡萝卜
   - **高频交易告警**：单用户 1 小时内交易次数超过 50 次
   - **平台账户异常告警**：平台账户余额异常下降（应只增不减）
   - 告警渠道：邮件 + 企业微信 + 管理后台通知

5. **对账差异自动冻结**
   - 差异非 0 时，自动冻结所有转账操作（仅保留查询）
   - 冻结后在管理后台显著提示
   - 差异修复后需管理员手动解除冻结
   - 冻结期间记录所有查询操作日志

6. **对账差异追溯工具**
   ```csharp
   public class ReconciliationDifferenceTracer
   {
       // 找出差异来源
       public async Task<List<TransactionAnomaly>> TraceAnomaliesAsync(DateTime date)
       {
           var anomalies = new List<TransactionAnomaly>();

           // 1. 找出变动日志缺失的交易
           var txWithoutLogs = await FindTransactionsWithoutLogsAsync(date);
           anomalies.AddRange(txWithoutLogs.Select(tx => new TransactionAnomaly
           {
               TransactionId = tx.Id,
               Type = "缺少变动日志",
               Description = $"交易 {tx.Id} 未生成对应的 balance_change_log"
           }));

           // 2. 找出金额不匹配的交易
           var amountMismatches = await FindAmountMismatchesAsync(date);
           anomalies.AddRange(amountMismatches);

           // 3. 找出孤立的变动日志（无对应交易）
           var orphanLogs = await FindOrphanLogsAsync(date);
           anomalies.AddRange(orphanLogs.Select(log => new TransactionAnomaly
           {
               TransactionId = log.TransactionId,
               Type = "孤立变动日志",
               Description = $"变动日志 {log.Id} 对应的交易 {log.TransactionId} 不存在"
           }));

           return anomalies;
       }
   }
   ```

---

## 15. 讨论问题清单

### 15.1 待确认的设计细节

1. **手续费率是否合理？**
   - 当前设计：10% / 5% / 3% 三档
   - 是否需要根据用户等级调整？

2. **神评/沙发奖励机制**
   - 当前机制有何问题？
   - 新机制的设想？（如：神评由作者指定，奖励从作者余额扣除？）

3. **初始赠送金额**
   - 50 胡萝卜是否合适？
   - 是否需要新手任务额外奖励？

4. **转账限额**
   - 单笔 10000 胡萝卜是否过低？
   - 每日 50000 胡萝卜是否合理？

5. **对账频率**
   - 每日对账是否足够？
   - 是否需要实时对账（每笔交易后）？

6. **平台账户用途**
   - 手续费收入如何使用？
   - 是否用于活动奖励、公益捐赠？

### 15.2 技术选型

1. **并发控制方案**
   - 乐观锁 vs 悲观锁？
   - 是否需要分布式锁（Redis）？

2. **缓存策略**
   - 余额是否缓存？
   - 缓存一致性如何保证？

3. **分库分表**
   - 何时启动分库分表？
   - 分表策略（按月/按用户ID）？

---

## 16. 实施落地待完善事项（2025-12-30 评审）

### 16.1 与现有论坛功能的集成细节

**问题**：文档第 14.7 节提到了与论坛的集成点，但实现细节不够具体。

**需要补充的内容**：

1. **PostService 和 CommentService 中的集成示例**
   ```csharp
   // 在 PostService.ToggleLikeAsync 中如何调用萝卜币服务？
   public async Task<PostLikeResultDto> ToggleLikeAsync(long userId, long postId)
   {
       // 现有逻辑：点赞/取消点赞

       // 新增：萝卜币奖励逻辑
       // ❓ 是否需要保证事务一致性？
       // ❓ 如果萝卜币发放失败，是否回滚点赞？
   }
   ```

2. **事务边界设计**
   - **方案A - 强一致性**：使用分布式事务（TransactionScope），保证点赞和萝卜币发放同时成功或同时失败
   - **方案B - 最终一致性**：点赞成功后异步发放萝卜币，失败后使用补偿机制重试
   - **推荐**：Phase 1 使用方案 A（简单），Phase 3+ 优化为方案 B（性能）

3. **定义 ICoinRewardService 接口**
   ```csharp
   public interface ICoinRewardService
   {
       // 发放点赞奖励（帖子作者获得）
       Task<bool> GrantLikeRewardAsync(long postId, long authorId);

       // 发放评论奖励（评论者获得）
       Task<bool> GrantCommentRewardAsync(long commentId, long authorId);

       // 发放神评奖励
       Task<bool> GrantGodCommentRewardAsync(long commentId, long authorId, int likeCount);

       // 发放沙发奖励
       Task<bool> GrantSofaRewardAsync(long commentId, long authorId, int likeCount);
   }
   ```

### 16.2 神评/沙发的萝卜币奖励机制

**问题**：文档第 3.2 节神评/沙发需要具体的奖励机制实现细节。

**最终方案**：

1. **神评奖励规则**
   - 基础奖励：`8 胡萝卜`
   - 点赞加成：每个点赞 `+5 胡萝卜`
   - 示例：一条点赞数为 10 的神评可获得 8 + 10×5 = 58 胡萝卜

2. **沙发奖励规则**
   - 基础奖励：`5 胡萝卜`
   - 点赞加成：每个点赞 `+3 胡萝卜`
   - 示例：一条点赞数为 10 的沙发可获得 5 + 10×3 = 35 胡萝卜

3. **发放时机**
   - 实时发放：当评论成为神评/沙发时立即发放基础奖励
   - 每日结算：每天凌晨 2 点统计昨日点赞增量，发放加成奖励
   - 失去地位不扣回：一旦发放不再追回（避免用户体验问题）

4. **点赞加成上限（防止无限增长）**
   - **神评点赞加成上限**：暂不设上限（根据运营情况后续调整）
   - **沙发点赞加成上限**：暂不设上限（根据运营情况后续调整）
   - **实现示例**：
     ```csharp
     public class HighlightRewardCalculator
     {
         public long CalculateGodCommentReward(int likeCount)
         {
             const long baseReward = 8;        // 基础奖励 8 胡萝卜
             const long perLikeBonus = 5;      // 每点赞 +5 胡萝卜

             return baseReward + likeCount * perLikeBonus;
         }

         public long CalculateSofaReward(int likeCount)
         {
             const long baseReward = 5;        // 基础奖励 5 胡萝卜
             const long perLikeBonus = 3;      // 每点赞 +3 胡萝卜

             return baseReward + likeCount * perLikeBonus;
         }
     }
     ```

5. **保留奖励机制（鼓励持续优质内容）**
   - **神评保留天数奖励**：
     - 连续保持神评地位每满 7 天，额外奖励 `15 胡萝卜`
     - 最多连续 3 周（21 天），总额外奖励 45 胡萝卜
   - **沙发保留天数奖励**：
     - 连续保持沙发地位每满 7 天，额外奖励 `10 胡萝卜`
     - 最多连续 3 周（21 天），总额外奖励 30 胡萝卜
   - **检测逻辑**：
     - 每日凌晨 2 点检查神评/沙发的保留天数
     - 如果当天失去地位，则停止发放保留奖励（但不追回已发放的）
   - **实现示例**：
     ```csharp
     public class RetentionRewardJob
     {
         [AutomaticRetry(Attempts = 3)]
         public async Task ProcessRetentionRewardsAsync()
         {
             var date = DateTime.Today;

             // 1. 获取所有活跃的神评/沙发记录
             var highlights = await _highlightRepository.QueryAsync(h =>
                 h.IsActive && h.CreatedAt <= date.AddDays(-7));

             foreach (var highlight in highlights)
             {
                 // 2. 计算保留天数（向下取整到周）
                 var retentionDays = (date - highlight.CreatedAt.Date).Days;
                 var retentionWeeks = retentionDays / 7;

                 // 3. 检查是否需要发放保留奖励
                 var lastRewardWeek = highlight.LastRetentionRewardWeek ?? 0;

                 // 最多 3 周
                 if (retentionWeeks > lastRewardWeek && retentionWeeks <= 3)
                 {
                     // 4. 计算并发放奖励
                     var rewardAmount = highlight.HighlightType == "GodComment" ? 15 : 10;

                     await _coinRewardService.GrantRetentionRewardAsync(
                         highlightId: highlight.Id,
                         userId: highlight.UserId,
                         amount: rewardAmount,
                         week: retentionWeeks
                     );

                     // 5. 更新最后发放周数
                     await _highlightRepository.UpdateColumnsAsync(
                         h => h.Id == highlight.Id,
                         h => new CommentHighlight { LastRetentionRewardWeek = retentionWeeks }
                     );
                 }
             }
         }
     }
     ```

6. **数据库设计**
   ```sql
   -- 在 CommentHighlight 表中记录奖励发放
   ALTER TABLE comment_highlight ADD COLUMN coin_rewarded BOOLEAN DEFAULT FALSE;
   ALTER TABLE comment_highlight ADD COLUMN coin_amount BIGINT DEFAULT 0;
   ALTER TABLE comment_highlight ADD COLUMN last_reward_at TIMESTAMP;
   ALTER TABLE comment_highlight ADD COLUMN last_retention_reward_week INT DEFAULT 0;  -- 最后发放保留奖励的周数
   ALTER TABLE comment_highlight ADD COLUMN total_retention_reward BIGINT DEFAULT 0;   -- 累计保留奖励金额

   -- 神评/沙发奖励配置表（支持动态调整）
   CREATE TABLE highlight_reward_config (
       id BIGINT PRIMARY KEY,
       highlight_type VARCHAR(20) NOT NULL,     -- GodComment / Sofa
       base_reward BIGINT NOT NULL,             -- 基础奖励
       per_like_bonus BIGINT NOT NULL,          -- 每点赞加成
       like_cap INT NOT NULL,                   -- 点赞加成上限
       retention_reward BIGINT NOT NULL,        -- 保留奖励（每周）
       max_retention_weeks INT NOT NULL,        -- 最大保留周数
       is_enabled BOOLEAN DEFAULT TRUE,
       created_at TIMESTAMP NOT NULL,
       updated_at TIMESTAMP NOT NULL,
       INDEX idx_type (highlight_type)
   );

   -- 初始化配置数据
   INSERT INTO highlight_reward_config (id, highlight_type, base_reward, per_like_bonus, like_cap, retention_reward, max_retention_weeks, created_at, updated_at) VALUES
   (1, 'GodComment', 8, 5, 999999, 15, 3, NOW(), NOW()),
   (2, 'Sofa', 5, 3, 999999, 10, 3, NOW(), NOW());
   ```

### 16.3 平台账户的具体实现

**问题**：文档第 14.1 节提到平台账户，但没有说明如何创建和管理。

**建议方案**：

1. **平台账户 UserId 固定为 1**
   - 在系统初始化时自动创建
   - 使用 SqlSugar 种子数据机制

2. **初始化代码**
   ```csharp
   public class CoinSystemInitializer : IHostedService
   {
       public async Task StartAsync(CancellationToken cancellationToken)
       {
           // 检查平台账户是否存在
           var platformAccount = await _balanceRepository.QueryByIdAsync(1);
           if (platformAccount == null)
           {
               // 创建平台账户
               await _balanceRepository.AddAsync(new UserBalance
               {
                   UserId = 1, // 平台账户固定 ID
                   Balance = 0,
                   FrozenBalance = 0,
                   CreateTime = DateTime.Now,
                   CreateBy = "System"
               });
           }
       }
   }
   ```

3. **配置文件**
   ```json
   // appsettings.json
   {
       "CoinSystem": {
           "PlatformUserId": 1,
           "TransferFeeRates": [
               { "Max": 10000, "Rate": 0.10 },
               { "Max": 50000, "Rate": 0.05 },
               { "Max": null, "Rate": 0.03 }
           ]
       }
   }
   ```

### 16.4 并发冲突的重试策略

**问题**：文档提到了乐观锁，但版本冲突后如何重试没有说明。

**建议方案**：

1. **使用 Polly 库实现重试**
   ```csharp
   var retryPolicy = Policy
       .Handle<DbUpdateConcurrencyException>()
       .WaitAndRetryAsync(
           retryCount: 3,
           sleepDurationProvider: retryAttempt =>
               TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
           onRetry: (exception, timeSpan, retryCount, context) =>
           {
               Log.Warning("乐观锁冲突，第 {RetryCount} 次重试", retryCount);
           });

   await retryPolicy.ExecuteAsync(async () =>
   {
       await TransferWithOptimisticLockAsync(fromUserId, toUserId, amount);
   });
   ```

2. **重试配置**
   - 最大重试次数：3 次
   - 退避策略：指数退避（100ms, 200ms, 400ms）
   - 超过重试次数后返回用户友好错误信息

### 16.5 API 接口详细契约

**问题**：文档第 14.6 节列出了 API 列表，但缺少详细的请求/响应示例。

**补充示例**：

1. **转账接口**
   ```http
   POST /api/v2/Coin/Transfer
   Content-Type: application/json
   Authorization: Bearer {token}

   {
       "toUserId": 12345,
       "amount": 5000,        // 5 白萝卜 = 5000 胡萝卜
       "remark": "感谢分享"
   }

   // Response - 成功
   {
       "isSuccess": true,
       "message": "转账成功",
       "responseData": {
           "transactionNo": "TXN20250101123456789",
           "actualAmount": 5000,
           "fee": 500,
           "newBalance": 95000,
           "createdAt": "2025-01-01T12:34:56Z"
       }
   }

   // Response - 失败（余额不足）
   {
       "isSuccess": false,
       "message": "余额不足",
       "code": "INSUFFICIENT_BALANCE",
       "responseData": {
           "currentBalance": 3000,
           "requiredAmount": 5500
       }
   }
   ```

2. **查询余额接口**
   ```http
   GET /api/v2/Coin/Balance
   Authorization: Bearer {token}

   // Response
   {
       "isSuccess": true,
       "responseData": {
           "userId": 12345,
           "balance": 95000,          // 胡萝卜
           "balanceDisplay": "95.000", // 白萝卜（三位小数）
           "frozenBalance": 0,
           "totalEarned": 150000,
           "totalSpent": 55000
       }
   }
   ```

### 16.6 定时任务的监控机制

**问题**：文档第 14.8 节提到对账和清理任务，但没有说明如何监控。

**建议方案**：

1. **创建对账日志表**
   ```sql
   CREATE TABLE reconciliation_log (
       id BIGINT PRIMARY KEY,
       reconcile_date DATE NOT NULL,
       total_users INT NOT NULL,
       inconsistent_count INT NOT NULL,
       status VARCHAR(20) NOT NULL, -- SUCCESS/FAILED/PARTIAL
       error_message TEXT,
       execution_time_ms INT,
       created_at TIMESTAMP NOT NULL,
       INDEX idx_date (reconcile_date)
   );
   ```

2. **Hangfire 定时任务**
   ```csharp
   public class CoinReconciliationJob
   {
       [AutomaticRetry(Attempts = 3)]
       public async Task ExecuteAsync()
       {
           var startTime = DateTime.Now;
           var result = await ReconcileAllUserBalancesAsync();

           // 记录对账结果
           await _reconciliationLogRepository.AddAsync(new ReconciliationLog
           {
               ReconcileDate = DateTime.Today,
               TotalUsers = result.TotalUsers,
               InconsistentCount = result.InconsistentCount,
               Status = result.IsSuccess ? "SUCCESS" : "FAILED",
               ErrorMessage = result.ErrorMessage,
               ExecutionTimeMs = (int)(DateTime.Now - startTime).TotalMilliseconds
           });

           // 如果有不一致，发送告警
           if (result.InconsistentCount > 0)
           {
               await _alertService.SendAlertAsync(
                   $"萝卜币对账发现 {result.InconsistentCount} 个账户不一致"
               );
           }
       }
   }
   ```

3. **注册定时任务（Program.cs）**
   ```csharp
   RecurringJob.AddOrUpdate<CoinReconciliationJob>(
       "coin-reconciliation",
       job => job.ExecuteAsync(),
       "0 2 * * *", // 每天凌晨 2 点
       new RecurringJobOptions
       {
           TimeZone = TimeZoneInfo.Local
       });
   ```

### 16.7 实施优先级建议

**Phase 0（准备阶段）- 1天**
- [ ] 确定神评/沙发奖励金额和规则
- [ ] 设计平台账户初始化方案
- [ ] 定义 API 接口的详细契约（Request/Response DTO）

**Phase 1（MVP）- 3-5天**
- [ ] 实现核心数据库表（user_balance, coin_transaction, balance_change_log）
- [ ] 实现基础 Service 层（CoinBalanceService, CoinTransactionService）
- [ ] 实现系统发放和余额查询 API
- [ ] 在用户注册流程中集成注册奖励

**Phase 2（论坛集成）- 3-5天**
- [ ] 在 PostService.ToggleLikeAsync 中集成点赞奖励
- [ ] 在 CommentService.AddCommentAsync 中集成评论奖励
- [ ] 实现神评/沙发的萝卜币奖励（基于 CommentHighlightJob）
- [ ] 添加交易历史查询 API

**Phase 3（转账功能）- 2-3天**
- [ ] 实现用户间转账 API
- [ ] 实现手续费计算逻辑
- [ ] 添加转账限额和防刷机制

**Phase 4（对账与监控）- 2-3天**
- [ ] 实现每日对账定时任务
- [ ] 添加对账日志和告警机制
- [ ] 实现管理员调账 API（Admin）

### 16.8 补充建议

1. **多租户支持**
   - 在 `user_balance` 和 `coin_transaction` 表中添加 `tenant_id` 字段
   - 平台账户在每个租户下独立（tenant_id + user_id = 1 的组合）

2. **国际化支持**
   - 萝卜币的显示名称（"胡萝卜"/"白萝卜"）使用 i18n 资源文件
   - 支持 zh-CN, en-US 等语言

3. **前端展示**
   - 在用户个人中心添加"萝卜币钱包"页面
   - 展示余额、交易历史、收支统计图表

4. **测试覆盖**
   - 并发转账的幂等性测试
   - 余额不足的异常处理测试
   - 对账逻辑的准确性测试

### 16.9 前端展示优化

**问题**：用户对萝卜币的感知和参与度直接影响系统的活跃度，需要优化前端展示体验。

**建议方案**：

1. **实时余额更新（WebSocket/SignalR）**

   **问题场景**：
   - 用户 A 在个人中心查看余额时，其他用户打赏或点赞导致余额变化，但页面不刷新看不到
   - 用户完成任务后，需要手动刷新才能看到奖励到账

   **解决方案**：
   ```csharp
   // 后端 - SignalR Hub
   public class CoinNotificationHub : Hub
   {
       private readonly IHubContext<CoinNotificationHub> _hubContext;

       // 余额变动通知
       public async Task NotifyBalanceChangeAsync(long userId, long newBalance, long changeAmount, string reason)
       {
           await _hubContext.Clients.User(userId.ToString()).SendAsync("BalanceChanged", new
           {
               NewBalance = newBalance,
               ChangeAmount = changeAmount,
               Reason = reason,
               Timestamp = DateTime.Now
           });
       }
   }

   // 在 CoinTransactionService 中集成
   public async Task<long> CreateTransactionAsync(CoinTransaction transaction)
   {
       // ... 交易逻辑

       // 发送实时通知
       if (transaction.ToUserId.HasValue)
       {
           await _coinNotificationHub.NotifyBalanceChangeAsync(
               transaction.ToUserId.Value,
               newBalance,
               transaction.Amount,
               transaction.Remark
           );
       }

       return transactionId;
   }
   ```

   ```typescript
   // 前端 - React Hook
   import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
   import { useEffect, useState } from 'react';

   export function useCoinBalance() {
       const [balance, setBalance] = useState(0);
       const [connection, setConnection] = useState<HubConnection | null>(null);

       useEffect(() => {
           const conn = new HubConnectionBuilder()
               .withUrl('/hub/coin-notification', {
                   accessTokenFactory: () => localStorage.getItem('access_token') || ''
               })
               .withAutomaticReconnect()
               .build();

           conn.on('BalanceChanged', (data: {
               NewBalance: number;
               ChangeAmount: number;
               Reason: string;
               Timestamp: string;
           }) => {
               setBalance(data.NewBalance);

               // 显示动画提示
               if (data.ChangeAmount > 0) {
                   showCoinAnimation(data.ChangeAmount, data.Reason);
               }
           });

           conn.start().catch(console.error);
           setConnection(conn);

           return () => { conn.stop(); };
       }, []);

       return { balance, connection };
   }
   ```

2. **萝卜币飞入动画**

   **视觉效果**：当用户获得萝卜币时，显示金币从触发位置飞向余额显示区的动画。

   ```tsx
   // radish.client/src/components/CoinAnimation/CoinFlyAnimation.tsx
   import React, { useState, useEffect } from 'react';
   import styles from './CoinFlyAnimation.module.css';

   interface CoinFlyAnimationProps {
       amount: number;
       fromX: number;  // 起始 X 坐标
       fromY: number;  // 起始 Y 坐标
       toX: number;    // 目标 X 坐标（余额显示位置）
       toY: number;    // 目标 Y 坐标
       onComplete?: () => void;
   }

   export const CoinFlyAnimation: React.FC<CoinFlyAnimationProps> = ({
       amount, fromX, fromY, toX, toY, onComplete
   }) => {
       const [isVisible, setIsVisible] = useState(true);

       useEffect(() => {
           const timer = setTimeout(() => {
               setIsVisible(false);
               onComplete?.();
           }, 1000);

           return () => clearTimeout(timer);
       }, [onComplete]);

       if (!isVisible) return null;

       return (
           <div
               className={styles.coinFly}
               style={{
                   '--from-x': `${fromX}px`,
                   '--from-y': `${fromY}px`,
                   '--to-x': `${toX}px`,
                   '--to-y': `${toY}px`,
               } as React.CSSProperties}
           >
               <div className={styles.coin}>
                   <img src="/assets/carrot-icon.png" alt="胡萝卜" />
                   <span className={styles.amount}>+{amount}</span>
               </div>
           </div>
       );
   };
   ```

   ```css
   /* CoinFlyAnimation.module.css */
   .coinFly {
       position: fixed;
       z-index: 9999;
       pointer-events: none;
       animation: flyToWallet 1s cubic-bezier(0.25, 0.46, 0.45, 0.94) forwards;
   }

   @keyframes flyToWallet {
       0% {
           transform: translate(var(--from-x), var(--from-y)) scale(1);
           opacity: 1;
       }
       50% {
           transform: translate(
               calc((var(--from-x) + var(--to-x)) / 2),
               calc((var(--from-y) + var(--to-y)) / 2 - 50px)
           ) scale(1.2);
           opacity: 1;
       }
       100% {
           transform: translate(var(--to-x), var(--to-y)) scale(0.3);
           opacity: 0;
       }
   }

   .coin {
       display: flex;
       align-items: center;
       gap: 4px;
       background: linear-gradient(135deg, #ffd700, #ffed4e);
       border-radius: 50%;
       padding: 8px;
       box-shadow: 0 4px 12px rgba(255, 215, 0, 0.5);
   }

   .amount {
       font-weight: bold;
       color: #ff6b00;
       font-size: 16px;
       text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
   }
   ```

3. **余额趋势图表（Recharts）**

   ```tsx
   // radish.client/src/apps/profile/components/CoinBalanceChart.tsx
   import React, { useEffect, useState } from 'react';
   import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts';
   import { getCoinBalanceHistory } from '@/api/coin';

   interface BalanceHistory {
       date: string;
       balance: number;
       earned: number;
       spent: number;
   }

   export const CoinBalanceChart: React.FC = () => {
       const [data, setData] = useState<BalanceHistory[]>([]);
       const [period, setPeriod] = useState<'week' | 'month' | 'year'>('week');

       useEffect(() => {
           const fetchData = async () => {
               const response = await getCoinBalanceHistory(period);
               setData(response.data);
           };

           fetchData();
       }, [period]);

       return (
           <div className="coin-balance-chart">
               <div className="chart-header">
                   <h3>萝卜币趋势</h3>
                   <div className="period-selector">
                       <button onClick={() => setPeriod('week')} className={period === 'week' ? 'active' : ''}>
                           近7天
                       </button>
                       <button onClick={() => setPeriod('month')} className={period === 'month' ? 'active' : ''}>
                           近30天
                       </button>
                       <button onClick={() => setPeriod('year')} className={period === 'year' ? 'active' : ''}>
                           近一年
                       </button>
                   </div>
               </div>

               <ResponsiveContainer width="100%" height={300}>
                   <LineChart data={data} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                       <CartesianGrid strokeDasharray="3 3" />
                       <XAxis dataKey="date" />
                       <YAxis />
                       <Tooltip
                           formatter={(value: number, name: string) => {
                               const labels: Record<string, string> = {
                                   balance: '余额',
                                   earned: '获得',
                                   spent: '消费'
                               };
                               return [`${value} 胡萝卜`, labels[name]];
                           }}
                       />
                       <Line type="monotone" dataKey="balance" stroke="#ff6b00" strokeWidth={2} name="余额" />
                       <Line type="monotone" dataKey="earned" stroke="#52c41a" strokeWidth={2} name="获得" />
                       <Line type="monotone" dataKey="spent" stroke="#f5222d" strokeWidth={2} name="消费" />
                   </LineChart>
               </ResponsiveContainer>
           </div>
       );
   };
   ```

4. **Toast 通知组件**

   ```tsx
   // radish.client/src/components/CoinToast/CoinToast.tsx
   import React from 'react';
   import { Toast } from '@radish/ui';
   import styles from './CoinToast.module.css';

   interface CoinToastProps {
       amount: number;
       reason: string;
       type: 'earn' | 'spend' | 'transfer';
       visible: boolean;
       onClose: () => void;
   }

   export const CoinToast: React.FC<CoinToastProps> = ({
       amount, reason, type, visible, onClose
   }) => {
       const icons = {
           earn: '🎉',
           spend: '💸',
           transfer: '💰'
       };

       const colors = {
           earn: '#52c41a',
           spend: '#f5222d',
           transfer: '#1890ff'
       };

       return (
           <Toast
               visible={visible}
               onClose={onClose}
               duration={3000}
               position="top-right"
           >
               <div className={styles.coinToast} style={{ borderColor: colors[type] }}>
                   <div className={styles.icon}>{icons[type]}</div>
                   <div className={styles.content}>
                       <div className={styles.amount} style={{ color: colors[type] }}>
                           {type === 'earn' ? '+' : '-'}{amount} 胡萝卜
                       </div>
                       <div className={styles.reason}>{reason}</div>
                   </div>
               </div>
           </Toast>
       );
   };
   ```

5. **萝卜币钱包页面（完整示例）**

   ```tsx
   // radish.client/src/apps/profile/pages/CoinWallet.tsx
   import React from 'react';
   import { useCoinBalance } from '@/hooks/useCoinBalance';
   import { CoinBalanceChart } from '../components/CoinBalanceChart';
   import { CoinTransactionList } from '../components/CoinTransactionList';
   import styles from './CoinWallet.module.css';

   export const CoinWallet: React.FC = () => {
       const { balance, loading } = useCoinBalance();

       return (
           <div className={styles.coinWallet}>
               <div className={styles.balanceCard}>
                   <div className={styles.balanceHeader}>
                       <img src="/assets/carrot-icon.png" alt="胡萝卜" className={styles.icon} />
                       <h2>我的萝卜币</h2>
                   </div>

                   <div className={styles.balanceDisplay}>
                       <div className={styles.mainBalance}>
                           {loading ? (
                               <div className={styles.skeleton} />
                           ) : (
                               <>
                                   <span className={styles.amount}>{balance.toLocaleString()}</span>
                                   <span className={styles.unit}>胡萝卜</span>
                               </>
                           )}
                       </div>
                       <div className={styles.radishEquivalent}>
                           ≈ {(balance / 1000).toFixed(3)} 白萝卜
                       </div>
                   </div>

                   <div className={styles.stats}>
                       <div className={styles.statItem}>
                           <span className={styles.label}>累计获得</span>
                           <span className={styles.value}>12,345</span>
                       </div>
                       <div className={styles.statItem}>
                           <span className={styles.label}>累计消费</span>
                           <span className={styles.value}>3,456</span>
                       </div>
                       <div className={styles.statItem}>
                           <span className={styles.label}>今日获得</span>
                           <span className={styles.value}>+123</span>
                       </div>
                   </div>
               </div>

               <div className={styles.chartSection}>
                   <CoinBalanceChart />
               </div>

               <div className={styles.transactionSection}>
                   <h3>交易记录</h3>
                   <CoinTransactionList />
               </div>
           </div>
       );
   };
   ```

6. **后端 API 支持（余额历史）**

   ```csharp
   // Radish.Api/Controllers/v2/CoinController.cs
   [HttpGet("api/v2/Coin/BalanceHistory")]
   [Authorize(Policy = "Client")]
   public async Task<MessageModel> GetBalanceHistory([FromQuery] string period = "week")
   {
       var userId = long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

       var startDate = period switch
       {
           "week" => DateTime.Today.AddDays(-7),
           "month" => DateTime.Today.AddDays(-30),
           "year" => DateTime.Today.AddYears(-1),
           _ => DateTime.Today.AddDays(-7)
       };

       // 按日期聚合余额历史
       var history = await _db.Queryable<BalanceChangeLog>()
           .Where(l => l.UserId == userId && l.CreatedAt >= startDate)
           .GroupBy(l => l.CreatedAt.Date)
           .Select(g => new
           {
               Date = g.Key.ToString("yyyy-MM-dd"),
               Earned = g.Sum(l => l.ChangeAmount > 0 ? l.ChangeAmount : 0),
               Spent = g.Sum(l => l.ChangeAmount < 0 ? -l.ChangeAmount : 0),
               Balance = g.Max(l => l.BalanceAfter)
           })
           .ToListAsync();

       return Success(history);
   }
   ```

**实施优先级**：
- **P0（MVP）**：Toast 通知、基础余额显示
- **P1**：WebSocket 实时更新、交易记录列表
- **P2**：萝卜币飞入动画、余额趋势图表
- **P3**：高级统计分析、自定义图表周期

---

### 16.10 性能测试与容量规划

**问题**：萝卜币系统作为核心功能，需要确保在高并发场景下的稳定性和性能。

**建议方案**：

1. **性能测试指标**

   **关键性能指标（KPI）**：
   | 指标 | 目标值 | 测试方法 |
   |-----|--------|---------|
   | **转账 TPS** | ≥ 1000 次/秒 | JMeter 压测 |
   | **余额查询 QPS** | ≥ 5000 次/秒 | Redis 缓存命中率 ≥ 95% |
   | **对账任务耗时** | ≤ 5 分钟（10万用户） | Hangfire 监控 |
   | **交易成功率** | ≥ 99.9% | 排除余额不足等业务失败 |
   | **平均响应时间** | ≤ 200ms（P95） | APM 工具监控 |
   | **数据库连接池** | ≤ 80% 使用率 | SqlSugar AOP 监控 |

2. **压力测试脚本（JMeter）**

   **场景1：高并发转账**
   ```xml
   <!-- JMeter Test Plan: Coin Transfer Stress Test -->
   <jmeterTestPlan version="1.2" properties="5.0">
       <hashTree>
           <ThreadGroup guiclass="ThreadGroupGui" testname="Transfer Test">
               <intProp name="ThreadGroup.num_threads">500</intProp>
               <intProp name="ThreadGroup.ramp_time">10</intProp>
               <longProp name="ThreadGroup.duration">60</longProp>

               <HTTPSamplerProxy>
                   <elementProp name="HTTPsampler.Arguments">
                       <collectionProp>
                           <elementProp>
                               <stringProp name="Argument.name">toUserId</stringProp>
                               <stringProp name="Argument.value">${__Random(1,10000)}</stringProp>
                           </elementProp>
                           <elementProp>
                               <stringProp name="Argument.name">amount</stringProp>
                               <stringProp name="Argument.value">${__Random(10,1000)}</stringProp>
                           </elementProp>
                       </collectionProp>
                   </elementProp>
                   <stringProp name="HTTPSampler.path">/api/v2/Coin/Transfer</stringProp>
                   <stringProp name="HTTPSampler.method">POST</stringProp>
               </HTTPSamplerProxy>
           </ThreadGroup>
       </hashTree>
   </jmeterTestPlan>
   ```

   **场景2：混合负载测试**
   - 60% 余额查询（轻量级）
   - 25% 交易记录查询（中量级）
   - 10% 转账操作（重量级）
   - 5% 对账操作（极重量级）

3. **容量规划模型**

   **数据库容量估算**：
   ```csharp
   public class CoinSystemCapacityPlanner
   {
       // 假设参数
       private const int DAILY_ACTIVE_USERS = 100_000;        // 日活用户
       private const int AVG_TRANSACTIONS_PER_USER = 5;       // 人均日交易数
       private const int TRANSACTION_RECORD_SIZE = 500;       // 单条交易记录大小（字节）
       private const int BALANCE_LOG_SIZE = 300;              // 单条余额日志大小（字节）

       public CapacityReport Calculate(int months)
       {
           var dailyTransactions = DAILY_ACTIVE_USERS * AVG_TRANSACTIONS_PER_USER;
           var totalDays = months * 30;
           var totalTransactions = dailyTransactions * totalDays;

           // 交易记录表容量（按月分表）
           var transactionTableSize = totalTransactions * TRANSACTION_RECORD_SIZE;

           // 余额变动日志表容量（每笔交易至少2条日志）
           var balanceLogSize = totalTransactions * 2 * BALANCE_LOG_SIZE;

           // 索引开销（估算 30%）
           var indexOverhead = (transactionTableSize + balanceLogSize) * 0.3;

           return new CapacityReport
           {
               TotalTransactions = totalTransactions,
               TransactionTableSizeGB = transactionTableSize / 1024.0 / 1024.0 / 1024.0,
               BalanceLogSizeGB = balanceLogSize / 1024.0 / 1024.0 / 1024.0,
               TotalStorageGB = (transactionTableSize + balanceLogSize + indexOverhead) / 1024.0 / 1024.0 / 1024.0,
               RecommendedShardingStrategy = totalTransactions > 100_000_000
                   ? "建议启用分库分表（按月分表 + 按用户ID哈希分库）"
                   : "当前单库单表可支撑"
           };
       }
   }

   // 输出示例（12 个月）：
   // 总交易数：1.8 亿笔
   // 交易表容量：84 GB
   // 日志表容量：100 GB
   // 总存储需求：239 GB（含索引）
   // 分片建议：建议启用分库分表
   ```

4. **缓存容量规划**

   **Redis 内存估算**：
   ```
   余额缓存：
   - Key 格式: "coin:balance:{userId}"
   - 单条大小: 100 字节（Key + Value + 元数据）
   - 10 万活跃用户 × 100 字节 ≈ 10 MB

   配额计数器：
   - Key 格式: "quota:transfer:count:{tenantId}:{userId}:{date}"
   - 单条大小: 80 字节
   - 10 万用户 × 2 个 Key（count + amount） × 80 字节 ≈ 16 MB

   交易幂等键：
   - Key 格式: "tx:idempotency:{transactionNo}"
   - 过期时间: 24 小时
   - 日均 50 万笔交易 × 120 字节 ≈ 60 MB

   总计：~100 MB（建议预留 1 GB Redis 内存用于萝卜币系统）
   ```

5. **数据库连接池配置**

   ```json
   // appsettings.Production.json
   {
       "Databases": [
           {
               "ConnId": "Main",
               "DbType": 4,
               "ConnectionString": "...",
               "MaxConnectionPoolSize": 200,       // 最大连接数
               "MinConnectionPoolSize": 10,        // 最小连接数
               "ConnectionTimeout": 30,
               "CommandTimeout": 60
           }
       ]
   }
   ```

   **连接池监控**：
   ```csharp
   public class DatabaseConnectionMonitor
   {
       [AutomaticRetry(Attempts = 0)]
       public async Task MonitorConnectionPoolAsync()
       {
           var db = _db.AsTenant().GetConnection("Main");
           var poolStats = db.Ado.GetDataTable("SHOW STATUS LIKE 'Threads_connected'");
           var threadsConnected = int.Parse(poolStats.Rows[0]["Value"].ToString());

           if (threadsConnected > 160) // 80% of 200
           {
               await _alertService.SendAlertAsync(
                   $"数据库连接池使用率过高: {threadsConnected}/200 ({threadsConnected * 100 / 200}%)"
               );
           }
       }
   }
   ```

6. **性能优化 Checklist**

   - [ ] **数据库索引优化**
     - `coin_transaction(from_user_id, created_at)` - 转账发起人查询
     - `coin_transaction(to_user_id, created_at)` - 收款人查询
     - `balance_change_log(user_id, created_at)` - 余额历史查询
     - `user_balance(balance)` - 富豪榜查询

   - [ ] **缓存策略**
     - 用户余额缓存（TTL: 5 分钟，LRU 淘汰）
     - 租户配额缓存（TTL: 1 小时）
     - 热点交易记录缓存（最近 100 条）

   - [ ] **异步化改造**
     - 奖励发放：同步写库 → 消息队列异步处理
     - 神评/沙发统计：实时计算 → 定时任务批量计算
     - 对账任务：串行执行 → 并行分片对账

   - [ ] **分库分表**
     - 交易记录表：按月分表（`coin_transaction_202501`）
     - 余额变动日志：按月分表 + 按用户ID哈希分库
     - 对账报告：独立存储（归档到对象存储）

   - [ ] **限流保护**
     - 用户级限流：每秒 10 次转账请求（滑动窗口）
     - 租户级限流：按配额等级动态调整
     - 全局限流：转账 API 总 QPS 不超过 5000

7. **容灾与高可用**

   **数据库主从复制**：
   - 主库（Master）：处理所有写操作
   - 从库（Slave）：处理读操作（余额查询、交易记录查询）
   - 读写分离：SqlSugar `SlaveConnectionConfigs` 配置

   **Redis 高可用**：
   - Redis Sentinel 模式（1 主 + 2 从 + 3 哨兵）
   - 自动故障转移（Failover）
   - 配置示例：
     ```json
     {
         "Redis": {
             "Enable": true,
             "Sentinel": {
                 "MasterName": "radish-coin-master",
                 "Endpoints": [
                     "sentinel1:26379",
                     "sentinel2:26379",
                     "sentinel3:26379"
                 ]
             }
         }
     }
     ```

**压测时间表**：
- **Phase 1（开发环境）**：单接口压测，确认基准性能
- **Phase 2（预发布环境）**：混合负载测试，模拟真实流量
- **Phase 3（生产环境灰度）**：10% 真实流量验证
- **Phase 4（全量上线）**：持续监控，逐步放开限流

---

## 17. 参考资料

- [支付宝积分规则](https://render.alipay.com/p/f/fd-izto3ght/index.html)
- [微信支付分设计](https://pay.weixin.qq.com/index.php/public/wechatpay_score)
- [虚拟货币监管政策](https://www.gov.cn/zhengce/)
- [电子商务法](http://www.npc.gov.cn/npc/c30834/201809/e1e066e1c8b24c6e8a4e3c3f14d1b9c8.shtml)

---

**文档版本**：v1.2
**创建日期**：2025-12-28
**最后更新**：2026-01-03
**负责人**：待定
**审核状态**：已更新（奖励数额已调整，注册奖励集成指南已合并）
