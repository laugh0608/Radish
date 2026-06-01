# 2026-06-01 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `1ccbc6f1 feat(flutter): add recent visit history` | Flutter 复访 | 登录态“我的”页可打开最近访问，承接公开帖子 / 文档 / 商品详情并覆盖加载、刷新、加载更多和返回状态。 |
| `3f293d5d feat(flutter): mark notifications read` | Flutter 通知 | 通知列表支持单条显式标记已读，复用现有通知接口，不扩展批量已读、删除或推送。 |
| `06d59290 feat(flutter): add text post composer` | Flutter 论坛 | 登录态用户可发布纯文本帖子，成功后刷新列表并打开新帖子详情。 |
| `b14d11e8 feat(flutter): add single product purchase` | Flutter 商城 | 登录态用户可从原生商品详情购买 `1` 件商品，成功后进入订单详情确认结果；匿名态可登录回流到当前商品。 |
| `805d6f40 fix(coins): align registration rewards and balance adjustment` | 胡萝卜资产 | 注册、登录补偿和首个管理员初始化统一走默认奖励契约；余额查询、发放、扣除和管理员调账前校验真实用户。 |
| `c17b0b21 fix(tests): restore public head and purchase validation contracts` | 回归修复 | 修复公开 head 资源路径测试与商城购买校验返回契约，恢复后端全量测试。 |
| `45743785 fix(console): preserve user ids as strings` | ID Phase A | Console 当前用户、个人资料和用户列表中的 `VoUserId / uuid` 保持字符串，避免 JavaScript 大整数精度丢失。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已补 Flutter 单商品购买后续复核、胡萝卜资产治理、Console 用户 ID 字符串安全和明日事项。
- 已同步专题规划：[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 已补受控写入完成范围、购买真实数据复核和 `ID Phase A` 下一步建议。
- 已同步设计 / 架构说明：[ID 与联邦路线图](/architecture/id-and-federation-roadmap) 已补 Console 用户 ID 入口回拉事实和后续自动化检查方向。
- 已同步开发日志：[2026 年 6 月开发日志](/changelog/2026-06) 与 [2026 年 6 月第 1 周开发日志](/changelog/2026-06/week1) 已建立并记录今日提交。
- 已同步记录索引：本记录已加入 [记录与验收索引](/records/)。
- 今日没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量；对应部署说明、视觉规范和设计源文件无需跟随更新。

## 今日验证

- Flutter 单商品购买批次：`flutter test test/shop_product_detail_page_test.dart test/shop_product_list_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、`flutter test`、`git diff --check`
- 胡萝卜资产 / 购买契约批次：后端定向 `dotnet test`、`dotnet build Radish.slnx -c Debug`、后续 `dotnet test Radish.Api.Tests`
- Console 用户 ID 字符串安全批次：`npm run type-check --workspace=radish.console`、`npm run test --workspace=radish.console`、`npm run build --workspace=radish.console`

说明：今天没有启动 `dotnet run`、`npm run dev` 或安装依赖；本地 SQLite 曾按用户确认清理错误 rounded UserId 的孤儿余额记录，并已保留清理前备份。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)、[ID 与联邦路线图](/architecture/id-and-federation-roadmap) 和本记录。
2. 第一件事做 Flutter 单商品购买真实数据复核：用真实登录用户、正确 UserId 和胡萝卜余额数据，复核商品详情购买、登录回流、余额显示、订单详情、失败提示和返回状态。
3. 若复核发现购买 / 资产链路仍有阻断，优先修该链路；若复核通过，推进 `ID Phase A` 外部 LongId 字符串安全自动化守护，优先覆盖 Console / Flutter / Web 的 `Number(...Id)`、`voUserId: number`、`uuid: number` 等回潮风险。
4. 不启动完整 `PublicId` 全量迁移、数据库主键迁移、联邦预研、完整移动商城、完整通知中心、完整创作器、退款、权益使用或 WebOS 新功能。
