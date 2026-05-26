# 2026-05-26 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `e217090c fix(client): 持久化公开详情来源返回状态` | 纯 Web 来源返回 | 公开 forum / docs / profile / shop 详情来源返回状态写入 `history.state`，刷新或浏览器历史恢复后仍保留来源返回语义，且不污染公开分享 URL。 |
| `6887b633 fix(client): 修正公开商城购买回流入口` | 纯 Web 商城回流 | 公开商品详情只读购买提示入口从 `/` 修正为 `/desktop`，避免根路径切向 `/discover` 后购买 / 订单 / 背包回流误入公开发现页。 |
| `e0aeeab2 fix(client): 桥接公开商品到工作台详情` | WebOS 保留入口桥接 | 公开商品详情工作台入口改为 `/desktop?app=shop&productId=...`，桌面壳层解析 `shop` 商品深链并打开对应商品详情。 |
| `f8f91942 fix(client): 承接商城订单与背包深链` | WebOS 保留入口承接 | 桌面壳层支持 `shop` 的 `orderId`、`view=orders` 与 `view=inventory` 深链，订单和背包入口保持登录后消费，避免未登录时误显空订单或空背包。 |
| `9c93b497 feat(client): 完善商城登录后购买回流` | 纯 Web 登录回流 | 未登录用户从公开商品页进入工作台商品详情后，可保存商品上下文登录，并在 OIDC 回调后恢复原商品详情继续购买。 |
| `2194ce66 feat(flutter): 串联榜单公开主页回流` | Flutter 公开主页 | 经验榜条目可打开原生公开主页，并通过 Android Back 回到榜单。 |
| `16a17203 feat(flutter): 保留通知回流来源位置` | Flutter 通知回流 | 最新论坛通知打开原生帖子详情时保留打开通知前的 tab，详情返回后回到原位置。 |
| `67ca084a feat(flutter): 补齐轻回应登录回流` | Flutter 轻回应 | 从 forum detail 轻回应区发起登录后，会回到当前帖子轻回应区并提示可继续发布。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已记录今日 8 个 `P3-8-D` 小闭环，并继续把当前主线限定为纯 Web + Flutter，`/desktop` 只作为 WebOS 保留入口。
- 已同步专题规划：[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 已补公开详情来源返回、公开商城购买回流、商品 / 订单 / 背包深链、纯 Web 登录后购买回流、Flutter 榜单公开主页、通知回流和轻回应登录回流。
- 已同步 Flutter 说明书：`Clients/radish.flutter/README.md` 已记录榜单公开主页回流、通知来源回流和轻回应登录后回到 composer 的当前能力与不做范围。
- 已同步开发日志：本记录、[2026 年 5 月第 5 周开发日志](/changelog/2026-05/week5) 与 [2026 年 5 月开发日志](/changelog/2026-05) 已补今日汇总。
- 今日改动没有新增后端 API、数据库结构、权限模型、视觉设计稿或共享 UI token；`Docs/frontend/design.md`、视觉规范、部署说明、API 契约说明和 Pencil 设计源文件无需跟随更新。
- 今日仍未启动完整移动商城、完整通知中心、完整创作器、完整评论 / 发帖 / 点赞 / 投票能力；相关说明书边界保持不变。

## 今日验证

- `npm run test --workspace=radish.client -- --test-name-pattern="publicRoute|shop"`。
- `npm run type-check --workspace=radish.client`。
- `npm run build --workspace=radish.client`。
- `flutter test test/leaderboard_page_test.dart`。
- `flutter test test/smoke_test.dart`。
- `flutter test test/forum_detail_page_test.dart`。
- `flutter analyze`。
- `flutter test`。
- `git diff --check`。

说明：`radish.client` 构建仍保留既有 `app-shop` chunk size warning；今日未处理该历史拆包项。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 和本记录，确认继续走 `P3-8-D`，不要回到 WebOS 新功能或 Console 剩余页面微调。
2. 优先做一次今日链路的批量验收复核：公开 shop 商品页来源返回、`/desktop?app=shop&productId=...`、`orderId`、`view=orders`、`view=inventory`、登录后恢复商品上下文，以及 Flutter 榜单 / 通知 / 轻回应登录回流的关键路径。
3. 若本地服务已由人工启动，优先通过 Gateway `https://localhost:5000` 做浏览器主路径复核；不要由 AI 直接执行 `dotnet run` 或 `npm run dev`。
4. 若复核未发现新的 `P0/P1`，下一批只选一个一天级小闭环：优先从纯 Web 移动阅读 / 来源返回细节、公开商城登录后订单 / 背包轻入口验收补洞，或 Flutter 个人链路轻复访中择一。
5. 暂不启动完整移动商城、完整通知中心、完整创作器、公开 Web 整体 UI 重构、完整评论 / 发帖 / 点赞 / 投票能力，也不把 WebOS 当作新增功能承载方向。
6. 明日若进入代码实现，按命中端选择精准验证：`radish.client` 至少跑类型检查、命中路径测试、生产构建和 `git diff --check`；Flutter 至少跑命中测试、`flutter analyze`，涉及壳层回流再跑 `flutter test`。
