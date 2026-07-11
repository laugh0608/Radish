# P3-12-D64 UI 专题候选前集中验收准备

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`P3-12-D61 Public Web`、`P3-12-D62 Private / Author`、`P3-12-D63 Console` 重新实现后的候选前证据整理与 D65 验证清单

## 背景

`P3-12-D60` 已确认：D59 的无阻断 Gateway smoke 不能替代 Pencil 逐页 UI 与功能缺口实现。随后 D61、D62、D63 分别补齐 Public Web、Private / Author 和 Console 当前发布前范围的首批逐页实现，并完成对应静态验证与必要真实页面复核。

本批 D64 不新增功能代码，不提前进入 `P3-12-E`，而是把 D61-D63 的证据、剩余限制和候选前验证清单收成一个可执行入口，为下一批 D65 验证执行做准备。

## 当前判断

- `P3-12-D61 Public Web` 当前发布前范围已完成首批实现，`P01-P14` 覆盖公开发现、论坛、文档、商城、榜单、公开主页和移动公开任务流。
- `P3-12-D62 Private / Author` 当前发布前页面族已完成首批实现，覆盖 `/workbench`、`/me` 子页、资产 / 订单 / 背包、通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态。
- `P3-12-D63 Console` 已完成治理、商业运营、文档治理、用户管理、权限矩阵页面族首批实现、成组静态收口和 Gateway 成组真实页面复核。
- D61-D63 均保持“不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷”的开发边界。
- 当前仍不能直接进入 `P3-12-E`；下一步应执行候选前验证，而不是创建 tag、恢复 PR 或进入生产部署流程。

## 证据矩阵

### Public Web

- 记录入口：[P3-12-D61 Public Web `/discover` Pencil 首批实现记录](/records/p3-12-d61-public-web-discover-pencil-first-implementation-2026-07-04)。
- 覆盖范围：`/discover`、`/forum`、论坛详情、`/docs`、Docs 详情、`/shop`、商品详情、`/leaderboard`、公开个人页和移动公开任务流。
- 主要结论：`P01-P14` 已完成当前发布前范围首批实现；公开聊天室与移动聊天回复流继续作为 Public 小专题内产品 / API 后置缺口。
- 已有验证：`radish.client` 静态检查、构建、repo hygiene、`git diff --check` 和 Gateway 真实页面复核已在 D61 记录内留痕。

### Private / Author

- 记录入口：
  - [P3-12-D62 Private / Author Workbench 首批实现记录](/records/p3-12-d62-private-author-workbench-first-implementation-2026-07-04)
  - [P3-12-D62 `/me` 内容历史复访组首批实现记录](/records/p3-12-d62-me-content-history-first-implementation-2026-07-05)
  - [P3-12-D62 资产 / 订单 / 背包页面族首批实现记录](/records/p3-12-d62-assets-orders-inventory-first-implementation-2026-07-05)
  - [P3-12-D62 通知 / 消息页面族首批实现记录](/records/p3-12-d62-notifications-messages-first-implementation-2026-07-05)
  - [P3-12-D62 圈子 / 宠物页面族首批实现记录](/records/p3-12-d62-circle-pet-first-implementation-2026-07-05)
  - [P3-12-D62 论坛 / Docs 作者页面族首批实现记录](/records/p3-12-d62-forum-docs-author-first-implementation-2026-07-05)
- 覆盖范围：`/workbench`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience`、`/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`、`/notifications`、`/messages`、`/circle`、`/pet`、`/forum/compose`、论坛详情作者 intent、Docs 作者台。
- 主要结论：Private / Author 当前发布前页面族已按 `private-web-workflows.pen` 的首批逐页范围补齐任务面、指标、上下文 rail、移动单列任务流和必要真实回流入口。
- 已有验证：D62 内多批次已覆盖 `radish.client` type-check / build、changed lint、repo hygiene 和 `git diff --check`；其中 `/me`、资产 / 订单 / 背包等批次已补 Gateway PC / mobile 真实复核。

### Console

- 记录入口：
  - [P3-12-D63 Console 治理工作台首批实现记录](/records/p3-12-d63-console-governance-workbench-first-implementation-2026-07-05)
  - [P3-12-D63 Console 商业运营页面族首批实现记录](/records/p3-12-d63-console-commerce-ops-first-implementation-2026-07-05)
  - [P3-12-D63 Console 文档治理页面族首批实现记录](/records/p3-12-d63-console-document-governance-first-implementation-2026-07-05)
  - [P3-12-D63 Console 用户管理表格 CRUD 首批实现记录](/records/p3-12-d63-console-user-management-table-crud-first-implementation-2026-07-05)
  - [P3-12-D63 Console 权限矩阵页面族首批实现记录](/records/p3-12-d63-console-rbac-permission-matrix-first-implementation-2026-07-05)
  - [P3-12-D63 Console 成组静态收口与后置缺口整理记录](/records/p3-12-d63-console-grouped-static-closure-and-deferred-gap-review-2026-07-05)
  - [P3-12-D63 Console Gateway 成组真实页面复核记录](/records/p3-12-d63-console-gateway-grouped-smoke-2026-07-05)
- 覆盖范围：`/console/moderation`、`/console/experience`、`/console/products`、`/console/orders`、`/console/documents`、`/console/users`、`/console/roles/:roleId/permissions`。
- 主要结论：D63 目标页没有目标样式 / 直接请求 / 控制台日志残留，Gateway 登录回流、PC `1920x1080` 与移动 `390x844` CSS 视口页面复核未发现阻断级问题。
- 已有验证：`npm run build --workspace=radish.console`、`npm run lint:changed`、repo hygiene、`git diff --check`、Gateway 成组复核和截图证据已留痕。

## 剩余限制

- Public Web `P15-P16` 公开聊天室 / 移动聊天回复流仍是 Public 小专题内产品 / API 后置缺口，不纳入当前发布前范围。
- D63 Console 移动复核已覆盖 `390x844` CSS 视口，但本地 Playwright 会话中 DPR 读取仍受工具限制，最终结论以 CSS 视口无横向溢出和页面可用为准。
- D63 Gateway 复核未执行高影响写动作：未保存权限、未执行治理动作、未重试订单、未删除商品、未导入 / 回滚文档。
- 独立移动 Console、内部 Jobs 平台、新的治理 API、完整聊天平台、完整钱包、退款 / 售后、支付口令和资产风控仍按专题记录后置。
- 本批 D64 不执行新的 Gateway 真实页面 smoke；真实 smoke 仍必须在执行前由用户于当轮明确确认前后端已启动。

## D65 验证清单

### 启动无关验证

- `npm run validate:ci -- --report`
- `npm run validate:baseline`
- `npm run validate:identity`
- `npm run validate:baseline:host -- --report`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

### 启动相关验证

仅在用户于执行当轮明确说明前后端已启动后执行：

- `npm run check:host-runtime -- --details --report`
- Gateway PC `1920x1080`：覆盖 Public Web、Private / Author 和 Console 当前发布前代表页面。
- Gateway mobile `390x844`：覆盖同一页面族；如工具能稳定设置 DPR，再补 `@ DPR 3` 读数。
- Console 继续以只读和低风险交互为主，高影响写动作如权限保存、治理动作、订单重试、文档导入 / 回滚必须先确认安全数据与回滚方式。

## 本批不做

- 不新增功能代码、API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不把 D64 记录写成 D61-D63 全量通过的发布结论。
- 不创建发布 tag，不恢复 `dev -> master` PR，不进入 M15 测试或生产部署。

## 验证

本批为文档与规划口径准备批次，已完成：

- `wc -l Docs/planning/current.md Docs/planning/p3-12-web-completion-webos-retirement.md Docs/records/p3-12-d64-ui-candidate-acceptance-prep-2026-07-05.md`：通过，`current.md` 保持 300 行。
- `npm run check:repo-hygiene:changed`：通过。
- `npm run lint:changed`：通过，本批没有需要 lint 的前端脚本文件。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:staged`：通过。
- `git diff --cached --check`：通过。

## 后续

- 下一顺位进入 `P3-12-D65 UI 专题候选前验证执行`。
- D65 应先完成启动无关验证，再在用户明确确认前后端已启动后执行 Gateway / host runtime 验证。
- D65 通过并完成记录后，才判断是否具备进入 `P3-12-E` 的条件。
