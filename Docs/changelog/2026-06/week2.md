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

## 2026-06-10

- `P3-10-B4 / B5` 评论实时与神评稳定性人工联调已由用户确认通过，转入维护和发布候选前批次级回归线。
- `P3-10-B2` 首批代码已完成：新增 `/circle` 登录态个人圈子入口，承接关注动态、我的关注和我的粉丝；发现 / 论坛 / 圈子的职责分工已写入 P3-10 专题，关系链用户项补 `VoPublicId` 并支持 `/u/usr_...` 跳转。
- 评论实时高亮一致性完成修复：高亮变化事件、前端评论树合并和并列神评 / 沙发展示继续保持与 B5 稳定性口径一致。
- 通知全部已读缓存残留完成修复：全部已读后未读数缓存和列表状态不再残留旧未读提示。
- 页面真实联调规则已补视图基线：PC 默认 `1920x1080`，移动端默认使用 `390x844 @ DPR 3` 或 `412x915 @ DPR 3` 的高分屏口径；`1280x720` 只作为窄桌面兼容补充。
- `P3-10-B6` Token 不活跃过期治理首批代码已完成：Auth refresh idle 校验、refresh token 最近活跃 claim、前端页面活动记录、refresh 参数、idle 过期退出、Console 请求前异步 refresh、通知 / 聊天 Hub 停连和评论 Hub 匿名恢复已落地。
- B6 运行时说明已拆到 [Token 不活跃过期治理](/guide/auth-idle-session)，避免继续向超长认证说明追加细节；[当前进行中](/planning/current) 与 P3-10 专题已同步下一步联调和后续开发入口。
- 今日验证覆盖 `dotnet test Radish.Api.Tests`、`npm run test --workspace=radish.client`、`npm run type-check --workspace=@radish/http`、`npm run type-check --workspace=radish.console`、`npm run build --workspace=radish.client`、`npm run build --workspace=radish.console`、`npm run validate:identity`、`npm run check:repo-hygiene:changed` 和 `git diff --check`。
- B6 页面真实联调放到 2026-06-11：本机 Gateway / Vite 服务未运行，且 AI 协作规则不直接启动 `dotnet run` 或 `npm run dev`；明日由用户启动服务后按 Gateway 覆盖 PC + 移动视图。

## 2026-06-13

- B6 Gateway 首轮真实联调完成：Console 登录授权、refresh 请求参数、刷新后受保护页、Web 登录态页面、通知 / 聊天 Hub 和评论 Hub 均未发现新增代码阻断；移动 DPR 与真实 7 天 idle 仍保留为发布候选前补验项。
- B7 公共壳层与公开详情首批回迁完成：公共页头部统一为“社区发现 / 我的圈子 / 工作台”，`/circle` 自身只保留“社区发现 + 工作台”，`/desktop` 保留但不作为新增功能主动作；论坛公开详情已支持登录后直接发布轻回应和根评论，公开边界文案同步对齐。
- `P3-10-C` 公开链接契约完成首批推进：公开发现、公开论坛列表 / 搜索 / 标签 / 类型流和公开个人页内容项输出真实公开 URL，普通点击保留公开壳层来源返回，新开标签和复制链接使用公开路径。
- `/circle` 到公开详情 / 公开个人页来源返回契约完成：关注动态进入 `/forum/post/:postPublicId`、关注 / 粉丝用户进入 `/u/usr_...` 后可返回“我的圈子”；公开内容路由与可返回来源路由已拆分，避免 `/circle` 进入 public head / JSON-LD。
- Gateway PC / 移动视图复核覆盖 `/discover`、公开帖子详情、公开个人页、`/circle` 和 `/desktop`；PC 普通点击通过，移动端因 Chrome 插件鼠标事件派发超时，使用唯一链接 / 按钮键盘激活验证路由事件与来源返回，不把触控点击写成已验证。
- 文档同步复核确认：已同步 [当前进行中](/planning/current)、P3-10 专题、[个人圈子](/features/circle)、[前端多壳层策略](/frontend/shell-strategy)、6 月月志 / 周志和日交接记录；今天没有新增视觉 token、Pencil 设计源、部署配置、数据库结构或 Console 权限模型，视觉规范、设计源文件、部署说明和权限说明无需跟随更新。

## 2026-06-14

- 公开作者 PublicId 与登录来源返回继续补齐：论坛详情、评论树、轻回应墙、发现流、公开论坛列表 / 搜索 / 标签 / 类型流和公开个人内容项已补作者公开标识与登录后来源恢复，公开详情参与文案同步对齐 Web-first 语义。
- Console 高频治理入口完成串联：用户排障页可进入内容治理上下文，内容治理页支持 URL 状态承接用户过滤；订单详情描述布局完成小修，不继续回到 Console 低频页面逐页筛查。
- B7 WebOS 功能迁移口径完成收口判断：`/desktop` 继续作为历史工作台保留入口，不把 WebOS 全量迁移作为 B8 前置门槛。
- `/notifications` 纯 Web 通知复访入口落地：接入主路由、登录恢复、通知列表、目标分流和 Web / WebOS 通知中心组件复用；聊天通知目标转向 `/messages`，订单等仍保留工作台深链。
- `/me` 纯 Web 我的状态入口落地：聚合公开主页入口、个人状态、成长、资产只读概览和最近复访，并支持从公开主页返回“我的状态”；完整资料编辑、完整钱包、安全设置和完整经验图表继续后置。
- `/messages` 纯 Web 消息复访入口落地：复用现有聊天 API、Chat Hub、频道列表和历史定位，支持通知中的 `channelId/messageId` 定位以及公开个人页返回“消息”；私聊、搜索、Reaction、阅读回执、移动系统通知和设备级会话治理继续后置。
- Gateway 真实 smoke 已覆盖 `/me`、`/notifications`、`/messages` 的 PC `1920x1080` 与移动 `390x844 @ DPR 3` 视图；真实通知链路用 `admin` 提及 `test` 验证 extData 到 `/messages?channelId=...&messageId=...`，用户手测确认纯 Web 通知单击可跳转。
- 文档同步复核确认：已同步 [当前进行中](/planning/current)、[开发路线图](/development-plan)、P3-10 专题、[前端多壳层策略](/frontend/shell-strategy)、6 月月志 / 周志和日交接记录；今天没有新增数据库结构、运行时环境变量、部署流程、视觉 token、Pencil 设计源或 Console 权限模型，相关说明无需跟随更新。
