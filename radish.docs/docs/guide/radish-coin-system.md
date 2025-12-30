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

### 14.5 对账口径与定时任务（上线前必须有）

1. **资金守恒公式落地**
   - 需要明确“发行量/消耗量”的口径来自哪些 `transaction_type`。

2. **每日对账任务**
   - 建议实现一个 Hangfire Job：
     - 汇总用户余额、平台余额、当日发行/消耗，写入 `daily_balance_report`。
     - 差异非 0：告警 + 标记 UNBALANCED。

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

**问题**：文档第 15.3 节标注为"待定"，但这是论坛的核心激励机制。

**建议方案**：

1. **神评奖励规则**
   - 基础奖励：500 胡萝卜（0.5 白萝卜）
   - 点赞加成：每个点赞 +10 胡萝卜
   - 示例：一条点赞数为 50 的神评可获得 500 + 50×10 = 1000 胡萝卜（1 白萝卜）

2. **沙发奖励规则**
   - 基础奖励：300 胡萝卜（0.3 白萝卜）
   - 点赞加成：每个点赞 +5 胡萝卜
   - 示例：一条点赞数为 30 的沙发可获得 300 + 30×5 = 450 胡萝卜（0.45 白萝卜）

3. **发放时机**
   - 实时发放：当评论成为神评/沙发时立即发放基础奖励
   - 每日结算：每天凌晨 2 点统计昨日点赞增量，发放加成奖励
   - 失去地位不扣回：一旦发放不再追回（避免用户体验问题）

4. **数据库设计**
   ```sql
   -- 在 CommentHighlight 表中记录奖励发放
   ALTER TABLE comment_highlight ADD COLUMN coin_rewarded BOOLEAN DEFAULT FALSE;
   ALTER TABLE comment_highlight ADD COLUMN coin_amount BIGINT DEFAULT 0;
   ALTER TABLE comment_highlight ADD COLUMN last_reward_at TIMESTAMP;
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

---

## 17. 参考资料

- [支付宝积分规则](https://render.alipay.com/p/f/fd-izto3ght/index.html)
- [微信支付分设计](https://pay.weixin.qq.com/index.php/public/wechatpay_score)
- [虚拟货币监管政策](https://www.gov.cn/zhengce/)
- [电子商务法](http://www.npc.gov.cn/npc/c30834/201809/e1e066e1c8b24c6e8a4e3c3f14d1b9c8.shtml)

---

**文档版本**：v1.1
**创建日期**：2025-12-28
**最后更新**：2025-12-30
**负责人**：待定
**审核状态**：评审中（已补充实施落地建议）
