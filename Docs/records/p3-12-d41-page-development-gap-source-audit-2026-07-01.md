# P3-12-D41 页面开发缺口源码核对记录

## 基本信息

- 日期：`2026-07-01`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 性质：D37 设计源矩阵后的源码级页面缺口核对与第一组页面状态修正
- 范围：`radish.client` public / private / author 页面族、`radish.console` 管理页面族、D38 已裁决后置项

## 输入依据

- [P3-12-D37 UI 设计源差距矩阵记录](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)
- [P3-12-D38 UI 边界裁决与阶段验收清单](/records/p3-12-d38-ui-boundary-and-stage-acceptance-plan-2026-07-01)
- [P3-12-D39 Gateway PC / Mobile 阶段验收记录](/records/p3-12-d39-gateway-pc-mobile-stage-acceptance-2026-07-01)
- [P3-12-D40 UI 专题退出判断修正与页面开发回拉](/records/p3-12-d40-ui-topic-exit-decision-2026-07-01)
- 源码入口：
  - `Frontend/radish.client/src/bootstrap/entryRoute.ts`
  - `Frontend/radish.client/src/main.tsx`
  - `Frontend/radish.client/src/public/PublicEntry.tsx`
  - `Frontend/radish.client/src/docs/DocsAuthorApp.tsx`
  - `Frontend/radish.console/src/router/routes.ts`
  - `Frontend/radish.console/src/router/routeMeta.ts`

## 判定口径

- **已开发可复核**：源码存在真实路由、页面组件和主要数据读取 / 状态渲染，后续只需按成组验收覆盖 PC / mobile 与真实数据态。
- **已开发，需动作或数据复核**：页面与路由存在，但写入、上传、可编辑数据、权限态或目标跳转需要准备安全测试数据后复核。
- **已裁决后置**：D38 已明确不作为 P3-12-D 退出条件，不在本批重新拉入开发。
- **需补设计**：当前未发现新的必须补 Pencil 设计源后才能开发的页面级缺口；若后续要扩展后置能力，需要先补设计与接口契约。

## 源码核对结论

### Public 页面族

- `/` 在 Browser 环境下解析到 `/discover`，公开首页发布前由发现页承接，符合 D38 裁决。
- `/discover`、`/forum`、`/forum/search`、`/forum/question`、`/forum/poll`、`/forum/lottery`、`/forum/post/:id`、`/docs`、`/docs/:slug`、`/shop`、`/shop/:productId`、`/leaderboard`、`/u/:userId` 均有源码路由和页面组件。
- 公开论坛详情的来源返回、公开文档详情返回、公开商城详情返回和公开个人页返回由 `PublicEntry` 的 source state 管理，不是独立缺失页面。
- `/chat` 公开聊天室仍无正式公开路由；该项已在 D38 后置，不进入本批开发。

### Private / Author 页面族

- `/workbench`、`/me`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience`、`/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:id`、`/shop/inventory`、`/notifications`、`/messages`、`/circle`、`/pet` 均有正式入口和页面组件。
- `/forum/compose` 与帖子详情 intent 仍归属论坛页面族，源码存在 route state 与组件分支；后续应在成组复核里覆盖草稿、编辑、评论定位等真实数据态。
- `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` 已有作者态路由和页面组件。D39 未看到 `/docs/edit/:id` 更可能是测试数据为内置或已删除文档导致编辑入口不展示，不是路由缺失。
- 本批已修正 `DocsAuthorApp` 文档列表的只读状态表达：内置或已删除文档不再静默隐藏编辑入口，而是展示只读原因；权限、保存、上传和路由契约保持不变。

### Console 页面族

- Console 路由元信息覆盖 `/console/`、`/applications`、`/products`、`/orders`、`/users`、`/users/:userId`、`/roles`、`/roles/:roleId/permissions`、`/categories`、`/tags`、`/documents`、`/stickers`、`/stickers/:groupId/items`、`/moderation`、`/coins`、`/experience`、`/system-config`、`/profile`、`/settings`、`/hangfire` 等页面。
- Dashboard 仍是运营总览，不等同于 D38 已后置的内部调度中心。
- `/hangfire` 仍是受保护的外部 Hangfire Dashboard 承载入口，不等同于项目内 Jobs 平台；内部 Jobs 平台已后置。
- Console 移动画板继续作为响应式后台验收参考，不扩展为独立移动 Console 应用。

### Foundation / Visual 基座

- 本批未修改主题 token、Pencil 设计源、共享壳层、窗口系统或 WebOS 桌面能力。
- 当前没有新增硬编码颜色或跨页面视觉体系改造；作者文档列表只读状态属于单点状态表达修正。

## 缺口排序

1. **作者文档真实写动作与可编辑数据复核**
   - 页面与路由已存在，本批先补只读原因展示。
   - 下一步需要准备非内置、未删除的安全测试文档，覆盖编辑入口、保存、上传、版本历史和公开阅读回跳。
2. **Public / Private 主路径真实数据态复核**
   - 优先覆盖公开论坛类型流 / 详情 intent、作者发帖、商城订单 / 背包、我的内容 / 历史 / 附件等用户主路径。
   - 目标是确认页面已开发后的真实数据态和关键动作，不把代表页访问等同于页面族完成。
3. **Console 深层管理动作复核**
   - 优先覆盖商品、订单、用户、角色权限、文档治理、系统设置和内容治理的保存、抽屉、详情、表格换行和权限态。
   - Dashboard 内部调度中心与 Jobs 平台仍后置，不在当前 Console 页面组内补开发。
4. **移动响应式抽样**
   - public / private / author / console 页面组完成代码侧复核后，再按 PC 与移动 CSS 视口集中复核。
   - 独立移动 Console 应用仍后置。

## 本批变更

- 新增本 D41 源码核对记录。
- `DocsAuthorApp` 文档列表对不可编辑文档展示“内置只读 / 已删除只读”，避免误判为编辑页缺失。
- 未新增 API、权限、路由、保存载荷、Pencil 画板或后端数据模型。

## 验证建议

- 开发中：执行 `npm run build --workspace=radish.client` 与仓库变更卫生检查。
- 成组验收：完成下一组页面动作复核后，再请用户确认前后端已启动，并按 Gateway 覆盖 PC / mobile。
- 准备合并到 `master` 前：再集中执行 baseline、identity、host runtime 和必要页面补验。
