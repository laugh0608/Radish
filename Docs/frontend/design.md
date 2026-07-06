# 前端设计文档

> Radish 第一开发阶段以前端 **WebOS / 超级应用** 为主入口完成首版交付；第二开发阶段开始演进出公开内容壳层、Flutter 移动客户端与 Tauri 桌面壳验证。`2026-05-25` 路线复盘后，前端主线收敛为 **纯 Web + Flutter**：根路径 `/` 与默认浏览器入口转向纯 Web，`/desktop` 仅保留为 WebOS 历史入口，PC/Tauri 后置且不再绑定 WebOS。`2026-06-21` 起，公开商品购买回流、订单 / 背包、完整个人中心子路径、论坛作者态和 Docs 作者态首批能力已进入正式 Web 路由；`2026-06-23` 起，`/workbench` 承担正式 Web 功能地图，公共头部“工作台”动作默认进入 `/workbench`，再由其中的历史入口进入 `/desktop`。本文档描述当前前端事实、演进方向与相关实现约束。

## 1. 设计理念

### 1.1 第一阶段核心概念：WebOS

**Radish 不是一个网站，而是一个运行在浏览器中的操作系统。**

```
用户访问 radish.client
        ↓
桌面系统（Desktop Shell）
        ↓
根据显示规则呈现应用图标
        ↓
匿名可直接打开公开应用
        ↓
登录后解锁聊天 / 个人能力
        ↓
[论坛] [文档] [商城] → 窗口模式
[控制台] → 外部应用
```

### 1.2 当前定位：纯 Web + Flutter 主线

截至 `2026-05-25`，当前官方定位已经从“所有能力统一走桌面入口”和“三端并行分工”进一步收敛为：

- **纯 Web 壳层**
  - 面向公开浏览、分享传播、搜索流量、PC / 移动浏览器与登录后轻量链路
- **Flutter 移动客户端壳层**
  - 面向 Android / iOS 原生客户端，不复刻 WebOS 窗口系统
- **WebOS `/desktop` 保留入口**
  - 面向已有桌面工作台能力和迁移过渡，不再作为新增功能默认承载层
- **PC/Tauri 后置增强壳**
  - 若后续重启，面向纯 Web 增强体验，不再默认分发 WebOS

当前决策以 [前端多壳层策略](/frontend/shell-strategy) 为准。

### 1.3 设计目标

1. **统一产品身份**：保持一个 Radish，而不是分裂成互不相认的多套前端
2. **权限控制**：公开内容匿名可访问，登录后能力按登录态与权限分层控制
3. **Web 优先体验**：浏览器默认入口服务公开访问、移动阅读、PC 浏览器使用和轻量登录后链路
4. **内容直达能力**：公开内容不强制要求先进入桌面再打开窗口
5. **多端扩展性**：移动 Web 与 Flutter 可以复用数据、认证和主题语义，但不强求界面结构一致

### 1.4 当前边界

- 当前代码事实仍然保留 `Desktop Shell + WindowManager`，但该能力后续仅作为 `/desktop` 历史入口维护和迁移来源
- `Clients/radish.flutter` 当前已完成 Android MVP 第一轮 RC 验收并持续补齐移动主路径：壳层登录态分发、公开 forum / docs / discover / profile 读取、forum detail 阅读与评论发布 / 回复、detail 原地登录续接、已登录态轻量 forum notification 列表回流、profile 复访、docs 搜索 / 内链、公开商城列表与详情、登录态单商品购买、商品详情余额展示、订单 / 背包 / 钱包只读回流、公开详情链接复制、轻回应即时前插、作者帖子正文编辑和作者根评论编辑均已落地；当前仍明确保持移动主路径边界，不扩完整通知中心、系统通知栏推送、完整发帖编辑器、完整商城工作台、系统分享 SDK、点赞、投票、回答编辑、子评论编辑或桌面治理能力
- Android / iOS 移动安装包继续以 Flutter 为主线；Capacitor Android spike 已清理出当前代码，只保留历史记录作为公开只读 React 页面复用的技术参考，不进入登录态移动端产品化路线
- Windows / macOS / Linux 桌面安装包曾完成 `Tauri 壳 + WebOS 桌面工作台` 个人开发阶段验证；路线复盘后，PC/Tauri 放到最后再评估，若重启应增强纯 Web 体验，不再默认绑定 WebOS。Tauri 不是移动端替代方案，也不是原生 UI 重写路线
- WebOS 桌面工作台当前已补首批“继续使用”复访面板：桌面首页按最近应用、最近浏览、我的轻回应分组承接已登录用户的回到工作台场景；最近应用使用本地轻量记录，最近浏览与我的轻回应复用既有 API 与工作台打开能力；forum 回流统一优先使用 `postPublicId`，旧 `postId` 仅作为兼容 fallback，docs / shop 仍保留现有 slug 或 long 路由兼容但不把旧 long 路径作为用户可见文案；该面板不等于完整历史中心，不扩删除 / 清空、跨端同步或新的后端 API
- 公开内容壳层当前已完成 `/discover`、forum、docs、个人公开页、公开榜单与公开商城浏览入口，并继续补到 forum 公开分类、forum 公开搜索与 docs 公开搜索首批：`/discover`、`/forum`、`/forum/category/:categoryId`、`/forum/search`、`/forum/post/:postId`、`/docs`、`/docs/search`、`/docs/:slug`、`/u/:identifier`、`/leaderboard`、`/leaderboard/:type`、`/shop`、`/shop/products` 与 `/shop/product/:productId` 都已可直接进入公开壳层；其中 forum detail 路由参数当前可承接 `Post.PublicId` 或旧 long 字符串，公开个人页路由参数当前可承接 `User.PublicId` 或旧 long 字符串，canonical / 分享 / 普通内容入口优先使用 `PublicId`
- 纯 Web 已开始承担根路径 `/` 与默认浏览器入口；普通浏览器 `/` 当前进入 `/discover` 公开分发页，公开内容壳层的已有路径是纯 Web 主线的第一批基础，不再回塞进 WebOS 窗口系统
- 纯 Web 登录态私域与作者态入口当前已覆盖 `/notifications`、`/circle`、`/me`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience`、`/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`、`/messages`、`/pet`、`/forum/compose`、`/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`：分别承接通知列表 / 目标分流、关注动态与关系链复访、个人状态与内容历史复访、资产流水、商城购买结果 / 订单 / 背包、会话 / 消息定位、电子宠物领取与照顾、论坛发帖和 Docs 作者入口；这些路由不进入公开 sitemap，不替代 `/desktop` 的完整工作台能力，细节见 [纯 Web 私域复访入口设计说明](/frontend/private-web-revisit) 与 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)
- `/workbench` 当前作为正式 Web 社区活动中心与功能总入口，除公开浏览、登录态私域、后台治理和历史桌面四组功能地图外，还会汇总通知行动、聊天未读 / 提及 / 草稿、论坛草稿、订单 / Docs / 宠物等继续处理项；公共头部“工作台”动作指向 `/workbench`，`/desktop` 作为 WebOS 历史工作台入口保留在功能地图内
- `/legal` 当前作为公开用户承诺入口，承载社区内容规范、隐私边界、账号安全、通知、虚拟商品和退款 / 不退款边界；`/legal` 与登录态 `/me` 复用同一套隐私与安全边界组件，帮助用户区分公开、本人私域、仅 Console 和不可公开数据。
- 公开内容壳层当前已形成共享头部视觉和动作基线：forum / docs / discover / leaderboard / shop / `u/:identifier` 在窄屏下统一使用品牌字、图标与按钮 token；主动作收口为“社区发现 / 我的圈子 / 工作台”，其中“工作台”进入 `/workbench`，不直接打开 WebOS 桌面壳；共享头部和移动底栏由 `components/web-shell/WebShellHeader` 承接，页面状态槽由 `WebStateSlot` 承接
- `P3-12-D2` 已将公开 Web 统一体验设计源 `Docs/frontend/design-sources/public-web-unified-experience.pen` 扩展为 `P01-P16` 公开社区 App 页面族，覆盖公开首页、发现流、论坛列表 / 详情、紧凑评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开主页和移动公开任务流；实现口径见 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- `P3-12-D3` 已将私域与作者态 Web 工作流设计源 `Docs/frontend/design-sources/private-web-workflows.pen` 扩展为 `P01-P30` 真实路由驱动页面族，覆盖 `/workbench`、`/me` 系列、资产流水、订单、背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态和移动端 10 个单任务页面；实现口径见 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)
- `P3-12-D4 / D7` 已将 Web UI 共享基座设计源 `Docs/frontend/design-sources/web-ui-foundation.pen` 扩展为 `F01-F02`，统一 public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab、client 公共壳层组件契约和 5 项以内浮动胶囊移动底栏；跨业务设计源的视觉样式先在 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design) 确认，再同步到具体 `.pen`
- `P3-12-D8` 已将共享 Web shell 首批落入 `radish.client` 代码：`Frontend/radish.client/src/components/web-shell/` 提供 `WebShellHeader`、`WebStateSlot` 和对应类型，公开 / 私域页面优先复用该目录，不再各自维护分叉 header、状态卡、移动 tab 或内容宽度硬编码
- 正式 Web 可恢复错误反馈遵循 [可恢复错误与诊断复制](/frontend/recoverable-error-diagnostics)：页面级加载失败、上传失败、聊天发送失败和通知目标缺失必须提供可继续行动的恢复路径与有限诊断上下文
- 进入跨页面视觉代码实现前，必须先确认对应设计源、设计说明和共享基座是否一致；不得绕过 Pencil 设计稿直接把 public、private 或 Console 的 header、按钮、卡片样式各自写成分叉版本
- Console 当前已形成 `Case Desk` 设计方向：低饱和暖灰 / 纸色背景、轻侧栏、克制边框、明确按钮层级和可扫描的后台信息密度，设计稿见 `Docs/frontend/design-sources/console-governance-workbench.pen`；该方向可作为 `radish.client` 后续重新设计时的视觉气质参考，但不直接复刻 Console 的管理后台信息结构
- Console 当前按页面类型选择实现基座：治理页使用“队列 / 详情 / 动作留痕”，表格 CRUD 使用“指标 / 工具条 / 表格 / 摘要栏”，设置页使用“分组导航 / 设置列 / 影响范围”，调度总览使用“关键指标 / 快捷操作 / 最近事项 / 右侧入口”；内容治理、经验治理、订单 / 商品、文档治理、用户管理和权限矩阵已补任务流提示和证据 rail，移动端内容治理优先按“筛选队列 -> 目标证据 -> 处理动作 -> 留痕回看”顺序承载；新增或明显改动页面优先复用 `--console-*` token、`AdminLayout` 和 `adminFeature.css`
- `/discover` 当前已从公开导航聚合页推进为公开内容流：首屏和内容区会复用公开帖子、公开文档、商品和榜单入口，让用户先在同一页面判断下一步阅读路径，再进入 forum / docs / leaderboard / shop / 公开主页
- `/discover` 当前继续保留公开来源返回；从公开专题页顶部回到“社区发现”时，应优先回到公开发现语境，而不是每次都丢回桌面或其他专题默认页
- 公开内容卡片当前要求输出真实公开 `href`：普通点击可以通过 `history.state` 保留来源返回，新标签打开、复制链接、canonical、OpenGraph、JSON-LD 和 sitemap 不携带来源状态或桌面窗口参数
- 公开内容壳层当前仍保持分批阅读优先边界：forum 公开详情已开放登录后轻回应、根评论发布，以及受控作者态 `intent=answer|edit|history`；`/forum/compose` 承接正式 Web 发帖入口。点赞、评论回复直达、投票提交、删除、治理或完整通知中心仍不进入公开详情主流程；文档阅读不承载编辑、发布、回收站或版本历史等桌面治理交互
- `/circle` 当前作为“我的圈子”登录后关系流入口，不进入公开 SEO 或分享范围；未登录访问走登录回流，登录后保留圈子来源，并允许继续把来源状态一次性交接给公开详情
- `/notifications`、`/me`、`/messages`、`/pet` 进入公开详情或公开个人页时同样使用一次性来源转交，返回文案分别保持“通知中心 / 我的状态 / 消息 / 电子宠物”；新开标签、复制链接、canonical、OpenGraph 和 sitemap 仍只保留公开 URL
- forum 公开分类、公开标签、公开结构化类型与公开搜索首批当前只承载分类 / 标签 / 类型上下文、关键词检索、帖子列表阅读、排序分页与详情回跳上下文；标签 SEO 深化仍放在后续规划
- 个人公开页首批当前只承载公开资料、公开统计、公开帖子与公开评论阅读；不把编辑资料、浏览记录、附件管理或完整关系链治理搬进公开壳层
- 个人公开页首屏当前也已开始补“公开主页阅读说明”这一类只读说明增强：优先解释基础资料、公开帖子 / 评论阅读与工作台边界，而不是把个人治理或账号历史动作误带进公开壳层；帖子与评论内容项必须给出明确的“打开帖子详情 / 打开评论上下文”动作，普通点击进入公开 forum 详情并保留当前公开主页来源返回
- 公开 docs 详情当前已补访问属性、文档属性和时间线三组元信息：访问属性展示可见性 / 发布状态，文档属性展示 slug / 来源类型，时间线展示更新时间 / 创建时间；这些信息帮助公开读者理解文档来源和只读边界，不引入编辑、发布、回收站或版本历史入口
- 公开榜单首批当前只承载榜单切换、分页、登录用户“我的排名”增强，以及用户榜单跳转个人公开页；默认经验榜单页 `/leaderboard`（兼容 `/leaderboard/experience` 收口）当前会额外展示“经验体系公开展示”说明，但不把经验明细、商城详情、购买链路或其他工作台动作搬进公开壳层
- 公开榜单的非经验榜单当前也已开始补轻量只读说明：用户榜单会强调“公开比较 + 公开个人页跳转 + 不带账号明细”，商品榜单会强调“只读展示 + 购买从商品详情登录后继续 + 订单 / 背包留在私域 Web 路由”
- 公开商城浏览首批当前承载首页、商品列表与商品详情阅读；购买确认通过 `/shop/product/:productId?intent=purchase` 进入登录后购买现场，订单和背包分别由 `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 承接，举报或其他“我的”动作仍不搬进公开壳层
- 公开商城当前也已开始补结构化导览：`/shop` 与 `/shop/products` 会强调“先看内容 / 继续进入 / 不在这里”，商品详情则会明确“详情重点 / 登录后继续购买 / 订单与背包归属”，避免把公开浏览误读成完整私域商城页
- 公开 docs 搜索当前也已开始补结构化只读导览：`/docs/search` 会强调“先看结果 / 继续进入 / 不在这里”，把关键词检索、结果回跳与桌面治理边界拆清楚
- 公开入口的图片展示当前继续沿附件运行时 URL 口径：商品、榜单与社区分发页若引用仍有效的业务附件，不应再因后台清理误删而退化成前端 404 坏图
- `/discover` 内容流卡片在 PC 与移动视角下都要保持可扫描的信息密度、稳定高度和清晰去向；forum / docs / leaderboard / shop 分区推荐项在窄屏下也要保持一致的留白节奏
- Flutter forum 当前的登录、回流与轻互动语义也已进一步明确：详情页允许匿名用户从评论区或轻回应区原地发起 OIDC 登录，并在浏览器回跳后继续保留当前 `postPublicId / postId / commentId` 上下文；已登录壳层可读取最近 forum 通知并回到帖子 / 评论上下文，未读 forum 通知打开详情前会尝试标记已读；通知、个人公开页、我的轻回应与最近访问回流均优先消费 `postPublicId / targetSlug / PublicId`，旧 `postId / routePath` 只保留为字符串 fallback；进入详情后再使用真实 `VoId` 执行评论、轻回应和定位类内部接口，不把公开标识误传给内部接口；纯文本发帖成功后打开新帖详情并使用详情返回的 `Post.PublicId` 展示公开链接，失败保留输入；评论发布 / 回复成功只更新评论区，轻回应发布成功只更新轻回应墙与局部反馈，不刷新正文或来源 tab；作者帖子正文编辑和作者根评论编辑已接入失败重试 `clientSubmissionId`，但富文本、附件、点赞、投票、子评论编辑、回答编辑、完整通知中心与系统通知栏推送仍不在当前边界内
- Flutter 公开主页当前会记录发现页、forum 作者入口和榜单来源；公开主页内继续打开帖子 / 评论详情后，Android Back 先回公开主页，再回原来源 tab，不把用户强制留在 profile tab
- Flutter 登录态“我的”页当前承接基础个人资料编辑：读取 `User/GetMyProfile`，保存 `User/UpdateMyProfile`，支持用户名、邮箱、展示名称、年龄和地址；保存成功刷新原生公开资料摘要。头像上传、密码修改、完整账号设置和关注管理仍不在当前 Flutter 边界内
- Flutter 原生 docs detail 当前复用 Web 公开 docs 路由口径：公开文档正文里的 `/docs/:slug`、完整公开 URL、`docs/:slug`、`./:slug` 与普通相对 slug 链接会继续打开原生 docs detail；页内锚点、附件路径和非 docs 链接不在 Flutter 内扩成外部跳转或附件治理
- Flutter 原生 shop 当前承接公开商城列表、商品详情、登录态单商品购买、订单列表、订单详情、背包、订单扣款流水筛选和来源订单 / 商品查看；发现页来源回发现页，列表来源回商城列表。移动端购买固定为单商品动作，商品详情登录态先读取当前胡萝卜余额，再检查购买资格、输入支付口令并在成功后刷新余额和进入订单详情；订单详情可按订单 ID 查看扣款流水并进入背包发放确认，失败态保留商品、订单、背包或钱包来源上下文；购物车、退款、权益激活、道具使用和 Console 治理仍不在 Flutter 边界内
- Flutter 原生 forum / docs / shop detail 的公开链接展示与复制使用当前 Gateway Base URL 加 Web 公开路由，不复制内部 handoff、`radish://` deep link、API 地址、来源 tab 或评论定位状态；当前只提供剪贴板复制，不接系统分享 SDK、海报生成或分享统计
- 公开详情页来源返回使用 `history.state` 保留来源语义，不污染公开 URL、canonical、分享链接或 sitemap；详情加载后如需规范化到 `Post.PublicId`、`User.PublicId` 或真实 docs slug，replace 必须保留当前来源返回状态。公开商品详情需要继续购买时，当前通过 `/shop/product/:productId?intent=purchase` 进入正式 Web 登录回流，未登录用户可保存该返回路径并在 OIDC 回调后恢复到原商品详情。订单详情、订单列表和背包入口分别通过 `/shop/order/:orderId`、`/shop/orders`、`/shop/inventory` 承接，并要求登录后消费；`/desktop?app=shop...` 只作为 WebOS 历史工作台深链保留
- HTTPS Gateway 下的公开页面会把本地 HTTP 媒体、favicon、头像和 Markdown 附件归一到当前 Gateway origin；公开分享链接通过运行时公开域名配置生成，docs 分享保留锚点，canonical / sitemap 不携带临时来源状态；公开详情与公开集合页都会输出运行时 JSON-LD，但公开商品仍不把积分价格伪装成法币 offer
- 后续不立即删除现有 WebOS 路由，而是把 `/desktop` 作为保留入口，按价值把既有高价值能力逐步迁移到纯 Web 或 Flutter

## 2. 系统架构

> 说明：本节的大部分代码与结构图仍然描述 **当前 WebOS `/desktop` 保留入口的真实实现**。纯 Web、Flutter 与 PC/Tauri 后置方向的职责分工，请优先参考 [前端多壳层策略](/frontend/shell-strategy)。

### 2.1 整体结构

```
┌────────────────────────────────────────────────────┐
│               Radish Desktop Shell                  │
│  ┌────────────────────────────────────────────┐    │
│  │ 状态栏：用户 | IP | 消息 | 系统状态         │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  桌面应用图标（基于权限显示）：                       │
│  ┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐              │
│  │论坛 │  │聊天 │  │商城 │  │文档 │              │
│  │ 📝  │  │ 💬  │  │ 🛒  │  │ 📄  │              │
│  └─────┘  └─────┘  └─────┘  └─────┘              │
│  ┌─────┐  ┌─────┐                                 │
│  │后台 │  │游戏 │  ... (更多应用)                  │
│  │ ⚙️  │  │ 🎮  │                                │
│  └─────┘  └─────┘                                 │
│  ↑ 仅管理员可见                                      │
│                                                     │
│  继续使用：最近应用 | 最近浏览 | 我的轻回应          │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ Dock：论坛(运行中) | 聊天室(运行中)          │    │
│  └────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────┘
```

### 2.2 技术架构

```
Frontend/radish.client/
├── src/
│   ├── desktop/              # 桌面系统核心
│   │   ├── Shell.tsx         # 桌面外壳（容器）
│   │   ├── StatusBar.tsx     # 顶部状态栏
│   │   ├── Desktop.tsx       # 桌面图标网格
│   │   ├── components/       # 桌面分区组件（继续使用等）
│   │   ├── Dock.tsx          # 底部 Dock 栏
│   │   ├── WindowManager.tsx # 窗口管理器
│   │   ├── AppRegistry.tsx   # 应用注册表
│   │   └── types.ts          # 类型定义
│   │
│   ├── apps/                 # 子应用（各功能模块）
│   │   ├── forum/            # 论坛应用
│   │   │   ├── ForumApp.tsx  # 应用入口
│   │   │   ├── pages/        # 页面
│   │   │   ├── components/   # 组件
│   │   │   └── routes.tsx    # 路由
│   │   │
│   │   ├── chat/             # 聊天室应用
│   │   ├── shop/             # 商城应用
│   │   ├── admin/            # 后台管理应用
│   │   ├── wiki/             # 文档应用（窗口）
│   │   └── games/            # 游戏应用（示例）
│   │
│   ├── widgets/              # 桌面小部件
│   │   ├── DesktopWindow.tsx # 窗口组件
│   │   ├── AppIcon.tsx       # 应用图标
│   │   └── Notification.tsx  # 通知组件
│   │
│   ├── components/
│   │   └── web-shell/        # 纯 Web public / private 共享壳层与状态槽
│   │
│   ├── public/               # 公开 Web 页面族：discover / forum / docs / shop / leaderboard / profile
│   ├── workbench/            # 正式 Web 功能地图
│   ├── me/                   # 登录态个人状态与个人中心子路径
│   ├── messages/             # 登录态消息复访入口
│   ├── notifications/        # 登录态通知复访入口
│   ├── circle/               # 登录态关系链复访入口
│   ├── pet/                  # 登录态电子宠物入口
│   │
│   ├── shared/               # 共享代码
│   │   ├── ui/               # 基础 UI 组件
│   │   ├── api/              # API 客户端
│   │   ├── auth/             # 认证逻辑
│   │   ├── hooks/            # 通用 Hooks
│   │   └── utils/            # 工具函数
│   │
│   └── stores/               # 全局状态
│       ├── windowStore.ts    # 窗口状态
│       ├── dockStore.ts      # Dock 状态
│       └── userStore.ts      # 用户状态
```

## 3. 应用注册系统

当前口径：

- WebOS 应用统一通过应用注册表声明图标、窗口类型、权限和入口组件
- 匿名可打开公开应用；登录后解锁聊天、个人中心等用户能力
- Console 仍作为外部后台入口，不嵌入 WebOS 窗口
- 最近应用、最近浏览、我的轻回应共同承接“继续使用”入口

应用注册、权限控制和继续使用入口细节见 [WebOS 应用注册与窗口系统](/frontend/webos-shell-architecture)。

## 4. 窗口系统

当前口径：

- 窗口状态集中管理，避免各应用自行维护窗口生命周期
- 普通窗口、工具窗口、外部应用入口按场景区分
- 最小化窗口不渲染内容，避免无意义资源占用

窗口类型、窗口管理器和状态管理细节见 [WebOS 应用注册与窗口系统](/frontend/webos-shell-architecture)。

## 5. 子应用开发

### 5.1 论坛应用示例

```typescript
// apps/forum/ForumApp.tsx
export const ForumApp = () => {
  return (
    <div className="forum-app h-full flex flex-col">
      <ForumHeader />
      <div className="flex-1 overflow-hidden">
        <Routes>
          <Route path="/" element={<PostList />} />
          <Route path="/post/:id" element={<PostDetail />} />
          <Route path="/create" element={<CreatePost />} />
          <Route path="/category/:id" element={<CategoryView />} />
        </Routes>
      </div>
    </div>
  );
};

// apps/forum/pages/PostList.tsx
const PostList = () => {
  const { data } = useQuery({
    queryKey: ['posts'],
    queryFn: () => api.getPosts()
  });

  return (
    <div className="post-list">
      {data?.items.map(post => (
        <PostCard key={post.id} post={post} />
      ))}
    </div>
  );
};
```

### 5.2 后台管理应用

```typescript
// apps/admin/AdminApp.tsx
import { Layout, Menu } from 'antd';

export const AdminApp = () => {
  return (
    <Layout className="h-full">
      <Layout.Sider>
        <Menu
          items={[
            { key: 'dashboard', icon: <DashboardOutlined />, label: '仪表盘' },
            { key: 'apps', icon: <AppstoreOutlined />, label: '应用管理' },
            { key: 'users', icon: <UserOutlined />, label: '用户管理' },
            { key: 'roles', icon: <TeamOutlined />, label: '角色管理' }
          ]}
        />
      </Layout.Sider>
      <Layout.Content>
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/apps" element={<AppManagement />} />
          <Route path="/users" element={<UserManagement />} />
          <Route path="/roles" element={<RoleManagement />} />
        </Routes>
      </Layout.Content>
    </Layout>
  );
};
```

## 6. 移动端与纯 Web 适配（执行中：纯 Web 主线与 Flutter 主线并行）

> 截至 `2026-05-25`，`radish.client` 已形成公开内容直达路径和 WebOS `/desktop` 保留入口，普通浏览器根路径 `/` 已切向 `/discover` 公开分发页；移动安装包继续走 Flutter，不做移动版 WebOS。本节描述的是“已落地事实 + 纯 Web / Flutter 后续方向”的组合口径。

### 6.1 当前现实

- 论坛等个别页面已有窗口内响应式处理，但这不等于真正的移动端产品形态
- 当前代码仍保留桌面 Shell、Dock 与窗口系统；产品主入口口径已转向纯 Web
- 公开内容壳层当前已完成 forum、docs、个人公开页、公开榜单与公开商城浏览五个首批入口落地；帖子列表、分类直达、搜索直达、帖子详情、公开文档目录、公开文档详情、个人公开页、公开榜单与公开商城入口都可以绕开桌面 Shell 直接进入公开阅读形态
- Android MVP 第一轮已完成后，前端多端形态不再按“Flutter 扩所有平台”或“React WebView 统一所有端”继续推进；当前设计分工固定为纯 Web 浏览器主线、Flutter 移动原生安装包主线、WebOS `/desktop` 保留迁移线和 PC/Tauri 后置增强壳
- Tauri 桌面壳个人开发阶段安装包验证已通过，但 PC 客户端不作为近期主线；后续若重启，应承载纯 Web 增强体验，不再默认进入 `/desktop`
- 公开 forum 当前已冻结“列表 + 分类 + 标签 + 结构化类型列表 + 搜索 + 详情 + 轻回应墙展示 + 评论阅读”的公开阅读结构；详情页额外开放登录后轻回应、根评论和受控作者态 `answer / edit / history`，`/forum/compose` 承接正式 Web 发帖。评论回复直达、点赞、投票提交、删除和治理仍不进入公开壳层主流程
- 公开文档阅读当前冻结“目录 + 搜索 + 正文阅读 + 详情元信息分组 + 复制公开链接 + 返回浏览态 + 文档内链跳转”，并明确保持只读阅读边界；当前已补齐返回目录滚动位置保持、搜索结果上下文回跳、详情页复制链接入口，以及旧 `__documents__` 文档链接继续落入公开 docs 壳层
- 公开榜单当前已开始补“经验体系公开展示”这一类只读说明增强：优先解释排行依据、等级含义与公开边界，而不是直接把桌面里的“我的经验明细”搬进公开壳层
- 如果直接把完整窗口系统压缩到手机宽度，交互成本和信息密度都会失衡

### 6.2 规划策略

- 浏览器端应进入纯 Web 响应式主线，而不是继续以 WebOS 作为默认入口
- 第一批已先从公开内容浏览起步：forum 列表、分类直达、搜索直达、帖子详情、轻回应墙展示与评论阅读当前已进入公开内容壳层；公开帖子详情的登录后轻回应和根评论作为轻参与链路接入，不代表完整论坛工作台迁入公开壳层
- 个人公开页、公开榜单与公开商城浏览首批当前都已先接入公开内容壳层，更深的轻互动能力与商城购买链路仍按价值逐步接入，不一次性照搬桌面 App
- 登录后的轻量链路按价值逐步接入纯 Web 或 Flutter，不一次性照搬全部桌面 App

### 6.3 规划示意

```typescript
// 规划示意，非当前仓库实现
const Shell = () => {
  const isMobile = useMediaQuery('(max-width: 768px)');

  if (isMobile) {
    return <MobileShell />;
  }

  return <DesktopShell />;
};
```

### 6.4 移动端布局（规划）

```
移动端目标形态：

┌────────────────────────┐
│ 状态栏 / 顶部导航        │
├────────────────────────┤
│                        │
│   当前内容页面           │
│   （论坛 / 文档 / 我）   │
│                        │
├────────────────────────┤
│ Tab: 首页|论坛|消息|我   │
└────────────────────────┘
```

```typescript
// 规划示意，非当前仓库实现
const MobileShell = () => {
  const mobileRoutes = getMobileRoutes({
    isAuthenticated,
    userRoles,
    userPermissions
  });

  return (
    <div className="mobile-shell">
      <StatusBar />
      <Routes>{mobileRoutes}</Routes>
      <MobileTabBar />
    </div>
  );
};
```

## 7. 技术栈

当前技术栈总览：

- React 19 + Vite（Rolldown）+ TypeScript
- npm workspaces 管理 `radish.http`、`radish.client`、`radish.console`、`radish.ui`
- API 客户端统一使用 `@radish/http`
- 纯 Web、Flutter、WebOS `/desktop` 保留入口、Console 后台和 PC/Tauri 后置增强壳按职责分工推进

专题细节见 [前端技术栈细节](/frontend/technical-stack)、[@radish/http](/frontend/http-client) 与 [前端 workspace 开发指南](/frontend/development)。

## 8. 设计系统

### 8.1 Design Tokens

```typescript
// shared/config/tokens.ts
export const tokens = {
  colors: {
    desktop: {
      background: '#1a1a2e',
      foreground: '#eee'
    },
    primary: '#00adb5',
    secondary: '#393e46'
  },
  spacing: {
    dock: 64,
    statusBar: 40,
    appIconGap: 24
  },
  borderRadius: {
    window: 12,
    appIcon: 16
  },
  shadows: {
    window: '0 8px 32px rgba(0,0,0,0.3)',
    appIcon: '0 2px 8px rgba(0,0,0,0.2)'
  }
};
```

### 8.1.1 当前主题与 i18n 落地

当前口径：

- `radish.client` 主题状态由根级主题能力驱动
- 新增 UI 改造优先复用语义 token，不继续扩硬编码颜色
- 高频桌面壳层、商城、论坛、聊天、通知、个人中心和文档应用已完成首轮主题 / i18n 接入
- Console 后续新增或明显改动页面优先使用 `--console-*` 局部变量承接 `@radish/ui` / `--theme-*` token，并按页面类型复用 `adminFeature.css` 中的功能页、表格、设置、详情、工作台和摘要栏结构，不启动后台整站视觉重构
- 后续只在真实联调中处理残余边角，不在设计入口继续追加流水

主题与 i18n 落地细节见 [前端主题与 i18n 落地记录](/frontend/theme-i18n-implementation)、[视觉主题规范](/frontend/visual-theme-spec)、[视觉色彩参考](/frontend/visual-color-reference)、[UI 设计灵感参考](/frontend/ui-design-inspiration)、[Console 样式与 Token 使用说明](/frontend/console-style-guide) 与 [Console 表格布局说明](/frontend/console-table-layout-guide)。

### 8.1.2 Web UI 设计源与 Pencil 约束

正式 Web 视觉推进按三层读取：

1. [视觉主题规范](/frontend/visual-theme-spec) 与 [视觉色彩参考](/frontend/visual-color-reference) 固定色彩、主题和 token 语义
2. [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design) 固定跨 public / private 的 header、按钮、pill、卡片、状态槽和移动 tab 样板
3. [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)、[私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design) 和 Console 相关设计说明固定业务端点信息架构

Pencil 协作约束：

- `.pen` 文件只通过 Pencil 创建、读取和修改，不使用普通文本工具编辑
- Pencil 写入以当前活动窗口为准；修改前必须确认目标 `.pen` 已在 Pencil 当前窗口打开
- 切换 `.pen` 前必须在 Pencil 内手动保存；未保存时切换文件可能丢失更改或让后续写入误落到上一活动文件
- MCP `filePath` 可辅助读取、截图和布局检查，但不能替代当前活动窗口与手动保存确认

### 8.2 基础组件

| 组件 | 说明 | 用途 |
|------|------|------|
| Button | 统一按钮 | 所有应用 |
| Input | 统一输入框 | 所有应用 |
| Modal | 统一弹窗 | 所有应用 |
| Card | 卡片容器 | 论坛、商城 |
| ProTable | 高级表格 | 后台管理 |
| ProForm | 高级表单 | 后台管理 |

### 8.3 图标系统

```typescript
// 使用 @radish/ui 封装的 Icon 组件（基于本地 Iconify JSON 集合）
import { Icon } from '@radish/ui/icon';

<Icon icon="mdi:forum" />
<Icon icon="mdi:chat" />
<Icon icon="mdi:cart" />
```

### 8.4 UI 组件资源库

组件资源库不再在设计入口中维护完整清单。当前入口只约束：

- 共享组件优先沉淀到 `@radish/ui`
- 业务壳层组件留在对应 workspace
- 视觉 token 与主题口径以 [视觉主题规范](/frontend/visual-theme-spec) 和 [视觉色彩参考](/frontend/visual-color-reference) 为准

组件库细节见 [@radish/ui 组件库](/frontend/ui-library)、[组件开发指南](/frontend/components) 与 [UI 组件资源库专题](/frontend/ui-component-resource-library)。

## 9. 性能优化

### 9.1 应用懒加载

```typescript
// desktop/AppRegistry.tsx
const ForumApp = lazy(() => import('@/apps/forum/ForumApp'));
const ChatApp = lazy(() => import('@/apps/chat/ChatApp'));
const ShopApp = lazy(() => import('@/apps/shop/ShopApp'));
const AdminApp = lazy(() => import('@/apps/admin/AdminApp'));
```

### 9.2 窗口虚拟化

只渲染可见窗口，最小化的窗口不渲染内容：

```typescript
{openWindows.map(window => (
  window.isMinimized ? (
    <MinimizedPlaceholder key={window.id} />
  ) : (
    <DesktopWindow key={window.id} {...window} />
  )
))}
```

### 9.3 数据缓存

```typescript
// 使用 TanStack Query 缓存
const { data } = useQuery({
  queryKey: ['posts'],
  queryFn: fetchPosts,
  staleTime: 5 * 60 * 1000, // 5分钟
  cacheTime: 30 * 60 * 1000 // 30分钟
});
```

## 10. 开发规范

### 10.1 WebOS 保留入口内的应用维护

WebOS 不再作为新增功能默认承载层。只有维护 `/desktop` 既有能力或迁移过渡时，才继续按以下方式处理桌面内应用：

1. 在 `apps/` 下创建或调整应用目录
2. 创建 `{App}App.tsx` 入口文件
3. 在 `AppRegistry.tsx` 注册应用
4. 配置权限和窗口类型

### 10.2 应用间通信

```typescript
// 使用 EventBus 或全局状态
import { eventBus } from '@/shared/eventBus';

// 论坛应用发送消息
eventBus.emit('new-message', { count: 5 });

// 状态栏监听消息
eventBus.on('new-message', ({ count }) => {
  showNotification(`您有 ${count} 条新消息`);
});
```

### 10.3 路由规范

```
纯 Web 路由：/（普通浏览器当前进入 /discover）
WebOS 保留入口：/desktop
WebOS 内部应用状态：由 /desktop 内部 hash、query 或本地状态表达，不构成公开 URL 契约

示例：
/ - 纯 Web 默认入口（普通浏览器切向 /discover；Tauri 当前仍保留 /desktop）
/desktop - WebOS 保留入口
/forum - 论坛首页
/forum/tag/community-news - 公开标签页（canonical slug）
/forum/question - 公开问答列表
/forum/poll - 公开投票列表
/forum/lottery - 公开抽奖列表
/forum/post/pst_... - 论坛帖子详情（旧 long 字符串兼容读取）
/docs - 文档目录
/docs/getting-started - 文档详情
/u/usr_... - 公开个人页（旧 long 字符串兼容读取）
/leaderboard - 公开榜单首页 / 默认经验榜单页（canonical；兼容 /leaderboard/experience 收口）
/leaderboard/hot-product - 公开榜单类型页
/forum/category/12 - 公开分类页
/shop - 公开商城首页
/shop/products - 公开商品列表
/shop/product/123 - 公开商品详情
/notifications - 登录态通知中心
/me - 登录态我的状态
/messages - 登录态消息复访
/pet - 登录态电子宠物
/chat - 聊天室
/admin/apps - 后台应用管理
```

### 10.4 应用集成架构决策

当前集成结论：

- WebOS 内置应用用于高频工作台能力
- Console 保持独立后台，不嵌入 WebOS
- 外部工具通过受控跳转进入，不在桌面壳内强行 iframe 化

完整决策依据、选择标准和最佳实践见 [前端应用集成架构决策](/frontend/app-integration)。

### 10.5 公开内容 SEO 与分享基线

当前公开内容基线：

- 公开路由输出运行时 head 与 canonical
- forum / docs / shop 详情和公开个人页提供 canonical 复制入口
- forum / docs / shop 详情和公开个人页输出运行时 JSON-LD
- API + Gateway 已承载动态 sitemap index 与 `static / forum / docs / shop` 分片
- Gateway 已对公开集合页和 forum / docs / shop 公开详情做首包 head snapshot 注入；正文 HTML、完整 SSR / SSG 和预渲染继续后置

完整 URL 范围、head 契约和后置边界见 [公开内容 SEO 与分享基线](/frontend/public-seo-sharing)。

## 11. 迭代与交付（导航）

- 里程碑、按周计划与当前进度：以 [开发路线图](/development-plan) 为准
- 具体周更与变更记录：以 [开发日志](/changelog/) 为准
- 本文档仅描述前端架构与设计约束；若迭代中出现影响架构的关键决策，请同步 [前端多壳层策略](/frontend/shell-strategy)、[当前进行中](/planning/current) 并在开发日志中记录。

## 12. 构建拆包策略

当前策略：

- 应用入口继续按动态导入懒加载
- 第三方库和高频应用按 `manualChunks` 拆分
- chunk size warning 只按真实加载性能和复用收益治理，不为消除提示做无边界拆分

拆包细节、当前结果和后续边界见 [前端构建拆包策略](/frontend/build-chunking)。

## 13. 参考资料

- Nebula OS 原型：`public/webos.html`
- 窗口拖拽：react-rnd
- macOS Big Sur 设计规范
- Windows 11 设计规范
- [UI 设计灵感参考](/frontend/ui-design-inspiration)

---

> 本文档是 Radish 前端架构事实来源之一；当前路线口径请以 [前端多壳层策略](/frontend/shell-strategy)、[当前进行中](/planning/current) 与 [开发路线图](/development-plan) 为准。WebOS 交互范式仅适用于 `/desktop` 保留入口。
