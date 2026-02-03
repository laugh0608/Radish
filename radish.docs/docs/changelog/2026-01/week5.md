# 2026-01 第五周 (01-27 至 02-02)

## 2026-02-02 (周一)

### 认证与配置

- **OpenIddict 生命周期配置落地**：Access/Refresh/AuthorizationCode 三类 Token 的有效期从 `appsettings.json` 读取并应用
- **短 Token 测试准备**：本地将 Access/Refresh/AuthorizationCode 调整为 3/15/3 分钟，用于验证自动续期与登录态保持

## 2026-01-31 (周五)

### 架构重构

#### Base 类目录重构 (f2886ce)
- **影响范围**: 53 个文件
- **变更内容**: 将 Base 类移动到 `Base/` 子目录，提升代码组织结构
  - `IBaseRepository` → `Radish.IRepository/Base/IBaseRepository.cs`
  - `IBaseService` → `Radish.IService/Base/IBaseService.cs`
  - `BaseRepository` → `Radish.Repository/Base/BaseRepository.cs`
  - `BaseService` → `Radish.Service/Base/BaseService.cs`
- **目的**: 统一基础类的目录结构，便于维护和扩展

#### BaseService 和 BaseRepository 功能扩展 (ff94b1f)
- **新增代码**: 414 行
- **Repository 层新增方法**:
  - `QueryByIdsAsync()` - 批量 ID 查询
  - `QueryMaxAsync<TResult>()` - 查询最大值
  - `QueryMinAsync<TResult>()` - 查询最小值
  - `QueryAverageAsync()` - 查询平均值
  - `QueryWithOrderAsync()` - 带排序的列表查询
- **Service 层新增方法**:
  - `QueryDistinctAsync<TResult>()` - 去重查询
  - `QuerySumAsync<TResult>()` - 求和聚合
  - `QueryMaxAsync<TResult>()` - 最大值聚合
  - `QueryMinAsync<TResult>()` - 最小值聚合
  - `QueryAverageAsync()` - 平均值聚合
  - `QueryByIdsAsync()` - 批量查询
  - `QueryWithOrderAsync()` - 带排序列表查询
  - `QueryPageAsync()` (重载) - 二级排序分页查询
  - `ExecuteWithRetryAsync<TResult>()` - 乐观锁重试机制
  - `GetOrCreateAsync()` - 获取或创建模式
- **意义**: 提供丰富的数据库级聚合查询能力，避免内存过滤，提升性能

#### 前端包重命名 (db9a7fe)
- **变更**: `radish-api` → `radish.http`
- **包名**: `@radish/api` → `@radish/http`
- **原因**: 统一前端项目命名风格（小写 + 点号分隔）

### 新功能实现

#### 排行榜功能 (1f67e54)
- **新增文件**: 13 个
- **支持的排行榜类型** (8 种):
  1. **Experience** (等级排行) - 按总经验值排序
  2. **Balance** (萝卜余额榜) - 按当前余额排序
  3. **TotalSpent** (花销榜) - 按累计消费排序
  4. **PurchaseCount** (购买达人榜) - 按购买数量排序
  5. **HotProduct** (热门商品榜) - 按销量排序
  6. **PostCount** (发帖达人榜) - 按发帖数排序
  7. **CommentCount** (评论达人榜) - 按评论数排序
  8. **Popularity** (人气排行榜) - 按总点赞数排序

- **后端实现**:
  - `LeaderboardController` - 统一 API 端点 (`/api/v1/Leaderboard`)
  - `LeaderboardService` - 业务逻辑层 (688 行)
  - `LeaderboardRepository` - 数据聚合查询层 (288 行)
  - `LeaderboardTypeVo` - 排行榜类型描述 ViewModel
  - `UnifiedLeaderboardItemVo` - 统一数据结构 ViewModel

- **前端实现**:
  - `leaderboard.ts` - API 客户端
  - `LeaderboardApp.tsx` - 主应用组件
  - `UserLeaderboardItem.tsx` - 用户排行榜项组件
  - `ProductLeaderboardItem.tsx` - 商品排行榜项组件

- **技术亮点**:
  - 统一的数据结构设计，支持用户和商品两种实体类型
  - 数据库级聚合查询，性能优化
  - 类型安全的枚举设计

### 前端重构

#### API 客户端抽离 (260d79e)
- **变更**: 从 `@radish/ui` 抽离 API 客户端代码到独立的 `@radish/http` 包
- **新增目录**: `radish.http/`
  - `client.ts` - API 客户端实现 (fetch 封装)
  - `types.ts` - 类型定义 (ApiResponse, ApiClientConfig)
  - `error-handler.ts` - 统一错误处理
  - `index.ts` - 导出接口
- **目的**: 职责分离，UI 库专注组件，HTTP 客户端独立维护

#### 字段映射清理 (260d79e)
- **变更**: 移除前端映射函数，直接使用后端 Vo 前缀字段
- **影响文件**: 43 个
- **原因**: 简化前端代码，统一使用后端 ViewModel 字段命名规范

#### 后端匿名对象替换 (260d79e)
- **新增 ViewModel 类**:
  - `StatisticsVo.cs` - 统计数据 VO (包含 4 个子类)
    - `UserStatisticsVo` - 用户统计
    - `PostStatisticsVo` - 帖子统计
    - `ProductStatisticsVo` - 商品统计
    - `OrderStatisticsVo` - 订单统计
  - `SystemConfigVo.cs` - 系统配置 VO
- **修复 Controller**:
  - `StatisticsController` - 使用 Vo 类替代匿名对象
  - `SystemConfigController` - 使用 Vo 类替代匿名对象
- **意义**: 符合架构规范，禁止 Controller 返回匿名对象

#### 统一认证服务 (5710490)
- **新增文件**: `radish.client/src/services/auth.ts`
- **提供方法**:
  - `redirectToLogin()` - 跳转到 OIDC 登录页面
  - `logout()` - 执行 OIDC 登出
  - `hasAccessToken()` - 检查是否有有效 Token
- **后端优化**:
  - 删除多余的 `/Account/Logout` 接口
  - 保持 `/connect/endsession` 为唯一登出入口
- **目的**: 统一前端认证逻辑，避免代码重复

#### 前端组件优化
- **ExperienceDetailApp 添加经验条组件** (9760254)
  - 集成 `ExperienceBar` 组件，可视化显示经验值进度
- **重构 ShowcaseApp 组件展示页面** (6be4375)
  - 优化展示页面布局和交互
- **更新应用注册和用户信息卡片** (3f7fea0)
  - 优化用户信息展示
- **ExperienceBar 使用 Vo 前缀字段名** (58e3225)
  - 统一字段命名规范

### Bug 修复

#### Cookie SecurePolicy 配置 (b91b035)
- **问题**: `SameSite=None` 要求 `Secure=true`，但 `SecurePolicy` 设置为 `SameAsRequest`
- **修复**: 将 `Cookie.SecurePolicy` 从 `SameAsRequest` 改为 `Always`
- **影响**: 确保 Cookie 在 HTTPS 环境下正确工作

#### WebSocket 和 API 认证修复 (de5f113)
- **问题**: 未登录时仍尝试建立 WebSocket 连接和发送 API 请求
- **修复**:
  - 添加 JWT token 有效性验证
  - 未登录时跳过 WebSocket 连接
  - 优化 Dock.tsx 轮询逻辑
- **影响**: 减少无效请求，提升性能

#### 个人主页字段映射 (b54649c)
- **问题**: 个人主页字段映射错误
- **修复**: 修复字段映射问题，移除钱包选项卡
- **影响**: 个人主页正常显示

#### 萝卜坑统计选项卡白屏 (fce1596)
- **问题**: 萝卜坑统计选项卡出现白屏
- **修复**: 修复统计数据加载逻辑
- **影响**: 统计选项卡正常显示

### 文档更新

#### BaseService 规范更新 (440d003)
- **更新文件**:
  - `CLAUDE.md` - 强调优先使用泛型 `IBaseService`
  - `AGENTS.md` - 同步更新
  - `radish.docs/docs/architecture/specifications.md` - 添加聚合查询和工具方法说明
- **内容**: 补充 BaseService 新增方法的使用规范

#### 无关紧要的更改 (37fa340)
- 删除 `radish.client/CHANGELOG.md`
- 删除 `radish.console/CHANGELOG.md`
- 更新 `Radish.slnx`

## 本周总结

### 关键成果
1. **架构优化**: Base 类目录重构，BaseService/BaseRepository 功能大幅扩展
2. **新功能**: 实现完整的排行榜系统，支持 8 种排行榜类型
3. **前端重构**: API 客户端独立化，统一认证服务，清理字段映射
4. **代码质量**: 消除匿名对象，统一 ViewModel 规范
5. **Bug 修复**: 修复 Cookie、WebSocket、字段映射等多个问题

### 技术亮点
- **数据库级聚合**: 新增的聚合查询方法避免内存过滤，提升性能
- **统一数据结构**: 排行榜功能采用统一的 ViewModel 设计
- **职责分离**: API 客户端从 UI 库中独立出来
- **认证优化**: 统一 OIDC 认证逻辑，减少代码重复

### 代码统计
- **提交数**: 16 个
- **影响文件**: 约 120+ 个
- **新增代码**: 约 1500+ 行
- **删除代码**: 约 800+ 行

### 下周计划
- 继续完善排行榜功能（缓存优化、实时更新）
- 补充功能文档和使用指南
- 优化前端性能和用户体验
