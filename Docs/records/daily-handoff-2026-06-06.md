# 2026-06-06 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-06 00:00"` 在收工文档提交前回顾到今日 7 个功能 / 治理提交。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `0ea2070c docs: 添加称呼协作约定` | 协作规则 | 同步 Agent 协作文件，约定对话结束总结时称呼用户为 `萝卜`。 |
| `a33d315e fix(flutter): 收口发帖登录回流草稿恢复` | Flutter / 发帖登录回流 | 匿名态从发帖表单提交后保留页面存活期间的草稿，登录回调后回到发帖表单继续发布。 |
| `05cbe986 fix(console): 保留订单排障来源回流` | Console / 订单排障 | 订单 URL 状态构造抽出为 helper，详情关闭、分页、筛选和重置保留合法 `returnTo`，LongId 查询参数继续保持字符串。 |
| `b740e5e2 fix(console): 守护角色授权重复保存` | Console / 权限授权 | 角色授权保存中禁用保存按钮并阻止重复提交，避免旧 `expectedModifyTime` 触发伪并发冲突。 |
| `d5d31adf fix(client): 对齐公开帖子详情预览 canonical` | 公开 Web / 分享预览 | 旧 LongId forum 详情加载到 `VoPublicId` 后，canonical、OpenGraph 与 JSON-LD 刷新到同一个 PublicId 路径。 |
| `f66228d1 fix(flutter): 收敛背包来源回流 LongId 口径` | Flutter / 背包来源 | 背包来源订单 / 商品 ID 按规范字符串 LongId 承接，不再通过 `int.tryParse` 数值化，并补非规范来源 ID 守护。 |
| `7aeadb3e fix(console): 保留商品订单排障回流上下文` | Console / 商品订单排障 | 商品详情进入相关订单时携带当前商品详情作为 `returnTo`，并保留原订单来源上下文。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已把复核日期更新到 `2026-06-06`，并把明日第一顺位写成购买 / 订单 / 背包跨端回流的可验收批次。
- 已同步专题规划：[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 已补今天完成的 Flutter 发帖登录回流、公开 forum canonical、Flutter 背包来源 LongId、Console 商品 / 订单排障和角色授权重复保存结论。
- 已同步说明书：[Flutter 移动端 handoff 与回流说明](/guide/flutter-mobile-handoff)、[Console 核心概念](/guide/console-core-concepts)、[公开内容 SEO 与分享基线](/frontend/public-seo-sharing) 与 [ID 与联邦路线图](/architecture/id-and-federation-roadmap) 已补相关长期契约。
- 已同步开发日志：[2026 年 6 月开发日志](/changelog/2026-06) 与 [2026 年 6 月第 1 周开发日志](/changelog/2026-06/week1) 已补 2026-06-06 日结。
- 已同步记录索引：本记录已加入 [记录与验收索引](/records/)。
- 今日没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置、运行时环境变量或新的后端接口；视觉规范、设计源文件、部署说明和 API 说明书无需跟随更新。

## 今日验证

- Flutter：
  - `flutter test test/forum_page_test.dart`
  - `flutter test test/shop_product_detail_page_test.dart`
  - `flutter analyze`
- Console：
  - `npm run test --workspace=radish.console`
  - `npm run type-check --workspace=radish.console`
  - `npm run build --workspace=radish.console`
- Client / 公开 head：
  - `radish.client` 定向测试与 type-check
  - public head smoke self-test
- 仓库级：
  - `npm run validate:identity`
  - `npm run check:repo-hygiene:changed`

说明：`validate:identity` 仍有项目既有 XML 注释 warning。今天没有启动 `dotnet run`、`npm run dev`，也没有安装依赖。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 和本记录。
2. 第一件事围绕购买 / 订单 / 背包跨端回流做一天级可验收批次：复核同一笔购买在 Flutter 订单详情 / 背包来源、纯 Web / `/desktop` 商品购买入口、Console 商品 / 订单 / 胡萝卜流水排障之间的返回、确认、筛选上下文和 LongId 字符串契约。
3. 若购买链路暂未暴露新缺口，再回拉权限授权或公开访问中的高信号问题，优先处理重复提交、来源返回、head / canonical、`check-long-id-safety` 或真实编译暴露的新命中。
4. 继续维护 `ID Phase A` 自动化守护；新增外部对象 ID 边界时只做字符串安全、同源相对路径和 PublicId 试点口径的定向治理。
5. 不启动完整移动商城、完整通知中心、完整资产中心、完整创作器、权益使用、退款、完整 `PublicId` 全量迁移、数据库主键迁移、联邦预研或 WebOS 新功能。
