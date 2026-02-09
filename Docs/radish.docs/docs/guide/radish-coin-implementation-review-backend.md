# 16. 实施落地待完善事项（2025-12-30 评审）：后端与接口

> 入口页：[萝卜币系统设计方案](/guide/radish-coin-system)

## 16.1 与现有论坛功能的集成细节

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

## 16.2 神评/沙发的萝卜币奖励机制

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

## 16.3 平台账户的具体实现

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

## 16.4 并发冲突的重试策略

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

## 16.5 API 接口详细契约

**问题**：文档第 14.6 节列出了 API 列表，但缺少详细的请求/响应示例。

**补充示例**：

1. **转账接口**
   ```http
   POST /api/v1/Coin/Transfer
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
   GET /api/v1/Coin/Balance
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

## 16.6 定时任务的监控机制

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

