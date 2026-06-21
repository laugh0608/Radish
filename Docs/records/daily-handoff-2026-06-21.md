# 2026-06-21 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-21 00:00 +0800"` 在本记录提交前回顾到今日 22 个提交；本记录自身作为收工文档提交追加。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `ff4395eb feat(flutter): 承接论坛作者编辑入口` | Flutter 论坛 | 原生端承接作者帖子正文编辑和作者根评论编辑，继续复用 `clientSubmissionId` 提交意图。 |
| `342d8321 docs(planning): 收束 P3-10 后续增量评审` | P3-10 / 评审 | 确认 Flutter 子评论编辑和回答编辑不进入下一批默认代码，P3-10 转向阶段收束准备。 |
| `f1701ef8 docs(planning): 补充 P3-10 阶段收束准备` | P3-10 / 收束 | 整理 P3-10 完整批次范围、验证结论和剩余风险。 |
| `b93fad3f docs(planning): 记录 P3-10 批次验证结果` | P3-10 / 验证 | 补齐 P3-10 批次级验证结果和 PR 前判断材料。 |
| `46b9e8b2 docs(planning): 记录 P3-10 运行态复核` | P3-10 / 运行态 | 记录 P3-10 阶段运行态复核结论，后续暂缓 PR。 |
| `0e0cccd8 docs(planning): 启动 P3-11 发布候选整备` | P3-11 / 规划 | 建立 P3-11 发布候选验收矩阵与整备入口。 |
| `0003f66c docs(planning): 记录 P3-11 轻量复访审计` | P3-11 / 审计 | 完成轻量复访缺口只读审计并形成阶段收束决策，未触发定向回修。 |
| `5b2f9d87 docs(planning): 启动 P3-12 Web 完全化专题` | P3-12 / 主线切换 | 启动 Web 完全化与 WebOS 收束专题，明确正式 Web 主路径优先。 |
| `0beb6ee9 docs(planning): 完成 P3-12-A 功能资产盘点` | P3-12-A / 盘点 | 输出 WebOS 与正式 Web 功能资产矩阵，确定 B1 先做账户资产与商城交易。 |
| `3a548258 docs(planning): 梳理 P3-12-B1 Web 交易方案` | P3-12-B1 / 方案 | 梳理账户资产、商城购买、订单、库存和交易回流的正式 Web 路由与登录回流边界。 |
| `bd9e0caf feat(client): 补齐 P3-12-B1 Web 交易路由契约` | P3-12-B1 / 路由 | 新增资产与商城交易正式 Web return path、route state 和静态契约测试。 |
| `a6e289cf feat(client): 接入商城私域 Web 入口` | P3-12-B1 / 商城私域 | 接入 `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 等私域商城路径。 |
| `012664a3 feat(client): 接入我的资产 Web 入口` | P3-12-B1 / 资产 | 接入 `/me/assets` 与 `/me/assets/transactions`，把完整资产流水迁到正式 Web。 |
| `2571593b feat(client): 接入公开商城 Web 购买回流` | P3-12-B1 / 购买回流 | 公开商品详情购买从 `/desktop` 回跳改为 `/shop/product/:productId?intent=purchase` 登录回流。 |
| `d4c7dc8f feat(client): 收口商城订单通知 Web 路由` | P3-12-B1 / 通知回流 | 订单通知目标切到正式 Web 订单路由。 |
| `495e83a8 fix(client): 收口公开商品榜单购买口径` | P3-12-C1 / 文案口径 | 公开商品榜单只读与商品详情登录购买口径完成收口。 |
| `b6cde141 fix(client): 清理公开商城壳层旧语义` | P3-12-C1 / 残留清理 | 清理公开商城旧购买按钮翻译和壳层式旧说法。 |
| `c0b2560f fix(client): 收口公开账号动作私域口径` | P3-12-C1 / 残留清理 | 公开发现和公开个人页中的订单、背包、资产动作改为私域 Web 语义。 |
| `b5eae384 docs(planning): 进入 P3-12-B2 个人中心 Web 化` | P3-12-B2 / 方案 | 梳理完整个人中心 Web 化边界，明确 `/me` 子路径、旧 ProfileApp 复用边界和 API helper 收口。 |
| `712e6dd4 feat(client): 接入个人中心 Web 子路径` | P3-12-B2 / 个人中心 | 接入 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 正式 Web 路由和页面。 |
| `b8c4e870 feat(client): 补齐个人中心正式链接语义` | P3-12-B2 / 链接语义 | 我的内容与浏览历史列表补真实公开 `href`，旧 WebOS opener 仅保留在历史入口。 |
| `83ea8306 feat(client): 接入论坛作者态 Web 路径` | P3-12-B3 / 论坛作者态 | 接入 `/forum/compose` 和详情 `intent=answer|edit|history`，发帖、回答、采纳、作者编辑和编辑历史查看进入正式 Web 路由。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已记录 B1 / C1 / B2 / B3 首批代码完成，并把明日事项调整为 B3 小阶段验收准备或 B4 文档作者态归属裁决。
- 已同步路线总览：[开发路线图](/development-plan) 已把当前开发精力从 B1 实现改为 B3 验收准备、B4 归属裁决和 P3-12 文档 / 验证口径同步。
- 已同步 P3-12 专题：[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement) 已覆盖 A / B1 / C1 / B2 / B3 状态和下一顺位。
- 已同步 B3 记录：[P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21) 已写入首批代码结果、验证口径、后置项和真实 smoke 约束。
- 已复核既有治理说明：[论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance) 仍与 B3 对齐；B3 继续复用既有 `clientSubmissionId`，未新增 API 治理口径。
- 本次收工补同步开发日志：[2026 年 6 月第 3 周开发日志](/changelog/2026-06/week3)、[2026 年 6 月开发日志](/changelog/2026-06)、[2026 年开发日志](/changelog/2026) 和 [记录与验收索引](/records/)。
- 已复核无需跟随更新范围：今天没有页面级 UI 设计或跨页面视觉重塑，未新增 Pencil / 设计源文件要求；没有启动 Flutter 新主线、PR、发布、无关 WebOS 清扫、完整聊天平台、推荐算法、联邦社交、完整 PWA 或完整 E2E 平台。

## 今日验证

P3-12-B1 / C1 / B2 / B3 已按批次执行代码侧验证：

- B1：路由 / 登录回流 / 公开购买 / 资产与订单相关 Node 契约测试、`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`、`git diff --check`。
- C1：公开商城 / 公开账号动作相关 Node 契约测试、`npm run type-check --workspace=radish.client`、`git diff --check`。
- B2：`node --test --test-isolation=none ./Frontend/radish.client/tests/meRouteState.test.ts ./Frontend/radish.client/tests/authReturnPath.test.ts ./Frontend/radish.client/tests/entryRoute.test.ts ./Frontend/radish.client/tests/realUsagePathContracts.test.ts ./Frontend/radish.client/tests/publicSeoStatic.test.ts`、`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`、`git diff --check`。
- B3：`node --test --test-isolation=none ./Frontend/radish.client/tests/publicRouteState.test.ts ./Frontend/radish.client/tests/forumNavigation.test.ts ./Frontend/radish.client/tests/authReturnPath.test.ts ./Frontend/radish.client/tests/entryRoute.test.ts ./Frontend/radish.client/tests/realUsagePathContracts.test.ts ./Frontend/radish.client/tests/publicSeoStatic.test.ts`、`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`。
- 收工文档批次执行：`git diff --check`、`npm run check:repo-hygiene:changed`、`npm run check:repo-hygiene:staged`、`git diff --cached --check`。

运行态说明：

- 今天未启动 API / Auth / Gateway / Vite。
- 今天未执行真实 Gateway / 浏览器 PC + mobile smoke。
- 后续真实 smoke 不沿用历史启动状态；只有用户在新会话明确说明前后端已经启动时，才执行 Gateway 页面复核。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)、[P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21) 和本记录，确认 B1 / C1 / B2 / B3 首批代码均已完成。
2. 第一顺位：推进 `P3-12-B3` 小阶段验收准备，整理 `/forum/compose`、`/forum/post/:postId?intent=answer|edit|history`、登录回流、作者权限、写入幂等、公开 canonical 和失败态的 PC / mobile 验收矩阵。
3. 若执行真实 smoke，必须先让用户明确确认 API / Auth / Gateway / 前端已启动；确认前不访问 Gateway，不启动 dev server，不沿用历史运行状态。
4. 若暂不做运行态验收，进入 `P3-12-B4` 文档作者态归属裁决：先盘点 WebOS Wiki / Docs、公开文档页、文档写入 API / 路由、权限模型和 Console 可能归属，再决定正式 Web 管理页、Console 治理页或 WebOS 历史维护线。
5. B4 涉及页面归属、接口契约、权限边界或运行时行为时，先写清方案并等待确认；若只是现状盘点、文档边界或低风险说明同步，可直接整理。
6. 继续遵守 P3-12 约束：不启动 Flutter、PR、发布或无关 WebOS 清扫；页面级 UI 设计或跨页面视觉重塑必须先用 Pencil 做设计稿；普通功能迁移继续使用 `@radish/http`，不新增自定义 fetch / axios 封装。
