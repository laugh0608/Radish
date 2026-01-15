# 3-5. 获取、流转与消费机制

> 入口页：[萝卜币系统设计方案](/guide/radish-coin-system)

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
| **神评** | 评论者 | 基础 `+8` + 点赞加成 `+5/点赞` + 保留奖励 `+15/周` | 详见 [16.2 神评/沙发的萝卜币奖励机制](/guide/radish-coin-implementation-review-backend) |
| **沙发** | 评论者 | 基础 `+5` + 点赞加成 `+3/点赞` + 保留奖励 `+10/周` | 详见 [16.2 神评/沙发的萝卜币奖励机制](/guide/radish-coin-implementation-review-backend) |

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
   [HttpPost("api/v1/Coin/Admin/UpdateTenantQuota")]
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
