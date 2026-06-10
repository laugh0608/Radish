# P3-10 Web-first 信息架构与下一批开发任务选择

> 状态：`进行中（P3-10-B1 / B2 / B3 / B4 / B5 / B6 首批代码完成，准备 B6 页面真实联调）`
>
> 启动日期：2026-06-08（Asia/Shanghai）
>
> 本页承载 Phase 3 内 `P3-10` 的 Web-first 信息架构、历史功能策划回拉、ID 契约治理、评论互动治理、UI 改造和下一批开发任务选择口径。快速入口仍以 [当前进行中](/planning/current) 为准。

## 背景判断

`P3-9` 已把访客公开访问、Flutter 登录移动用户和 Console 管理员排障三类真实使用路径组织成可验收批次，并完成 `dev -> master` 合并。当前决定不创建发布 tag、不等待镜像、不进入 M15 测试 / 生产部署流程。

这不代表项目进入生产稳定运营。Web、Flutter、PC/Tauri 与 Console 的功能完整度、UI 一致性和任务归属仍然不足，继续开发应先回答“哪些能力应该在哪个入口承接”，再进入具体实现。

本轮方向调整为：

- 先把 `Flutter` 从当前第一顺位后移，保留为后续跨端承接与复访体验线。
- 当前优先推进 `Web` 默认入口、信息流 / 个人圈子边界、公开与登录后轻互动、UI 改造和去 WebOS 化迁移。
- 同步把 `PublicId` 分批 rollout、评论实时 / 编辑状态、神评稳定性、Token 不活跃过期等长期契约问题纳入下一批候选。
- 对 `Docs/features/` 与相关专题规划中的未完成项做回拉筛选，避免旧规划长期沉淀为无归属 TODO。

## 阶段目标

1. **明确 Web-first 产品入口**
   - 纯 Web：承接默认首页、公开阅读、分享回流、登录后轻互动、个人公开页、信息流和基础社区关系。
   - WebOS `/desktop`：保留历史桌面工作台，不承接新增功能主线。
   - Flutter：后移到 Web 信息架构和 API 契约稳定之后，承接移动原生复访、通知、消息和轻互动。
   - PC/Tauri：继续后置，只作为纯 Web 的安装包增强，不恢复 WebOS 绑定路线。
   - Console：继续承接治理、排障、权限、内容审核、订单和资产流水管理。
2. **形成下一批开发候选矩阵**
   - 每个候选任务必须说明用户价值、端归属、当前缺口、实现风险、验证入口和不做范围。
   - 优先选择能改善真实使用路径、跨端一致性、登录恢复、对象标识契约、评论互动或治理效率的任务。
3. **回拉历史功能规划中的有效缺口**
   - 不把旧文档里的历史 TODO 原样搬回主线。
   - 只回拉仍符合当前 Web-first、长期契约治理和真实使用增长的事项。
   - 已被后续批次覆盖或需要代码复核的旧缺口，先标为“复核后再决定”。

## P3-10-A 重新定向结论

| 方向 | 当前判断 | P3-10 处理 |
| --- | --- | --- |
| Web 默认入口 | `/` 已转向纯 Web，`/discover` 当前是公开分发入口，但仍偏导航聚合，不足以承接长期首页 | 优先规划首页信息流、瀑布流展示、登录后轻互动和个人圈子边界 |
| 个人圈子 / 关注流 | 关系链、公开主页、关注动态流已有基础，但还没有稳定的“个人圈子”产品承载 | 先做 Web 侧产品边界，不直接把完整社交平台能力塞进首页 |
| PublicId | `Post.PublicId` 已试点，完整 rollout 后置 | 进入分批方案，优先梳理 `User / Comment / Attachment / WikiDocument / Channel / Notification` |
| 评论互动 | 评论写入、点赞、回复和定位已具备，但 B 用户实时看到 A 的新评论、A 正在编辑状态尚未形成统一链路 | 设计评论实时 Hub / post group，优先 Web，Flutter 后续复用 |
| 神评 / 沙发 | 当前支持定时统计与写路径实时重算；同赞数时可能出现展示抖动和奖励语义不稳 | 做稳定窗口、并列规则、替换阈值和通知 / 奖励边界治理 |
| Token 不活跃过期 | Access / Refresh lifetime 已配置化，但缺少“连续不访问页面 N 天即退出”的前后端协同规则 | 设计 `IdleSession` 口径，前端活跃记录 + 后端刷新 / 会话校验共同生效 |
| Flutter | 已补多条登录态路径，但继续做 Flutter IA 会抢占当前 Web 产品形态建设 | 后移为后续承接线，本批只保留 API 契约和复用能力要求 |
| UI 改造 | 视觉 token 和主题文档已存在，Web / Console / WebOS 历史样式仍有分叉 | Web 首页、详情页、个人页和评论区优先按新壳层改造 |

## 源码复核结论

本轮已按高频用户视角复核当前 Web 与后端实现，确认以下缺口可以作为进入开发的依据：

| 缺口 | 源码现状 | 开发判断 |
| --- | --- | --- |
| `/discover` 仍是公开聚合页 | `Frontend/radish.client/src/public/discover/PublicDiscoverApp.tsx` 当前只拉取少量最新帖子、公开文档和商品，并以 hero / 路由卡片组织首屏 | `P3-10-B1` 应优先把默认入口改造成内容流，而不是继续扩展导航卡片 |
| 公开主页仍依赖用户 LongId | `Frontend/radish.client/src/api/user.ts` 的公开资料、公开帖子和公开评论接口仍以 `LongId` 作为输入 | `P3-10-B3` 应先处理 `User.PublicId` 与兼容路由，支撑个人圈子和分享回流 |
| 评论缺少 post 级实时组 | 当前只映射 `/hub/notification` 和 `/hub/chat`，评论提交仍通过 REST 后刷新 | `P3-10-B4` 可复用 `ChatHub` 的分组、typing 和客户端连接模式，新增评论实时事件 |
| 神评 / 沙发展示口径不稳 | 后端支持同赞数多条 current，高赞并列会被记录；前端默认只置顶一个并按最新优先 | `P3-10-B5` 需要稳定窗口、并列展示、替换阈值和奖励幂等，不按简单定期刷新处理 |
| Token idle 规则缺失 | 前端只基于 token 过期 / 即将刷新和 foreground revalidate 判断登录态 | `P3-10-B6` 需要前端活跃记录与后端 refresh / session 校验共同生效 |
| 活动类内容查询会影响首页扩展 | 投票、抽奖等列表存在先查业务表 id 再过滤帖子的路径 | 首页信息流实现前应先限定首批数据来源，后续再考虑 feed API 或读模型 |

## 第五点专题：首页信息流与个人圈子边界

用户提出的“首页瀑布流”与“个人圈子 / 朋友圈 / 微博 / Twitter / Mastodon 类信息流”不能只当作页面排版问题处理。建议拆成两层：

1. **默认首页 / `/discover` 信息流改造**
   - 面向未登录、轻登录和外链回流用户。
   - 首批可用瀑布流或分栏信息流展示公开帖子、问答、投票、抽奖、轻回应、热门标签、公开文档和精选用户。
   - 保持 SEO、canonical、sitemap、公开 head snapshot 与分享路径稳定。
   - 登录后可增加轻互动入口，但不承载完整私域工作台。
2. **个人圈子 / 关注流能力**
   - 面向登录用户的长期复访与关系链互动。
   - 可作为 `/feed`、`/circle` 或独立 App 形态评估，优先承接关注动态、个人短内容、转发 / 引用、评论、轻回应、推荐解释和关系链发现。
   - 需要与 `PublicId / FederationId`、公开主页、未来 ActivityPub / WebFinger 方向保持兼容。
   - 不在首批直接实现完整联邦社交协议，也不把所有社交能力一次塞进首页。

首批建议先做“Web 首页信息流产品方案 + 信息结构原型”，再决定是否进入代码实现。

### `P3-10-B2` 圈子、论坛与发现职责分工

圈子长期产品边界已拆分到 [个人圈子](/features/circle)，本节只保留 P3-10 阶段决策摘要。

`圈子`、`论坛` 与 `发现` 可以展示同一批公开内容，但不能承担同一类产品职责。当前口径固定为：

| 入口 | 主要问题 | 内容来源 | 交互边界 | 不承担 |
| --- | --- | --- | --- | --- |
| `/discover` | “我现在不知道看什么” | 公开帖子、公开文档、公开商品、榜单、热门标签和后续精选用户 | 公开浏览、分享、SEO、来源返回和登录后轻互动入口 | 不承载我的关注流、粉丝列表、私域动态或工作台操作 |
| `/forum` | “围绕话题发帖、回帖、检索和沉淀讨论” | 帖子、评论、问答、投票、抽奖、标签和分类 | 发布、评论、点赞、回复、搜索、分类筛选、神评 / 沙发展示 | 不按个人关系链组织长期复访流 |
| `/circle` | “我关注的人最近有什么动态” | 当前用户关注作者发布的论坛帖子、我的关注、我的粉丝 | 登录态关系链复访、打开帖子详情、打开公开个人页、关注关系查看 | 不创建新的内容对象，不做推荐算法、转发 / 引用、私信或联邦协议 |

执行规则：

- 同一篇帖子可以同时出现在 `发现`、`论坛` 和 `圈子`，但对象权威归属仍在 `论坛`；详情、评论、点赞、神评、搜索和分类规则不在 `圈子` 重新实现。
- `发现` 是公开分发面，必须继续保持 canonical、share URL、公开 head snapshot 和 sitemap 口径稳定；匿名用户不应在这里看到“我的关注”语义。
- `圈子` 是登录态私域复访面，首批 URL 固定为 `/circle`；匿名访问应进入登录并回到 `/circle`，不作为公开 SEO 页面进入 sitemap。
- `圈子` 首批只复用 `UserFollow` 与 `Post` 既有数据：关注动态使用帖子 `PublicId` 进入 `/forum/post/:postPublicId`，用户列表使用 `User.PublicId` 进入 `/u/:userPublicId`。
- `圈子` 不新增 `CirclePost`、短动态、转发、引用或联邦对象；后续如果进入 ActivityPub / WebFinger，应先在对象标识和隐私专题中重新评审。

## 历史功能策划回拉扫描

本轮已扫描 `Docs/features/`、部分 `Docs/guide/` 路线文档和 [Backlog](/planning/backlog)。当前回拉建议如下：

| 来源 | 未完成 / 后续项 | 当前处理 |
| --- | --- | --- |
| [comment-highlight](/features/comment-highlight) | 前端主动推送刷新仍在后续规划；同赞数稳定性需要治理 | 回拉到 P3-10，纳入“神评稳定性 + 评论实时”候选 |
| [forum-features](/features/forum-features) | 更复杂推荐 / 热门排序、社交卡片、推荐解释能力 | 回拉为首页信息流输入，不直接启动完整推荐系统 |
| [forum-category-tag](/features/forum-category-tag) | 热门标签导流、标签相关推荐、标签 SEO 深度 | 回拉为首页 / 信息流内容组织输入 |
| [forum-quick-reaction-wall](/features/forum-quick-reaction-wall) | 贴纸型轻回应、审核效率增强、Flutter 登录续接 | 轻回应可进入信息流展示；贴纸型轻回应继续后置 |
| [forum-poll-mvp](/features/forum-poll-mvp) | 多选、匿名、撤票 / 改票、通知联动、活动页、Console 管理 | 仅把“活动卡片进入首页信息流”作为当前输入，其余继续后置 |
| [forum-qa-mvp](/features/forum-qa-mvp) | 回答排序优化、回答编辑历史、更多问答视图 | 可作为信息流排序与详情增强输入，暂不抢主线 |
| [forum-lottery-mvp](/features/forum-lottery-mvp) | 奖励联动、防刷、公示页、审计日志、Console 管理 | 当前不回拉实现；只作为活动内容卡片与治理后续输入 |
| [chat-app-roadmap](/features/chat-app-roadmap) | 私聊、消息搜索、Reaction、置顶、阅读回执、权限细化 | 后移到 Web 信息流和评论实时之后；保留为消息体系 P2 |
| [notification-realtime](/guide/notification-realtime) | 多端同步、通知聚合、偏好设置、容量治理、神评通知 | 评论实时和神评通知可回拉设计；完整通知 P2 继续后置 |
| [emoji-sticker-system](/features/emoji-sticker-system) | Reaction、聊天室 Reaction、表情商店、热门表情 | Forum Reaction 已部分演进，聊天室 Reaction / 表情商店继续后置，需代码复核后再回拉 |
| [file-upload-design](/features/file-upload-design) | 前端分片上传 UI、配置校验、临时令牌管理、测试覆盖 | 不作为当前主线；后续在创作器 / 大文件真实需求出现时回拉 |
| [experience-level-roadmap](/guide/experience-level-roadmap) | 防刷、冻结逻辑、异常检测、后台统计、测试 | 与神评奖励和信息流激励有关，先作为治理输入，不直接启动全套经验治理 |
| [radish-pit-roadmap](/guide/radish-pit-roadmap) | 用户搜索、安全日志、通知集成、支付相关增强、导出增强 | 与电子宠物和萝卜币玩法有关，先作为经济系统输入 |
| [shop-roadmap](/guide/shop-roadmap) | 优惠券、购物车、限时活动、礼物系统、推荐 | 商城活动卡片可进入首页候选；完整商城扩展继续后置 |
| [open-platform](/features/open-platform) | 第三方应用、SDK、配额、审核 | 与未来联邦 / 生态有关，当前不回拉 |

## 下一批候选矩阵

| 候选 | 端归属 | 用户价值 | 当前缺口 | 风险与验证 | 不做范围 |
| --- | --- | --- | --- | --- | --- |
| `P3-10-B1 Web 首页信息流方案` | 纯 Web | 默认入口更像可持续浏览的社区首页，而不是功能导航页 | `/discover` 聚合感强，瀑布流、内容卡片、关系链入口和登录后轻互动边界未定 | 先文档 / 设计稿；实现时验证公开 head、分享、移动布局和返回语义 | 不做完整推荐系统，不做完整联邦社交 |
| `P3-10-B2 个人圈子产品边界` | 纯 Web | 登录用户有稳定复访场景，关注动态不再只是个人页里的附属入口 | 关系链有基础，但没有独立信息流产品形态 | 需要先定 URL、对象类型、PublicId 和隐私边界 | 不直接实现 ActivityPub / WebFinger |
| `P3-10-B3 PublicId 分批 rollout 方案` | 后端 / Web / Flutter | 外部链接、通知、分享、未来联邦不继续依赖 long 主键 | 只有 `Post.PublicId` 试点，用户、评论、附件等仍未分批 | 触达接口契约，需补 identity guard 与定向测试 | 不做数据库主键迁移，不一次性全量改完 |
| `P3-10-B4 评论实时与编辑状态方案` | Web-first | A 发布评论后 B 即时看到，B 也能看到 A 正在编辑 | 评论仍以 REST 拉取为主，缺少 post 级实时组和 typing 事件 | 复用 ChatHub 的连接、节流、去重经验；验证双账号链路 | 不强制把评论全部改成弹窗；Flutter 后续复用 |
| `P3-10-B5 神评 / 沙发稳定性治理` | 后端 / Web | 同赞数和实时重算不造成展示抖动、奖励重复或用户困惑 | 当前同赞数会记录多个 current，但前端只置顶一个，且按最新优先 | 需定义稳定窗口、替换阈值、并列展示和奖励幂等 | 不简单改成每 7 天全量刷新 |
| `P3-10-B6 Token 不活跃过期治理` | Auth / Web / Console | 多天不访问后自动失效并退出登录，前后端口径一致 | Access / Refresh lifetime 与页面活跃状态没有统一 idle 规则 | 涉及 Auth 配置、refresh 校验、前端 bootstrap / focus 行为 | 不把 access token 直接拉长，不只做前端清理 |
| `P3-10-B7 Web UI 改造与去 WebOS 化迁移图` | 纯 Web / UI | 默认浏览器入口形成稳定产品体验，减少 WebOS 历史感 | Web、WebOS、Console 历史样式和入口语义仍分叉 | 大页面先走设计源文件；实现时做桌面 / 移动截图验证 | 不删除 `/desktop`，不做移动版 WebOS |
| `P3-10-B8 Radish 电子宠物规划` | Web-first / 玩法 | 为萝卜币、经验、日常复访和轻社区互动提供长期玩法容器 | 尚无独立规划文档与经济边界 | 先写专题路线，避免过早落代码造成数值和治理债务 | 不在当前批次实现完整游戏系统 |

当前 `P3-10-B1`、`P3-10-B2`、`P3-10-B3`、`P3-10-B4 / B5` 和 `P3-10-B6` 已完成首批代码推进；下一步先对 B6 会话治理做 Gateway 下 PC + 移动视图人工联调，再进入 Web UI 改造或 Radish 电子宠物专题。

## P3-10-B 收口条件

进入代码开发前，每个首批候选至少满足以下条件：

| 候选 | 开发前必须明确 | 首批代码入口 |
| --- | --- | --- |
| `P3-10-B1 Web 首页信息流` | 内容来源、卡片类型、瀑布流 / 分栏规则、公开 head、登录后轻互动、移动 / PC 布局验收 | 改造 `/discover` 首屏和内容区，先用现有公开帖子 / 文档 / 商品数据组合成真实内容流 |
| `P3-10-B3 User PublicId` | 用户公开标识字段、公开主页 URL、LongId 兼容读取、分享 / 通知回流、identity guard 覆盖点 | 先做用户公开主页读取与前端路由兼容，不做数据库主键迁移 |
| `P3-10-B4 评论实时` | Hub 路径、post group 命名、事件类型、typing 节流、客户端去重、双用户验证方式 | 新增评论实时 Hub / service，并接入 Web 评论区 |
| `P3-10-B5 神评 / 沙发稳定性` | 稳定窗口、并列展示、替换阈值、奖励幂等、旧数据兼容 | 先统一后端 current 语义和前端展示，不扩大为完整推荐系统 |
| `P3-10-B6 Token 不活跃过期` | `IdleSession` 配置项、前端活跃写入、后端 refresh / session 判定、退出登录 UX | 在前三项方案稳定后推进，避免打断当前 Web 入口改造 |

## 首批代码推进记录

截至 2026-06-08，`P3-10-B1 / B3` 已完成首批代码实现：

- `P3-10-B1 Web 首页信息流`：`/discover` 已从公开导航聚合页改为可持续浏览的信息流，首批复用公开帖子、公开文档和商品数据；继续保留公开 head、分享、移动 / PC 布局和登录后轻互动边界，不引入完整推荐系统、个人圈子或联邦社交。
- `P3-10-B3 User PublicId`：新增 `User.PublicId` 公开标识，公开主页和榜单契约开始输出 `VoPublicId / VoUserPublicId`；`/u/:id` 支持 `usr_...` 与历史 LongId 双读，公开帖子 / 评论 / 统计等内部接口仍使用 LongId。
- `DbMigrate` 已纳入 `User.PublicId` 缺列补齐、旧用户 PublicId 回填和 `idx_user_public_id` 唯一索引补丁；旧 SQLite 开发库需先执行 `DbMigrate apply` 并重启 API / Gateway 后再复核运行时。

本批仍明确不做数据库主键迁移，不启动完整 PublicId 全量 rollout，不把评论、附件、文档、频道、通知等其他对象一次性改完。

发布候选前仍需按数据库结构变更治理补 `User.PublicId` 的版本化差异 SQL；当前 P3-10 仍处于开发阶段，今天只完成开发库 `DbMigrate` 路径和本地验证。

截至 2026-06-09，`P3-10-B4 / B5` 已完成首批代码实现：

- `P3-10-B4 评论实时`：新增 `/hub/comment` SignalR Hub 和 Gateway 路由，按 `post-comments:{postId}` 分组广播评论创建、更新、删除、点赞、高亮变化和 typing 事件；Web 论坛详情和公开详情页已接入订阅、去重、树合并和断开清理。
- `P3-10-B5 神评 / 沙发稳定性`：实时重算改为返回高亮变化结果；后端新增稳定窗口与替换阈值配置，避免同赞数或短窗口内微弱领先造成展示抖动；前端支持并列神评 / 沙发置顶展示。
- 奖励边界已收敛：神评 / 沙发基础萝卜币奖励和经验奖励按评论维度幂等，当前高亮点赞增长走增量奖励，后台统计任务也补充经验奖励重复检查。
- 本批仍不做完整推荐系统、完整 E2E 平台、Flutter 评论实时接入或完整通知偏好治理。

截至 2026-06-10，`P3-10-B2` 已完成首批边界与代码落地：

- 职责分工已固定：`/discover` 负责公开发现与分发，`/forum` 负责帖子 / 评论 / 标签 / 分类等讨论对象权威归属，`/circle` 负责登录态关系链复访。
- 新增 `/circle` 纯 Web 登录态入口，首批承接关注动态、我的关注和我的粉丝；匿名访问走 OIDC 登录回流，不进入公开 sitemap、canonical 或 public head 契约。
- 关系链用户项新增 `VoPublicId`，`UserFollowService` 按 User PublicId 口径补齐旧用户回填；圈子用户列表优先进入 `/u/usr_...`，帖子动态继续进入 `/forum/post/:postPublicId`。
- 公开主页入口白名单已同步支持 `/u/usr_...`，避免 User PublicId 路由被误判为 WebOS 工作台入口。
- 本批仍不新增短动态、转发、引用、私信、推荐算法、ActivityPub / WebFinger 或新的圈子内容对象。

截至 2026-06-10，`P3-10-B6` 已完成首批代码实现：

- Auth Server 新增 `OpenIddict:Server:IdleSession` 配置，refresh token 成功签发 / 刷新时写入最近活跃 claim，刷新校验超过 idle 窗口后返回 `invalid_grant / session_idle_expired`；运行时口径见 [Token 不活跃过期治理](/guide/auth-idle-session)。
- `radish.client` 和 `radish.console` 已接入页面活动记录，refresh 请求附加 `radish_last_active_at`；本地判定或服务端返回 idle 过期时清理 token，Console 跳回 `/console/login?auto=1&reason=idle`。
- 通知和聊天 Hub 在会话过期后停止连接；评论 Hub 支持断开后按已加入帖子组以匿名连接恢复，公开详情页订阅不因登录态过期整体失效。
- 共享 `@radish/http` 请求拦截器已支持异步等待，Console 的请求前 refresh 才能真正更新 Authorization。
- 本批仍不拉长 access token，不把 idle 过期处理为 endsession，不新增全端 E2E 平台；运行时体验需按 `Docs/guide/browser-smoke.md` 使用 Gateway 覆盖 PC 与移动视图。

## 明日开发建议

明日第一顺位围绕 `P3-10-B6` 做页面真实联调，建议按以下顺序推进：

1. 按 [页面真实联调](/guide/browser-smoke) 使用 Gateway 覆盖 PC 与移动端视图，重点验证登录成功后活动时间写入、refresh 请求附加 `radish_last_active_at`、长时间不活跃后退出登录 UX。
2. 覆盖页面恢复场景：前台恢复、`pageshow`、focus、受保护页面请求前 refresh、Console 登录页回跳和公开详情页继续匿名阅读。
3. 覆盖 Hub 场景：通知 / 聊天在 token idle 过期后停止，评论详情页连接可恢复为匿名订阅，重新登录后可重新进入登录态互动。
4. 若联调暴露问题，按 Auth refresh 判定、前端活动记录、退出登录 UX、Hub token factory / 重连和页面恢复入口成组修复，并执行对应精准验证。
5. 若 B6 联调通过且没有新的阻断，进入 `P3-10-B7 Web UI 改造与去 WebOS 化迁移图`：先审 `/discover`、`/forum` 详情、`/u/usr_...`、`/circle` 和 Console 高频入口的页面层级、设计边界与验证矩阵，再选择同一组高价值入口推进代码。
6. 不启动完整 E2E 平台，不把 Flutter 完整会话治理、完整通知偏好或设备管理并入本批；不把 WebOS `/desktop` 作为新增功能承载方向。

## 候选任务排序规则

优先级从高到低：

1. 阻断真实用户连续完成浏览、互动、回流或登录恢复的问题。
2. 影响 Web 默认入口产品形态、信息流承载和去 WebOS 化方向的问题。
3. LongId / PublicId、公开链接、通知回流、评论定位、会话过期等契约风险。
4. 评论实时、神评稳定性、轻互动、关系链和个人圈子等真实社区复访能力。
5. 管理员无法快速定位用户、订单、商品、流水或权限问题的治理效率缺口。
6. 明显影响可读性、可操作性或主路径信任感的 UI 断裂。

低优先级：

- 纯样式偏好微调。
- 历史 WebOS 能力补齐。
- Console 低频页面迁移。
- 不影响主路径的移动 Web 边角页面打磨。
- Flutter 完整能力套件与 PC/Tauri 分发增强。

## 当前不做

- 不创建本轮发布 tag，不进入 M15 测试 / 生产部署流程。
- 不宣布进入 Phase 4 稳定运营。
- 不把 Flutter 作为本批第一顺位，不直接实施 Flutter 底部导航重组。
- 不一次性启动完整移动商城、完整通知中心、完整资产中心、完整创作器或完整浏览历史中心。
- 不把首页改造成完整 Mastodon / ActivityPub 客户端。
- 不把个人圈子、首页瀑布流、推荐算法和联邦社交一次性混成同一个开发任务。
- 不重启完整 PC/Tauri 分发、签名、自动更新或托盘能力。
- 不把 WebOS `/desktop` 当作新增功能承载方向。
- 不启动完整 Playwright / E2E 平台或完整可观测性平台。

## 首批开发批次

1. `P3-10-A Web-first 任务归属与历史功能回拉评估`（已完成初版）
   - 输出 Web、Flutter、PC/Tauri、Console 的任务归属调整表。
   - 输出首页信息流与个人圈子边界判断。
   - 输出历史功能规划回拉扫描表。
2. `P3-10-B 方案层收口`（当前）
   - 首页信息流方案。
   - User PublicId 分批 rollout 方案。
   - 评论实时 / 神评稳定性方案。
   - Token 不活跃过期方案。
   - Radish 电子宠物专题规划。
3. `P3-10-C 下一批代码开发`（准备进入）
   - 优先从 `/discover` 首页信息流、用户 PublicId、评论实时 / 神评稳定性中选择一组同类任务进入实现。
   - 明确完成条件、验证入口和不做范围。
   - 不把评估项直接扩成全套能力建设。

## 验证口径

文档与规划批次默认运行：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

若触达默认执行面、门禁文档或身份语义边界，补：

```bash
npm run validate:identity
```

若触达前端 / Flutter / 后端代码，再按影响面选择对应 workspace 测试、类型检查、Flutter 测试或后端测试。
