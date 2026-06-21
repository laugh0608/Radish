# P3-12-C1 WebOS 残留入口清理记录

> 日期：2026-06-21（Asia/Shanghai）
>
> 状态：首轮代码侧扫描与文案 / 文档口径修正已完成；真实 Gateway PC / mobile 复核后置到 B1 + C1 小阶段验收

## 范围

本轮只处理与 `P3-12-B1` 账户资产与商城交易直接相关的残留：

- 默认产品路径仍误回 `/desktop` 的链接、登录回流和 route helper 假设。
- 购买、订单、库存、资产流水相关的公开文案和文档节奏口径。
- 与正式 Web 交易路径相关的静态契约测试。

本轮不处理：

- WebOS Dock、窗口系统、桌面背景、窗口几何记忆和 app 外壳。
- WebOS `ShopApp`、`radish-pit`、`buildDesktopShop*ReturnPath`、`desktopEntryNavigation` 的历史深链维护线。
- 页面级 UI 设计、跨页面视觉重塑、Flutter、PR、发布或无关 WebOS 清扫。

## 扫描结论

- 正式 Web 交易主路径未发现仍把 `/me` 完整钱包、公开商品购买、订单通知、订单详情或库存默认导向 `/desktop` 的源码命中。
- `buildDesktopShop*ReturnPath` 的源码调用集中在 WebOS `ShopApp` / `ProductDetail` 和对应历史深链测试，继续作为 `/desktop` 维护线保留。
- `/me` 最近访问已通过真实公开 href 返回 `/forum/post/*`、`/docs/*`、`/shop/product/*`、`/u/*`，未复用 WebOS 工作区 opener。
- 公开商品榜单 / 发现页榜单文案仍存在“只读、不带购买”的旧口径，容易和 B1 后“商品详情可登录购买”冲突；首轮已改为“榜单只读、购买从商品详情登录后继续、订单 / 背包留在私域 Web 路由”。
- 第二轮继续收口公开商品推荐语义：删除未使用的旧购买按钮翻译 key，把商品浏览的壳层式旧说法改为“public shop / 公开商城”，避免公开商城购买路径继续带出 WebOS 壳层暗示。
- `Docs/planning/current.md`、`Docs/planning/p3-12-web-completion-webos-retirement.md` 和 B1 方案记录已改为 C1 代码侧残留清理先行，真实 Gateway PC / mobile 复核放到 B1 + C1 小阶段准备验收时集中执行。

## 已验证

- `node --test --test-isolation=none ./tests/publicSeoStatic.test.ts ./tests/realUsagePathContracts.test.ts ./tests/authReturnPath.test.ts`：45 个测试通过。
- `npm run type-check --workspace=radish.client`：通过。
- `git diff --check`：通过。

第二轮补充验证：

- `node --test --test-isolation=none ./tests/publicSeoStatic.test.ts ./tests/realUsagePathContracts.test.ts ./tests/authReturnPath.test.ts`：45 个测试通过。
- `npm run type-check --workspace=radish.client`：通过。
- `git diff --check`：通过。

未执行：

- 未启动 API / Auth / Gateway / Vite。
- 未做真实页面 smoke；后续必须先等待用户明确确认前后端已启动。

## 下一步

1. 继续只针对 B1 交易链路做 C1 残留扫描；若没有新的默认路径冲突，不扩展到 WebOS 全量清扫。
2. B1 + C1 小阶段准备验收时，再覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/product/:id?intent=purchase`、`/shop/orders`、`/shop/order/:id`、`/shop/inventory` 和购买成功回流的 Gateway PC / mobile 复核。
3. 页面迁移齐后再进入 `P3-12-D` 统一 UI 设计与美化专题。
