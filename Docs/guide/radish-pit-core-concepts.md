# 萝卜坑应用核心概念

> 版本：v1.0 | 最后更新：2026-01-24 | 状态：设计中

本文档定义萝卜坑应用的核心概念、数据结构和业务规则。

---

## 2. 核心概念

### 2.1 基础概念定义

#### 萝卜币货币体系
- **胡萝卜**：系统最小货币单位，用于存储和计算
- **白萝卜**：用户显示单位，1白萝卜 = 1000胡萝卜
- **转换规则**：系统内部使用胡萝卜，用户界面显示白萝卜

#### 账户类型
- **个人账户**：普通用户的萝卜币账户
- **系统账户**：用于奖励发放的虚拟账户
- **冻结账户**：临时冻结资金的特殊状态

#### 交易类型枚举

```typescript
enum TransactionType {
  SYSTEM_REWARD = 'system_reward',      // 系统奖励
  LIKE_REWARD = 'like_reward',          // 点赞奖励
  COMMENT_REWARD = 'comment_reward',    // 评论奖励
  TRANSFER_IN = 'transfer_in',          // 转账收入
  TRANSFER_OUT = 'transfer_out',        // 转账支出
  ADMIN_ADJUST = 'admin_adjust',        // 管理员调整
  FREEZE = 'freeze',                    // 资金冻结
  UNFREEZE = 'unfreeze'                 // 资金解冻
}
```

#### 交易状态枚举

```typescript
enum TransactionStatus {
  PENDING = 'pending',        // 待处理
  PROCESSING = 'processing',  // 处理中
  SUCCESS = 'success',        // 成功
  FAILED = 'failed',         // 失败
  CANCELLED = 'cancelled'     // 已取消
}
```

### 2.2 数据结构定义

#### 用户余额结构 (UserBalance)

```typescript
interface UserBalance {
  id: number;                    // 主键ID
  userId: number;                // 用户ID
  balance: number;               // 可用余额(胡萝卜)
  frozenBalance: number;         // 冻结余额(胡萝卜)
  totalEarned: number;           // 累计获得(胡萝卜)
  totalSpent: number;            // 累计消费(胡萝卜)
  totalTransferredIn: number;    // 累计转入(胡萝卜)
  totalTransferredOut: number;   // 累计转出(胡萝卜)
  version: number;               // 乐观锁版本号
  createTime: Date;              // 创建时间
  modifyTime: Date;              // 修改时间
}
```

#### 交易记录结构 (CoinTransaction)

```typescript
interface CoinTransaction {
  id: number;                    // 主键ID
  transactionNo: string;         // 交易流水号
  fromUserId?: number;           // 发起方用户ID
  toUserId?: number;             // 接收方用户ID
  amount: number;                // 交易金额(胡萝卜)
  fee: number;                   // 手续费(胡萝卜)
  theoreticalAmount: number;     // 理论金额(白萝卜)
  roundingDiff: number;          // 舍入差额(白萝卜)
  transactionType: TransactionType; // 交易类型
  status: TransactionStatus;     // 交易状态
  businessType?: string;         // 业务类型
  businessId?: number;           // 业务ID
  remark?: string;               // 备注
  createTime: Date;              // 创建时间
}
```

#### 支付密码结构 (UserPaymentPassword)

```typescript
interface UserPaymentPassword {
  id: number;                    // 主键ID
  userId: number;                // 用户ID
  passwordHash: string;          // 密码哈希值
  salt: string;                  // 盐值
  failedAttempts: number;        // 失败尝试次数
  lockedUntil?: Date;            // 锁定到期时间
  lastUsedTime?: Date;           // 最后使用时间
  createTime: Date;              // 创建时间
  modifyTime: Date;              // 修改时间
}
```

### 2.3 业务规则定义

#### 转账规则

| 规则项 | 限制值 | 说明 |
|--------|--------|------|
| 单笔转账最小金额 | 1胡萝卜 | 避免微小金额转账 |
| 单笔转账最大金额 | 100万胡萝卜 | 防止大额异常转账 |
| 日累计转账限额 | 500万胡萝卜 | 单日转账总额限制 |
| 转账手续费 | 0胡萝卜 | 当前免手续费 |
| 转账冷却时间 | 10秒 | 防止频繁操作 |

#### 支付密码规则

| 规则项 | 限制值 | 说明 |
|--------|--------|------|
| 密码长度 | 6位数字 | 纯数字密码 |
| 失败锁定次数 | 5次 | 连续失败5次锁定 |
| 锁定时间 | 30分钟 | 自动解锁时间 |
| 密码有效期 | 90天 | 定期提醒更新 |
| 重复密码限制 | 不能与最近3次相同 | 防止密码重复使用 |

#### 余额计算规则

```typescript
// 总余额 = 可用余额 + 冻结余额
totalBalance = balance + frozenBalance

// 可转账金额 = 可用余额
availableForTransfer = balance

// 累计净收入 = 累计获得 - 累计消费
netIncome = totalEarned - totalSpent

// 累计净转账 = 累计转入 - 累计转出
netTransfer = totalTransferredIn - totalTransferredOut
```

### 2.4 状态机定义

#### 交易状态流转

```
[PENDING] ──验证通过──> [PROCESSING] ──执行成功──> [SUCCESS]
    │                        │
    │                        └──执行失败──> [FAILED]
    │
    └──验证失败/用户取消──> [CANCELLED]
```

#### 支付密码状态流转

```
[NORMAL] ──输入错误──> [FAILED_ATTEMPT] ──达到上限──> [LOCKED]
    │                        │                      │
    │                        └──重置计数──> [NORMAL] │
    │                                              │
    └──管理员解锁/时间到期──<─────────────────────────┘
```

### 2.5 安全机制

#### 并发控制
- **乐观锁**：使用版本号防止并发修改
- **重试机制**：失败时指数退避重试（最多3次）
- **事务隔离**：确保转账操作的原子性

#### 数据完整性
- **余额一致性**：转账前后总金额保持不变
- **审计追踪**：所有操作记录完整日志
- **数据校验**：关键数据多重校验

#### 安全防护
- **密码加密**：BCrypt哈希存储
- **防暴力破解**：失败次数限制和锁定机制
- **操作日志**：完整的用户操作记录

---

## 相关文档
- [萝卜坑应用总体设计](/guide/radish-pit-system)
- [萝卜坑功能模块](/guide/radish-pit-game-mechanics)
- [萝卜币系统](/guide/coin-system)

---

> 本文档随萝卜坑应用开发持续更新，如有变更请同步修改 [更新日志](/changelog/)。