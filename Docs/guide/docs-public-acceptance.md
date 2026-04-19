# docs 公开阅读首批人工验收清单

> 适用范围：`Phase 2-2` 下 docs 公开阅读首批与公开内容壳层中的 `/docs`、`/docs/search`、`/docs/:slug` 直达入口。
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [文档系统](/guide/document-system)
> - [人工验收模板](/guide/manual-acceptance-template)

## 1. 适用改动范围

- `Frontend/radish.client/src/public/docs/` 下公开 docs 路由、状态同步或内容展示逻辑改动后
- `/docs`、`/docs/search`、`/docs/:slug` 的公开壳层布局、目录浏览、关键词搜索、正文阅读、分享入口或返回链路改动后
- docs 公开正文内链改写、旧 `__documents__` 兼容路径、canonical slug 收口或只读边界改动后

## 2. 前置条件

- 已完成前端最小自动化验证：
  - `npm run type-check --workspace=radish.client`
  - `npm run build --workspace=radish.client`
- 已启动本地前端与相关宿主，能在浏览器直接访问：
  - `/docs`
  - `/docs/search`
  - `/docs/:slug`
  - `/discover`
- 浏览器优先覆盖 `390 x 844` 与 `430 x 932` 两档窄屏视口
- 准备至少以下样例数据：
  - 一篇可公开阅读的文档
  - 一条可命中搜索结果的关键词
  - 一篇正文中包含 docs 内链或标题锚点的文档
- 若本轮触达 slug 兼容或 canonical 收口，建议额外准备：
  - 一条旧 `__documents__` 文档链接样例
  - 一条非 canonical slug 或保留字冲突样例

## 3. 联调资产

- 公开壳层入口：`Frontend/radish.client/src/public/PublicEntry.tsx`
- 公开 docs 路由：`Frontend/radish.client/src/public/docsRouteState.ts`
- 公开 docs 页面：`Frontend/radish.client/src/public/docs/PublicDocsApp.tsx`
- 公开 docs 读取：`Frontend/radish.client/src/public/docs/publicDocsApi.ts`
- 公开社区分发页：`Frontend/radish.client/src/public/discover/PublicDiscoverApp.tsx`

说明：

- 本清单以公开壳层 UI / 路由 / 匿名读取边界人工验收为主，不要求额外新增 `HttpTest`
- 若本轮同时修改桌面文档治理、Wiki 管理接口或在线文档编辑能力，再并行补：
  - `Radish.Api.Wiki.http`
  - [文档系统](/guide/document-system)

## 4. 最小人工验收顺序

1. 直接打开 `/docs`，确认页面进入公开内容壳层，而不是先进入桌面 `Shell`。
2. 在窄屏下检查首屏，确认品牌头部、只读说明、目录区、文档卡片、“搜索文档”入口与“社区发现”轻入口没有重叠、截断或横向撑破。
3. 从目录页点击一篇文档进入 `/docs/:slug`，确认详情页只展示正文、元信息与只读动作；再点击返回按钮，确认会回到原目录浏览态，且目录滚动位置不会异常丢失。
4. 直接打开 `/docs/search`，先检查页首公开搜索提示区，确认会明确表达“先看结果 / 继续进入 / 不在这里”三层信息；再输入关键词并翻到第 `2` 页及之后任意一页，确认 `q / page` 会稳定回写到 URL，刷新页面后仍能恢复当前搜索上下文。
5. 从 `/docs/search` 进入 `/docs/:slug` 后点击返回按钮，确认会优先回到原搜索结果，而不是退回默认目录；若详情页带来源进入，返回文案应按真实来源显示为“返回搜索结果”或等价提示。
6. 从 `/discover` 的 docs 推荐区进入任一 `/docs/:slug` 后点击返回按钮，确认会优先回到 `/discover`，而不是退回 docs 默认目录或搜索页；此时返回文案应显示为“返回社区发现”。
7. 直接打开一条旧 `__documents__` 链接、非 canonical slug 链接或其他兼容路径，确认页面会继续落在公开 docs 壳层；普通文档会收口到 canonical 公共路径，保留字冲突文档也不会误落到 `/docs/search`。
8. 在详情页点击“复制链接”入口，确认复制结果是当前公开文档的 canonical 公共链接，而不是桌面窗口路由、内部 API 地址或兼容重写前的旧地址；若当前由兼容路径打开，也会先完成收口再复制。
9. 人工检查分享反馈，确认复制成功与失败时都有轻量提示，且不会打断当前阅读或破坏返回上下文。
10. 在正文中点击 docs 内链、旧 `__documents__` 链接或标题锚点，确认跳转后仍停留在公开 docs 壳层；若是锚点跳转，地址栏 hash 与正文定位保持一致。
11. 在未登录状态直接刷新 `/docs`、`/docs/search` 与 `/docs/:slug`，确认匿名公开读取仍可用，不会因为认证初始化或 token 刷新噪音而中断阅读。
12. 人工抽查一个不存在或不可公开访问的 `slug`，确认公开壳层会给出只读状态页，而不是静默退回目录、跳回桌面或空白页。

## 5. 预期结果

- `/docs`、`/docs/search` 与 `/docs/:slug` 以公开内容壳层直达，不绕回桌面入口。
- 公开 docs 在手机上保持“目录可浏览、搜索可回跳、正文可持续阅读”的节奏。
- `/docs/search` 当前会明确给出只读搜索导览，说明搜索重点、下一步去向与桌面治理边界，而不是只剩输入框与结果列表。
- 搜索与分页状态可被 URL 保存，刷新与“详情 -> 返回搜索”后不会丢失上下文。
- 从 `/discover` 或搜索结果进入详情后，返回动作会优先回到原公开来源，而不是一律退回默认目录。
- 文档详情返回按钮会按真实来源显示为“返回搜索结果”“返回社区发现”或“返回目录”等具体提示，而不是泛化成“返回上一入口”。
- 旧 `__documents__` 链接、canonical slug 收口与保留字兼容路径保持一致，不会把公开文档误导到错误路由。
- 分享入口继续保持最小只读能力：只复制公开 canonical 链接，不扩展海报、社交卡片、分享统计或其他治理语义。
- 正文内链与锚点跳转继续留在公开 docs 壳层，不回退桌面文档应用。
- 公开 docs 继续保持只读边界，不误带入编辑、发布、回收站、版本历史或其他桌面治理能力。

## 6. 可跳过项

- 若本轮只改 changelog、规划或纯文档，可跳过整份清单。
- 若本轮只改桌面 `wiki` 应用，且未触达 `src/public/docs/`、公开 docs 路由或公开读取链路，可跳过本清单。
- 若本轮只改公开 docs 文案或纯样式，且未影响目录 / 搜索 / 详情直链 / 分享 / 内链跳转，可只抽查 `/docs`、`/docs/search` 和 1 条 `/docs/:slug`。

## 7. 结论记录格式

```md
- 验收日期：2026-04-15
- 验收人：<name>
- 验收范围：docs 公开阅读首批
- 执行入口：/docs、/docs/search、/docs/:slug、/discover
- 结果：通过 / 阻塞
- 备注：<如有问题，记录 slug、搜索词、来源入口、视口尺寸与复现步骤>
```
