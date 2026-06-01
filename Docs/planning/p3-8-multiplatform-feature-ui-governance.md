# P3-8 多端功能补全与 UI 设计治理

> 状态：`P3-8-C2` Console 首批高频页面类型试点已阶段收口，当前切到 `P3-8-D` 纯 Web + Flutter 主线缺口补齐与验收矩阵
>
> 启动日期：2026-05-21（Asia/Shanghai）
>
> 本页承载单人开发期的多端功能补全、客户端适配和 UI 设计治理口径。快速入口仍以 [当前进行中](/planning/current) 为准。

## 背景判断

`P3-6` 与 `P3-7` 已完成公开增长观察、WebOS / PC 工作台复访小闭环和高信号候选筛查。它们证明当前没有新的 `P0/P1` 阻断项，但不等于项目已经进入稳定运营期。

当前项目仍处于开发阶段，没有稳定用户反馈和专职测试。公开页面、移动端 Web 视图、Flutter 移动客户端、Console 等仍存在未适配、未开发完或体验割裂的能力。路线复盘后，前端投入收敛为纯 Web + Flutter 主线：根路径 `/` 与默认浏览器入口转向纯 Web，WebOS 只保留 `/desktop` 历史入口并逐步迁移高价值能力，PC/Tauri 放到最后再评估且不再绑定 WebOS。后续不能继续把“等待真实使用观察”作为默认主线，而应恢复主动盘点、主动开发、主动验收的节奏。

同时，各个页面 UI、各客户端 UI、`radish.client` 和 `radish.console` 的页面风格仍有明显历史分叉：局部 CSS、硬编码颜色、组件复用不足、交互反馈不一致、端点视觉层级不统一。需要单独设立 UI 设计治理专题，先形成设计稿和设计源文件，再按设计稿推进实现。

`P3-7-C` 已完成工作台与后端 Service 首批热区治理，继续硬拆低风险候选的收益下降。`P3-8-A` 已完成多端功能缺口与 UI 设计入口审计，首批实现任务 `P3-8-B1 Flutter 公开榜单只读入口` 已完成。`P3-8-B2` 已建立 Console 治理工作台设计端点，`P3-8-C1` 已完成内容治理与经验治理首批结构基座，`P3-8-C2` 已把 `P01 / P04 / P05 / P06` 页面类型试点落到首批高频历史页面。继续默认筛 Console 剩余 CSS / 历史页面会进入低收益打磨，因此当前切到 `P3-8-D`，先从移动 Web 公开视图验收矩阵、根路径 `/` 默认入口切换评估和纯 Web 主路径缺口补齐推进。

## 阶段目标

1. **多端功能缺口可见化**
   - 建立覆盖纯 Web 公开页、移动 Web 公开视图、Flutter、Console 与 WebOS 保留迁移线的功能缺口矩阵。
   - 区分未开发、未适配、只读缺口、登录态缺口、跨端回流缺口、迁移缺口和治理效率缺口。
   - 按用户主路径、产品价值、实现风险、验证成本和长期路线排序。
2. **UI 设计治理专题化**
   - 为公开页、客户端和 Console 建立统一设计入口。
   - 使用 Pencil 先画设计稿，再根据设计稿开发。
   - 设计源文件进入项目，并与视觉规范、主题 token、共享组件和实现代码同步维护。
3. **开发节奏回到主动推进**
   - 正式稳定运营前，由 AI 协作者主动按矩阵批量验收、成组修复和一次性交付结论。
   - 维护观察只作为并行线，不再阻塞未完成功能和 UI 治理继续推进。

## 首批范围

`P3-8-A 多端功能缺口与 UI 设计入口审计` 已完成，第一批实现任务已从审计矩阵中选出并推进。

首批审计结论见：[P3-8-A 多端功能缺口与 UI 设计入口审计](/planning/p3-8-a-feature-ui-audit)。

产出物：

- 多端功能缺口矩阵：
  - 纯 Web 公开页面
  - 移动端 Web 公开视图
  - Flutter 移动客户端
  - WebOS `/desktop` 保留迁移线
  - Tauri PC 后置增强壳
  - Console 管理后台
- UI 端点分组：
  - 哪些端点需要独立设计稿
  - 哪些页面应归入同一个设计稿
  - 哪些已有视觉规范或共享组件可以复用
  - 哪些历史页面需要优先收敛
  - 哪些页面可参考 [UI 设计灵感参考](/frontend/ui-design-inspiration) 中的布局、密度、设置页、仪表盘或社区列表模式
- 设计源文件治理建议：
  - `.pen` 文件目录
  - 文件命名
  - 端点与设计稿映射
  - 设计稿更新和实现同步规则
- 首批开发批次建议：
  - 只选择 `1-2` 个一天级可验证任务
  - 明确完成条件、验证入口和不做范围

已完成：

- `P3-8-B1 Flutter 公开榜单只读入口`
  - 新增 Flutter 原生 `榜单` tab，保留 `发现 / 论坛 / 文档 / 榜单 / 我的` 顺序。
  - 复用现有 `/api/v1/Leaderboard/GetLeaderboard` 公开经验榜接口。
  - 覆盖加载、空态、错误态、刷新态、当前用户标记和发现页跳转返回。
  - 未加入我的排名、商品榜、购买、订单、背包、登录增强或管理员调整。
- `P3-8-B2 Console 治理工作台设计端点`
  - 建立 `Docs/frontend/console-governance-workbench-design.md` 端点说明，明确内容治理、经验治理、工作台信息架构、实现拆分顺序和验证入口。
  - 新增 `Docs/frontend/design-sources/console-governance-workbench.pen` 设计源文件入口，并约定 `.pen` 只能通过 Pencil 工具维护。
  - 后续扩展为 `P01-P08` 编号画板，覆盖壳层基座、内容审核、经验台账、治理调度总览、表格 CRUD、设置策略和移动端治理视图。
- `P3-8-C1 Console 治理工作台结构基座`
  - `ModerationPage` 完成 helper、列定义、手动治理区拆分，并接入治理工作台布局承载。
  - `ExperienceAdminPage` 完成 helper、列定义、用户查询摘要、观察摘要、复核区、经验流水区、治理动作表单、页面头部和等级配置拆分，并接入治理工作台布局承载。
  - 页面拆分保持 API、权限、表单字段、表格列、经验规则、冻结 / 解冻语义和数据契约行为等价。
- `P3-8-C2 Console 设计稿到实现的对齐试点`
  - 首个试点选择低风险 `Settings` 设置页，对齐 `P06` 设置 / 权限 / 配置型页面方向。
  - `Settings` 已迁入分组导航、居中设置列和右侧影响范围摘要，保留个人时区偏好、重置默认和密码修改行为。
  - `adminFeature.css` 新增 `admin-settings-*` 与 `admin-setting-section` 可复用布局类，供后续设置 / 策略类页面继续复用。
  - 第二个试点选择 `UserList` 用户管理页，对齐 `P05` 普通表格 CRUD 页面方向。
  - `UserList` 已迁入顶部指标、筛选工具条、表格主体和右侧摘要栏，保留用户查询、分页、状态 / 角色筛选和详情跳转行为。
  - `AdminLayout`、`Breadcrumb`、`index.css` 与 `adminFeature.css` 已继续沉淀 Case Desk 壳层、表格型布局和 `--console-*` token 基座。
  - 第三个试点选择 `Dashboard` 仪表盘，对齐 `P04` 治理 / 运营调度总览页方向。
  - `Dashboard` 已迁入总览指标、快捷操作、最近订单和右侧调度入口，保留统计数据、最近订单加载、权限判断和跳转行为。
  - `adminFeature.css` 新增 `admin-overview-*` 与 `admin-dispatch-*` 可复用布局类，供后续总览 / 调度类页面继续复用。
  - 后续列表迁移首个落点选择 `TagList` 标签管理页，继续复用 `P05` 表格型布局基座。
  - `TagList` 已迁入顶部指标、筛选工具条、表格主体和右侧摘要栏，保留标签 CRUD、启停、恢复、排序、软删除显示和分页行为。
  - 后续列表迁移第二个落点选择 `CategoryList` 分类管理页，继续复用 `P05` 表格型布局基座。
  - `CategoryList` 已迁入顶部指标、筛选工具条、表格主体和右侧摘要栏，保留分类 CRUD、启停、恢复、排序、层级显示、软删除显示和分页行为。
  - 后续列表迁移第三个落点选择 `SystemConfigList` 系统配置页，继续复用 `P05` 表格型布局基座。
  - `SystemConfigList` 已迁入配置指标、筛选工具条、品牌图标配置面板、表格主体和右侧摘要栏，保留配置 CRUD、站点图标上传 / 恢复默认、分类筛选和关键词筛选行为。
  - 后续列表迁移第四个落点选择 `RoleList` 角色管理页，继续复用 `P05` 表格型布局基座。
  - `RoleList` 已迁入角色指标、列表状态工具条、表格主体和右侧权限摘要栏，保留角色 CRUD、启停、权限配置跳转和分页行为。
  - 后续列表迁移继续覆盖 `Applications / StickerGroupList / StickerList`，复用 `P05` 表格型布局基座，保留客户端 CRUD、密钥重置、表情包启停、表情排序与批量上传行为。
  - 商城管理迁移继续覆盖 `ProductList / OrderList`，复用 `P05` 表格型布局基座，保留商品 CRUD、上下架、关联订单跳转、订单详情、失败重试和管理员备注行为。
  - 工具型页面试点选择 `CoinAdminPage`，迁入余额指标、查询工具条、调账主区和右侧摘要栏，保留用户余额查询、管理员调账、权限判断和表单字段行为。
  - 权限配置型页面试点选择 `RolePermissionPage`，迁入页面标题卡、授权指标、角色信息卡、资源树主区和权限预览区，保留资源树勾选、父级继承、接口预览、并发保存参数和权限判断行为。
  - 详情型页面试点选择 `UserDetail`，迁入标题卡、用户指标、基本信息、最近胡萝卜流水 / 订单记录和右侧摘要栏，保留用户详情、资产、经验、订单查询与订单治理详情跳转行为。
  - 个人资料设置型页面试点选择 `UserProfile`，复用 `P06` 设置型布局基座，保留头像上传、个人资料保存、表单校验和 `UserContext` 刷新行为。

`P3-8-C2` 收口判断：

- 已覆盖 Console 高频管理页面、治理页面、设置页面、详情页面和工具型页面。
- 已沉淀 `--console-*` token、`AdminLayout`、`Breadcrumb` 与 `adminFeature.css` 页面类型基座。
- 后续不继续把 Console 剩余页面筛查作为默认主线；只在真实使用、权限页面、明显视觉断裂或新增 / 明显改动页面时回拉。

当前主线：`P3-8-D 纯 Web + Flutter 主线缺口补齐、验收矩阵与受控写入评估`

- 第一批建立移动 Web 公开视图验收矩阵，覆盖 `/discover / forum / docs / u/:id / leaderboard / shop`。
- 验收重点是窄屏信息密度、公开直达、来源返回、分享入口、公开只读边界、公开链接和跨端回流。
- 首批矩阵已建立：[P3-8-D 移动 Web 公开视图验收矩阵（2026-05-25）](/records/p3-8-d-mobile-web-public-view-acceptance-matrix-2026-05-25)。
- 矩阵已补充 `390 x 844` Vite 直连实测，暂未发现新的 `P0/P1`；第一小闭环已完成：公开页顶部入口已从指向 `/` 的 `WebOS` 链接收敛为指向 `/desktop` 的工作台保留入口。
- 根路径 `/` 默认入口切换已完成：普通浏览器访问 `/` 时进入 `/discover` 公开分发页，Vite 直连与 Gateway 入口均已复核；Tauri 当前仍保留 `/desktop`；历史 `?demo` 认证测试页已移除，`/oidc/callback` 保留为独立正式回调入口。
- 第二个纯 Web 小闭环已完成：公开个人页 `/u/:id` 补显式复制链接入口，forum / docs / shop / profile 详情类公开分享状态收敛到统一 hook，底层 `copyToClipboard` 改为先尝试同步 textarea 复制、再回退 `navigator.clipboard`，覆盖嵌入浏览器 / 权限受限上下文。
- 第三个纯 Web 小闭环已完成：公开 forum / docs / profile / shop 详情来源返回状态写入 `history.state`，浏览器历史恢复或刷新后仍能保留来源返回语义，不通过 URL 参数污染公开分享链接。
- 公开商城购买回流小闭环已完成：公开商品详情只读购买提示的工作台入口从 `/` 修正到 `/desktop`，避免根路径切到 `/discover` 后购买、订单和背包回流被带回公开发现页。
- 公开商城到工作台上下文桥接已完成：公开商品详情的工作台入口改为 `/desktop?app=shop&productId=...`，桌面壳层扩展 `shop` 商品深链解析并复用现有 `ShopApp` 的 `productId` 参数承接，进入工作台后可直接打开对应商品详情。
- 工作台商城订单 / 背包入口承接已完成：桌面壳层扩展 `shop` 的 `orderId`、`view=orders` 与 `view=inventory` 深链解析，复用 `ShopApp` 既有订单详情并新增订单列表 / 背包初始视图参数；订单和背包入口保持登录后消费，避免未登录时误显空订单或空背包。
- 纯 Web 登录后轻量链路闭环已完成：未登录用户从公开商品页进入工作台商品详情后，可通过“登录后继续购买”保存 `/desktop?app=shop&productId=...` 上下文；OIDC 回调成功后一次性恢复到该商品详情，再继续购买动作。
- 公开商城私有回流批量验收已完成：匿名态打开 `/desktop?app=shop&orderId=...`、`/desktop?app=shop&view=orders` 或 `/desktop?app=shop&view=inventory` 会保存原深链并进入登录，登录成功后回到对应订单详情、订单列表或背包；`ShopApp` 私有视图也补明确登录门禁状态，避免把未登录状态误显示成空订单或空背包。
- Flutter 榜单到公开主页小闭环已完成：经验榜条目新增“打开公开主页”，复用既有 `ProfilePage` 公开主页能力，并保留 Android Back 返回榜单的来源语义。
- Flutter 通知回流小闭环已完成：最新论坛通知打开原生帖子详情时保留打开通知前的 tab，详情返回后回到原位置，不把用户强制留在 forum tab。
- Flutter 轻回应登录回流小闭环已完成：从 forum detail 轻回应区发起登录后，成功回到当前详情的轻回应区并提示继续发布；本轮不扩展完整评论、发帖、点赞或投票能力。
- Flutter 论坛详情评论发布与回复已完成：已登录用户可在原生 forum detail 发布根评论、回复根评论或子评论；提交后局部更新评论区，回复请求携带 `parentId / replyToCommentId / replyToCommentSnapshot / replyToUserName` 上下文，失败仅在评论输入区提示。本轮不扩展发帖编辑器、点赞、投票、审核治理或富文本评论。
- Flutter 论坛评论登录回流与来源回归已完成：匿名态从评论区发起登录后会回到当前评论输入区；从公开主页评论上下文进入 forum detail 并回复后，Android Back 仍返回原公开主页来源。
- Flutter 公开个人页来源返回批量验收已完成：从发现页、论坛作者和榜单进入原生公开主页时记录来源 tab；公开主页继续打开帖子 / 评论详情并返回后，Android Back 仍回到原发现、论坛或榜单位置，不把用户直接送到后台。
- Flutter 公开个人页详情 Android Back 回归已补：新增 shell smoke 覆盖 `榜单 -> 公开主页 -> 帖子 / 评论详情 -> Android Back 回公开主页 -> Android Back 回榜单`，现有 shell 路由状态可直接承接，未改业务逻辑。
- 纯 Web / 工作台论坛登录回流小闭环已完成：桌面入口新增 `forum` 深链解析，论坛发帖、评论和轻回应登录入口改为统一 `redirectToLogin({ returnPath })`；登录成功后回到论坛工作台或原帖子 / 评论上下文，不再因根路径 `/` 切向 `/discover` 而丢失 forum 场景。
- Dock 主动登录回流小闭环已完成：工作台右侧登录按钮改为保存当前合法 `/desktop...` 路径；用户在 forum / shop 等保留入口主动登录后回到原工作台上下文，非 desktop 公开页仍不进入登录回流白名单。
- 公开商城工作台入口契约已完成收口：公开商品详情“打开工作台”复用统一 `buildDesktopShopProductReturnPath` 构造 `/desktop?app=shop&productId=...`，并补非法商品详情 ID 回落商城首页的路由边界测试。
- 公开商品榜单到商品详情小闭环已完成：公开热门商品榜单条目可进入公开商品详情，并通过 `history.state` 来源状态返回榜单；榜单仍保持只读浏览边界，不直接打开购买、订单或背包流程。
- 公开商城详情来源返回文案批量收口已完成：`/shop/products` 或商品榜进入 `/shop/product/:productId` 后，详情返回按钮按来源显示“返回商品列表 / 返回榜单”等精确文案，避免移动端只看到泛化的“返回上一入口”。
- Gateway 公开页资源 URL 收口小闭环已完成：当页面通过 `https://localhost:5000` 访问时，浏览器可见的本地 HTTP 媒体、favicon、头像和 Markdown 附件资源地址会归一到当前 Gateway origin；公网、CDN、非 localhost 地址不改写。
- 移动 Web 公开阅读链路小闭环已完成：共享 `MarkdownRenderer` 补齐长链接、长 inline code、代码块、表格和图片的窄屏防溢出约束；公开 docs / forum 详情补齐标题、slug chip、评论摘要 chip、返回 / 分享按钮和评论内容的 390px 窄屏换行约束；公开分享链接统一走运行时公开域名配置，并保留 docs 详情文档锚点。
- 移动 Web 公开阅读链路二轮复核已完成：公开 docs 详情页内 Markdown 相对文档链接和同页锚点统一解析到公开 docs 路由，避免阅读中触发整页跳转或丢失来源语义；docs / forum 阅读容器继续补齐嵌套 flex、卡片和 Markdown 内容的 `min-width: 0` / `max-width` 约束。
- 公开来源返回批量验收已完成：公开详情页的显式来源返回动作会保留既有 `history.state` 来源链路，避免 `discover -> forum detail -> profile -> 返回 forum detail` 后把 forum 详情来源改写成 profile 形成返回循环；从个人公开页内容卡片主动进入 forum 详情仍按普通导航记录 profile 来源。
- 移动 Web 公开分发页二轮复核已完成：`/discover` 论坛卡片进入公开帖子详情时改为优先使用 `Post.PublicId`，与 forum 列表 / 搜索 / 标签页的公开 URL 口径保持一致，避免发现页链路继续生成 long id 公开详情路径。
- 移动 Web 公开视图矩阵阶段收口已完成：批量复核 `/discover / forum / docs / shop / leaderboard / u/:id` 的窄屏布局、来源返回、分享入口、公开只读边界、长文本防溢出和公开链接契约；`/discover` 与 `/leaderboard` 补显式分享入口，`/u/:id` 移除登录态关注 / 取关写操作，`/shop` 公开链路补齐商品长文本防溢出约束。本轮未发现新的 `P0/P1`，后续不再逐页小循环式打磨 Web 公开页。
- Flutter 公开商品只读详情小闭环已完成：发现页商城精选商品可打开原生商品详情，详情页复用公开 `Shop/GetProduct` 契约展示名称、价格、库存、限购、有效期、描述、权益值和只读购买边界；Android Back 返回发现页。本轮不扩展完整移动商城、购买、订单、背包、支付口令或权益激活。
- Flutter 轻量 forum 通知列表已完成：已登录壳层从 `/api/v1/Notification/GetNotificationList` 读取最近少量可跳 forum 通知，展示轻量列表并复用既有 forum detail handoff 打开帖子 / 评论，详情返回后回到打开前 tab；本轮不扩展完整通知中心、已读 / 删除、系统推送、完整移动商城或创作器。
- Flutter 公开商城列表只读入口已完成：发现页新增进入公开商城列表入口，列表复用 `/api/v1/Shop/GetProducts`，商品项打开既有只读商品详情并可返回商城列表；本轮不扩展购买、订单、背包、支付口令或权益激活。
- Flutter 原生公开详情分享入口复核与补齐已完成：forum detail、docs detail 与 shop detail 均补齐完整公开链接展示和复制入口，复制口径统一为 `Gateway Base URL + Web 公开路由`；无有效 `Post.PublicId` 或公开 slug 时继续显示“公开链接尚未生成”并禁用复制，避免把内部长数字 ID 当作公开分享契约。本轮只使用系统剪贴板能力，不扩展系统分享 SDK、完整商城、完整通知中心、完整创作器或 WebOS 新功能。
- Flutter 原生公开文档阅读链路补强已完成：只读 Markdown 阅读器可识别 `/docs/:slug`、完整公开 URL、`docs/:slug`、`./:slug` 与普通相对 slug 形式的文档内链，统一转换为应用内 docs detail 打开；纯页内锚点、附件路径和非 docs 链接继续按文本展示，避免引入外部浏览器、编辑器或创作能力。
- Flutter 原生公开主页链接 / 展示 / 来源返回复核已完成：`ProfilePage` 接入 `PublicLinkCopyPanel` 展示并复制 `/u/:userId` Web 公开链接，壳层传入当前 `AppEnvironment` 以统一 Gateway Base URL 口径；匿名公开主页、已登录公开主页、长文本约束、帖子 / 评论 handoff 和榜单来源返回继续由 profile 单测与 shell smoke 覆盖。本轮不扩展关注 / 取关、私信、完整系统分享 SDK 或 WebOS 复刻。
- 纯 Web forum 公开详情到工作台参与回流已完成：公开帖子详情在只读阅读边界内新增“发送轻回应 / 参与讨论”工作台入口，`/desktop?app=forum&postPublicId=...&intent=quickReply|comment` 会打开同一帖子详情并聚焦对应操作区域；若需要登录，既有 OIDC returnPath 会保留该 desktop 深链。本轮不扩展公开页内写操作、点赞、投票、发帖编辑器或完整创作器。
- 纯 Web shop 登录态购买意图回流已完成：公开商品详情和工作台商品详情的购买入口统一携带 `/desktop?app=shop&productId=...&intent=purchase`，匿名用户登录后回到同一商品并打开购买流程；订单 / 背包私有深链仍由 `/desktop` 门禁和 `ShopApp` 私有视图承接。本轮不扩展公开页内购买、支付口令、权益激活或完整移动商城。
- Flutter 登录态商城订单 / 背包只读入口已完成：登录态“我的”页新增“查看商城订单”和“查看背包”入口，复用现有 `Shop/GetMyOrders`、`Shop/GetMyBenefits`、`Shop/GetMyInventory` 私有接口；订单页覆盖分页加载、刷新、加载更多、空态、错误态和返回“我的”，背包页覆盖权益 / 道具只读展示、加载、空态、错误态、刷新和返回“我的”。本轮不扩展购买、取消订单、支付口令、权益激活、道具使用、完整移动商城、完整通知中心或 WebOS 新功能。
- Flutter 订单详情与背包来源只读承接已完成：订单列表项可打开原生订单详情，展示商品、数量、单价、合计、订单状态轨迹、取消 / 失败原因和来源商品入口；背包权益可打开来源订单 / 来源商品，背包道具可打开来源商品，返回后保留背包上下文。本轮继续保持只读，不扩展购买、取消订单、支付口令、权益激活、道具使用、完整移动商城、完整通知中心或 WebOS 新功能。
- Flutter 登录态通知只读列表已完成：已登录壳层从 `/api/v1/Notification/GetNotificationList` 读取完整站内通知列表，展示标题、内容、类型、已读状态和时间；forum 通知可继续回到帖子 / 评论并保留来源返回，系统等不可跳通知只展示内容。本轮不扩展标记已读、删除、推送、完整通知中心、完整移动商城或 WebOS 新功能。
- Flutter 登录态胡萝卜资产只读入口已完成：登录态“我的”页新增“查看胡萝卜资产”入口，复用 `/api/v1/Coin/GetBalance` 与 `/api/v1/Coin/GetTransactions` 私有接口；资产页展示可用余额、冻结余额、累计统计和最近流水，覆盖加载、空态、错误、刷新、加载更多和返回“我的”。本轮不扩展转账、打赏、调账、支付、完整资产中心或 WebOS 新功能。
- Flutter 登录态经验记录只读入口已完成：登录态“我的”页新增“查看经验记录”入口，复用 `/api/v1/Experience/GetMyExperience` 与 `/api/v1/Experience/GetTransactions` 私有接口；经验页展示等级、当前经验、总经验、升级进度、冻结状态和最近经验流水，覆盖加载、空态、错误、刷新、加载更多和返回“我的”。本轮不扩展经验调整、冻结治理、管理员复核、完整资产中心或 WebOS 新功能。
- Flutter 登录态“我的浏览记录 / 最近访问”只读入口已完成：登录态“我的”页新增“查看最近访问”入口，复用 `/api/v1/User/GetMyBrowseHistory` 私有接口；最近访问页展示公开帖子、文档和商品浏览记录，覆盖加载、空态、错误、刷新、加载更多、返回“我的”，并承接到既有原生 forum detail、docs detail 和 shop product detail。本轮不扩展清空 / 删除、推荐系统、完整浏览历史治理、完整移动商城或 WebOS 新功能。
- Flutter 通知已读状态受控写入已完成：Flutter API 客户端补齐 `PUT` 方法，`HttpNotificationRepository` 复用 `/api/v1/Notification/MarkAsRead`；已登录通知列表支持对单条未读通知显式标记已读，成功后本地更新该条状态，失败在通知 sheet 内提示并保留原状态。本轮不扩展批量已读、删除、通知设置、系统推送或完整通知中心。
- Flutter 纯文本发帖受控写入已完成：`ForumPage` 读取 `/api/v1/Category/GetTopCategories` 作为发帖分类来源，登录态用户通过 `/api/v1/Post/Publish` 发布 `contentType=text` 的纯文本帖子；提交前校验标题、分类、正文和 `1-5` 个标签，成功后刷新列表并通过既有 forum detail handoff 打开新帖子。本轮不扩展富文本、附件、投票、抽奖、草稿箱、编辑、点赞、完整创作器或 WebOS 新功能。
- Flutter 单商品购买路径受控写入已完成：`ShopProductDetailPage` 复用现有 `/api/v1/Shop/CheckCanBuy/{productId}` 与 `/api/v1/Shop/Purchase` 私有契约，登录态用户可在原生商品详情输入支付口令购买 `1` 件商品，成功后进入既有订单详情页确认订单号、状态和扣款结果；匿名态点击购买会发起登录，登录成功后回到当前商品详情继续购买。失败态覆盖购买资格、支付口令、接口失败和返回格式异常。本轮不扩展购物车、退款、权益使用、通知中心、完整移动商城、完整工作台或 WebOS 新功能。
- 商城工作台构建 warning 已完成治理：`ShopApp` 改为按首页、商品、订单、背包与购买弹窗懒加载，`vite.config.ts` 同步细分商城手动 chunk，`app-shop` 已收敛到 500k 警告阈值以内；npm 自身 update notice 不进入仓库级配置治理。
- 阶段约束已复盘：移动 Web 公开视图逐页打磨、Console 剩余页面默认迁移和 Flutter “只能只读”的总门禁可以结束；后续保留范围门禁，允许成熟 API 支撑的单一用户动作进入受控写入评估。
- 后续继续按验收矩阵做主动批量复核、成组修复和一次性交付结论；受控写入仍必须每次只推进一个真实用户动作，并覆盖登录回流、错误态、返回状态和定向测试。
- 下一步不继续重复 Flutter 个人主页来源返回、公开主页链接展示、轻量通知列表、公开商城列表、论坛详情评论发布 / 回复、评论登录回流、原生公开详情分享入口、公开文档阅读链路、订单 / 背包只读入口、订单详情 / 背包来源承接、登录态通知只读列表、通知已读状态、胡萝卜资产、经验记录、最近访问只读入口、纯文本发帖或单商品购买验收，也不继续逐页打磨 Web 公开页；不直接启动完整浏览历史治理、清空 / 删除、推荐系统、完整移动商城、完整通知中心、完整创作器或 WebOS 复刻。
- WebOS 只保留 `/desktop` 历史入口，不再作为新增功能候选；PC/Tauri 放到最后再评估，后续若重启也只增强纯 Web。
- 不直接启动完整移动商城、完整通知中心、完整创作器、公开 Web 整体 UI 重构或多端同时重写。

## UI 设计稿治理

Pencil 设计稿是 UI 专题的设计源文件。后续涉及大页面重设计、端点级视觉治理或跨页面视觉体系变更时，原则上先更新对应 `.pen` 设计稿，再进入实现。

纯功能缺口、后端治理、行为等价拆分、小范围表单 / 状态 / 文案修正和验收矩阵中暴露的明确小修复，不被 Pencil 前置阻塞。

启动 `P3-8-A` 的 UI 审计和 Pencil 设计稿拆分前，应先阅读 [UI 设计灵感参考](/frontend/ui-design-inspiration)，从 AFFINE、CodexApp、Cloudflare、GitHub、Discourse 与 1Panel 的截图中提炼布局、信息密度、控件气质和配色节奏，再结合 Radish 自身内容重新设计，不直接照搬外部产品。

候选端点包括但不限于：

- 公开页面设计稿
- 移动端 Web 公开视图设计稿
- WebOS `/desktop` 保留 / 迁移设计稿
- Flutter 移动客户端设计稿
- Tauri / PC 纯 Web 增强壳设计稿
- Console 管理后台设计稿

上述拆分只是候选，不在本页提前固定。`P3-8-A` 需要结合实际页面、客户端边界、复用关系和维护成本决定最终端点粒度。例如公开页与移动端 Web 公开视图是否拆开、WebOS 保留迁移是否需要单独设计稿、Console 是否按后台模块拆分，都需要审计后再定。

治理规则：

- `.pen` 文件必须进入项目仓库或项目约定的设计目录，不能只存在个人本地。
- `.pen` 文件名应表达端点和职责，避免 `new-design.pen`、`ui-v2.pen` 这类无意义名称。
- 每个 `.pen` 文件应能追溯到对应实现范围、视觉 token、共享组件和主要页面。
- 设计稿变更影响实现时，应同步更新对应文档或开发批次说明。
- `.pen` 文件为 Pencil 工具管理的设计源文件；后续创建、读取和修改 `.pen` 时应通过 Pencil 工具完成，不直接用普通文本工具读取或改写。

## 开发边界

本阶段做：

- 主动盘点并补齐未完成的多端功能。
- 优先处理公开访问、移动浏览、移动客户端、纯 Web 登录后轻量链路和 Console 主路径缺口。
- Flutter 真实移动主路径从“可查看、可返回、状态完整”推进到“少量高价值动作可完成”；受控写入必须复用已成熟 API，并限制为单一用户动作。
- 受控写入已完成通知已读状态和纯文本发帖；后续首批剩余候选优先单商品购买路径，购物车、退款、权益使用、富文本、推送、通知设置、资产转账和管理员治理仍不纳入本批。
- 建立 UI 设计稿与实现同步机制。
- 在新增或改动页面中逐步收敛到 `@radish/ui`、主题 token 和统一交互反馈。
- 将 WebOS 既有高价值能力作为迁移来源评估，不再作为新增功能承载方向。

本阶段不做：

- 不把等待真实用户反馈当作继续开发的前置条件。
- 不一次性重构所有 UI。
- 不把所有端点一次性改成同一套布局。
- 不绕过设计稿直接大面积改视觉。
- 不让 Pencil 前置阻塞纯功能缺口、后端治理、行为等价拆分、小范围表单 / 状态 / 文案修正。
- 不继续把 Console 剩余 CSS / 历史页面筛查作为默认主线。
- 不继续把 Flutter 只能只读作为长期总门禁；但也不把受控写入扩大成完整移动商城、完整通知中心、完整资产中心或完整创作器。
- 不把 Tauri 当作原生 UI 重写路线。
- 不把 Tauri 当作 WebOS 默认分发路线。
- 不把新功能默认加入 WebOS。
- 不让 PC 客户端抢占纯 Web / Flutter 主线。
- 不启动完整 Playwright / E2E 平台作为继续开发的前置。
- 不启动完整可观测性平台或大而全运营平台。

## 排序原则

优先级从高到低：

1. 阻断用户主路径或跨端回流的缺口。
2. 明显影响公开访问、移动阅读、客户端使用和治理效率的未完成功能。
3. 会导致各端 UI 持续分叉的设计和组件问题。
4. 能用一天级批次验证并提交的高收益小闭环。
5. 低收益微体验、分发材料、平台化工程和一次性历史数据补齐。

`P3-8-D` 当前排序：

1. 验收矩阵暴露的高信号公开 / 移动 / 回流问题。
2. 购买 / 订单 / 背包、权限授权或公开访问中的真实阻断缺口。
3. 纯 Web 登录后轻量链路补强。
4. WebOS `/desktop` 保留迁移线阻断级问题。
5. Console 和共享 UI 维护项。

## 验证口径

文档与审计批次默认验证：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

进入实现批次后，再按触达范围选择：

- 前端页面或 UI 改动：`npm run type-check --workspace=radish.client`、`npm run type-check --workspace=radish.console`、必要的 node tests 或浏览器联调
- 共享 UI 改动：`npm run type-check --workspace=@radish/ui`，并覆盖 client / console 受影响入口
- Flutter 改动：运行对应 Flutter 单测或最小 smoke
- 后端 / Console 数据契约改动：`dotnet test Radish.Api.Tests` 或定向 filter
- 跨端链路改动：按 [验证基线说明](/guide/validation-baseline) 选择 quick / baseline / host 入口

`P3-8-B1` 已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-B2` 文档 / 设计端点批次默认执行：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-C1` 已执行：

```bash
npm run type-check --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-C2` 设置页试点已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-C2` 表格 CRUD 试点已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 调度总览页试点已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 标签列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 分类列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 系统配置列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 角色列表迁移已执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
git diff --check
```

`P3-8-C2` 后续页面类型试点已按批次执行：

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 文档 / 矩阵批次默认执行：

```bash
npm run check:repo-hygiene:changed
git diff --check
```

若验收矩阵后进入前端实现，再按命中端点补：

```bash
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
```

`P3-8-D` 个人公开页分享小闭环已执行：

```bash
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client -- --test-name-pattern="copyToClipboard|publicRoute|entryRoute"
```

`P3-8-D` 公开详情来源返回持久化小闭环已执行：

```bash
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client -- --test-name-pattern=publicRouteNavigation
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 公开商城购买回流入口小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern=公开商城详情购买回流入口
npm run type-check --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 公开商城到工作台商品上下文桥接小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="desktop.*商城|公开商城详情购买回流入口|desktop 商城|stripDesktopExternalEntrySearch"
npm run type-check --workspace=radish.client
git diff --check
```

`P3-8-D` 工作台商城订单 / 背包入口承接小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern=desktop
npm run type-check --workspace=radish.client
```

`P3-8-D` 公开商城私有回流批量验收已执行：

```bash
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client
npm run validate:baseline:quick
npm run lint:changed
npm run check:repo-hygiene:changed
npm run check:identity-impact
npm run validate:identity
```

`P3-8-D` 公开商品榜单到商品详情小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern=ShopDetailSource
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` Gateway 公开页资源 URL 收口小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern=normalizeBrowserVisibleUrl
npm run type-check --workspace=radish.client
npm run type-check --workspace=@radish/ui
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 移动 Web 公开阅读链路小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="publicRoute|publicHead|publicStructuredData"
npm run type-check --workspace=radish.client
npm run type-check --workspace=@radish/ui
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 移动 Web 公开阅读链路二轮复核已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="publicRouteState|publicHead"
npm run type-check --workspace=radish.client
npm run type-check --workspace=@radish/ui
npm run build --workspace=radish.client
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 公开来源返回批量验收已执行：

```bash
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client -- --test-name-pattern=publicRouteNavigation
npm run validate:baseline:quick
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 移动 Web 公开分发页二轮复核已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="publicRouteNavigation|forumNavigation|publicHead"
npm run type-check --workspace=radish.client
npm run lint:changed
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 纯 Web / 工作台论坛登录回流小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="authReturnPath|desktopExternalEntry"
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

人工联调已通过：`https://localhost:5000/desktop?app=forum`、论坛发帖登录、帖子详情轻回应登录、评论登录和 forum 帖子 / 评论深链回流均未再落回 `/discover`。

`P3-8-D` Dock 主动登录回流小闭环已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="authReturnPath"
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 公开商城工作台入口契约收口已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="publicSeoStatic|publicRouteState|authReturnPath"
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 移动 Web 公开视图矩阵阶段收口已执行：

```bash
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client -- --test-name-pattern="discoverRouteState|publicHead|publicSeoStatic|publicRouteNavigation|publicRouteState"
```

`P3-8-D` Flutter 公开商品只读详情小闭环已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 轻量 forum 通知列表小闭环已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 公开商城列表只读入口小闭环已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 原生公开详情分享入口复核与补齐已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 原生公开文档阅读链路补强已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 原生公开主页链接 / 展示 / 来源返回复核已执行：

```bash
cd Clients/radish.flutter
flutter test test/profile_page_test.dart
flutter test test/smoke_test.dart
```

`P3-8-D` 纯 Web forum 公开详情到工作台参与回流已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="authReturnPath|desktopExternalEntry|forumNavigation|publicSeoStatic"
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` 纯 Web shop 登录态购买意图回流已执行：

```bash
npm run test --workspace=radish.client -- --test-name-pattern="authReturnPath|desktopExternalEntry|publicSeoStatic"
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
npm run lint:changed
npm run check:repo-hygiene:changed
git diff --check
```

`P3-8-D` Flutter 登录态商城订单 / 背包只读入口已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 订单详情与背包来源只读承接已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 登录态通知只读列表已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 登录态胡萝卜资产只读入口已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

`P3-8-D` Flutter 登录态经验记录只读入口已执行：

```bash
cd Clients/radish.flutter
flutter test
flutter analyze
```

## 与维护线关系

`P3-6` 公开增长观察和 `P3-7` WebOS / PC 工作台复访链路继续作为维护线存在。若公开访问、head / sitemap、分享预览、购买 / 订单 / 背包、运行日志或权限授权暴露高信号问题，可以从维护线回拉小闭环。WebOS / PC 相关问题只在影响 `/desktop` 保留入口可用性、阻碍能力迁移或暴露阻断级风险时回拉。

维护线不能再反向阻塞 P3-8 的主动开发。只有当维护线暴露 `P0/P1` 阻断项时，才临时抢占当前批次。
