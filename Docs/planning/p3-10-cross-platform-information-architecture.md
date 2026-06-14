# P3-10 Web-first 信息架构与下一批开发任务选择

> 状态：`进行中（P3-10-B1 / B2 / B3 / B4 / B5 / B6 首批代码完成；B7 / C 公开入口治理与 WebOS 功能迁移图已进入维护收口，准备 B8 边界设计）`
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
| 用户身份语义 | 当前 `LoginName / UserName / UserEmail / PublicId` 已存在，但登录凭证、展示名、公开搜索索引和隐私边界仍需正式分层 | 新增 [用户身份语义与公开索引](/architecture/user-identity-semantics)，作为注册、登录、公开搜索、艾特和邮箱通知前置契约 |
| 评论互动 | 评论写入、点赞、回复和定位已具备，但 B 用户实时看到 A 的新评论、A 正在编辑状态尚未形成统一链路 | 设计评论实时 Hub / post group，优先 Web，Flutter 后续复用 |
| 神评 / 沙发 | 当前支持定时统计与写路径实时重算；同赞数时可能出现展示抖动和奖励语义不稳 | 做稳定窗口、并列规则、替换阈值和通知 / 奖励边界治理 |
| Token 不活跃过期 | Access / Refresh lifetime 已配置化，但缺少“连续不访问页面 N 天即退出”的前后端协同规则 | 设计 `IdleSession` 口径，前端活跃记录 + 后端刷新 / 会话校验共同生效 |
| 系统设置治理 | 当前已有 `SystemConfig` 与 Console 系统配置入口，但仍偏简单 key-value | 新增 [系统设置治理专题](/guide/system-settings-governance)，后续升级为设置定义、默认值、覆盖值、风险等级、二次确认和审计 |
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
| `P3-10-B9 用户身份语义与公开索引` | 后端 / Web / Console | 登录凭证、公开展示、搜索、艾特和隐私边界不再混用 | `UserName` 与 `LoginName` 仍容易被混称，邮箱当前注册可选，公开搜索索引未定义 | 需触达注册、登录、公开资料、搜索、提及和 Console 用户排障 | 不迁移数据库主键，不把公开索引替代 `PublicId` |
| `P3-10-B10 系统设置治理` | Console / 后端 | 管理员可安全调整长度、发布、通知、会话等运营参数，并能恢复默认与审计 | 当前 `SystemConfig` 偏 key-value，缺少设置定义、默认值、风险等级和二次确认 | 先做文档与设置定义模型，再分批开放低风险设置 | 不把部署密钥或基础设施配置搬进 Console |

当前 `P3-10-B1`、`P3-10-B2`、`P3-10-B3`、`P3-10-B4 / B5` 和 `P3-10-B6` 已完成首批代码推进；`P3-10-B7 / C` 已完成公共壳层一致性、论坛公开详情轻互动、公共头部动作策略、公开内容链接契约、圈子来源返回、公开参与 / 登录恢复 / 来源返回矩阵、Console 高频治理入口，以及 `/notifications`、`/me`、`/messages` 三个纯 Web 私域复访入口。下一步不再继续围绕同一批公开返回文案、Console 高频入口或 WebOS 历史应用反复深挖，转入 B8 Radish 电子宠物边界设计。

`P3-10-B9 / B10` 当前只完成专题方案落档，用于后续排期。它们不抢占 B7 当前页面策略与高价值入口推进，也不要求立即进入代码实现。

### `P3-10-B7` 视觉参考口径

Web UI 改造的公开入口可参考 MiMo Code 的淡雅古风视觉气质，但不参考它的营销首页结构。当前口径如下：

- `/` 与 `/discover` 仍是公开发现内容流，不改成品牌营销页、产品承诺首屏或“进入发现 / 登录 / 文档”三按钮入口。
- `/discover` 首屏优先呈现可持续浏览的真实内容、必要筛选、登录后轻互动和分享回流，不为视觉叙事牺牲内容密度。
- 不在默认公开入口新增宽幅能力 band；淡水墨、山纹、云纹、书页纹理或萝卜 IP 线稿只作为边缘、分区收边、空态、标题气质和内容承托。
- 真实内容应来自公开帖子、公开文档、公开商品、用户摘要、圈子动态和 Console 高频治理入口，避免抽象插画替代真实分发价值。
- 视觉方向保持淡雅新中式：纸色、淡墨、青灰、少量朱砂或品牌色点缀；禁止红金国潮、厚重渐变、满屏纹样和营销海报式构图。
- 大页面实现前仍需先更新 Pencil / 设计稿；小范围状态、文案、按钮或等价行为修正不被该要求阻塞。

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

截至 2026-06-13，`P3-10-B6` Gateway 首轮真实联调完成，结论如下：

- `Console`：通过 `https://localhost:5000/console/` 进入统一登录，使用开发管理员账号登录并授权后回到 `/console/`；仪表盘和 `/console/users` 受保护页正常。`14:04:04` 签发的 `radish-console` token 在 `14:08:04` 自动 refresh，Auth 日志确认请求体包含 `grant_type=refresh_token`、`client_id=radish-console` 和 `radish_last_active_at=1781330644`，返回 200；刷新后受保护页继续可访问。
- `Web`：`/circle` 登录态页面可访问，`/desktop` 已登录工作台正常渲染；通知 / 聊天 Hub 通过 Gateway negotiate，API 端按 `radish-client` token 校验为 UserId `20001`，`NotificationHub` 与 `ChatHub` 均建立连接。
- `评论 Hub`：公开帖子详情 `/forum/post/pst_019ea24e37ae7f75afd6503c88d01d3a` 正常加载，评论区显示 6 条讨论；`/hub/comment/negotiate` 与 `/hub/comment` 经 Gateway 建立连接，API 端 token 校验通过。
- `Console /console/login?auto=1&reason=idle`：在 Chrome 现有 Auth 会话仍存在时会立即进入 `radish-console` 授权确认页。这符合 idle 退出不调用 `endsession` 的口径，但不会停留在 Console 登录页展示 idle 原因。
- `验证限制`：本轮因 in-app Browser 触发 GPU 异常改用 Chrome 插件；Chrome 插件没有视口 / DPR 控制能力，无法按 `390x844 @ DPR 3` 做严格移动端 smoke。真实 7 天 idle 过期无法通过等待完成，也未通过篡改 Chrome storage 制造结果；由 `IdleSessionPolicyTest` 和 `authSession.test.ts` 覆盖服务端 / 前端判定逻辑。
- `精准验证`：`dotnet test Radish.Api.Tests --filter FullyQualifiedName~IdleSessionPolicyTest` 通过 5 个测试；`node --test --test-isolation=none Frontend/radish.client/tests/authSession.test.ts` 通过 8 个测试。`dotnet test` 仅出现既有 `CoinController` XML 注释 warning。

本轮未发现新的 B6 代码阻断。移动 DPR 视图、真实 7 天 idle 等待、Hub 停连与评论匿名恢复的完整运行时回归，保留到工具条件满足或发布候选前批次级复核。

### `P3-10-B7` 首轮审计与迁移图

截至 2026-06-13，`P3-10-B7 Web UI 改造与去 WebOS 化迁移图` 已启动首轮审计。Chrome 插件通过 Gateway 读取 PC 视图，覆盖 `/discover`、公开帖子详情 `/forum/post/pst_019ea24e37ae7f75afd6503c88d01d3a`、公开用户页 `/u/usr_019eac8d2a087d1e8130ffbe552c8317`、`/circle` 和 Console 入口；Console 当前停在 `radish-console` 授权确认页，未为布局审计点击授权。

当前页面层级判断：

- `PublicEntry` 与 `RootEntry` 分流已经清晰：浏览器根路径 `/` 进入 `/discover`，`/desktop` 保留为历史桌面工作台，不作为新功能默认承载。
- 公共 Web 入口已共享 `PublicShellHeader`，头部动作策略固定为：公开发现承接公共分发，`/circle` 承接登录态关系链复访，`/desktop` 作为历史工作台入口保留但降为次级动作；论坛详情“发送轻回应 / 参与讨论”已首批从 `/desktop?app=forum...` 回迁到纯 Web 详情页，登录回流只放开带 `intent=comment|quickReply` 的 `/forum/post/:id`。
- `/discover` 已能承接真实内容流、来源返回和公开只读说明；`/forum` 详情负责帖子正文、轻回应墙和评论阅读；`/u/usr_...` 负责公开身份与公开内容；`/circle` 负责登录态关系链复访，仍不承担公开 SEO。
- Console 高频入口继续以 `AdminLayout`、全局搜索、侧边导航和权限路由承接治理效率；B7 首轮不回到 Console 低频页面逐页筛查。

设计边界：

- 本批可以直接推进无运行时行为变化的壳层一致性清理：删除公共页已经不用的旧头部 CSS，修正公共论坛副标题 i18n，降低后续共享壳层改造的样式分叉。
- `/desktop` 入口露出策略已按“保留但不作为新增功能主动作”落入 `PublicShellHeader`；公开详情继续扩展评论回复 / 点赞 / 投票等更重交互，或后续调整公共页主动作时，仍先输出页面策略再改代码。
- B7 视觉继续遵循淡雅新中式 token 口径，优先提升真实内容密度、阅读顺序、返回语义和轻互动可达性，不把公开入口改成营销页。

验证矩阵：

| 面向 | 验证入口 | 通过口径 |
| --- | --- | --- |
| 静态代码 | `npm run build --workspace=radish.client` | 公共页、圈子和论坛详情编译通过；i18n key 不回退为原始 key 文本 |
| Gateway PC 视图 | Chrome 插件访问 `/discover`、`/forum/post/:postPublicId`、`/u/:userPublicId`、`/circle` | 共享头部、内容流、公开详情、公开主页和圈子入口可读，来源返回与只读边界清晰 |
| Gateway Console | `/console/`、`/console/users` | 已登录时进入 Console 高频治理入口；未授权时停在授权确认，不把授权弹窗误判为 Console 布局异常 |
| 移动视图 | 工具可控视口时补 `390x844 @ DPR 3` | 头部动作、标题、卡片、标签和分页不挤压、不遮挡；Chrome 插件无法控视口时如实记录限制 |
| 公开契约 | share URL、canonical、公开 head、详情返回 | B7 UI 调整不破坏公开路由、分享回流、来源返回和 sitemap / head 口径 |

首批代码入口固定为“公共壳层一致性清理”：`PublicForumApp` 副标题接入 i18n，`PublicDiscoverApp / PublicDocsApp / PublicLeaderboardApp` 删除已被 `PublicShellHeader` 替代的旧头部样式。后续再进入公共头部动作策略和论坛详情参与路径迁移评审。

第二批代码与 Gateway 页面补验已完成：公开详情页登录后可直接发布轻回应和根评论，评论 typing 继续复用评论 Hub；评论回复、点赞、投票、编辑和治理动作仍留在工作台边界。Chrome 插件经 Gateway 覆盖 PC 视图与开发者手动切换的移动视图，确认未登录参与会回到纯 Web 详情并聚焦目标输入，登录后轻回应 `B7验` 和根评论 `B7 公开详情根评论联调` 发布成功，移动视图无横向溢出。联调中发现并修复两类一致性问题：无删除回调时轻回应墙不再展示删除动作；根评论创建 / Hub 事件不再因 React dev updater 重入造成计数重复，并同步更新帖子摘要评论数。

第三批代码已完成公共头部动作策略收口：`PublicShellHeader` 统一支持社区发现、我的圈子和工作台三层动作；`/discover` 默认展示“我的圈子”作为登录态复访入口，论坛 / 文档 / 榜单 / 商城 / 公开个人页展示“社区发现 + 我的圈子 + 工作台”，`/circle` 自身只保留“社区发现 + 工作台”。移动端保留主动作文本，圈子和工作台次级动作转为图标，避免窄屏头部挤压；公开发现和论坛阅读边界文案同步改为“公开详情开放轻回应与根评论，评论回复 / 点赞 / 投票 / 治理留在工作台”。本批完成 `radish.client` 构建、公开路由 / 登录回流契约测试和 Gateway PC / 移动视图复核。

`P3-10-C` 首批代码从公开内容卡片链接契约推进：`PostCard` 支持真实 `href`，公开发现信息流、公开论坛列表 / 搜索 / 标签 / 类型流和公开个人页内容项补齐公开 URL；普通点击继续走公开壳层导航以保留来源返回，新开标签、复制链接和浏览器原生链接行为使用真实路径。Gateway PC / 移动视图复核已覆盖 `/discover`、公开帖子详情、公开个人页评论卡片跳转、`/circle` 和 `/desktop`，未发现新增代码阻断。

`P3-10-C` 第二批代码补齐 `/circle` 到公开详情 / 公开个人页的来源返回契约：圈子关注动态和关注 / 粉丝用户项继续输出真实公开 URL，普通点击通过一次性来源转交进入公开壳层，详情页和公开个人页可返回“我的圈子”；新开标签、复制链接和浏览器原生链接仍只保留公开 URL。公开内容路由与可返回来源路由已拆分类型边界，避免把 `/circle` 写入 public head / JSON-LD 契约。本批已通过 `npm run test --workspace=radish.client` 与 `npm run build --workspace=radish.client`。

Gateway 真实复核已覆盖 PC `1318x809 @ DPR 2` 和移动 `412x915 @ DPR 3.5`：`/circle` 关注动态进入 `/forum/post/pst_019ea24e37ae7f75afd6503c88d01d3a` 后出现“返回我的圈子”并返回 `/circle`；`/circle?tab=following` 进入 `/u/usr_019ea76872bf787981ad3e9d3c6a3417` 后出现“返回我的圈子”并返回 `/circle?tab=following`；圈子页头部只保留“社区发现 + 工作台”，公开详情 / 公开个人页头部保持“社区发现 + 我的圈子 + 工作台”，两种视图均未横向溢出，页面 error 日志为空。移动视图中 Chrome 插件对 DevTools 移动标签的鼠标事件派发超时，本轮使用唯一链接 / 按钮的键盘激活验证路由事件与来源返回，不把移动触控点击写成已完成。

截至 2026-06-14，`P3-10-C` 第三批公开参与 / 登录恢复 / 来源返回矩阵和 Console 高频治理入口已完成；公开详情内部轻回应 / 评论输入区也已对齐“登录后回到本帖参与”的 Web-first 文案。当前结论是：公开入口治理默认收口，后续只在真实缺口、发布候选回归或新增路径暴露问题时回拉。

### `P3-10-B7` WebOS 功能迁移图收口

本轮口径不是“进入新阶段”，也不是“把 WebOS 全部功能搬到 Web”。`/desktop` 仍是历史工作台保留入口；B7 后续要回答的是：哪些现有 WebOS 能力仍有长期用户价值，应该迁移到纯 Web 或 Flutter；哪些只保留在 `/desktop`；哪些应后置或重做。

迁移判断顺序：

1. 已经有纯 Web 或 Flutter 主路径覆盖的能力，进入维护补漏，不重复迁移。
2. 影响登录后复访、通知回流、个人状态、经济系统或轻互动的能力，优先评估纯 Web 直达入口。
3. 更依赖移动生命周期、系统通知、原生返回或移动使用频次的能力，后续交给 Flutter 承接。
4. 需要多窗口、桌面工作台或开发者工具语义的能力，保留在 `/desktop`。
5. 只是 WebOS 历史形态遗留、没有当前真实使用价值的能力，不进入迁移批次。

当前源码入口以 `Frontend/radish.client/src/desktop/AppRegistry.tsx` 为准，首轮决策表如下：

| WebOS 应用 | 现有覆盖 | 当前决策 | 首批处理 |
| --- | --- | --- | --- |
| `welcome` | `/discover` 已承接公开默认入口，`/desktop` 欢迎页偏历史工作台引导 | 保留在 `/desktop`，不迁移为默认首页 | 不进入首批 |
| `showcase` | 组件库预览属于开发辅助 | 保留开发工具属性 | 不进入产品迁移 |
| `console` / `scalar` | 已是外部独立 Web 入口，经 Gateway 访问 | 不属于 WebOS 迁移对象 | 只维护入口可达性 |
| `document` | `/docs` / `__documents__` 已覆盖公开阅读、搜索和详情 | 公开阅读已迁移；导入导出、编辑和管理能力后置评估 | 首批只补真实阅读缺口 |
| `forum` | `/forum`、`/forum/post/:id`、搜索、标签、类型流和公开详情轻参与已覆盖 | 公开与轻参与已迁移；发帖创作器、回复、点赞、投票等更重工作台动作后置评估 | 不继续作为当前默认迁移对象 |
| `chat` | 当前主要在 WebOS 窗口和 Hub 中工作，聊天通知已有 `channelId/messageId` 深链参数 | 已迁移轻量复访入口：`/messages` 承接登录后会话 / 消息定位、通知回流和公开个人页来源返回；完整聊天平台继续后置 | 不做私聊、搜索、Reaction、阅读回执、移动系统通知或设备级会话治理 |
| `profile` | `/u/usr_...` 覆盖公开主页，`/me` 已承接登录后个人状态聚合，Flutter 已承接部分“我的”路径 | 私域状态已迁移首批；完整资料编辑、附件、社交管理仍后置 | 维护 `/me -> /u/usr_... -> 返回我的状态` |
| `radish-pit` | WebOS 承接钱包、转移、流水、统计、安全；`/me` 已只读展示资产状态和近期流水，Flutter 已补资产 / 流水主路径 | 经济系统轻量状态已迁移首批；转移 / 安全设置保持 WebOS 或后续专项 | 等电子宠物或经济玩法需要时再评估写操作 |
| `notification` | WebOS 通知中心完整管理，通知 Hub / API 已存在 | 首批高价值迁移已完成：`/notifications` 纯 Web 登录态入口接入登录恢复、通知列表和目标分流；聊天通知转入 `/messages` | 完整通知偏好、聚合规则和移动系统通知仍后置 |
| `leaderboard` | `/leaderboard` 公开排行已覆盖 | 已迁移，保留维护 | 不进入首批 |
| `experience-detail` | WebOS 承接登录态经验详情，公开榜单只展示排行，`/me` 已只读展示成长状态和近期经验明细 | 成长状态已迁移首批；完整图表和分页详情后置 | 与 B8 电子宠物前置关系一起观察 |
| `shop` | `/shop` 公开浏览、登录购买、订单 / 背包 / 钱包回流已多轮治理；Flutter 也已覆盖登录态路径 | 已有主路径，避免回到 P3-8-D 旧线 | 只在真实缺口回拉 |

首批代码已选择 `notification`：

- `/notifications` 作为登录态私域入口，不进入公开 sitemap，不替代 `/discover`、`/forum`、`/circle` 的内容职责。
- 通知目标优先分流到纯 Web 可承接的公开帖子详情、公开个人页、论坛列表和 `/messages` 会话复访；订单等仍依赖私域工作台的能力继续回到 `/desktop` 深链。
- 本批不做完整通知偏好、完整聊天平台、移动系统通知或通知聚合规则重构。

第二批代码选择 `profile + experience-detail + radish-pit` 的只读聚合：

- `/me` 作为登录态私域入口，不进入公开 sitemap，不替代 `/u/usr_...` 公开主页、`/circle` 个人圈子或 `/notifications` 通知复访。
- `/me` 聚合公开主页入口、成长状态、经验明细、资产余额、近期流水和最近浏览；从 `/me` 进入公开个人页或公开详情时使用一次性来源转交返回“我的状态”。
- 本批不做完整资料编辑、完整钱包、转账、支付密码、安全设置、完整经验图表、完整浏览历史中心或电子宠物玩法。
- Gateway PC / 移动 smoke 已覆盖未登录回流、登录态概览、公开主页返回 `/me`、圈子和通知动作；后续只在发布候选回归或新真实缺口暴露时回拉。

第三批代码选择 `chat` 的轻量复访：

- `/messages` 作为登录态私域入口，不进入公开 sitemap，不替代 `/circle` 的关系链复访、`/notifications` 的通知列表或 `/desktop` 的完整工作台。
- `/messages` 复用现有聊天 API、Chat Hub、频道列表、历史窗口定位和消息发送能力；从通知点击聊天目标时使用 `channelId/messageId` 进入指定会话和消息上下文。
- `/messages` 进入公开个人页时使用一次性来源转交返回“消息”，避免纯 Web 会话入口里的头像点击退回 WebOS 窗口语义。
- 本批不拆完整聊天平台，不做私聊、消息搜索、Reaction、置顶、阅读回执、权限细化、移动系统通知或 Flutter 会话治理。
- Gateway PC / 移动 smoke 已覆盖登录态频道列表、通知到 `/messages?channelId=...&messageId=...`、消息定位、公开个人页返回 `/messages` 和窄屏布局；自动化单击曾出现 Chrome 插件事件派发不稳定，用户真实手测确认纯 Web 通知单击可跳转，不作为页面缺陷阻塞 B7 收口。

截至 2026-06-14，B7 进入 B8 前的首批迁移收口判断已经完成：

- `/notifications`、`/me`、`/messages` 均已完成代码、定向契约测试、`radish.client` type-check / build 和 Gateway PC / 移动 smoke。
- 真实通知复访链路已用 `admin` 发送提及、`test` 接收通知验证：通知 extData 能落到 `/messages?channelId=...&messageId=...`，PC 与移动视图均可渲染目标消息。
- WebOS `/desktop` 的聊天、通知、个人中心、钱包和经验详情继续作为保留入口维护；完整聊天平台、完整钱包、完整个人中心、Flutter 系统通知和设备级会话治理不作为 B8 前置门槛。

## 后续开发建议

下一步从 `P3-10-B7 Web UI 改造与去 WebOS 化迁移图` 转入维护收口和 B8 边界设计：

1. 公开参与 / 登录恢复 / 来源返回矩阵和 Console 高频治理入口进入维护线；不再作为下一批默认开发入口。
2. B7 视觉参考以 [视觉主题规范](/frontend/visual-theme-spec) 与 [UI 设计灵感参考](/frontend/ui-design-inspiration) 中的淡雅古风 UI 口径为准，只借鉴视觉气质，不直接复制外部品牌、插画、命令框、页面结构或文案。
3. 迁移图首批代码已完成 `/notifications`、`/me`、`/messages` 三个纯 Web 私域复访入口；进入 B8 前不再要求迁移完整聊天、完整钱包、完整个人中心或 WebOS 其他历史应用。
4. B8 首先输出 Radish 电子宠物边界设计：玩法循环、经济约束、复访入口、Web-first 页面归属、与经验 / 资产 / 通知的关系、Console 配置和流水审计需求。
5. B7 不删除 `/desktop`，不把 WebOS `/desktop` 作为新增功能承载方向，不启动完整 E2E 平台，也不把 Flutter 完整会话治理、完整通知偏好、完整聊天平台或设备管理并入本批。
6. `P3-10-B6` 保留补验入口：工具条件满足时补移动 DPR 视图；发布候选前再做真实 idle、通知 / 聊天 Hub 停连、评论匿名恢复和 Console 回跳批次级回归。
7. 若 B8 或后续 B7 维护发现需要跨页面视觉体系、页面结构或设计口径调整，先更新 Pencil / 设计稿；小范围状态、文案、按钮或等价行为修正不被该要求阻塞。

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
- 不把 WebOS 全部功能迁移设为下一阶段前置门禁。
- 不在 WebOS 迁移图收口前直接启动 `P3-10-B8` 电子宠物代码实现。
- 不启动完整 Playwright / E2E 平台或完整可观测性平台。

## 首批开发批次

1. `P3-10-A Web-first 任务归属与历史功能回拉评估`（已完成初版）
   - 输出 Web、Flutter、PC/Tauri、Console 的任务归属调整表。
   - 输出首页信息流与个人圈子边界判断。
   - 输出历史功能规划回拉扫描表。
2. `P3-10-B 方案层收口`（首批已完成，保留回归）
   - 首页信息流方案。
   - User PublicId 分批 rollout 方案。
   - 用户身份语义与公开索引方案。
   - 评论实时 / 神评稳定性方案。
   - Token 不活跃过期方案。
   - 系统设置治理方案。
   - Radish 电子宠物专题规划。
3. `P3-10-C 下一批代码开发`（进行中）
   - 优先进入 `P3-10-B7 Web UI 改造与去 WebOS 化迁移图`，从 `/discover`、`/forum` 详情、`/u/usr_...`、`/circle` 和 Console 高频入口中选择同一组高价值入口推进。
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
