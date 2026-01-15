# 12-15. 开发计划、风险合规与落地清单

> 入口页：[萝卜币系统设计方案](/guide/radish-coin-system)

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

