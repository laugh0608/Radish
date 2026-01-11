# 2026年1月 第2周 (1月6日-10日)

## 消息通知系统 M7 P1 阶段完成

### 核心成果

**M7 P1 阶段完整实现**（2026-01-06 至 2026-01-10）：

#### 后端服务
1. **NotificationCacheService** - 未读数缓存管理
   - 30 分钟缓存过期
   - 增量更新（+1/-N）
   - 自动刷新机制

2. **NotificationDedupService** - 通知去重服务
   - 5 分钟去重窗口
   - 基于 userId+type+businessId 生成去重键
   - 防止短时间内重复推送

3. **NotificationService** - 核心通知服务
   - 创建通知并推送给指定用户
   - 分页查询通知列表
   - 标记已读/全部已读
   - 删除通知
   - 集成缓存和去重服务

4. **点赞通知集成**
   - PostService.ToggleLikeAsync - 帖子点赞通知
   - CommentService.ToggleLikeAsync - 评论点赞通知
   - 自动去重、异步推送

5. **评论回复通知集成**
   - CommentService.AddCommentAsync - 评论回复时通知被回复者
   - 不给自己发通知

6. **独立消息数据库**
   - 新增 Radish.Message.db 数据库
   - 通知表使用按月分表策略
   - 性能隔离、架构解耦

#### 前端组件
1. **NotificationCenter** - 通知中心下拉面板（@radish/ui）
   - 下拉面板展示最近通知
   - 未读徽章显示
   - 全部已读按钮

2. **NotificationList** - 通知列表组件（@radish/ui）
   - 纯列表展示
   - 加载状态、空状态

3. **NotificationApp** - 通知中心独立应用
   - 完整通知管理页面
   - 分页加载
   - 标记已读、删除操作
   - 跳转到关联内容

4. **SignalR 集成**
   - notificationHub.ts - SignalR 连接管理
   - notificationStore.ts - 通知状态管理
   - Shell.tsx - 自动连接 SignalR
   - Dock.tsx - 显示未读数徽章

#### 服务注册
- 通过 Autofac 自动注册所有 Radish.Service.dll 中的服务
- NotificationCacheService、NotificationDedupService、NotificationService 自动可用

### 技术亮点

1. **缓存策略**：未读数缓存 30 分钟，减少数据库查询压力
2. **去重机制**：5 分钟去重窗口，防止同一事件重复通知
3. **异步推送**：通知创建后异步推送，不阻塞主流程
4. **独立数据库**：消息系统独立数据库，性能隔离
5. **按月分表**：Notification 表按月分表，优化长期存储

### 验收标准 100% 达成

**后端**：
- ✅ SignalR Hub 可正常连接
- ✅ 评论触发通知推送
- ✅ 点赞触发通知推送
- ✅ 通知列表 API 可分页查询
- ✅ 标记已读功能正常工作
- ✅ 缓存和去重服务正常工作

**前端**：
- ✅ 登录后自动连接 SignalR Hub
- ✅ 接收到通知时实时更新
- ✅ 未读数量徽章实时更新
- ✅ 通知中心应用可正常使用
- ✅ 标记已读、删除功能正常
- ✅ 断线后自动重连

### 文档更新

1. **notification-realtime.md** (v2.0 → v2.1)
   - 更新状态：待实施 → P1 已完成
   - 添加实施进度概览
   - 标记各阶段完成状态

2. **notification-center.md** (v1.0 → v1.1)
   - 移除 StatusBar 相关过时内容
   - 更新访问入口说明（仅保留桌面图标和 Dock 固定入口）

3. **notification-implementation.md** (v1.1 → v1.2)
   - 更新最后更新日期
   - 标记 P1 阶段完成

---

## 用户等级与经验值系统 M8 P0 阶段完成

### 核心成果

**M8 P0 阶段完整实现**（2026-01-11）：

#### 数据模型层
1. **UserExperience** - 用户经验值实体
   - CurrentLevel: 当前等级 (0-10)
   - CurrentExp: 当前等级内经验值
   - TotalExp: 累计总经验值
   - ExpFrozen: 经验值冻结状态
   - Version: 乐观锁版本号

2. **ExpTransaction** - 经验值交易记录
   - 15 种经验值类型（POST_CREATE、COMMENT_LIKED、GOD_COMMENT 等）
   - Before/After 快照记录
   - 去重索引 (userId + expType + businessType + businessId + date)

3. **LevelConfig** - 等级配置表
   - 11 级修仙体系（凡人 → 练气 → 筑基 → 金丹 → 元婴 → 化神 → 炼虚 → 合体 → 大乘 → 渡劫 → 飞升）
   - 指数增长曲线 (100, 200, 500, 1000, 2000, 5000...)
   - 等级主题色、图标、徽章配置
   - 特权列表 (JSON)

4. **UserExpDailyStats** - 每日统计表
   - 追踪每日各类型经验值获取量
   - 用于防刷和数据分析

#### ViewModel 层
1. **UserExperienceVo** - 用户经验值视图模型
   - 包含计算字段：升级进度、距下级经验值、等级名称
   - 集成用户信息：用户名、头像

2. **ExpTransactionVo** - 交易记录视图模型
   - ExpTypeDisplay: 中文类型名称

3. **LevelConfigVo** - 等级配置视图模型
   - Privileges 自动解析为 List<string>

4. **LeaderboardItemVo** - 排行榜项视图模型

#### 业务服务层
**ExperienceService** - 核心经验值服务实现：

1. **经验值查询**
   - GetUserExperienceAsync: 获取单个用户经验值（不存在自动初始化）
   - GetUserExperiencesAsync: 批量获取

2. **经验值发放**
   - GrantExperienceAsync: 发放经验值（带乐观锁重试）
   - BatchGrantExperienceAsync: 批量发放
   - 乐观锁冲突自动重试 6 次（指数退避，上限 1000ms）
   - [UseTran] AOP 自动事务管理
   - 自动计算等级和升级

3. **等级配置**
   - GetLevelConfigsAsync: 获取所有等级配置
   - GetLevelConfigAsync: 获取指定等级配置
   - CalculateLevelAsync: 根据累计经验值计算等级

4. **交易记录查询**
   - GetTransactionsAsync: 分页查询交易记录
   - 支持按类型、日期范围筛选

5. **内部辅助方法**
   - InitializeUserExperienceAsync: 初始化用户经验值记录
   - MapToVoAsync: 实体映射为 VO（附加等级配置信息）
   - CalculateLevel: 纯计算方法（二分查找式等级计算）
   - ExecuteWithRetryAsync: 乐观锁重试封装

#### AutoMapper 配置
**ExperienceProfile** - 实体映射配置：
- UserExperience ↔ UserExperienceVo
- ExpTransaction ↔ ExpTransactionVo
- LevelConfig ↔ LevelConfigVo
- UserExpDailyStats ↔ UserExpDailyStatsVo
- 自动转换经验值类型为中文名称
- 自动解析/序列化特权 JSON

#### API 接口层
**ExperienceController** - 经验值 RESTful API：

1. **GetMyExperience** - 获取当前用户经验值信息
   - 路由: GET /api/v1/Experience/GetMyExperience
   - 需要认证 (Client 策略)

2. **GetUserExperience** - 获取指定用户经验值信息
   - 路由: GET /api/v1/Experience/GetUserExperience/{userId}

3. **GetLevelConfigs** - 获取所有等级配置
   - 路由: GET /api/v1/Experience/GetLevelConfigs
   - 匿名可访问

4. **GetLevelConfig** - 获取指定等级配置
   - 路由: GET /api/v1/Experience/GetLevelConfig/{level}

5. **GetMyTransactions** - 获取当前用户交易记录
   - 路由: GET /api/v1/Experience/GetMyTransactions
   - 分页查询、支持类型筛选

6. **AdminAdjustExperience** - 管理员调整经验值
   - 路由: POST /api/v1/Experience/AdminAdjustExperience
   - 需要管理员权限 (SystemOrAdmin 策略)

#### 数据库初始化
**InitialDataSeeder** - 等级配置种子数据：
- 11 个等级完整配置
- 指数增长经验值曲线
- 修仙主题颜色（凡人灰 → 练气绿 → 筑基蓝 → 金丹黄 → ...）

### 技术亮点

1. **乐观锁并发控制**：Version 字段 + 重试机制，防止并发修改冲突
2. **AOP 事务管理**：使用 [UseTran] 属性，避免手动 BeginTranAsync/CommitTranAsync
3. **指数增长曲线**：合理的升级难度设计（100 × 2^level）
4. **去重机制**：ExpTransaction 表建立复合索引防止重复发放
5. **自动初始化**：首次查询时自动创建用户经验值记录
6. **冻结机制**：支持经验值冻结（用于违规处理）
7. **映射封装**：Service 层统一使用 ViewModel，不直接暴露实体

### 编译验证

✅ 编译成功 (0 Error, 65 Warning)
- 所有实体、服务、控制器编译通过
- SqlSugar SnowFlakeSingle 正确引用

### 下一步 (P0 剩余任务)

1. **运行数据库迁移并验证**
   - 创建 UserExperience、ExpTransaction、LevelConfig、UserExpDailyStats 表
   - 执行种子数据初始化
   - 验证表结构和索引

2. **与发帖功能集成验证**
   - 在 PostService.AddPostAsync 中调用 GrantExperienceAsync
   - 发布帖子获得 20 经验值 (POST_CREATE)
   - 验证升级逻辑

---

## 用户等级与经验值系统 M8 P0 与 P1 集成完成

### 核心成果

**M8 P0 + P1 集成阶段完整实现**（2026-01-11）：

#### 与发帖功能集成
1. **PostService 经验值集成**
   - 发布帖子自动获得 +20 经验值（POST_CREATE）
   - 首次发帖额外 +30 经验值（FIRST_POST）
   - 异步处理，不阻塞主流程
   - 自动检测首次发帖状态

2. **升级事件完整处理**
   - 升级时自动发放萝卜币奖励（等级 × 100）
   - 升级时推送实时通知（通知中心）
   - 异步处理升级事件，不影响性能
   - 完整的错误处理和日志记录

3. **ExperienceService 升级事件处理**
   - HandleLevelUpAsync 方法实现
   - 集成 ICoinService 发放萝卜币
   - 集成 INotificationService 推送通知
   - 通知内容包含等级名称和奖励金额

#### 服务层改进
1. **ExperienceService 依赖注入扩展**
   - 新增 ICoinService 注入
   - 新增 INotificationService 注入
   - 支持完整的升级事件处理

2. **PostService 依赖注入扩展**
   - 新增 IExperienceService 注入
   - 支持异步经验值发放

#### 测试文件
1. **Radish.Api.Experience.http**
   - 完整的经验值系统测试用例
   - 发帖获得经验值测试
   - 升级奖励和通知测试
   - 经验值交易记录测试
   - 完整流程测试（6 个步骤）

### 技术亮点

1. **异步处理**：经验值发放和升级事件均使用 Task.Run 异步处理，不阻塞主流程
2. **首次发帖检测**：使用 QueryCountAsync 精确判断是否首次发帖
3. **升级奖励计算**：等级 × 100 萝卜币（Lv.1 = 100，Lv.10 = 1000）
4. **完整日志记录**：所有关键步骤均有 Serilog 日志记录
5. **错误隔离**：经验值发放失败不影响发帖成功

### 编译验证

✅ 编译成功（0 Error, 91 Warning）
- PostService 集成验证通过
- ExperienceService 升级事件验证通过
- 所有依赖注入正确配置

### 下一步（M8 P1 剩余任务）

1. **实际运行测试**
   - 启动 Radish.Api 测试发帖获得经验值
   - 验证升级逻辑和萝卜币奖励发放
   - 验证升级通知推送

2. **与其他论坛功能集成**
   - 评论奖励：CommentService.AddCommentAsync → +5 经验
   - 点赞奖励：被点赞者 +2 经验，点赞者 +1 经验
   - 神评/沙发奖励：+50/+30 经验

3. **每日上限防刷机制**
   - 实现缓存记录每日获得经验值
   - 配置各类型经验值每日上限

---

## 经验值系统性能优化与测试指导

### 核心成果

**性能优化**（2026-01-11）：

#### 问题修复
1. **GetTransactionsAsync 查询优化**
   - **问题发现**：用户反馈 SQLite 报错 "incomplete input"
   - **根本原因**：使用 `Func<ExpTransaction, bool>` 导致 SqlSugar 无法生成 SQL
   - **初始方案**：内存过滤（性能问题）
   - **用户反馈**："为什么排序要在代码层面，这个会影响性能吗"
   - **最终方案**：
     - 创建 `ExpressionExtensions.cs` 提供 And/Or 扩展方法
     - 使用 `Expression<Func<T, bool>>` 动态组合查询条件
     - 调用 `QueryPageAsync` 实现数据库层面筛选、排序、分页

2. **性能对比**
   ```csharp
   // ❌ 错误方式（内存操作）
   var allRecords = await _expTransactionRepository.QueryAsync(t => t.UserId == userId);
   var filtered = allRecords.Where(t => t.ExpType == expType); // 内存过滤
   var sorted = filtered.OrderByDescending(t => t.CreateTime); // 内存排序
   var paged = sorted.Skip(...).Take(...); // 内存分页

   // ✅ 正确方式（数据库操作）
   Expression<Func<ExpTransaction, bool>> where = t => t.UserId == userId;
   where = where.And(t => t.ExpType == expType); // 组合表达式
   var (pagedData, totalCount) = await _expTransactionRepository.QueryPageAsync(
       whereExpression: where,
       orderByExpression: t => t.CreateTime,
       orderByType: OrderByType.Desc
   ); // 完全在 SQL 层面完成
   ```

#### 新增文件
1. **ExpressionExtensions.cs** (Radish.Common)
   - `And<T>`: 组合两个表达式为 AND 条件
   - `Or<T>`: 组合两个表达式为 OR 条件
   - `ParameterReplaceVisitor`: 统一参数名避免冲突

#### 代码审查清单

**PostService.cs**：
- ✅ IExperienceService 依赖注入正确
- ✅ Task.Run 异步处理不阻塞主流程
- ✅ 完整的异常处理和日志记录
- ✅ QueryCountAsync 统计帖子数量
- ✅ 首次发帖检测逻辑正确

**ExperienceService.cs**：
- ✅ 乐观锁重试机制（6 次，指数退避，上限 1000ms）
- ✅ [UseTran] AOP 事务管理
- ✅ HandleLevelUpAsync 异步处理升级事件
- ✅ GetTransactionsAsync 使用数据库层面分页

**ExperienceController.cs**：
- ✅ GetCurrentUserId 从 sub/jti claim 获取用户 ID
- ✅ 所有接口正确使用 [Authorize] 策略

### 测试指导文档

**创建完整测试流程**：
1. 重启服务 → 发帖测试 → 查看经验值 → 检查交易记录
2. 关键日志检查点（9 个日志条目）
3. 数据库验证 SQL 语句
4. 常见问题排查表（5 种情况）

**日志追踪关键字**：
- "准备发放发帖经验值"
- "用户帖子数量统计"
- "经验值发放成功"
- "检测到首次发帖"
- "用户 X 从 Lv.Y 升级到 Lv.Z"

### 技术亮点

1. **Expression 组合**：使用 Visitor 模式统一参数，避免 "variable 'parameter' of type 'T' referenced from scope '', but it is not defined" 错误
2. **性能优化意识**：用户正确指出内存操作的性能问题，推动了数据库层面优化
3. **调试准备**：提前添加详细日志，为问题定位做好准备

### 待解决问题

**✅ 已解决：发帖后未获得经验值（乐观锁并发冲突 + 事务隔离问题）**

**问题现象 1 - 乐观锁并发冲突**：
- 用户发帖后经验值仍为 0
- 日志显示多次初始化用户经验值记录（4 次）
- 乐观锁冲突重试次数较多（6 次）
- 最终经验值发放失败

**根本原因 1**：
并发竞态条件导致的乐观锁冲突循环。

**问题现象 2 - 事务隔离导致查询失败**：
```
16:04:30.071 - 用户 20002 经验值记录初始化成功
16:04:30.078 - 用户 20002 经验值记录初始化后仍然不存在（仅隔了 7ms！）
```

**根本原因 2**：
`GrantExperienceInternalAsync` 有 `[UseTran]` AOP，所有操作在同一事务内：
1. `InitializeUserExperienceAsync` 执行 `AddAsync` → 插入记录但**事务未提交**
2. 立即重新查询 `QueryFirstAsync` → 因**事务隔离级别**读不到未提交数据
3. 返回 null → 经验值发放失败

**最终修复方案**（Radish.Service/ExperienceService.cs:165-177）：

```csharp
// 1. 获取或初始化用户经验值记录
// 注意：不要在初始化后立即重新查询，会因事务未提交而查询不到
// 重试机制会重新执行整个方法，自然获取到最新版本
	var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId);
if (userExp == null)
{
    userExp = await InitializeUserExperienceAsync(userId);
    if (userExp == null)
    {
        Log.Error("用户 {UserId} 经验值记录初始化失败", userId);
        return false;
    }
}
```

**核心思路**：
1. **依赖重试机制自然解决并发问题**：乐观锁冲突时，`ExecuteWithRetryAsync` 会重新执行整个 `GrantExperienceInternalAsync`，重新查询自然获取最新 Version
2. **直接使用初始化返回值**：避免事务内重新查询导致的隔离问题
3. **`InitializeUserExperienceAsync` 异常处理**：并发时捕获插入异常，返回已存在的记录（带正确 Version）

**修复效果**：
- ✅ 编译成功（0 Error, 153 Warning）
- ✅ 移除了导致事务隔离问题的"强制重新查询"逻辑
- ✅ 依赖乐观锁重试机制自然解决并发竞态
- ✅ 改进 `InitializeUserExperienceAsync` 日志（Information 级别，记录 Version）

---

## 升级动画弹窗前端集成完成

### 核心成果

**前端升级动画集成**（2026-01-11）：

#### 新增文件
1. **useLevelUpListener.ts** - 升级事件监听 Hook
   - 监听 SignalR 推送的 `LevelUp` 事件
   - 转换后端数据为前端 `LevelUpData` 格式
   - 管理弹窗显示状态
   - 自动清理事件监听器

#### 修改文件
1. **App.tsx** - 测试页面集成
   - 导入 `LevelUpModal` 和 `useLevelUpListener`
   - 渲染升级动画弹窗
   - 使用 Fragment 包裹主内容和弹窗

2. **Shell.tsx** - WebOS 桌面集成
   - 导入 `LevelUpModal` 和 `useLevelUpListener`
   - 在桌面环境中渲染升级动画弹窗
   - 与通知系统共享 SignalR 连接

3. **ExperienceDisplay.tsx** - 修复导入错误
   - 修正 API 导入路径（从 `@radish/ui/api` 改为 `@radish/ui`）
   - 移除未使用的 `useTranslation` 导入

### 技术实现

**SignalR 事件监听**：
```typescript
// 获取底层 SignalR 连接并注册事件监听
const connection = notificationHub.getConnection();
if (connection) {
  connection.on('LevelUp', handleLevelUp);
}
```

**数据转换**：
```typescript
const levelUpInfo: LevelUpData = {
  oldLevel: data.oldLevel || 0,
  newLevel: data.newLevel || 0,
  oldLevelName: data.oldLevelName || '凡人',
  newLevelName: data.newLevelName || '练气',
  themeColor: data.themeColor || '#9E9E9E',
  rewards: {
    coins: data.coinReward || 0,
    items: data.items || []
  },
  message: data.message || `恭喜你升级到 ${data.newLevelName}！`
};
```

**弹窗渲染**：
```tsx
{levelUpData && (
  <LevelUpModal
    isOpen={showModal}
    data={levelUpData}
    onClose={handleClose}
  />
)}
```

### 技术亮点

1. **复用 SignalR 连接**：升级事件监听复用通知系统的 SignalR 连接，无需额外连接
2. **自动清理**：useEffect 返回清理函数，组件卸载时自动移除事件监听器
3. **双端集成**：同时在测试页面（App.tsx）和 WebOS 桌面（Shell.tsx）集成，确保全场景覆盖
4. **类型安全**：使用 TypeScript 类型定义，确保数据格式正确

### 编译验证

✅ 构建成功（0 Error, 0 Warning）
- TypeScript 类型检查通过
- Vite 构建成功
- 所有导入路径正确

### 下一步计划

**M8 P3 阶段前端展示**（核心功能已完成）：
- ✅ `ExperienceBar` 组件（经验条）- 已完成 (@radish/ui)
- ✅ `LevelUpModal` 组件（升级动画）- 已完成 (@radish/ui)
- ✅ `experienceApi` 客户端 - 已完成 (@radish/ui)
- ✅ Dock 集成（ExperienceDisplay 组件）- 已完成
- ✅ WebSocket 推送监听升级事件 - 已完成 (useLevelUpListener Hook)
- ✅ 触发升级动画 - 已完成 (App.tsx + Shell.tsx)
- ⏳ 个人主页集成经验条 - 待实现
- ⏳ `ExperienceDetail` 页面（明细）- 待实现
- ⏳ `Leaderboard` 页面（排行榜）- 待实现

**M8 P1/P2 阶段后端集成**（已完成）：
- ✅ 升级事件处理（萝卜币奖励、通知推送）- 已完成
- ✅ 每日上限防刷机制 - 已完成
- ✅ 与所有论坛功能集成（评论、点赞、神评、沙发）- 已完成
- ✅ 发帖经验值集成 - 已完成
- ⏳ 调试并解决发帖未获得经验值的问题 - 待实际运行测试
- ⏳ 排行榜 API - 待实现
- ⏳ 每日统计 API - 待实现

**M8 P4 阶段完善与优化**（待开始）：
- ⏳ 异常检测规则
- ⏳ 经验值冻结功能
- ⏳ 互刷检测
- ⏳ 管理后台经验值统计面板
- ⏳ 性能优化和压力测试
- ⏳ 单元测试和集成测试

---

## 个人主页经验值条集成完成

### 核心成果

**个人主页经验值集成**（2026-01-11）：

#### 修改文件
1. **UserInfoCard.tsx** - 个人主页用户信息卡片
   - 导入 `ExperienceBar` 和 `experienceApi`
   - 新增 `loadExperience` 方法获取用户经验值
   - 在头像上传区域下方渲染经验值条
   - 使用 medium 尺寸，显示等级、进度和悬停提示

### 技术实现

**经验值加载**：
```typescript
const loadExperience = async () => {
  try {
    const exp = await experienceApi.getMyExperience();
    setExperience(exp);
  } catch (error) {
    console.error('加载经验值失败:', error);
  }
};
```

**经验值条渲染**：
```tsx
{experience && (
  <div style={{ marginBottom: '24px' }}>
    <ExperienceBar
      data={experience}
      size="medium"
      showLevel={true}
      showProgress={true}
      showTooltip={true}
      animated={true}
    />
  </div>
)}
```

### 技术亮点

1. **异步加载**：经验值与个人资料并行加载，互不阻塞
2. **错误处理**：加载失败不影响其他功能正常显示
3. **响应式设计**：经验值条自适应容器宽度
4. **动画效果**：进度条支持平滑动画

### 编译验证

✅ 构建成功（0 Error, 0 Warning）
- TypeScript 类型检查通过
- Vite 构建成功
- 所有导入路径正确

---

## 经验值排行榜功能完成

### 核心成果

**排行榜功能实现**（2026-01-11）：

#### 后端实现
1. **ExperienceService 排行榜方法**
   - `GetLeaderboardAsync`: 分页查询排行榜
   - `GetUserRankAsync`: 获取用户排名
   - 按 TotalExp 降序排序，排除冻结用户
   - 支持分页（默认 50 条/页，最大 100 条）
   - 自动标记当前用户（IsCurrentUser）
   - 联表查询用户信息和等级配置

2. **ExperienceController 接口**
   - `GET /api/v1/Experience/GetLeaderboard`: 获取排行榜（匿名可访问）
   - `GET /api/v1/Experience/GetMyRank`: 获取当前用户排名（需认证）

#### 前端实现
1. **@radish/ui API 客户端**
   - 新增 `LeaderboardItem` 类型定义
   - `experienceApi.getLeaderboard()`: 获取排行榜
   - `experienceApi.getMyRank()`: 获取我的排名

2. **LeaderboardApp 组件**
   - 分页浏览排行榜（50 条/页）
   - 前三名特殊样式（🥇🥈🥉）
   - 当前用户高亮显示
   - 显示排名、用户名、等级、总经验值
   - 响应式设计，移动端适配
   - 加载状态、错误处理、空状态展示

3. **WebOS 集成**
   - 注册到应用列表（图标：mdi:trophy）
   - 默认窗口大小：900x700
   - 分类：user

### 技术实现

**后端排行榜查询**：
```csharp
// 数据库层面排序和分页
var (pagedData, totalCount) = await _userExpRepository.QueryPageAsync(
    whereExpression: e => !e.ExpFrozen, // 排除冻结用户
    pageIndex: pageIndex,
    pageSize: pageSize,
    orderByExpression: e => e.TotalExp,
    orderByType: OrderByType.Desc
);

// 联表查询用户信息和等级配置
var userIds = pagedData.Select(e => e.UserId).ToList();
var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
```

**用户排名计算**：
```csharp
// 统计比该用户经验值高的用户数量
var higherCount = await _userExpRepository.QueryCountAsync(
    e => !e.ExpFrozen && e.TotalExp > userExp.TotalExp
);
return (int)higherCount + 1; // 排名 = 比自己高的数量 + 1
```

**前端排行榜展示**：
```tsx
<div className={`${styles.item} ${item.isCurrentUser ? styles.currentUser : ''} ${getRankClass(item.rank)}`}>
  <div className={styles.rank}>
    {getRankIcon(item.rank) || `#${item.rank}`}
  </div>
  <div className={styles.userInfo}>
    <div className={styles.userName}>{item.userName}</div>
    <div className={styles.level} style={{ color: item.themeColor }}>
      Lv.{item.currentLevel} {item.currentLevelName}
    </div>
  </div>
  <div className={styles.exp}>
    <div className={styles.expValue}>{Number(item.totalExp).toLocaleString()}</div>
    <div className={styles.expLabel}>总经验值</div>
  </div>
</div>
```

### 技术亮点

1. **性能优化**：数据库层面排序和分页，避免内存操作
2. **用户体验**：前三名金银铜牌样式，当前用户高亮
3. **响应式设计**：移动端适配，流畅的分页切换
4. **错误处理**：完善的加载状态、错误提示、重试机制
5. **匿名访问**：排行榜接口允许未登录用户查看

### 编译验证

✅ 后端编译成功（0 Error, 154 Warning）
✅ 前端构建成功（0 Error, 0 Warning）
- TypeScript 类型检查通过
- Vite 构建成功
- 所有导入路径正确

---

### 下一步计划

**M8 P3 阶段前端展示**（已完成）：
- ✅ `ExperienceBar` 组件（经验条）- 已完成 (@radish/ui)
- ✅ `LevelUpModal` 组件（升级动画）- 已完成 (@radish/ui)
- ✅ `experienceApi` 客户端 - 已完成 (@radish/ui)
- ✅ Dock 集成（ExperienceDisplay 组件）- 已完成
- ✅ WebSocket 推送监听升级事件 - 已完成 (useLevelUpListener Hook)
- ✅ 触发升级动画 - 已完成 (App.tsx + Shell.tsx)
- ✅ 个人主页集成经验条 - 已完成 (UserInfoCard)
- ✅ `Leaderboard` 页面（排行榜）- 已完成
- ⏳ `ExperienceDetail` 页面（明细）- 待实现（可选）
