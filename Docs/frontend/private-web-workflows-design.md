# 私域与作者态 Web 工作流设计说明

> 日期：2026-06-25（Asia/Shanghai）
>
> 更新：2026-06-28（Asia/Shanghai）：设计源已按 `F02` 公共壳层契约二次重构为 `P01-P30`，旧模板化页面已替换为真实路由驱动的 PC / mobile 页面族；统一 private header 与移动 tab，并补齐移动端单任务页面。复审后已继续提高 `P18 / P20` Docs 作者态和 `P21-P30` 移动页的信息密度，移动页不再保留空白模板壳或同质化卡片堆叠。private 移动底栏作为跨源基准样式，固定为 5 项浮动胶囊栏。
>
> 状态：设计源 `P01-P30` 已补齐；当前作为 `radish.client` 视觉实现前说明

## 设计源

```text
Docs/frontend/design-sources/private-web-workflows.pen
```

画板：

| 画板 | 职责 |
| --- | --- |
| `P01 - Workbench Route Map` | `/workbench` 正式 Web 功能地图、继续处理队列和 WebOS 历史入口边界 |
| `P02 - Me Overview Dashboard` | `/me` 个人状态、公开身份、经验、资产摘要、宠物摘要和最近复访 |
| `P03 - My Content Tabs` | `/me/content` 帖子、评论、轻回应 tab 和选中内容预览 |
| `P04 - Browse History` | `/me/history` 浏览历史、公开详情来源返回和复访决策 |
| `P05 - Attachment Browser` | `/me/attachments` 按业务归属浏览头像、帖子、评论和文档附件 |
| `P06 - Experience Ledger` | `/me/experience` 等级进度、经验来源和经验流水 |
| `P07 - Assets Overview` | `/me/assets` 胡萝卜余额、近期流水和资产入口 |
| `P08 - Asset Transactions` | `/me/assets/transactions` 资产流水筛选、表格和选中流水详情 |
| `P09 - Orders List` | `/shop/orders` 订单状态分组、购买后回流和订单详情入口 |
| `P10 - Order Detail` | `/shop/order/:orderId` 支付、权益发放、商品回看和背包回流 |
| `P11 - Inventory Benefits` | `/shop/inventory` 背包权益、来源订单、激活 / 停用 / 使用入口 |
| `P12 - Notifications Center` | `/notifications` 未读通知、频道筛选和正式 Web 目标分流 |
| `P13 - Messages Workspace` | `/messages` 会话列表、聊天正文、回复输入和订单 / 用户上下文 |
| `P14 - Circle Feed` | `/circle` 关注动态、正在关注、关注者和公开详情来源返回 |
| `P15 - Pet Care` | `/pet` 宠物档案、基础照护动作、状态条和变化流水 |
| `P16 - Forum Compose` | `/forum/compose` 论坛发帖编辑器、分类 / 标签、发布检查和提交反馈 |
| `P17 - Forum Edit History` | `/forum/post/:id?intent=answer|edit|history` 作者编辑、问答回答和编辑历史 |
| `P18 - Docs Mine` | `/docs/mine` Docs 作者库、文档树、文档列表和低风险作者动作 |
| `P19 - Docs Compose Edit` | `/docs/compose`、`/docs/edit/:id` 文档编辑器、元信息和保存 / 预览动作 |
| `P20 - Docs Revisions` | `/docs/revisions/:id` 文档版本栈、只读预览和差异回看 |
| `P21 - Mobile Workbench` | 移动端 `/workbench`，正式 Web 功能地图和今日队列 |
| `P22 - Mobile Me` | 移动端 `/me`，个人状态、经验、资产摘要和我的入口 |
| `P23 - Mobile Content History` | 移动端 `/me/content`、`/me/history`、`/me/attachments` 的复访任务 |
| `P24 - Mobile Assets Ledger` | 移动端 `/me/assets`、`/me/assets/transactions`，余额和近期流水 |
| `P25 - Mobile Orders Inventory` | 移动端 `/shop/orders`、`/shop/order/:id`、`/shop/inventory` 订单背包任务 |
| `P26 - Mobile Notifications` | 移动端 `/notifications`，未读通知和目标跳转 |
| `P27 - Mobile Messages` | 移动端 `/messages`，会话正文和回复输入 |
| `P28 - Mobile Circle` | 移动端 `/circle`，关注动态和关系链 |
| `P29 - Mobile Pet` | 移动端 `/pet`，宠物状态、照护动作和状态流水 |
| `P30 - Mobile Author` | 移动端 `/forum/compose`、`/docs/mine`、`/docs/edit/:id` 的继续创作入口 |

## 目标

- 让登录态私域与作者态在正式 Web 中形成统一、可复访的工作流。
- 把 `/workbench`、`/me`、资产 / 订单、通知 / 消息、圈子 / 宠物、论坛作者态和文档作者态整理为一致的信息节奏。
- 固定作者态与治理态边界：普通作者处理草稿、发布、保存、版本回看；Console 处理治理、权限、审计和高风险操作。
- 在进入视觉代码前，先固定桌面密度、移动单列顺序、状态槽和停止线。

## 统一契约

### 私域壳层

- 登录态头部保留品牌、私域导航、当前身份和同步 / 登录状态。
- “工作台”默认进入 `/workbench`，不直接打开 `/desktop`。
- 私域页面展示 `DisplayName / DisplayHandle`；`PublicId` 只用于 URL、分享和传参。
- `/desktop` 继续作为 WebOS 历史兼容入口，不承接新增正式 Web 主路径。

### 私域首页

- `/workbench` 的 dominant region 是正式 Web 功能地图和继续处理队列。
- 快速入口覆盖 `/me`、资产流水、订单 / 背包、通知、消息、圈子、宠物、我的内容、附件、历史和作者入口。
- 右侧身份操作组必须贴合真实按钮内容，不能用固定宽度外框撑出无意义空白；业务 rail 只在页面主体承接状态理解、决策或动作。
- 状态槽必须覆盖待处理、新通知、宠物状态、加载、空态、错误和权限限制。

### 资产 / 订单 / 背包

- `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:id` 和 `/shop/inventory` 共享私域数据面节奏。
- 桌面端使用余额摘要、筛选工具条、资产流水表、订单详情 rail 和背包卡片。
- 订单详情展示支付、发放、取消状态、商品回看和回流来源；背包只展示已发放权益和可用状态。
- 公开商品 `intent=purchase` 登录后进入私域购买确认、订单详情和背包回流，不回跳 WebOS 商城窗口。
- 当前设计不新增独立售后 / 退款平台；不扩展转账、支付口令、完整钱包、资产风控或独立退款平台。

### 消息 / 通知 / 宠物 / 圈子

- `/messages`、`/notifications`、`/pet` 和 `/circle` 作为正式 Web 私域复访入口。
- 通知目标必须分流到正式 Web 路由，例如公开帖子、订单详情、文档详情和消息会话。
- 宠物只承接领取、命名、基础照顾、状态反馈和变化流水，不扩展经济系统或商城道具。
- 圈子展示关注动态、关系链和公开主页跳转，来源返回语义保持在正式 Web 内。

### 作者入口

- 不新增独立“作者工作台”路由；作者态由 `/forum/compose`、`/forum/post/:id?intent=answer|edit|history`、`/docs/mine`、`/docs/compose`、`/docs/edit/:id` 和 `/docs/revisions/:id` 承接。
- 论坛作者态与文档作者态在视觉节奏上保持一致，但页面形态必须贴合真实功能：论坛是发帖 / 编辑 / 问答，Docs 是文档树 / Markdown 编辑 / 版本回看。
- 写入继续保留 `clientSubmissionId`、草稿同步、保存状态、发布成功和发布失败反馈。
- 作者态不包含审核治理、权限策略、文档治理列表、回收站或 Console 管理动作；普通作者的导出 / 分享只作为低风险二级动作呈现。

### 编辑器 / 版本

- 编辑器主区展示标题、正文、元信息、保存状态、发布动作和提交前检查。
- 版本侧栏展示当前草稿、发布版本、编辑历史和初稿，并提供只读差异预览。
- 版本回看以阅读、对比和留痕为主；版本回退、完整富文本、批量导入导出、撤回、归档和回收站治理若进入实现需单独拆分确认。
- 发布成功后进入公开详情，失败时保留错误说明、重试和回到草稿的路径。

### 移动单列

- 移动端按单列顺序展示：状态栏、私域头部、用户状态、关键入口、复访消息、状态槽、边界提示和底部 tab。
- 移动端拆为工作台、我的状态、内容历史、资产流水、订单背包、通知、消息、圈子、宠物和创作 10 个任务页面，避免把桌面三栏压缩到手机宽度。
- 移动页必须围绕各自真实任务组织信息：工作台展示今日队列和功能地图，通知展示未读汇总和目标队列，消息展示会话上下文，宠物展示状态条和照护日志，创作展示草稿 / 发布检查 / 版本状态；不得只用统一 header + 简单卡片列表撑页。
- 主要动作放在可触达区域，关键入口使用纵向列表，不依赖横向滚动或桌面三栏。
- 底部 tab 只保留工作台、资产、创作、消息和我的，使用 64px 高浮动胶囊、图标上文字下和柔和品牌色激活态；不复刻 Dock、窗口系统、桌面背景或 WebOS app 外壳。

## 视觉约束

- 继续使用 `rx-*` 设计变量，与公开 Web 设计源保持同一 token 口径。
- 私域桌面默认使用中等偏紧密密度，资产 / 订单和作者态页面可比公开阅读页更紧凑。
- 卡片只用于功能入口、数据摘要、表格容器、状态块和必要工具面板，不把每个文本行都包成独立卡片。
- 不同功能页必须有领域特征：资产用余额 / 流水，订单用状态分组 / 详情时间线，消息用会话结构，创作用编辑器，宠物用状态与照护动作；禁止用同一三栏模板套所有页面。
- Docs 作者态 PC 页使用文档树、文档表格、版本对比、发布状态和侧栏证据形成信息密度；不得把 `/docs/mine`、`/docs/revisions/:id` 退化为两个空白面板或泛用列表模板。
- 右侧 rail 必须服务状态理解、决策或动作，不能用大面积空白和装饰撑满页面。
- 移动端不做桌面布局缩小版，必须保持单列可读和稳定触控目标。
- 实现边界、技术停止线和 WebOS 退场说明只写入设计 / 记录文档，不作为普通用户可见面板进入真实 UI。
- 小字号说明和路由元信息优先使用 `rx-text-secondary`，`rx-text-muted` 只用于低优先级辅助标签，避免 11-12px 文本过淡。

## 实现顺序

1. 先以本说明和 `.pen` 画板确认私域 / 作者态视觉实现边界。
2. public / private 业务设计源确认后，进入 `radish.client` 视觉代码实现，优先沉淀公共壳层、token、状态槽、功能入口、资产 / 订单 / 背包数据面、消息复访、作者态通用结构和移动单列节奏。
3. 完成代码后执行 `radish.client` 类型检查 / 构建；准备阶段性验收时再按用户确认的前后端启动状态执行 Gateway PC / mobile smoke。

## 当前不做

- 不绕过 Pencil 设计稿直接做跨页面视觉改造。
- 不把 WebOS Dock、窗口系统、桌面背景、窗口几何记忆或 `openApp` 语义迁入正式 Web。
- 不扩完整钱包、转账、支付口令、退款、资产风控、完整通知中心或完整聊天平台。
- 不把 Console 文档治理、权限策略、审核、回收站或高风险操作混入普通作者态。
- 不启动 Flutter 完整承接、移动版 WebOS、推荐算法或完整 PWA。
