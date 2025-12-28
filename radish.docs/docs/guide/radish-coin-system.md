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

### 3.2 互动奖励

| 行为 | 获得方 | 奖励金额 | 说明 |
|------|--------|----------|------|
| **发布帖子** | 作者 | 待定 | 鼓励内容创作 |
| **被点赞** | 作者 | `2 胡萝卜/次` | 内容质量认可 |
| **点赞他人** | 点赞者 | `1 胡萝卜/次` | 鼓励互动（每日上限） |
| **评论被回复** | 评论者 | `1 胡萝卜/次` | 促进讨论 |
| **神评/沙发** | 评论者 | 待定 | 需重新设计机制 |

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

### 4.2 手续费规则

**计算公式**：
```
手续费 = max(转账金额 × 费率, 最低手续费)
```

**费率表**：
| 转账金额（胡萝卜） | 费率 | 最低手续费 |
|-------------------|------|-----------|
| 10 - 99 | 10% | 1 胡萝卜 |
| 100 - 999 | 5% | 10 胡萝卜 |
| 1000+ | 3% | 50 胡萝卜 |

**示例**：
- 转账 `50 胡萝卜`：手续费 = `max(50 × 10%, 1)` = `5 胡萝卜`
- 转账 `500 胡萝卜`：手续费 = `max(500 × 5%, 10)` = `25 胡萝卜`
- 转账 `5000 胡萝卜`：手续费 = `max(5000 × 3%, 50)` = `150 胡萝卜`

**手续费向上取整**，确保平台收入不因精度问题损失。

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

## 14. 讨论问题清单

### 14.1 待确认的设计细节

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

### 14.2 技术选型

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

## 15. 参考资料

- [支付宝积分规则](https://render.alipay.com/p/f/fd-izto3ght/index.html)
- [微信支付分设计](https://pay.weixin.qq.com/index.php/public/wechatpay_score)
- [虚拟货币监管政策](https://www.gov.cn/zhengce/)
- [电子商务法](http://www.npc.gov.cn/npc/c30834/201809/e1e066e1c8b24c6e8a4e3c3f14d1b9c8.shtml)

---

**文档版本**：v1.0
**创建日期**：2025-12-28
**最后更新**：2025-12-28
**负责人**：待定
**审核状态**：待讨论
