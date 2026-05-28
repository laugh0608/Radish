# 2026-05-28 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `a60b7992 feat(frontend): 完善论坛登录回流链路` | 纯 Web / 工作台 | 桌面入口支持 forum 深链，论坛发帖、评论和轻回应登录后可回到原 forum 上下文。 |
| `11f266a5 feat(frontend): 完善 Dock 登录回流` | 工作台登录 | Dock 主动登录会保存当前合法 `/desktop...` 路径，避免 forum / shop 场景落回 `/discover`。 |
| `46cc5428 refactor(frontend): 收口公开商城工作台入口` | 公开商城契约 | 公开商品详情工作台入口统一使用 `/desktop?app=shop&productId=...`，非法商品详情路径回落商城首页。 |
| `d97880f4 fix(public): 完善移动公开阅读链路` | 移动 Web 阅读 | 公开 docs / forum 与共享 Markdown 补齐窄屏长内容约束、公开分享域名和 docs 链接阅读口径。 |
| `d9ce2033 fix(client): 收口移动 Web 公开视图矩阵` | 移动 Web 矩阵 | `/discover / forum / docs / shop / leaderboard / u/:id` 公开视图阶段收口，发现页 forum 公开路径改为 PublicId 优先。 |
| `ceadf8bb feat(flutter): 补公开商品只读详情` | Flutter 商品 | 发现页商城精选商品可打开原生只读商品详情并返回发现页。 |
| `7174e120 docs(planning): 评估 Flutter 下一批主路径` | 规划记录 | 建立 Flutter 下一批候选矩阵，选出轻量 forum 通知列表为当日后续实现方向。 |
| `4ff9ac60 feat(flutter): 补轻量论坛通知列表` | Flutter 通知 | 已登录壳层展示最近少量可跳 forum 通知，选择后打开帖子 / 评论并返回打开前 tab。 |
| `8ec25d76 feat(flutter): 补公开商城列表入口` | Flutter 商城 | 发现页可进入公开商城列表，列表项打开只读商品详情并返回商城列表。 |
| `28316d76 feat(flutter): 补论坛详情评论发布与回复` | Flutter forum | 已登录用户可在原生帖子详情发布根评论、回复根评论或子评论，提交后局部更新评论区。 |
| `aec839f2 test(flutter): 补论坛评论回流验收` | Flutter 回归 | 匿名评论登录回流与公开主页评论上下文来源返回均有自动化覆盖。 |
| `1aa831de feat(flutter): 补齐公开详情链接复制入口` | Flutter 分享 | forum/docs/shop 原生详情页展示完整公开链接并支持复制，复制口径为 Gateway Base URL 加 Web 公开路由。 |
| `c0cebe3f feat(flutter): 补强公开文档内链阅读` | Flutter docs | 只读 Markdown 阅读器识别 Web 公开路由、完整公开 URL 和相对 docs 链接并打开原生 docs detail。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 与 [开发路线图](/development-plan) 已补今日 Flutter 商品、通知、商城列表、评论、公开详情分享和 docs 阅读链路进展，并把下一建议转向 Flutter 原生公开主页链接 / 展示 / 来源返回复核。
- 已同步专题规划：[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 已补原生公开详情分享入口、docs 文档内链阅读和明日建议。
- 已同步说明书：[Flutter README](../../Clients/radish.flutter/README.md) 已补原生公开详情链接复制、docs 多形态文档内链解析和 Android 人工验证 checklist。
- 已同步开发日志：[2026 年 5 月第 5 周开发日志](/changelog/2026-05/week5) 与 [2026 年 5 月开发日志](/changelog/2026-05) 已补今日汇总。
- 已同步记录索引：本记录已加入 [记录与验收索引](/records/)。
- 今日没有新增后端 API、数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量；对应架构说明、视觉规范、Pencil 设计源文件和部署说明无需跟随更新。

## 今日验证

- `cd Clients/radish.flutter && flutter test`
- `cd Clients/radish.flutter && flutter analyze`
- `flutter test test/docs_page_test.dart`
- `flutter test test/forum_detail_page_test.dart`
- `flutter test test/shop_product_detail_page_test.dart`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

说明：今天没有启动 `dotnet run`、`npm run dev` 或安装依赖；Flutter 本轮仍不扩展系统分享 SDK、完整移动商城、完整通知中心、完整创作器、发帖编辑器、点赞、投票、审核治理或 WebOS 新功能。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 和本记录，继续走 `P3-8-D`。
2. 明日优先推进 Flutter 原生公开主页链接复制 / 展示复核：覆盖从发现、论坛作者、榜单进入公开主页后的公开主页链接展示、复制、长用户名 / 长 userId / 长个人简介防溢出和 Android Back 来源返回。
3. 同步检查匿名 / 已登录边界：公开主页仍保持只读，不加入关注 / 取关、编辑资料、私信、完整创作器或 WebOS 新功能。
4. 若发现公开主页已有能力完整，只补验收结论和必要说明；不要回到移动 Web 公开页逐页打磨。
5. 若进入 Flutter 实现，至少运行命中测试、`flutter analyze`；涉及壳层来源或 Android Back 时运行全量 `flutter test`。
