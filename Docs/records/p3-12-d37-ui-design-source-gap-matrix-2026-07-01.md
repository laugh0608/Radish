# P3-12-D37 UI 设计源差距矩阵记录

> 日期：2026-07-01（Asia/Shanghai）
>
> 类型：规划口径整理 / UI 专题差距矩阵
>
> 状态：已建立 public / private / foundation / console 四类设计源到真实页面族的首轮对齐矩阵；本记录不代表 `P3-12-D` UI 专题完成

## 背景

`P3-12-D36` 已明确：`radish.client` D8-D13 与 `radish.console` D14-D35 都只能视为首轮视觉实现或代码侧治理，不能直接等同于四份 Pencil 设计源全量同步。

本批按 D36 要求建立差距矩阵，只读对齐设计说明、真实路由和既有实现证据；不改业务代码，不启动 Gateway 页面联调，不进入 `P3-12-E` 发布候选。

## 判定口径

| 状态 | 含义 | 后续处理 |
| --- | --- | --- |
| 已落地 | 真实路由 / 页面族已存在，且已有首轮视觉实现或结构收口记录 | 发布候选前集中运行态复核 |
| 首轮落地待观察 | 代码入口存在，但设计源中的密度、移动节奏或复杂数据状态需要真实视口确认 | 进入 Gateway PC / mobile 阶段验收清单 |
| 需边界确认 | 设计源存在画板，但当前产品路由、数据来源或阶段范围尚未确认 | 先补产品 / 设计口径，不直接写代码 |
| 需代码治理 | 设计源和路由边界均明确，且现有代码缺口可静态确认 | 作为 D38+ 成组代码治理候选 |

## Public Web 矩阵

| 设计源 | 真实路由 / 页面族 | 当前代码状态 | 证据 | 剩余动作 |
| --- | --- | --- | --- | --- |
| `P01 - Public App Home` | `/` 当前浏览器入口解析到 `/discover` | 需边界确认；没有独立 `PublicHomeApp` | `entryRoute.ts` 中 `BROWSER_PUBLIC_ENTRY_PATH = '/discover'` | 确认 P01 是否只是公开首页概念层，还是后续需要独立根路径首页 |
| `P02 - Discover Content Stream` | `/discover` | 已落地 | `PublicDiscoverApp`、D8 首批实现记录 | 阶段验收观察信息密度、公开入口和移动首屏 |
| `P03 - Forum Thread List` | `/forum`、`/forum/category/:id`、`/forum/tag/:slug`、`/forum/search`、`/forum/question`、`/forum/poll`、`/forum/lottery` | 已落地 | `PublicForumApp`、`forumRouteState.ts` | 阶段验收观察列表密度、筛选与分页 |
| `P04 - Forum Thread Detail` | `/forum/post/:id`、`?intent=comment|quickReply|answer|edit|history` | 已落地 | `PublicForumDetail`、评论 / 轻回应回迁记录 | 阶段验收观察正文、轻回应、评论树和作者意图入口 |
| `P05 - Docs Index and Search` | `/docs`、`/__documents__` | 已落地 | `PublicDocsApp`、`docsRouteState.ts` | 阶段验收观察目录 / 搜索密度 |
| `P06 - Docs Article Reading` | `/docs/:slug`、`/__documents__/:slug` | 已落地 | `PublicDocsApp`、public head / structured data | 阶段验收观察正文阅读、目录和相关文档 |
| `P07 - Public Shop and Product` | `/shop`、`/shop/products`、`/shop/product/:productId` | 已落地 | `PublicShopApp`、B1 交易回流记录 | 阶段验收观察公开浏览和登录购买回流 |
| `P08 - Public Leaderboards` | `/leaderboard`、`/leaderboard/:type` | 已落地 | `PublicLeaderboardApp` | 阶段验收观察榜单切换、分页和公开主页跳转 |
| `P09 - Public Profile` | `/u/:id` | 已落地 | `PublicProfileApp`、B6 身份语义收口 | 阶段验收观察公开内容 tab、关注回流和来源返回 |
| `P10 - Mobile Discover Forum` | `/discover`、`/forum` 响应式视图 | 首轮落地待观察 | D13 之前已有 PC / mobile 成组验收，D37 未重新启动浏览器 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P11 - Mobile Post Detail` | `/forum/post/:id` 响应式视图 | 首轮落地待观察 | `PublicForumDetail` 与 D13 成组验收边界 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P12 - Mobile Docs Reading` | `/docs`、`/docs/:slug` 响应式视图 | 首轮落地待观察 | `PublicDocsApp` 与 D13 成组验收边界 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P13 - Mobile Workbench` | `/workbench` | 已落地，但归属 private / foundation 交叉 | `WorkbenchApp`、B5 Web 功能总入口 | 在 D38 确认 P13 是否继续保留在 public 设计源说明中 |
| `P14 - Mobile Public Profile` | `/u/:id` 响应式视图 | 首轮落地待观察 | `PublicProfileApp` | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P15 - Public Chat Room` | 设计目标为 `/chat` / `/chat/:room` | 需边界确认；正式公开路由当前未接入 `/chat` | `entryRoute.ts` 未把 `/chat` 纳入公开内容路由；现有聊天主要在 WebOS `ChatApp` 与私域 `/messages` | D38 先裁决公开聊天室是否进入 P3-12，或从 D 专题退出条件中后置 |
| `P16 - Mobile Chat Reply Flow` | 设计目标为移动 `/chat` 任务流 | 需边界确认；跟随 P15 | 同上 | 跟随 P15，不单独启动代码 |

## Private / Author Web 矩阵

| 设计源 | 真实路由 / 页面族 | 当前代码状态 | 证据 | 剩余动作 |
| --- | --- | --- | --- | --- |
| `P01 - Workbench Route Map` | `/workbench` | 已落地 | `WorkbenchApp`、B5 Gateway smoke | 阶段验收观察功能地图与移动入口 |
| `P02 - Me Overview Dashboard` | `/me` | 已落地 | `MeApp`、D8 首批实现 | 阶段验收观察个人状态摘要和最近复访 |
| `P03 - My Content Tabs` | `/me/content?tab=` | 已落地 | `MeApp`、`meRouteState.ts` | 阶段验收观察 tab 和公开详情回流 |
| `P04 - Browse History` | `/me/history` | 已落地 | `MeApp`、B2 记录 | 阶段验收观察来源返回语义 |
| `P05 - Attachment Browser` | `/me/attachments` | 已落地 | `MeApp`、B2 记录 | 阶段验收观察筛选、空态和移动密度 |
| `P06 - Experience Ledger` | `/me/experience` | 已落地 | `MeApp`、B2 记录 | 阶段验收观察等级 / 流水信息密度 |
| `P07 - Assets Overview` | `/me/assets` | 已落地 | `MeAssetsPage`、D9 记录 | 阶段验收观察资产摘要 |
| `P08 - Asset Transactions` | `/me/assets/transactions` | 已落地 | `MeAssetsPage`、D9 记录 | 阶段验收观察流水筛选和移动列表 |
| `P09 - Orders List` | `/shop/orders` | 已落地 | `ShopWebApp`、D9 记录 | 阶段验收观察订单状态分组 |
| `P10 - Order Detail` | `/shop/order/:orderId` | 已落地 | `ShopWebApp`、D9 记录 | 阶段验收观察支付 / 发放 / 背包回流 |
| `P11 - Inventory Benefits` | `/shop/inventory` | 已落地 | `ShopWebApp`、D9 记录 | 阶段验收观察权益卡和空态 |
| `P12 - Notifications Center` | `/notifications` | 已落地 | `NotificationsApp`、D10 记录 | 阶段验收观察未读、筛选和目标跳转 |
| `P13 - Messages Workspace` | `/messages?channelId=&messageId=` | 已落地 | `MessagesApp`、D10 记录 | 阶段验收观察会话定位和移动输入区 |
| `P14 - Circle Feed` | `/circle?tab=&page=` | 已落地 | `CircleApp`、D11 记录 | 阶段验收观察关注动态和公开来源返回 |
| `P15 - Pet Care` | `/pet` | 已落地 | `PetApp`、D11 记录 | 阶段验收观察状态条、冷却和流水 |
| `P16 - Forum Compose` | `/forum/compose` | 已落地 | `forumRouteState.ts`、D12 记录 | 阶段验收观察发布器和提交反馈 |
| `P17 - Forum Edit History` | `/forum/post/:id?intent=answer|edit|history` | 已落地 | `PublicForumDetail`、B3 / D12 记录 | 阶段验收观察作者意图入口和历史展示 |
| `P18 - Docs Mine` | `/docs/mine` | 已落地 | `DocsAuthorApp`、`docsAuthorRouteState.ts` | 阶段验收观察作者库和低风险动作 |
| `P19 - Docs Compose Edit` | `/docs/compose`、`/docs/edit/:id` | 已落地 | `DocsAuthorApp`、D12 记录 | 阶段验收观察编辑器和保存反馈 |
| `P20 - Docs Revisions` | `/docs/revisions/:id` | 已落地 | `DocsAuthorApp`、D12 记录 | 阶段验收观察版本栈和只读预览 |
| `P21 - Mobile Workbench` | `/workbench` 响应式视图 | 首轮落地待观察 | D13 成组验收边界 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P22 - Mobile Me` | `/me` 响应式视图 | 首轮落地待观察 | D13 成组验收边界 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P23 - Mobile Content History` | `/me/content`、`/me/history`、`/me/attachments` | 首轮落地待观察 | D13 成组验收边界 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P24 - Mobile Assets Ledger` | `/me/assets`、`/me/assets/transactions` | 首轮落地待观察 | D9 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P25 - Mobile Orders Inventory` | `/shop/orders`、`/shop/order/:id`、`/shop/inventory` | 首轮落地待观察 | D9 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P26 - Mobile Notifications` | `/notifications` | 首轮落地待观察 | D10 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P27 - Mobile Messages` | `/messages` | 首轮落地待观察 | D10 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P28 - Mobile Circle` | `/circle` | 首轮落地待观察 | D11 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P29 - Mobile Pet` | `/pet` | 首轮落地待观察 | D11 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P30 - Mobile Author` | `/forum/compose`、`/docs/mine`、`/docs/edit/:id` | 首轮落地待观察 | D12 / D13 记录 | 纳入阶段 Gateway mobile CSS 视图复核 |

## Foundation 矩阵

| 设计源 | 真实组件 / 页面族 | 当前代码状态 | 证据 | 剩余动作 |
| --- | --- | --- | --- | --- |
| `F01 - Web UI Foundation` | 主题 token、public / private header、按钮 / pill、状态槽、移动 shell / tab | 已落地但需跨源漂移复核 | `theme-tokens.css`、`WebShellHeader`、`WebStateSlot`、D4 / D8 / D13 记录 | 阶段验收前做一次 public / private / console token 与壳层用法抽样复核 |
| `F02 - Client Shell Common Components` | `PublicShellHeader`、`WebShellHeader`、`WebStateSlot`、`entryRoute`、public / private 路由族 | 已落地 | `main.tsx`、`entryRoute.ts`、`PublicShellHeader` | 继续观察 P01 / P13 / P15 这类跨 public / private 边界画板是否造成说明漂移 |

## Console 矩阵

| 设计源 | 真实路由 / 页面族 | 当前代码状态 | 证据 | 剩余动作 |
| --- | --- | --- | --- | --- |
| `P00 - Console Shell Common Components` | `/console/` 受保护壳层 | 已落地 | `AdminLayout`、`ConsolePage`、D14 记录 | 阶段验收观察移动侧栏与顶栏 |
| `P01 - Console Shell Foundation` | Console 页面容器、语义页头、指标、工具条、表格样板 | 已落地 | `ConsolePage`、`adminFeature.css`、D14-D35 记录 | 阶段验收观察中宽 PC 和移动窄屏 |
| `P02 - Content Moderation` | `/console/moderation` | 已落地 | `ModerationPage`、D24-D26 记录 | 阶段验收观察证据 / 动作 / 留痕区块 |
| `P03 - Experience Governance` | `/console/experience` | 已落地 | `ExperienceAdminPage`、D24-D26 记录 | 阶段验收观察流水 / 复核 / 表单区块 |
| `P04 - Governance Overview` | `/console/` Dashboard | 需边界确认；当前是总览与最近订单等首轮治理，不是完整调度中心 | `Dashboard`、D35 记录 | 若要内部治理分派，需要先补数据来源和 API 契约；否则作为后置 |
| `P05 - Table CRUD User Management` | `/console/users`、高频 CRUD 列表族 | 已落地 | `UserList`、订单 / 商品 / 标签 / 分类 / 贴纸 D15-D21 记录 | 阶段验收观察表格滚动、操作列和摘要侧栏 |
| `P06 - Settings Governance Policy` | `/console/system-config`、`/console/settings` | 已落地 | `SystemConfigList`、D14 / D21 / D35 记录 | 阶段验收观察历史弹窗、数字控件和风险说明 |
| `P07 - Mobile Content Moderation` | `/console/moderation` 响应式视图 | 首轮落地待观察 | D31-D35 未覆盖完整移动治理任务流 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P08 - Mobile Experience Governance` | `/console/experience` 响应式视图 | 首轮落地待观察 | D31-D35 未覆盖完整移动经验任务流 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P09 - Full Navigation & Permission IA` | Console 路由分组、权限守卫、侧栏 | 已落地 | `router/index.tsx`、`routeMeta.ts`、D14 记录 | 阶段验收观察权限反馈与侧栏分组 |
| `P10 - Commerce Operations` | `/console/products`、`/console/orders`、`/console/coins` | 已落地 | D15 / D17 / D34 / D35 记录 | 阶段验收观察商品详情、订单详情和萝卜币表格 |
| `P11 - Document Governance` | `/console/documents` | 已落地 | `DocumentGovernancePage`、D18 / D30 / D35 记录 | 阶段验收观察访问策略、版本弹窗和抽屉 |
| `P12 - RBAC Permission Matrix` | `/console/roles`、`/console/roles/:roleId/permissions` | 已落地 | `RoleList`、`RolePermissionPage`、D23 / D26 记录 | 阶段验收观察权限树和保存反馈 |
| `P13 - Operations Tools` | `/console/system-config`、`/console/hangfire` | 需边界确认；当前只承载系统设置与外部 Hangfire iframe | `HangfirePage`、D27 记录 | 内部任务队列 / 失败重试 / 运行审计需先补后端数据来源与 API 设计 |
| `P14 - Mobile Console Hub` | Console 移动响应式壳层 | 首轮落地待观察 | `AdminLayout` 移动侧栏，D31-D35 局部复核 | 纳入阶段 Gateway mobile CSS 视图复核，不单独做移动 Console App |
| `P15 - Mobile Commerce Operations` | `/console/orders`、`/console/products` 响应式视图 | 首轮落地待观察 | D31-D35 局部复核 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P16 - Mobile Document Governance` | `/console/documents` 响应式视图 | 首轮落地待观察 | D31-D35 局部复核 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P17 - Mobile RBAC Permission` | `/console/roles/:roleId/permissions` 响应式视图 | 首轮落地待观察 | D23 / D26 静态收口 | 纳入阶段 Gateway mobile CSS 视图复核 |
| `P18 - Mobile Operations Jobs` | `/console/hangfire` 响应式视图 | 需边界确认；内部 Jobs 不是当前项目内平台 | D27 记录 | 跟随 P13，先决定是否后置 |

## 优先差距

1. **公开聊天室边界**：`public-web-unified-experience.pen` 的 `P15 / P16` 当前没有正式 `/chat` 公开路由，不能视为已落地。下一步应先裁决是否后置，或补独立公开聊天室方案。
2. **公开首页语义**：`P01` 当前由 `/discover` 承接，根路径 `/` 会解析到 `/discover`。若设计源坚持独立公开首页，需要另起方案；否则应把 P01 解释为公开 App 首页概念层。
3. **Console 内部调度 / 运维平台边界**：`P04 / P13 / P18` 目前没有完整内部调度或任务平台数据来源。若要实现，前置条件是 API 与数据模型设计；当前不应只做静态 UI。
4. **移动 Console 画板定位**：`P07 / P08 / P14-P17` 目前是响应式 Console 的验收参考，不是独立移动 Console 应用。后续优先做运行态观察，不直接扩成新路由。
5. **阶段运行态验收清单**：public / private / console 大部分页面族已有首轮代码，但仍需要一次 Gateway PC `1920x1080` 与 mobile `390x844` CSS 视口成组复核；执行前必须等待用户明确说明前后端已启动。

## 后续建议

1. 先进入 `P3-12-D38` 边界裁决与验收清单整理，明确 `P01`、`P15 / P16`、Console `P04 / P13 / P18` 和移动 Console `P07 / P08 / P14-P17` 的发布前归属。
2. 若 D38 裁决为后置，则直接组织一次 UI 专题 Gateway PC / mobile 阶段验收。
3. 若 D38 裁决为发布前必需，再按页面族成组做代码治理；不得直接混入 `P3-12-E` 发布候选。

## 本批不做

- 不修改 Pencil 设计源。
- 不修改业务代码、API、权限、路由、保存动作或载荷。
- 不启动 Gateway 页面联调。
- 不进入 `P3-12-E`。
- 不创建发布 tag、不恢复 PR / 发布流程。
