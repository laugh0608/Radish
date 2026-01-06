# 2026年1月 第1周 (1月1日)

## 文档优化

### Gateway 文档正式化

**背景**：Gateway 项目已完成 Phase 0 实施，实现了统一服务入口、YARP 路由转发、健康检查聚合等核心功能。

**主要工作**：

1. **文档转换**
   - 将 `architecture/gateway-plan.md`（规划文档）转换为 `guide/gateway.md`（正式技术文档）
   - 移除所有规划性内容（Phase 0-6、技术选型对比、Blog.Core 对比等）
   - 保留并详细说明已实现的功能

2. **内容更新**（基于实际实现）
   - 完整的服务拓扑（10 个路由：docs、api、uploads、scalar、openapi、hangfire、console、Account、connect、frontend）
   - YARP 路由配置详解（路由匹配、路径转换、请求头转换、WebSocket 支持）
   - 核心功能说明（反向代理、健康检查、CORS、日志）
   - 开发指南（3 种启动方式、访问地址表格、调试技巧）
   - 部署指南（Docker、生产环境注意事项）
   - 常见问题（9 个实用 Q&A）

3. **文档引用更新**（共 12 处）
   - 根目录：CLAUDE.md、README.md、AGENTS.md、Docs/
   - 文档站：index.md、authentication.md、getting-started.md、framework.md、specifications.md、development-plan.md、open-platform.md
   - VitePress 配置：将文档从"架构设计"分类移到"开发指南"分类

**成果**：
- ✅ 删除旧规划文档 `architecture/gateway-plan.md`
- ✅ 创建新技术文档 `guide/gateway.md`（17KB，完整实用）
- ✅ 更新所有文档引用，确保链接正确
- ✅ 文档完全基于实际代码实现（Program.cs、appsettings.json）

**文档特点**：
- 📖 实用性强，包含大量代码示例和配置示例
- 🎯 完全反映当前实现状态
- 🔧 提供详细的开发和部署指南
- ❓ 包含常见问题的解决方案

---

## 前端修复

### 1月2日

- 修复用户头像不显示：补齐 `userStore` 头像字段，并在 Dock 侧处理相对 URL
- 优化论坛窗口化窄宽布局：基于容器宽度折叠左右分栏，优先展示帖子瀑布流

**提交信息**：docs: 将 Gateway 规划文档转换为正式技术文档

---

## 1月3日

### 萝卜币系统 MVP 完成

**核心功能实现**：

1. **萝卜币基础服务**
   - 实现 `CoinService` 和 `CoinBalanceService`
   - 用户余额管理（查询、增加、扣除）
   - 交易记录管理（分页查询、类型筛选）
   - 系统赠送功能（注册奖励 50 胡萝卜）

2. **论坛奖励服务**（`ICoinRewardService`）
   - 点赞奖励：被点赞 +2 胡萝卜，点赞者 +1 胡萝卜（每日上限 50）
   - 评论奖励：发表评论 +1 胡萝卜，评论被回复 +1 胡萝卜
   - 神评奖励：基础 +8 胡萝卜 + 点赞加成 +5/点赞
   - 沙发奖励：基础 +5 胡萝卜 + 点赞加成 +3/点赞
   - 防刷机制：每日点赞奖励上限、重复发放检测

3. **定时任务**
   - `CommentHighlightJob`：每日凌晨 2 点统计神评/沙发，发放基础奖励和点赞加成
   - `RetentionRewardJob`：每周执行，为保留的神评/沙发发放持续奖励（神评 +15/周，沙发 +10/周，最多 3 周）

4. **论坛功能集成**
   - `PostService.ToggleLikeAsync`：帖子点赞/取消点赞，发放点赞奖励
   - `CommentService.AddCommentAsync`：发表评论时发放评论奖励
   - `CommentService.ToggleLikeAsync`：评论点赞时发放奖励
   - 新增 `UserPostLike` 模型记录点赞关系

### 前端萝卜币集成

1. **通用组件**（@radish/ui）
   - 新增 Toast 通知组件（React Portal 渲染，发布订阅模式）
   - z-index 层级设置为 10001 高于 StatusBar 和 Dock

2. **萝卜币功能组件**
   - `CoinBalance`：顶栏余额显示组件
   - `CoinWallet`：个人中心钱包页面
   - `CoinTransactionList`：交易记录列表组件
   - 萝卜币奖励通知组件（features/coin）
   - 萝卜币演示应用（apps/coin-demo）

3. **API 客户端**
   - 新增 `coin.ts` 萝卜币 API 客户端
   - 集成 ToastContainer 到 Shell

### 架构优化

1. **BaseRepository 泛型聚合方法**
   - `QueryDistinctAsync`：查询不同的字段值（数据库级去重）
   - `QuerySumAsync`：字段求和聚合
   - 优势：减少内存占用和网络传输，避免 Service 层加载大量数据后在内存中过滤

2. **开发规范文档优化**
   - CLAUDE.md：804 行 → 274 行（-66%，节省约 35,000 tokens）
   - AGENTS.md：189 行 → 159 行（-16%）
   - 新增架构约束：Service 层禁止直接访问数据库实例
   - 明确仓储扩展策略优先级

### 认证功能修复

1. **注册功能完善**
   - 添加注册页面视图
   - 修复注册错误处理
   - 修复邮箱 null 值问题
   - 集成萝卜币注册奖励

2. **安全优化**
   - 移除登录页默认测试账号填充
   - 修复 User.UserBirth 字段的可空性定义

**提交记录**：
- `feat: 实现萝卜币系统 MVP`
- `feat(coin): 实现萝卜币论坛奖励服务`
- `feat(forum): 集成萝卜币奖励到帖子和评论功能`
- `feat(coin): 添加保留奖励定时任务并优化神评沙发统计`
- `feat: 注册神评萝卜奖励定时任务`
- `feat(client): 实现萝卜币前端集成`
- `feat: 扩展 BaseRepository 添加泛型聚合查询方法`
- `docs: 优化开发规范文档并强化分层架构约束`
- `feat(auth): 实现用户注册功能并集成萝卜币奖励`

---

## 1月4日

### 萝卜币精确计算系统完成

**背景**：为确保萝卜币交易的精度和可追溯性，避免浮点运算误差，实现了完整的精确计算系统。

#### 1. CoinCalculator 工具类（Radish.Common/Utils/CoinCalculator.cs）

**核心设计原则**：
1. **整数为王**：所有交易以胡萝卜（整数）为准，不产生小数
2. **统一舍入**：所有比例计算向下取整（Floor），确保可预测
3. **差额透明**：记录理论金额和实际金额，支持审计对账
4. **白萝卜展示**：仅用于前端显示（1 白萝卜 = 100 胡萝卜）

**主要功能**：
- **单位转换**：
  - `ToWhiteRadish(long carrot)` - 胡萝卜转白萝卜（向下取整）
  - `ToCarrot(decimal whiteRadish)` - 白萝卜转胡萝卜（向下取整）
- **显示格式化**：
  - `FormatDisplay(long carrot)` - 智能显示（< 1000 显示胡萝卜，>= 1000 显示混合格式）
  - `FormatAsWhiteRadish(long carrot)` - 紧凑显示（始终显示为白萝卜，带小数）
- **比例计算**：
  - `CalculateByRate(long amount, decimal rate)` - 按比例计算并记录舍入差额
  - `CalculateFee(long amount, decimal feeRate)` - 手续费计算（最小 1 胡萝卜，不足免收）
- **批量分配**：
  - `DistributeEqually(long total, int count)` - 均分（余额分配给前几项）
  - `DistributeByWeight(long total, List<int> weights)` - 按权重分配
- **累积计算器**：
  - `AccumulativeCalculator` - 处理小数累积（如每日利息 0.5 胡萝卜，累积到 1 才发放）
- **验证工具**：
  - `IsValidAmount(long amount)` - 金额有效性检查
  - `HasSufficientBalance(long balance, long amount)` - 余额充足性检查

**返回值设计**：
```csharp
public record CoinCalculationResult
{
    public decimal TheoreticalAmount { get; init; }  // 理论金额（保留小数）
    public long ActualAmount { get; init; }          // 实际金额（向下取整）
    public decimal RoundingDiff { get; init; }       // 舍入差额
}
```

#### 2. 单元测试全覆盖（CoinCalculatorTest.cs）

**测试覆盖**：52 个单元测试，所有测试通过 ✅

测试分类：
- **单位转换测试**（8 个）：
  - `ToWhiteRadish_ShouldConvertCorrectly` - 胡萝卜转白萝卜
  - `ToCarrot_ShouldFloorCorrectly` - 白萝卜转胡萝卜（验证向下取整）
  - `FormatDisplay_ShouldFormatCorrectly` - 智能显示格式（6 个测试用例）
  - `FormatAsWhiteRadish_ShouldFormatCorrectly` - 紧凑显示格式（5 个测试用例）

- **比例计算测试**（5 个）：
  - `CalculateByRate_ShouldCalculateCorrectly` - 比例计算准确性（4 个测试用例）
  - `CalculateByRate_ShouldThrow_WhenAmountIsNegative` - 负数金额异常
  - `CalculateByRate_ShouldThrow_WhenRateIsInvalid` - 无效比例异常

- **手续费计算测试**（5 个）：
  - `CalculateFee_ShouldApplyMinimumFeeRule` - 最小手续费规则（4 个测试用例）
  - `CalculateFee_ShouldRecordFullDiffWhenFeeWaived` - 免收手续费时差额记录

- **批量均分测试**（5 个）：
  - `DistributeEqually_ShouldDistributeCorrectly` - 均分分配（4 个测试用例）
  - `DistributeEqually_ShouldThrow_WhenCountIsZero` - 零分配数异常

- **按权重分配测试**（8 个）：
  - `DistributeByWeight_ShouldDistributeCorrectly` - 权重分配准确性
  - `DistributeByWeight_ShouldHandleRemainder` - 余额处理
  - `DistributeByWeight_ShouldThrow_WhenWeightsAreInvalid` - 无效权重异常（3 种情况）

- **累积计算器测试**（2 个）：
  - `AccumulativeCalculator_ShouldAccumulateCorrectly` - 累积计算准确性（4 个步骤）
  - `AccumulativeCalculator_Reset_ShouldClearAccumulated` - 重置功能

- **验证工具测试**（6 个）：
  - `IsValidAmount_ShouldValidateCorrectly` - 金额有效性验证（4 个测试用例）
  - `HasSufficientBalance_ShouldCheckCorrectly` - 余额充足性检查（4 个测试用例）

- **实际场景测试**（3 个）：
  - `Scenario_TransferWithFee_ShouldCalculateCorrectly` - 转账手续费场景
  - `Scenario_GodCommentRewardDistribution_ShouldDistributeCorrectly` - 神评奖励分配场景
  - `Scenario_DailyInterestAccumulation_ShouldAccumulateCorrectly` - 每日利息累积场景（10天模拟）

**测试执行结果**：
```bash
dotnet test Radish.Api.Tests --filter "FullyQualifiedName~CoinCalculatorTest"
# 结果：52 个测试全部通过，0 个失败
```

#### 3. 神评/沙发触发条件配置化

**问题**：神评和沙发的最小评论数阈值之前是硬编码（神评需 > 5 条父评论，沙发需 > 3 条子评论），不够灵活。

**解决方案**：
- 创建 `CommentHighlightOptions` 配置类（Radish.Common/OptionTool）
- 在 `appsettings.json` 中添加配置项：
  ```json
  "Hangfire": {
    "CommentHighlight": {
      "MinParentCommentCount": 5,  // 神评触发最小父评论数
      "MinChildCommentCount": 3    // 沙发触发最小子评论数
    }
  }
  ```
- 修改 `CommentService` 和 `CommentHighlightJob` 使用配置值替代硬编码

**影响范围**：
- `Radish.Service/CommentService.cs` - 实时神评/沙发检测
- `Radish.Service/Jobs/CommentHighlightJob.cs` - 定时统计任务

#### 4. 萝卜币文档完善

**文档更新**：
- 更新 `radish-coin-system.md` 第6节：精度处理与计算规范
  - 添加 5 大核心原则说明
  - 添加 CoinCalculator API 文档（12 个方法详解）
  - 添加使用场景示例（转账手续费、神评奖励分配、每日利息累积）
  - 添加数据库设计建议（理论金额和舍入差额字段）
  - 添加每日对账任务示例
  - 添加 DO/DON'T 最佳实践
- 添加第18节：M6 实施完成总结
  - 已完成功能清单
  - 技术亮点总结（精度保证、防刷机制、性能优化、配置灵活、测试覆盖）
  - 待优化项规划（M7+ 功能归属）
  - 验收结论：M6 验收标准 100% 达成

**文档版本**：v1.2 → v1.3（总行数：2715 行）

### 开发规划调整

**背景**：商城系统较复杂，需要更完善的基础设施支持（通知系统、等级系统）。

**调整方案**：
- **M6**（✅ 已完成）：聚焦萝卜币系统
- **M7**（⏳ 计划中）：消息通知系统（SignalR 实时推送，替代轮询）
- **M8**（⏳ 计划中）：用户等级与经验值系统（11 级修仙体系）
- **M9**（⏳ 计划中）：商城系统（依赖通知和等级系统）
- **M10/M11**：可观测性、测试、部署（原 M7/M8）

**优势**：
1. **依赖关系清晰**：通知系统 → 等级系统 → 商城系统
2. **复杂度分离**：每个里程碑聚焦单一主题
3. **渐进式交付**：用户可更早体验实时通知和等级系统

**文档更新**：
- `development-plan.md`：更新里程碑概览表
- 添加第7周详细计划（消息通知系统）
- 添加第8周详细计划（用户等级与经验值系统）
- 添加第9周计划（商城系统）
- 添加里程碑调整说明

### M6 完成总结

**验收标准 100% 达成**：
- ✅ 发帖/互动触发萝卜币奖励
- ✅ 神评/沙发保留奖励正常发放
- ✅ 前端余额显示与钱包页面可用
- ✅ 精确计算系统完整实现并通过测试（52 个单元测试全通过）
- ✅ 定时任务正常运行
- ✅ 防刷机制生效
- ✅ 注册奖励集成完成
- ✅ 文档完整（2715 行，18 个章节）

**技术亮点**：
1. **精度保证**：所有交易以整数为单位，统一向下取整，记录舍入差额
2. **防刷机制**：每日奖励上限、重复发放检测、基于 Redis 的去重键
3. **性能优化**：增量扫描、批量插入、异步发放奖励
4. **配置灵活**：触发阈值可配置、奖励金额可配置、Cron 表达式可配置
5. **测试覆盖**：CoinCalculator 52 个单元测试、CoinService 7 个单元测试

**提交记录**：
- `feat: 实现萝卜币精确计算系统`
- `refactor: 神评/沙发触发条件改为可配置`
- `docs: 调整开发里程碑规划`
- `docs: 添加萝卜币系统 M6 完成总结`

**下一步**：合并到主分支，开始 M7（消息通知系统）

---

## 1月7日

### 通知系统 SignalR 连接修复

**问题背景**：SignalR Hub 连接出现"无法获取用户 ID"错误，经诊断发现 WebSocket HTTP 101 握手成功，但后端 `OnConnectedAsync` 方法未被调用。

#### 问题诊断过程

**现象**：
1. ✅ WebSocket HTTP 101 Switching Protocols 成功
2. ✅ JWT 认证成功，Claims 正确提取
3. ✅ Authorization 策略验证通过
4. ❌ 但 `OnConnectedAsync` 从未执行，无 Hub 日志

**调试步骤**：
1. 添加详细的 JWT Bearer Events 日志（OnMessageReceived、OnTokenValidated、OnAuthenticationFailed、OnChallenge）
2. 临时移除 Hub 的 `[Authorize]` 属性测试 → 问题依旧
3. 排查前端连接逻辑，发现 React StrictMode 导致的组件双重挂载问题

**根本原因**：

React StrictMode 在开发模式下故意双重挂载组件以检测副作用：

```
Mount → start() 启动连接 → Unmount → cleanup stop() → Re-mount
              ↓
      WebSocket 101 成功，但 SignalR 协议握手被 cleanup 中断
```

当 `useEffect` 的 cleanup 函数立即调用 `notificationHub.stop()` 时，会在 SignalR 完成协议握手之前关闭连接，导致 Hub 生命周期方法未执行。

#### 解决方案

**后端改进**（`Radish.Api/Program.cs`）：
1. 添加 SignalR 服务注册和配置
2. 优化 CORS 配置，添加 `AllowCredentials` 支持 WebSocket
3. 禁用 JWT claim type 映射，保持 OIDC 标准 claims 原样
4. 添加详细的 JWT 认证调试日志（开发阶段保留）
5. 映射 NotificationHub 端点到 `/hub/notification`

**前端修复**（`radish.client/src/desktop/Shell.tsx`）：

使用 `useRef` 跟踪连接状态，延迟 cleanup 执行：

```typescript
const hasStartedRef = useRef(false);

useEffect(() => {
  const token = window.localStorage.getItem('access_token');

  // 防止 React StrictMode 导致重复启动连接
  if (token && !hasStartedRef.current) {
    hasStartedRef.current = true;
    void notificationHub.start();
  } else if (!token) {
    hasStartedRef.current = false;
    void notificationHub.stop();
  }

  // cleanup 函数：仅在组件真正卸载时执行
  return () => {
    // 延迟执行 stop，给 StrictMode 的第二次 mount 一个机会
    setTimeout(() => {
      // 再次检查：如果组件已重新挂载，hasStartedRef 会是 true，不执行 stop
      if (!hasStartedRef.current) {
        void notificationHub.stop();
      }
    }, 100);
  };
}, []);
```

**关键技术点**：
1. **useRef 防重复**：跟踪连接状态，避免 StrictMode 双重挂载时多次调用 `start()`
2. **延迟 cleanup**：`setTimeout(100ms)` 给 StrictMode 重新挂载的时间窗口
3. **智能判断**：cleanup 中再次检查 ref，只在组件真正卸载时执行 `stop()`

#### 验证结果

✅ StrictMode 启用后，连接正常：
```
[2026-01-06T17:27:14.304Z] Information: Using HubProtocol 'json'.
[NotificationHub] 连接成功
[NotificationHub] 未读数更新: 0
```

#### 文档更新

**更新文档**：`radish.docs/docs/guide/notification-frontend.md` v1.0 → v1.1
- 添加第 11 节：常见问题
- 记录 React StrictMode 问题的诊断过程、根本原因和解决方案
- 提供完整的代码示例和验证方法

**提交记录**：
- `fix: 修复 SignalR 在 React StrictMode 下连接失败问题`
- `docs: 更新通知系统前端文档，记录 StrictMode 兼容方案`

**技术价值**：
1. 解决了 React 18+ 开发模式下 SignalR 连接的常见问题
2. 提供了 StrictMode 兼容的 WebSocket 连接管理模式
3. 建立了完整的调试日志体系，便于未来问题排查
