# 2026-05-21 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| 本次提交 | API 契约 / 商城背包 | `UserInventoryVo` 最小暴露 `VoSourceProductId`，背包消耗品可回到相关商品；不新增订单来源，不改变消耗品聚合模型。 |

## 文档同步复核

- `P3-7-A` 商城背包消耗品来源入口已同步到 [当前进行中](/planning/current) 与本周开发日志。
- `P3-7-A` 首轮收尾复核结论已同步：购买、订单通知、订单详情、背包权益与背包消耗品相关商品回流可转入真实使用观察。
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

## 明日事项

1. `P3-7-A` 首轮转观察后，只基于真实使用反馈处理购买 / 订单 / 背包 / 通知回流的新高信号断点。
2. 不扩大到商城重构、运营平台、完整 E2E、SSR / SSG 或全量 `PublicId`。
