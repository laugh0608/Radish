# 2026 年 6 月第 2 周开发日志

## 2026-06-08

- `P3-9` 合并后完成本地验证并明确跳过发布：不创建 tag，不等待镜像，不进入 M15 测试 / 生产部署流程；项目继续处于 Phase 3 功能建设期，不切入生产稳定运营。
- `P3-10` Web-first 信息架构启动：完成 Web、Flutter、PC/Tauri、Console 的任务归属调整，明确纯 Web 承接默认入口、公开阅读、分享回流、登录后轻互动和信息流；Flutter 后移，WebOS / PC / Tauri 不承接新增功能主线。
- 历史功能规划已回拉筛选：评论实时、神评稳定性、热门标签、轻回应、活动卡片、经验激励和电子宠物进入 P3-10 候选；完整推荐系统、完整个人圈子、完整联邦社交、完整移动商城和完整通知中心继续后置。
- `P3-10-B1` 首批代码已完成：`/discover` 从公开导航聚合页调整为可持续浏览的公开内容流，复用公开帖子、公开文档、商品和榜单入口；继续保留公开 head、分享、移动 / PC 布局和登录后轻互动边界。
- `P3-10-B3` 首批代码已完成：新增 `User.PublicId`，公开主页和榜单输出 `VoPublicId / VoUserPublicId`，`/u/:id` 支持 `usr_...` 与历史 LongId 双读并规范化分享路由；公开帖子 / 评论 / 统计等内部接口仍使用 LongId。
- `DbMigrate` 已补 User PublicId 开发库路径：支持缺列补齐、旧用户 PublicId 回填和 `idx_user_public_id` 唯一索引；正式发布前仍需按数据库结构变更治理补版本化差异 SQL。
- 今日文档同步复核确认：`current.md`、P3-10 专题、6 月月志 / 周志、记录索引和日交接记录需要同步；B1/B3 未新增视觉 token、Pencil 设计源、部署配置或运行时环境变量，相关视觉规范、设计源文件和部署说明无需跟随更新。
- 今日验证覆盖 `dotnet build Radish.slnx -c Debug`、`npm run build --workspace=radish.client`、`npm run type-check --workspace=radish.client`、`npm run validate:identity`、`npm run check:repo-hygiene:changed`、`git diff --check`，并用浏览器在 1440×900 PC 视角复核 `/discover`、`/leaderboard` 和 `/u/usr_...`。
- 收工前补 [2026-06-08 收工回顾与明日事项](/records/daily-handoff-2026-06-08)：明日优先进入 `P3-10-B4 / B5`，复核评论服务、神评统计、现有 Hub 与 Web 评论区后，再推进评论实时和神评稳定性代码实现。

## 2026-06-09

- `P3-10-B4 / B5` 首批代码已完成：新增 `/hub/comment` SignalR Hub、Gateway 路由、Web / 公开详情订阅、typing 上报、评论创建 / 更新 / 删除 / 点赞 / 高亮变化事件广播，以及前端评论树实时合并。
- 神评 / 沙发稳定性治理已落地：服务端支持事件触发重算、并列展示、稳定窗口、替换阈值和当前高亮快照更新；奖励边界收敛为评论维度幂等和点赞增长增量奖励。
- 评论实时链路已补重连入组修复：前端 Hub `onreconnected` 后重新加入当前帖子分组，避免网络恢复后停留在已连接但未订阅状态。
- `DbMigrate` 开发默认用户 seed 已加固：`Seed:DeveloperDefaultsEnabled=true` 仅允许 `RadishDeployment:Stage=local/test`，生产或未配置安全阶段会拒绝创建 `system / admin / test` 开发默认账号。
- `DbMigrate` 本地运行支撑已收口：显式初始化 SQLite provider，避免 SQLite 连接类型初始化异常；seed 正常执行时改为按分类输出明细总数，失败时保留分类内明细便于排障。
- 订单购买失败测试已补应用初始化，避免测试因雪花 ID / 应用上下文未初始化而在前置步骤异常，继续保证 coin 失败回滚用例真实覆盖业务分支。
- 文档同步复核确认：评论功能说明、P3-10 专题、配置管理、部署指南、快速开始、6 月月志 / 周志、记录索引和日交接记录需要同步；今天没有新增视觉 token、Pencil 设计源、Console 权限模型或正式发布部署流程，相关视觉规范、设计源文件和 Console 权限说明无需跟随更新。
- 今日验证覆盖评论定向测试、奖励定向测试、`dotnet test Radish.Api.Tests`、`dotnet build Radish.DbMigrate/Radish.DbMigrate.csproj -c Debug`、`npm run build --workspace=radish.client`、`npm run type-check --workspace=@radish/http` 和 `git diff --check`；双用户浏览器联调因自动化临时运行时不稳定，改由人工继续执行。
- 收工前补 [2026-06-09 收工回顾与明日事项](/records/daily-handoff-2026-06-09)：明日优先做评论实时双用户主路径联调，发现问题后按事件契约、Hub 入组 / 重连、token 续接、前端树合并和奖励幂等边界成组修复；稳定后推进 `P3-10-B2` 个人圈子产品边界。
