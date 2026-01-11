# 经验值与等级系统：实施计划与待确认问题

> 入口页：[经验值与等级系统设计方案](/guide/experience-level-system)

## 12. 实施计划

### 12.1 Phase 0(准备阶段) - 1 天 ✅ 已完成

- [x] 确定等级昵称和经验值曲线
- [x] 设计数据库表结构
- [x] 定义 API 接口契约

### 12.2 Phase 1(核心功能) - 5-7 天 ✅ P0 阶段已完成 (2026-01-11)

**目标**:实现经验值发放、等级计算、个人主页展示

**任务清单**:
1. **数据模型** ✅
   - [x] 创建 `UserExperience` 实体
   - [x] 创建 `ExpTransaction` 实体
   - [x] 创建 `LevelConfig` 实体
   - [x] 创建 `UserExpDailyStats` 实体
   - [x] 配置 AutoMapper 映射 (`ExperienceProfile`)

2. **Repository 层** ✅
   - [x] 使用 `IBaseRepository<UserExperience>`
   - [x] 使用 `IBaseRepository<ExpTransaction>`
   - [x] 使用 `IBaseRepository<LevelConfig>`
   - [x] 使用 `IBaseRepository<UserExpDailyStats>`

3. **Service 层** ✅
   - [x] 创建 `IExperienceService` 接口
   - [x] 实现 `ExperienceService`(发放、升级、统计)
   - [x] 实现乐观锁重试机制 (3次重试，指数退避)
   - [x] 实现等级计算逻辑 (二分查找式算法)
   - [ ] 实现每日上限检查 (P1)

4. **API 接口** ✅
   - [x] `GET /api/v1/Experience/GetMyExperience` (当前用户)
   - [x] `GET /api/v1/Experience/GetUserExperience/{userId}` (指定用户)
   - [x] `GET /api/v1/Experience/GetLevelConfigs` (所有等级配置)
   - [x] `GET /api/v1/Experience/GetLevelConfig/{level}` (指定等级)
   - [x] `GET /api/v1/Experience/GetMyTransactions` (交易记录)
   - [x] `POST /api/v1/Experience/AdminAdjustExperience` (管理员调整)

5. **数据初始化** ✅
   - [x] 初始化 `LevelConfig` 表数据(Lv.0-10)
   - [x] 数据库表结构创建成功
   - [x] 11个等级配置种子数据初始化成功

6. **测试** ⏳ P1
   - [ ] 单元测试:等级计算、经验值发放
   - [ ] 集成测试:完整流程
   - [ ] 与发帖功能集成测试

### 12.3 Phase 2(业务集成) - 3-5 天

**目标**:与论坛功能集成,实现所有经验值获取途径

**任务清单**:
1. **发帖奖励**
   - [ ] 在 `PostService.PublishPostAsync` 集成
   - [ ] 首次发帖额外奖励

2. **评论奖励**
   - [ ] 在 `CommentService.AddCommentAsync` 集成
   - [ ] 首次评论额外奖励

3. **点赞奖励**
   - [ ] 在 `PostService.ToggleLikeAsync` 集成(帖子)
   - [ ] 在 `CommentService.ToggleLikeAsync` 集成(评论)

4. **神评/沙发奖励**
   - [ ] 在 `CommentHighlightJob.ExecuteAsync` 集成

5. **签到奖励**
   - [ ] 实现每日登录奖励
   - [ ] 实现连续登录奖励

6. **测试**
   - [ ] 端到端测试:各种互动场景

### 12.4 Phase 3(前端展示) - 3-5 天

**目标**:完善前端展示,包括经验条、升级动画、明细页面

**任务清单**:
1. **组件开发**
   - [ ] `ExperienceBar` 组件(经验条)
   - [ ] `LevelUpModal` 组件(升级动画)
   - [ ] `ExperienceDetail` 页面(明细)
   - [ ] `Leaderboard` 页面(排行榜)

2. **个人主页集成**
   - [ ] 在个人主页添加经验条
   - [ ] 添加"经验详情"入口

3. **Dock 集成**
   - [ ] 显示当前等级徽章
   - [ ] 鼠标悬停显示进度

4. **WebSocket 推送**
   - [ ] 监听升级事件
   - [ ] 触发升级动画

5. **测试**
   - [ ] UI 测试:各种等级展示
   - [ ] 动画测试:升级特效

### 12.5 Phase 4(完善与优化) - 2-3 天

**目标**:防刷机制、排行榜、管理后台

**任务清单**:
1. **防刷机制**
   - [ ] 实现异常检测规则
   - [ ] 实现经验值冻结功能
   - [ ] 实现互刷检测

2. **排行榜**
   - [ ] `GET /api/v1/Experience/GetLeaderboard`
   - [ ] 前端排行榜页面
   - [ ] 定时任务:更新排行榜缓存

3. **管理后台**
   - [ ] 经验值统计面板
   - [ ] 用户经验值管理
   - [ ] 异常检测日志

4. **性能优化**
   - [ ] 缓存策略优化
   - [ ] 数据库索引优化
   - [ ] 压力测试

5. **文档**
   - [ ] API 文档更新
   - [ ] 运维文档编写

---


## 13. 待确认问题

### 13.1 设计细节

- [ ] 等级昵称是否采用修仙体系?还是其他主题(如动物、植物、星座等)?
- [ ] Lv.10 之后是否需要继续扩展?还是 Lv.10 为最高等级?
- [ ] 升级奖励(萝卜币)的金额是否合理?
- [ ] 各行为的经验值奖励是否合理?是否需要调整?

### 13.2 业务规则

- [ ] 点赞后取消,是否扣除已获得的经验值?(建议:不扣除)
- [ ] 帖子/评论被删除,是否扣除已获得的经验值?(建议:不扣除)
- [ ] 用户被封禁,经验值是否清零?(建议:不清零,但冻结)

### 13.3 性能与容量

- [ ] 预期用户规模?影响排行榜缓存策略
- [ ] 经验值交易记录保留多久?
- [ ] 是否需要每月/每年汇总报表?

### 13.4 UI/UX

- [ ] 升级动画是否需要音效?
- [ ] 经验条是否需要在所有页面显示?(如帖子列表、评论区)
- [ ] 等级徽章的样式是否需要专业设计师设计?

---


## 14. 参考资料

### 14.1 内部文档

- [萝卜币系统设计](/guide/radish-coin-system) - 积分系统设计参考
- [通知系统实时推送](/guide/notification-realtime) - 升级通知推送
- [神评/沙发功能](/features/comment-highlight) - 经验值触发点

### 14.2 外部资源

- [游戏化设计:让产品更有黏性](https://book.douban.com/subject/26883342/)
- [等级系统设计的经验与教训](https://www.gamasutra.com/view/feature/134842/the_chemistry_of_game_design.php)
- [Reddit Karma 系统](https://www.reddit.com/wiki/karma)
- [Stack Overflow 声望系统](https://stackoverflow.com/help/whats-reputation)

---

**文档版本**:v1.2
**创建日期**:2026-01-02
**最后更新**:2026-01-11
**实施状态**:P0 阶段已完成（含经验值计算公式配置化），P1/P2 进行中
**负责人**:待定
**审核状态**:P0 已实施

**v1.2 更新内容**（2026-01-11）:
- ✅ 新增 2.3 节：经验值计算公式配置化
- ✅ 支持 4 种计算公式（混合/指数/多项式/分段）
- ✅ 提供推荐配置方案（新社区/长期运营/硬核/修仙阶段）
- ✅ 实现动态调整流程和管理员 API
- ✅ 扩展层 ExperienceExtension 完整实现
- ✅ 数据库初始化支持动态计算
