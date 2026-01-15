# 9-11. 安全性、未来扩展与技术实现要点

> 入口页：[萝卜币系统设计方案](/guide/radish-coin-system)

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

