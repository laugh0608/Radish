# 公开社区发现应用结构

> Radish 纯 Web 默认入口 `/discover` 的公开内容流说明。
>
> **最后更新**: 2026.08.05

## 定位

`/discover` 是普通浏览器根入口 `/` 的公开社区发现页，承接未登录、轻登录和外链回流用户的第一跳阅读。

当前它属于公开内容壳层，不等同于 WebOS 工作台，也不等同于完整推荐系统或个人圈子。

`2026-07-04` D61 后，`/discover` 承接旧 `public-web-unified-experience.pen` 中 `P01 - Public App Home` 与 `P02 - Discover Content Stream` 的发布前职责：首屏保留产品级 `Radish / 公开入口` 头部、紧凑公开脉搏、真实帖子讨论流和公共内容亮点；移动端在真实内容前提供搜索 / 筛选快捷入口，但不把页面改成营销首页、路由矩阵或大面积说明页。

`2026-08-03` 起旧文件只读留档，后续视觉裁决进入 `Docs/frontend/design-sources/radish-web-family-ui-v1.pen` 的 `R1-P01`；本页与当前代码继续负责功能、入口、数据和停止线。

`R1-P01` 首轮稿把真实 `newest` 数据误概括为热帖主流；第二轮虽分开分类、标签和结构化状态，但帖子仍占据绝大多数视觉篇幅。`2026-08-04` 经过参考转译、公开能力审计、低保真比较和多轮视觉重校准后，`R1-P01 / 社区发现 / PC 1440` 已确认为正式 PC 视觉基准。它保留 Radish 顶部公共导航和社区发现信息架构，使用现代自然紧凑表面、国风暖白、Geist 无衬线、克制圆角阴影、灰玉品牌语义、墨蓝操作语义与内嵌数据反馈；“社区正在发生”在同一扫描轴中并置公共频道话题、成员公开动态、跨帖神评、帖子和问答。`2026-08-05` 已在同一活动源中完成并确认唯一 `R1-P01 / 社区发现 / Mobile 390`：搜索与真实内容前置，社区脉搏、贡献者和知识主题分别作为首屏、中段和流后上下文，不缩放 PC，也不把右栏机械堆到末尾。首轮 mobile 评审指出混合流仍像异色列表块拼接，修订稿改为“焦点事件 + 连续编号轨道 + 嵌入式贡献者人物节点”：当前页第一条真实内容承担墨蓝焦点面，其余内容共享同一时间轴、排版和分隔节奏，不再重复图标盒与逐行箭头。节点、`2x` 视觉自检、用户评审及 Gateway PC / mobile 运行态复核均已通过；统一读模型、Channel opt-in 治理和页面结构已完成闭环。

## 当前实现事实

截至 `2026-08-05`，`/discover` 已按统一公开读模型完成代码侧改造：

- `GET /api/v1/PublicDiscover/GetFeed` 统一读取 `ChannelSummary / MemberActivity / HighlightedComment / Post / Question`，采用 snapshot cutoff 与稳定 keyset 游标，并显式输出 `VoPulse`。
- `Channel.DiscoverVisibility` 默认 `Hidden`；只有 Console 中具备 `console.channel-discoverability.manage` 的治理者显式开启 `Summary` 后，合格公共频道才输出元数据和安全聚合。变更使用期望版本、原因和 append-only 事件留痕，不会按 `ChannelType.Public` 自动开放。
- `Frontend/radish.http` 提供统一契约与 `getPublicDiscoverFeed`；页面不再并行调用帖子、标签、Wiki、商品等列表接口拼装首屏。
- PC 使用“主内容流 + 洞察区”非对称结构；mobile 前置搜索与“全部 / 最新 / 问答 / 知识”，并把贡献者节点嵌入第三条内容后。第一条真实内容使用焦点事件，其余内容按统一编号轨道连续扫描。
- 频道、文档、帖子 / 评论和贡献者均输出真实 `href`，普通点击继续由公共壳层接管；频道目标仍进入登录态 `/messages`，不会开放匿名消息正文。
- Docs、问答、榜单和商城只作为紧凑上下文入口，不在 `/discover` 复制各自列表、筛选、交易或治理逻辑。

本批没有把 `/circle`、通知、历史神评快照或现有授权聊天室接口伪装成匿名来源，也没有新增短动态写模型。全局 `guofeng` 品牌 token 会影响全部页面族，未随本页局部实现改色；本页只消费现有 `accent-jade / accent-ink / action` 等语义 token。

首批不包含：

- 完整推荐算法
- 完整个人圈子 / 关注流
- ActivityPub / WebFinger / Mastodon 客户端能力
- 登录后工作台治理动作
- 订单、背包、账号设置或 Console 排障入口
- 统一搜索 API、跨类型推荐读模型或公开聊天室路由

## `R1-P01` 统一公开读模型

### 定位与权威边界

统一公开读模型服务于“社区正在发生”的匿名可读混合流，但它不是推荐系统、匿名聊天室或新的动态写入产品：

1. 首批统一返回 `ChannelSummary / MemberActivity / HighlightedComment / Post / Question` 五种结构化 item；帖子与问答复用既有公开资格和公开标识，不另建平行可见性规则。
2. `PublicDiscoverFeedVo` 是只读 API 投影，不是新的业务真相源；每次读取都从频道、Wiki、论坛和用户当前权威状态得出资格。
3. 首批不新增“短动态”实体或发布入口。`MemberActivity` 只表达已经公开的成员贡献事件，不能从 `/circle`、通知、浏览记录或其他私域关系流复制数据。
4. `ChannelType.Public` 只表示登录态频道类型，不等于匿名公开。频道摘要必须另行显式选择公开摘要资格，不能调用现有 `[Authorize]` 的 Channel / Message 接口后在前端脱敏。
5. `HighlightedComment` 使用当前 Comment 与 Post 作为内容、可见性和目标权威；`CommentHighlight.ContentSnapshot` 只保留历史统计用途，不能作为公共流当前正文。
6. 用户屏蔽继续遵循 F4-K：它隔离互动和私域分发，不删除匿名公开内容，也不让公共 feed 按登录用户产生不同 SEO 或缓存版本。

### 来源与公开资格

| item | 首批来源 | 必须同时满足 | 明确不读取 |
| --- | --- | --- | --- |
| `ChannelSummary` | Chat 库 `Channel` 与安全聚合 | `Type=Public`、启用、未软删除、`DiscoverVisibility=Summary`、名称 / slug 有效 | 消息正文、附件、在线人数、成员名单、Reaction、Pin、阅读回执 |
| `MemberActivity` | Main 库成员拥有的公开 Wiki 首次发布 | Wiki 已发布、`Visibility=Public`、未软删除、`PublishedAt` 有效；Owner 用户启用、未删除且有合法 `PublicId` | 草稿、审核意见、受限文档、关注流、通知、任意“短动态”正文 |
| `HighlightedComment` | `CommentHighlight + Comment + Post + User` | 当前神评、`HighlightType=1`、点赞数大于 `0`；Comment 启用且未删除；父 Post 已发布、启用、未删除且有合法 `PublicId`；作者公开身份有效 | 沙发、历史快照正文、不可用帖子 / 评论、治理理由 |
| `Post` | 既有公开帖子查询 | 完整复用当前公开帖子资格、分类 / 标签 / 状态和 `PublicId` 契约 | 草稿、禁用 / 删除内容、客户端自造热度 |
| `Question` | 既有公开问答筛选 | 先满足公开帖子资格，再按既有结构化问答状态识别 | 回答私有状态、悬赏或未批准扩展 |

为 `Channel` 增加的匿名摘要开关固定为 `DiscoverVisibility`：`Hidden=0`、`Summary=1`，默认 `Hidden`。`Summary` 只允许输出频道名称、描述、slug、最后活动时间和可选的近 `24` 小时有效消息数量；目标继续使用现有 `buildMessagesPath({ channelId })` 进入登录后的 `/messages`，不得扩展为匿名消息详情。现有频道治理入口负责显式开启或关闭该字段，迁移不得根据 `ChannelType.Public` 自动批量开启。

`MemberActivity` 首批只接受公开 Wiki 的首次发布，`VoOccurredAtUtc` 使用不可变的 `PublishedAt`。Wiki 后续编辑、帖子 / 问答发布、排行榜变化和资料修改都不重复包装为动态；若未来扩充 allowlist，必须逐类补公开资格、撤回语义、去重和目标路由，不接受“所有审计事件直接上流”。

跨帖神评表示“从不同公开帖子汇集当前神评”，不表示评论被其他内容引用。其展示正文从当前 Comment 生成纯文本摘要，作者来自当前公开身份，父帖标题作为来源说明，跳转使用 `Post.PublicId + Comment.Id` 复用现有评论定位；评论编辑后展示当前正文，取消神评、禁用、删除或父帖退出公开后立即失去资格。

### Vo 与 HTTP 契约

新增公开接口：

```text
GET /api/v1/PublicDiscover/GetFeed?cursor={opaque}&pageSize={1..50}
```

- `[AllowAnonymous]`；默认 `pageSize=20`，`/discover` 首屏可以按设计密度请求较小页。
- `cursor` 只能由上一次响应取得；非法、版本未知或字段越界返回 `400 / PublicDiscover.CursorInvalid`。
- `pageSize` 超界返回 `400 / PublicDiscover.PageSizeInvalid`，不静默改成另一个大小。
- Controller 只注入 `IPublicDiscoverService`，不直接注入仓储或组合来源。

响应使用显式 Vo，不返回匿名对象：

```text
PublicDiscoverFeedVo
├── VoItems: PublicDiscoverItemVo[]
├── VoPulse: PublicDiscoverPulseVo
├── VoNextCursor: string?
├── VoHasMore: bool
└── VoGeneratedAtUtc: DateTime

PublicDiscoverItemVo
├── VoKey: string
├── VoKind: PublicDiscoverItemKind
├── VoOccurredAtUtc: DateTime
├── VoTitle / VoSummary: string
├── VoActor: PublicDiscoverActorVo?
├── VoTarget: PublicDiscoverTargetVo
└── VoPrimaryMetric: PublicDiscoverMetricVo?

PublicDiscoverPulseVo
├── VoWindowStartedAtUtc / VoWindowEndedAtUtc: DateTime
├── VoDiscoverableChannelCount: long
├── VoEligibleItemCount: long
└── VoKnowledgeContributionCount: long
```

`VoKey` 只作稳定渲染键，不是授权标识。`VoActor` 只允许 `VoPublicId / VoDisplayName / VoAvatarThumbnailUrl` 公共白名单；`VoTarget` 使用枚举区分 `Messages / Docs / ForumPost`，并按类型携带 `VoChannelId`、`VoDocumentSlug`、`VoPostPublicId`、`VoCommentId` 与 `VoRequiresAuthentication`。`VoChannelId` 只用于复用当前登录态消息路由，不能绕过 Channel / Message 的服务端授权；频道 slug 当前不是租户内唯一约束，不能单独作为深链标识。前端继续通过既有 route builder 生成真实 `href` 和当前标签页的 `history.state`，API 不接收或返回来源回跳状态。

`VoTitle / VoSummary` 必须由服务端从当前权威内容生成纯文本摘要：移除 Markdown / HTML、控制字符与附件表达，按 Unicode 文本安全截断；不得返回消息正文、草稿、审核备注或历史 CommentHighlight 快照。跨类型指标使用枚举语义，例如 `RecentReplies / Likes / Comments / Answers`，不返回已经本地化的自由文本标签。

`VoPulse` 只表达可从当前权威数据重算的聚合：当前显式开放摘要的频道数、以 `VoWindowEndedAtUtc` 为截止点的近 `24` 小时合格 item 总数，以及同一窗口内的公开 Wiki 首次发布数。三项都由仓储执行数据库侧 `COUNT`，窗口终点与首次请求的 `SnapshotCutoffUtc` 相同，续页沿用同一窗口；设计中的数值只是样例。首批不得把累计 `ViewCount` 包装成“今日浏览”，也不新增曝光埋点或分析事件来支撑装饰性数字。

### 稳定排序、去重与游标

1. 首批采用确定性的时间序，不提供“为你推荐”“综合热度”或个性化参数；设计源中的 PC / mobile 控件统一改为“最新公开”。
2. 排序键固定为 `VoOccurredAtUtc DESC, KindOrder ASC, SourceId DESC`。`KindOrder` 仅在时间完全相同时消除不确定性，不作为推荐权重。
3. 首次请求记录 `SnapshotCutoffUtc`；后续游标编码 `Version + SnapshotCutoffUtc + LastOccurredAtUtc + LastKindOrder + LastSourceId`，使用 Base64Url 的不透明版本化 payload。
4. 继续翻页只读取 `OccurredAtUtc <= SnapshotCutoffUtc` 且严格位于上一排序键之后的数据；刷新页面才接收新发布或重新取得资格的 item。
5. Question 只以 `Question` 出现，不再作为普通 `Post` 重复输出；其余不同源对象即使指向同一帖子也保持各自语义，不做客户端去重或随机轮换。
6. 每个来源在仓储侧按同一游标窗口最多取 `pageSize + 1` 条，Service 合并后再截取；禁止把全表读入内存排序，也禁止在 `PublicDiscoverApp.tsx` 拼接多个列表后自行续页。

### 隐私、治理、缓存与失败

- 可见性、启用、软删除、发布状态和当前神评资格在每次数据库读取时共同判断；举报存在本身不改变公开性，真正的治理动作沿原领域字段收回资格。
- actor 驱动的 `MemberActivity / HighlightedComment` 在用户禁用、删除或公共身份失效时整体退出；不会降级为泄漏历史名称的匿名动态。
- 频道关闭摘要资格、Wiki 转为非公开、评论 / 帖子禁用或删除后，不允许靠旧缓存继续展示。
- 首批完整 feed 响应使用 `Cache-Control: no-store`，只允许一次请求内的批量查询和映射复用；在所有来源都有可靠的租户级 revision 与同步失效生产者前，不启用 Redis、内存或 CDN 跨请求页缓存，也不以短 TTL 代替撤回正确性。
- 某个来源数据库或资格查询失败时，整个 `GetFeed` 返回稳定 `503 / PublicDiscover.SourceUnavailable`；不能返回缺少某类来源的“成功”页，否则同一游标会产生跳项和重复。Docs、问答、榜单与商城的静态上下文入口仍可使用，但不能伪装成已成功读取的内容模块。
- 日志只记录租户、来源类型、候选数、淘汰数、页大小、游标版本和耗时，不记录消息 / 评论正文、私人身份或完整游标 payload。

### 分层与实现状态

- `Radish.Model` 定义枚举和全部 `Vo`；`Radish.IService` 先定义 `IPublicDiscoverService`。
- 各来源通过返回实体的现有或专属仓储完成数据库侧资格与 keyset 窗口查询；Service 只组合仓储结果并映射 Vo，禁止直接使用 `_repository.Db.Queryable`。
- CommentHighlight 的公共集合必须使用能联动 Comment / Post 当前资格的专属仓储方法，不能复用当前只按 `PostId` 返回快照的 Controller / BaseService 查询。
- `Frontend/radish.http` 增加统一类型与 `getPublicDiscoverFeed`；`radish.client` 只消费该客户端，不新增 fetch / axios 封装。
- `2026-08-05` 已按以上分层完成实现，并由仓储 / 服务定向测试、前端静态契约、双语资源测试、type-check、lint 和 production build 约束；全局灰玉品牌 token 不属于本次局部页面实现。

### 分类、标签与结构化状态

- 分类是帖子的唯一主归属，使用品牌柔底、无 `#` 的独立 chip，并进入公开分类页。
- 标签是一篇帖子可拥有的横向主题，使用较轻的 `#标签` chip，并进入公开标签页；标签不得冒充分类，也不承担帖子状态。
- 问答、投票、抽奖、精华和置顶属于结构化状态，继续使用状态 chip；它们与分类、标签分组排列，不复用同一视觉样式。
- 统一流首批只展示类型、标题、摘要、actor 和结构化指标，不在紧凑轨道中复制完整 `PostCard` 的分类 / 标签 / 状态组；进入 Forum 详情或列表后继续由既有卡片明确区分三类语义。

## `P3-10-D` / `R1-P01` 信息结构口径

`/discover` 后续整理目标不是重做品牌首页，而是把已落地的公开内容流继续压实为“打开即可阅读”的社区起点。

### 页面层级

正式 PC 基准采用“公共头部 + 主内容 / 洞察非对称双区”，并保留以下稳定边界：

1. 公共壳层头部只承担产品级导航、登录和真实账户状态，不承载大块说明或后台侧栏。
2. mobile 可以前置搜索与必要筛选，但不能因为控件和说明堆叠而延迟真实社区内容。
3. 页面必须使用当前代码审计确认的真实公开实体、状态和链接；设计中用于表达长期方向的概念对象必须在文档中标明读模型缺口，不能伪装成已存在的数据能力。
4. 帖子、问答 / 投票、作者 / 圈子、Docs、商品、榜单等候选实体必须先分清“内容、关系、活动、知识、激励”职责，再决定模块尺度和优先级。
5. 分类、`#标签` 与结构化状态继续保持语义分层，但分类不再使用占据整行的大型浏览板。
6. 加载、空态、局部失败和分享反馈继续复用共享状态与动作附近反馈，不用整页说明替代内容。

第二轮稿采用的“全宽分类浏览 + 最新讨论主流 + 右侧社区动态 / 热门主题 / 相关内容”已经被否决；后续实现不得退回在帖子主流周围堆叠小卡片的结构。

D61 后的首屏实现要求：

- PC 首个主要视区需要体现社区的多实体、多状态和多尺度关系；候选布局至少并置三类经代码审计确认的社区实体，帖子不得继续占据绝大多数视觉面积。
- mobile 根据已确认 PC 的任务关系重新提炼单列顺序，不把 PC 洞察区机械堆到正文下方，也不等比缩放桌面稿。
- 页面结构应重点吸收参考 `13 / 16 / 18 / 27` 的非对称主次、异构模块尺度、连续工作面和紧凑工具区；`22 / 23` 只用于帖子卡片内部扫描和反馈表达。
- 公共页头右侧使用登录 / 登录态入口和工作台动作，文案保持“发现 / 公开入口”语义，不回退为 WebOS 或内部验收术语。

### 首屏内容密度

- PC 首屏应至少露出公共头部和一组具有明显主次差异的真实社区内容，不以同尺寸卡片网格或单一帖子长流占满首屏。
- 移动首屏应优先保证第一张可读内容卡片尽早出现；头部次级动作可以继续图标化，说明型卡片不应连续堆叠。
- hero / guide / summary 只能保留帮助用户理解内容来源的最小信息；如果后续改版需要新增大面积引导、插画或营销文案，必须先进入设计源文件评审。
- 空态、加载态、整流失败和续页失败态必须继续说明内容来源、下一落点和公开边界，但不能用长文案替代真实内容。

### 内容卡片优先级

跨实体能力审计、低保真比较和代码实现已经完成，当前能力优先级为：

1. 跨类型顺序、snapshot cutoff、去重与分页只由 `PublicDiscover` 服务端读模型负责，客户端不伪造热度、推荐或来源权重。
2. Docs、榜单和商品在本页只承担上下文导航，不读取各自列表来填充装饰性摘要。
3. 问答与帖子共享公开资格但保持不同 item 语义；圈子、投票、分类和标签不因已有接口就自动进入匿名混合流。
4. 若后续增加来源、推荐解释或个性化，必须扩展专题契约、撤回语义和版本化游标，不能在 `PublicDiscoverApp.tsx` 重新堆叠来源拼装规则。

### 与相邻页面分工

| 页面 | `/discover` 需要交接的内容 | 不应接管的职责 |
| --- | --- | --- |
| `/forum` / `/forum/post/:id` | 公开帖子入口、标签入口、轻互动登录回流 | 帖子详情、评论树、点赞、投票、编辑、治理 |
| `/docs` / `/docs/:slug` | 公开文档入口和阅读回流 | 文档目录治理、搜索完整体验、编辑 |
| `/shop` | 商品公开浏览入口 | 购买、订单、背包、资产校验 |
| `/leaderboard` | 榜单入口和公开个人页线索 | 榜单类型完整筛选和管理 |
| `/messages` | 登录后进入公共频道或具体消息 | 频道历史、成员、在线状态、消息权限和实时会话 |
| `/circle` | 登录态关系链复访入口 | 关注流、粉丝列表、私域动态、推荐算法 |
| `/me` | 登录态个人状态复访入口 | 完整个人中心、完整钱包、账号设置 |

### 实现闭环与验收停止线

- `R1-P01 / 社区发现` PC / mobile 已通过业务和视觉审核，统一公开读模型和 Channel opt-in 范围已获确认并完成代码实现。
- `publicSeoStatic.test.ts` 已从旧的“帖子主流 + 少量辅助内容”改为约束统一 feed、连续轨道、稳定游标消费和真实跨入口链接。
- 不改变 `/discover` 公开集合页 canonical、OpenGraph、JSON-LD 和 sitemap 口径。
- 不把来源返回状态写入 URL、分享链接或 head 输出。
- 不新增推荐系统、关系链私域数据、登录后工作台操作或 Console 入口。
- “社区正在发生”必须按本文统一公开读模型实现；禁止匿名直读现有授权聊天室 API，也不得把 `/circle` 私域关注流或尚不存在的短动态对象伪装成公开数据。
- 不破坏普通点击保留来源返回、新标签打开使用真实公开 `href` 的契约。
- PC 与移动两种代表视图都必须证明没有横向溢出、按钮文字挤压或首屏只剩说明文字；两张 Pencil 画板的节点检查与用户评审均已通过。
- Gateway 匿名 / 种子管理员登录回流、PC `1440 × 1000`、mobile `390 × 844`、双语、代表主题、真实链接和 Console 治理只读路径已通过；页面与 Console 均无页面级横向溢出。浏览器未提交频道状态变更，实际写入、幂等、版本冲突与事件继续由后端定向测试覆盖。

## 前端结构

```text
Frontend/radish.client/src/public/discover/
├── PublicDiscoverApp.tsx       # 数据装载、路由交接、分享和页面渲染
├── PublicDiscoverApp.module.css

Frontend/radish.client/src/public/components/PublicShellHeader.tsx
Frontend/radish.client/src/components/web-shell/                  # WebStateSlot

Frontend/radish.http/src/public-discover-contract.ts
Frontend/radish.http/src/public-discover-client.ts
```

## URL 与来源返回

- `/discover` 本身是公开集合页，canonical 和分享链接不携带工作台状态。
- 从 `/discover` 进入 forum / docs / shop / leaderboard / 公开主页后，返回动作应优先回到社区发现页和原阅读上下文。
- 公开详情来源返回使用浏览器 `history.state`，不写入 URL query、canonical、OpenGraph 或 sitemap。
- 内容卡片必须保留真实公开 `href`：普通点击可以同时写入 `history.state` 保存来源返回；新标签打开、复制链接、浏览器地址栏、canonical 和结构化数据只依赖公开真实路径。
- 普通浏览器根路径 `/` 当前进入 `/discover`；Tauri / WebOS 工作台仍保留 `/desktop`。

## 公开 head 与结构化数据

`/discover` 使用公开壳层统一 head helper 和结构化数据 helper：

- 作为公开集合页输出 `CollectionPage` JSON-LD。
- canonical 使用公开 Gateway origin。
- 分享链接只保留公开路径，不携带来源状态、窗口参数或登录后上下文。

## 维护约束

- 内容卡片应优先使用公开标识和公开路径；旧 LongId 只作为兼容打开或内部接口参数。
- 页面可以增加登录后轻互动入口，但不能把工作台动作搬进首屏内容流。
- 新增内容来源前应先确认已有公开 API、公开 head、移动 / PC 布局和来源返回语义。
- 从 `/discover` 进入 `/circle` 或公开详情后再继续打开内容详情时，来源交接只允许使用当前标签页的一次性状态；不得把来源状态固化进分享 URL 或 SEO 输出。
- 跨类型时间序只由统一公开读模型负责；若后续需要推荐解释或个性化，仍须单独设计，不在当前页面或现有游标上追加 ad hoc 权重。

## 验证要点

- PC 下 `/discover` 能区分分类、标签和结构化状态，首个主要视区并置至少三类经审计确认的公开实体，不使用全宽分类浏览大块，也不让帖子重新占据绝大多数面积；mobile 按获选结构的主任务重新排序，不把 PC 侧栏机械堆到正文下方，也不退回 WebOS Shell。
- 卡片进入 forum / docs / shop / leaderboard / 公开主页后，返回语义稳定。
- 公开帖子卡片优先进入 `/forum/post/:publicId`。
- 公开主页入口优先进入 `/u/usr_...`，旧 LongId 仅保留兼容。
- 卡片右键打开新标签、复制链接和普通点击都能进入同一公开详情；只有普通点击额外保留来源返回状态。
- head、canonical、OpenGraph 和 JSON-LD 不携带登录后状态或来源状态。
- Channel 默认不进入匿名流；只有显式 `DiscoverVisibility=Summary` 才输出元数据和安全聚合，响应中不存在消息正文、在线人数或成员名单。
- 社区脉搏只显示 `VoPulse` 的公开频道、近 `24` 小时新增内容和知识贡献；不把累计浏览量伪装成日增量，窗口与分页 snapshot cutoff 保持一致。
- Wiki 转私有、频道关闭摘要、神评失效、Comment / Post 禁用或删除、actor 失效后立即退出；CommentHighlight 历史快照不能绕过当前资格。
- 相同 cutoff 下多页遍历无重复、无跳项且刷新后才看到新 item；非法游标、来源失败、无跨请求缓存和 `Cache-Control: no-store` 均有定向测试。
