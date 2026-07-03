# P3-12-D51 radish.client 私域 / 作者态移动任务流 UI 对齐首批

## 基本信息

- 日期：`2026-07-02`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：`radish.client` 私域 / 作者态移动任务流 UI 代码实现首批
- 设计依据：[私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)、[P3-12-D37 UI 设计源差距矩阵记录](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)、[P3-12-D50 UI 实现缺口复盘与下一批实现排序](/records/p3-12-d50-ui-gap-recheck-and-next-implementation-order-2026-07-02)

## 背景

`P3-12-D50` 已纠正 D49 后过早进入发布候选的判断，确认下一批应继续围绕 `private-web-workflows.pen` 的 `P21-P30` 移动任务流做 UI 实现，而不是进入 `P3-12-E`。

本批不通过文件系统读取或修改 `.pen` 设计源。Pencil MCP 当前无法连接到运行中的 Pencil app，因此本批按已经同步到 `Docs/` 的设计说明、D37 差距矩阵和 D50 排序进行代码侧对齐。

## 实现范围

本批覆盖 `radish.client` 已有真实路由的移动任务页，不新增业务能力：

| 设计源范围 | 真实页面 | 本批处理 |
| --- | --- | --- |
| `P24 - Mobile Assets Ledger` | `/me/assets`、`/me/assets/transactions` | 移动资产摘要改为主余额横跨 + 两列紧凑摘要，流水筛选保持同屏双列，流水金额回到内容列避免撑宽 |
| `P25 - Mobile Orders Inventory` | `/shop/orders`、`/shop/inventory` | 订单列表移动摘要、订单号 / 状态、分页和背包摘要 / tab 收紧，保留背包权益卡单列稳定触控 |
| `P26 - Mobile Notifications` | `/notifications` | 通知摘要从三张大卡纵向堆叠改为主未读横跨 + 两列任务指标，通知中心高度改为 `dvh` 约束 |
| `P27 - Mobile Messages` | `/messages` | 消息摘要与通知保持同一移动任务密度，聊天区域高度改为 `dvh` 约束 |
| `P28 - Mobile Circle` | `/circle` | 圈子摘要改为主指标横跨 + 两列指标，移动 tab 固定在当前滚动任务区顶部 |
| `P29 - Mobile Pet` | `/pet` | 宠物状态摘要和照护动作在移动端保持两列任务密度，首要指标横跨，动作按钮降低高度但保持触控目标 |
| `P30 - Mobile Author` | `/docs/mine`、`/docs/compose`、`/docs/edit/:id` | Docs 作者台摘要在窄屏保持两列任务密度，文档行和作者动作按钮收紧；论坛作者入口复用既有公开论坛作者态结构，本批未改发布器业务结构 |

## 代码范围

- `Frontend/radish.client/src/me/MeAssetsPage.module.css`
- `Frontend/radish.client/src/notifications/NotificationsApp.module.css`
- `Frontend/radish.client/src/messages/MessagesApp.module.css`
- `Frontend/radish.client/src/circle/CircleApp.module.css`
- `Frontend/radish.client/src/pet/PetApp.module.css`
- `Frontend/radish.client/src/docs/DocsAuthorApp.module.css`
- `Frontend/radish.client/src/apps/shop/pages/OrderList.module.css`
- `Frontend/radish.client/src/apps/shop/pages/Inventory.module.css`

## 保持不变

- 不新增业务 API、权限键、数据库结构、路由语义、登录回流或保存 / 提交载荷。
- 不修改论坛发布器、聊天协议、通知目标分流、宠物照护动作、订单 / 背包数据模型或 Docs 作者权限判断。
- 不把公开聊天室、内部调度中心、内部 Jobs 平台或独立移动 Console 回拉到当前批次。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试 / 生产部署流程。

## 验证记录

- `npm run build --workspace=radish.client`：通过
- `git diff --check`：通过
- `npm run check:repo-hygiene:changed`：通过

本批是代码侧移动 UI 对齐，未执行 Gateway PC / mobile 真实页面 smoke。若下一批准备阶段验收或用户明确要求运行态复核，应先等待用户明确说明前后端已经启动，再按 [浏览器联调与 Smoke 指南](/guide/browser-smoke) 覆盖 PC 与移动视图。

## 下一步

进入 `P3-12-D52 Public Web 移动公开任务流 UI 对齐`。

D52 继续留在 `P3-12-D` UI 专题内，优先覆盖 `/discover`、`/forum`、论坛详情、`/docs`、Docs 详情、`/shop`、商品详情、`/leaderboard` 和公开主页的移动公开任务流；不进入发布候选。
