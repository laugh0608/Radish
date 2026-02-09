# 16. 实施落地待完善事项（2025-12-30 评审）：前端与性能

> 入口页：[萝卜币系统设计方案](/guide/radish-coin-system)

## 16.7 实施优先级建议

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

## 16.8 补充建议

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

## 16.9 前端展示优化

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
   // Frontend/radish.client/src/components/CoinAnimation/CoinFlyAnimation.tsx
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
   // Frontend/radish.client/src/apps/profile/components/CoinBalanceChart.tsx
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
   // Frontend/radish.client/src/components/CoinToast/CoinToast.tsx
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
   // Frontend/radish.client/src/apps/profile/pages/CoinWallet.tsx
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
   // Radish.Api/Controllers/v1/CoinController.cs
   [HttpGet("api/v1/Coin/BalanceHistory")]
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

## 16.10 性能测试与容量规划

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
                   <stringProp name="HTTPSampler.path">/api/v1/Coin/Transfer</stringProp>
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

