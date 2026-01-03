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
