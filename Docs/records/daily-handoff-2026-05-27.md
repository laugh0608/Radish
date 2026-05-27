# 2026-05-27 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `375ae381 feat(client): 补齐公开榜单与 Gateway 资源收口` | 纯 Web 公开链路 | 公开热门商品榜单可进入公开商品详情并保留榜单来源返回；HTTPS Gateway 下本地 HTTP 媒体、favicon、头像和 Markdown 附件资源会归一到当前 Gateway origin。 |
| `01ab775a feat(client): 优化移动公开阅读与分享配置` | 移动 Web 阅读 | 共享 Markdown、公开 docs / forum 详情补齐窄屏防溢出约束；公开分享链接统一走运行时公开域名配置并保留 docs 锚点。 |
| `e8141eec fix(shop): 补齐商城私有深链登录回流` | 商城登录回流 | `/desktop?app=shop&orderId=...`、`view=orders` 与 `view=inventory` 匿名态会先保存回流路径并登录，登录后回到订单详情、订单列表或背包。 |
| `cce49e0a fix(public): 保留公开详情来源返回链路` | 公开来源返回 | 显式来源返回时保留既有 `history.state` 来源链路，避免详情和 profile 之间形成返回来源循环。 |
| `d6b33207 fix(flutter): 补齐公开主页来源返回` | Flutter 个人链路 | 发现、论坛作者和榜单进入原生公开主页后，Android Back 可回到原来源；公开主页内容详情返回后仍保留来源 tab。 |
| `0a4aedd1 fix(public): 精确商城详情来源返回文案` | 公开商城来源返回 | 商品列表或商品榜进入详情后，返回按钮按来源显示“返回商品列表 / 返回榜单”等精确文案。 |
| `71b88190 build(client): 拆分商城构建 chunk` | 前端构建治理 | `ShopApp` 按页面和购买弹窗懒加载，商城手动 chunk 细分后 `app-shop` 已低于 500k 警告阈值。 |
| `0e7e4e92 chore: 移除仓库 npm 更新提示配置` | 配置回滚 | 移除仓库级 `.npmrc`，不把 npm 自身 update notice 作为仓库配置治理项。 |
| `4ab6d242 fix(public): 统一发现页帖子公开路径` | PublicId 口径 | `/discover` 论坛卡片进入公开帖子详情时优先使用 `Post.PublicId`，与 forum 列表 / 搜索 / 标签页 URL 口径一致。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已补今日纯 Web / Flutter 关键结论、商城 chunk warning 治理事实，并把明日建议收敛到 Flutter 公开个人页到内容详情主路径。
- 已同步专题规划：[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 已补公开来源返回、发现页 PublicId、商城构建 chunk 治理和明日 Flutter 个人链路建议。
- 已同步说明书：[Flutter README](../../Clients/radish.flutter/README.md) 已补公开主页来源返回能力；[前端构建拆包策略](/frontend/build-chunking) 已补商城工作台页级懒加载和 `app-shop` warning 收口事实。
- 已同步验收记录：[P3-8-D 移动 Web 公开视图验收矩阵](/records/p3-8-d-mobile-web-public-view-acceptance-matrix-2026-05-25) 已补 2026-05-27 二轮静态复核。
- 已同步开发日志：本记录、[2026 年 5 月第 5 周开发日志](/changelog/2026-05/week5) 与 [2026 年 5 月开发日志](/changelog/2026-05) 已补今日汇总。
- 今日没有新增后端 API、数据库结构、权限模型、视觉 token、Pencil 设计稿或部署配置；API 契约、视觉规范、部署说明和设计源文件无需跟随更新。
- 今日仍未启动完整移动商城、完整通知中心、完整创作器、完整评论 / 发帖 / 点赞 / 投票能力；相关边界保持不变。

## 今日验证

- `npm run test --workspace=radish.client -- --test-name-pattern="publicRoute|publicHead|publicStructuredData"`。
- `npm run test --workspace=radish.client -- --test-name-pattern=normalizeBrowserVisibleUrl`。
- `npm run test --workspace=radish.client -- --test-name-pattern=ShopDetailSource`。
- `npm run test --workspace=radish.client -- --test-name-pattern=publicRouteNavigation`。
- `npm run test --workspace=radish.client -- --test-name-pattern="publicRouteNavigation|forumNavigation|publicHead"`。
- `npm run test --workspace=radish.client -- --test-name-pattern="公开商城详情购买回流入口|desktop.*商城|authReturnPath"`。
- `npm run type-check --workspace=radish.client`。
- `npm run type-check --workspace=@radish/ui`。
- `npm run lint:changed`。
- `npm run build --workspace=radish.client`。
- `flutter test test/smoke_test.dart`。
- `flutter test`。
- `flutter analyze`。
- `npm run validate:baseline:quick`。
- `npm run check:repo-hygiene:changed`。
- `npm run check:repo-hygiene:staged`。
- `git diff --check`。
- `git diff --cached --check`。

说明：本轮没有启动 `dotnet run`、`npm run dev` 或安装依赖。`localhost:3000` 与 `localhost:5000` 晚间复核时均未监听，因此最后一轮没有做浏览器实测。npm 自身 update notice 仍可能出现，但不进入仓库配置治理。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 和本记录，继续走 `P3-8-D`，不要回到 WebOS 新功能或 Console 剩余页面微调。
2. 明日优先转向 Flutter 个人链路：复核 `公开个人页 -> 帖子 / 评论详情 -> Android Back 回到原 profile 来源`，覆盖从发现、论坛作者和榜单进入公开主页后的帖子 / 评论详情返回。
3. 若已有能力完整，只补验收结论和必要说明；不要为了纯 Web 矩阵继续凑低收益小闭环。
4. 若本地服务或 Android 环境未就绪，先做 Flutter 代码 / 测试层静态复核，不由 AI 直接执行 `dotnet run`、`npm run dev` 或安装依赖。
5. 暂不启动完整移动商城、完整通知中心、完整创作器、完整评论 / 发帖 / 点赞 / 投票能力，也不把 WebOS 当作新增功能承载方向。
6. 明日若进入 Flutter 实现，至少跑命中测试、`flutter analyze`，涉及壳层来源或 Android Back 再补 `flutter test`；若命中纯 Web 路由，继续按 `radish.client` 定向测试、类型检查、构建和文本卫生分层验证。
