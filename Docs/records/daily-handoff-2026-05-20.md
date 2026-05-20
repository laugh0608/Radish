# 2026-05-20 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `19aacca3 fix(client): 修复 Dock 用户信息与帖子头像显示` | 前端 / 后端契约 | Dock 当前用户信息补齐头像与缩略图字段，帖子头像展示与后端 `CurrentUserVo` 保持一致。 |
| `2278998a test(experience): 稳定冻结治理测试时间` | 测试稳定性 | 经验冻结治理测试时间固定化，降低时间漂移导致的偶发失败。 |
| `7f68bf74 docs(planning): 收口 P3-6 公开增长观察` | 规划 / 记录 | 生产公开域名 `https://radishx.com` public head smoke 通过，`P3-6` 阶段收口结论已同步到规划、开发路线和观察记录。 |
| `276274b4 chore(console): bridge admin styles to theme tokens` | Console 样式治理 | `radish.console` 局部样式接入 `--console-*` token 口径，为后续新增 / 改动页面的小范围收敛铺底。 |
| `dcbe5a56 chore(console): remove legacy entry styles` | Console 样式清理 | 删除旧 Vite 模板入口样式与全局污染，保留 Console 必要根样式。 |
| `31bc1f5f fix(client): route order notifications to shop detail` | WebOS / 商城回流 | 订单类通知携带合法业务 ID 时进入商城订单详情，缺失合法 ID 时回落商城入口。 |
| `c6b9af3b fix(client): link order detail back to product` | 商城复访链路 | 订单详情商品信息区新增回商品详情入口，减少从通知 / 订单进入后的上下文断点。 |
| `373f3c34 fix(client): open order detail after purchase` | 商城购买链路 | 购买成功后基于接口返回 `orderId` 进入订单详情，便于确认订单状态和权益 / 道具发放结果。 |
| `80fbe355 fix(shop): expose benefit source navigation` | API 契约 / 商城背包 | `UserBenefitVo` 正式暴露 `VoSourceOrderId / VoSourceProductId`，背包权益可回到来源订单 / 商品。 |

## 文档同步复核

- `P3-6` 公开增长观察收口已同步到 [当前进行中](/planning/current)、[开发路线图](/development-plan)、[第三开发阶段专题](/planning/phase-three-real-usage-contract-governance)、[开发日志](/changelog/2026-05/week4) 与 [P3-6 公开增长部署观察记录](/records/p3-6-public-growth-observation-record-2026-05-20)。
- Console 今日只落地 token bridge 与入口样式清理，符合 [Console UI 一致性评估记录](/records/console-ui-consistency-evaluation-2026-05-18) 的“小范围收敛、不启动整站视觉重构”边界；未改变 `Docs/frontend/visual-theme-spec.md` 或 `Docs/frontend/visual-color-reference.md` 的设计原则。
- 商城复访链路和权益来源契约已同步到 [当前进行中](/planning/current) 与本周开发日志；[商城前端设计说明](/guide/shop-frontend) 当前已超过设计类文档篇幅硬上限，本次不继续追加，后续若要迁入说明书应先拆分该文档。
- 今日没有改变公开壳层只读边界、SSR / SSG 后置判断、全量 `PublicId` 后置判断或多端路线分工，因此不更新公开 SEO 专题、ID 长期路线或壳层策略说明。

## 明日事项

1. 优先做 `P3-7-A` 商城复访链路收尾复核：购买成功、订单通知、订单详情、背包权益来源回流是否还存在高信号断点。
2. 如继续推进，只评审 `UserInventory.SourceProductId` 是否需要正式暴露到消耗品来源入口；注意背包消耗品当前可能聚合同类数量，不能把最后一次来源误写成完整订单来源。
3. 若没有新的 `P0/P1` 或高信号断点，形成 `P3-7-A` 首轮收尾结论，不扩大公开增长、部署、SSR / SSG、完整 E2E、运营平台或全量 `PublicId`。
