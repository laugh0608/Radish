# 2026-05-21 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `00e2188e docs(frontend): clarify shell route contracts` | 前端壳层契约 | 明确公开壳、WebOS 桌面壳和 Tauri 壳的路由归属；避免把 Tauri 当原生 UI 重写或移动版 WebOS。 |
| `35697b4e fix(shop): expose inventory product return` | API 契约 / 商城背包 | `UserInventoryVo` 最小暴露 `VoSourceProductId`，背包消耗品可回到相关商品；不新增订单来源，不改变消耗品聚合模型。 |
| `c00735cf docs(planning): close p3-7 shop return loop` | 规划收口 | `P3-7-A` 商城复访链路首轮可转观察，未发现新的购买、订单、背包或通知回流高信号断点。 |
| `ca66d13a docs(planning): record p3-7 workspace screening` | WebOS / PC 工作台 | `P3-7-B` 高信号候选筛查未发现新的 `P0/P1` 或需立即拉起的小闭环，转观察。 |
| `21f728fa docs(planning): realign p3 roadmap for active development` | 阶段主线重评估 | 确认不能把维护观察当作无事可做，也不直接提前切入 `P3-8`。 |
| `92015fdb docs(planning): correct current p3 task focus` | 阶段主线校正 | 将近期主线收敛为 `P3-7-C` 近期任务重评估与下一批一天级任务选择。 |

## 文档同步复核

- 壳层路由契约已同步到 [前端多壳层策略](/frontend/shell-strategy)、[WebOS 桌面壳架构说明](/frontend/webos-shell-architecture) 和 [公开 SEO 与分享说明](/frontend/public-seo-sharing)。
- `P3-7-A` 商城背包消耗品来源入口已同步到 [当前进行中](/planning/current) 与本周开发日志。
- `P3-7-A` 首轮收尾复核结论已同步：购买、订单通知、订单详情、背包权益与背包消耗品相关商品回流可转入真实使用观察。
- `P3-7-B` WebOS / PC 工作台高信号候选筛查结论已同步到 [当前进行中](/planning/current)、本周开发日志、[记录索引](/records/) 和专题记录。
- `P3-7-C` 主线重评估已同步到 [开发路线图](/development-plan)、[当前进行中](/planning/current)、[第三阶段专题](/planning/phase-three-real-usage-contract-governance) 与 [P3-8 专题](/planning/p3-8-multiplatform-feature-ui-governance)。
- 本次只补相关商品回流契约，不更新 [商城前端设计说明](/guide/shop-frontend)，避免在该超长文档继续追加内容。
- 本次没有改变公开壳层只读边界、SSR / SSG 后置判断、全量 `PublicId` 后置判断或多端路线分工。

## 验证

- `npm run type-check --workspace=radish.client`
- `dotnet test Radish.Api.Tests --filter FullyQualifiedName~ShopProfileTest`
- `git diff --check`
- `http://localhost:3000/desktop` 返回 `200`
- `http://localhost:5100/health` 返回 `200`
- `http://localhost:5100/api/v1/Shop/GetProducts` 返回 `200`
- `http://localhost:5100/api/v1/Shop/GetProduct/2052751277133135872` 返回 `200`
- 本地 `ShopUserInventory` 有 `1` 条有效消耗品库存，且已带 `SourceProductId`
- `node --test --test-isolation=none ./tests/desktopEntryNavigation.test.ts ./tests/desktopRecentApps.test.ts ./tests/workspaceNavigation.test.ts ./tests/forumNavigation.test.ts ./tests/chatNavigation.test.ts ./tests/windowGeometry.test.ts ./tests/desktopExternalUrl.test.ts`

## 明日事项

1. `P3-7-A / P3-7-B` 首轮转观察后，只基于真实使用反馈、运行日志或发布前回归处理新高信号断点。
2. 不扩大到商城重构、运营平台、完整 E2E、SSR / SSG 或全量 `PublicId`。
