# 公开社区分发页首批人工验收清单

> 适用范围：`Phase 2-2` 下公开社区分发页首批与公开内容壳层中的 `/discover` 直达入口。
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [前端设计文档](/frontend/design)
> - [人工验收模板](/guide/manual-acceptance-template)

## 1. 适用改动范围

- `Frontend/radish.client/src/public/discover/` 下公开社区分发页路由、状态同步或内容展示逻辑改动后
- `/discover` 的公开壳层布局、首屏分发结构、入口跳转或只读边界改动后
- 公开社区分发页所依赖的 forum / docs / leaderboard / shop 聚合读取逻辑改动后

## 2. 前置条件

- 已完成前端最小自动化验证：
  - `npm run type-check --workspace=radish.client`
  - `npm run build --workspace=radish.client`
- 已启动本地前端与相关宿主，能在浏览器直接访问：
  - `/discover`
  - `/forum`
  - `/docs`
  - `/leaderboard`
  - `/u/:id`
  - `/shop`
- 浏览器优先覆盖 `390 x 844` 与 `430 x 932` 两档窄屏视口
- 环境中至少存在：
  - 公开帖子数据
  - 公开文档数据
  - 至少一种可公开浏览的榜单
  - 至少一条可公开浏览的商品数据

## 3. 联调资产

- 公开壳层入口：`Frontend/radish.client/src/public/PublicEntry.tsx`
- 公开社区分发页路由：`Frontend/radish.client/src/public/discoverRouteState.ts`
- 公开社区分发页页面：`Frontend/radish.client/src/public/discover/PublicDiscoverApp.tsx`
- forum 公开读取：`Frontend/radish.client/src/api/forum.ts`
- docs 公开读取：`Frontend/radish.client/src/public/docs/publicDocsApi.ts`
- leaderboard 公开入口：`Frontend/radish.client/src/public/leaderboard/PublicLeaderboardApp.tsx`
- profile 公开入口：`Frontend/radish.client/src/public/profile/PublicProfileApp.tsx`
- shop 公开入口：`Frontend/radish.client/src/public/shop/PublicShopApp.tsx`

说明：

- 本清单以公开壳层 UI / 路由 / 分发节奏人工验收为主，不要求额外新增 `HttpTest`
- 若本轮同时修改了 forum / docs / leaderboard / shop 各自公开入口，再并行补对应专题验收清单

## 4. 最小人工验收顺序

1. 直接打开 `/discover`，确认页面进入公开内容壳层，而不是先进入桌面 `Shell`。
2. 在窄屏下检查首屏，确认标题、只读标识、入口按钮与统计卡片没有重叠、截断或横向撑破。
3. 检查首屏 forum 推荐区，确认能看到最新帖子或状态卡；点击任一帖子后应进入 `/forum/post/:postId`，而不是打开桌面窗口。
4. 点击推荐区里的热门标签，确认会进入 `/forum/tag/:tagSlug`，并继续保持公开只读阅读边界。
5. 检查 docs 推荐区，确认能打开 `/docs/:slug`；再点击详情页返回按钮，确认能稳定回到 `/discover`。
6. 点击榜单快捷入口，确认会进入 `/leaderboard` 或 `/leaderboard/:type`，而不是绕回桌面工作台。
7. 从榜单入口继续点击任一用户榜单项进入 `/u/:id`，再执行返回，确认会优先回到原榜单与 `/discover` 来源链路，而不是异常跳桌面或丢失公开上下文。
8. 点击商城推荐区任一商品，确认会进入 `/shop/product/:productId`，并继续保持只读商品阅读，不误带入购买、订单或背包动作；再点击详情页返回按钮，确认会优先回到 `/discover`，而不是丢失到商城默认列表。
9. 人工抽查任一“查看全部”按钮，确认 forum / docs / leaderboard / shop 都仍然落在 Public Content Shell，不跳桌面、不带多窗口语义。
10. 从 `/forum`、`/docs`、`/leaderboard`、`/shop` 任一公开页顶部点击“社区发现”，确认能稳定回到 `/discover`，且不触发整页跳桌面或异常丢失公开壳层样式。
11. 从 `/discover` 进入 forum 帖子详情或 docs 详情后，确认详情页返回动作会优先回到 `/discover`，而不是回退成专题默认列表。

## 5. 预期结果

- `/discover` 以公开内容壳层直达，不绕回桌面入口。
- 首屏分发结构能够自然串起 forum / docs / leaderboard / shop 四类公开内容入口。
- 每个区块都继续保持只读边界，不误带入登录后重交互或工作台动作。
- 公开分发页与各公开入口之间的前进 / 回退关系稳定，不出现异常跳桌面或上下文丢失。
- forum / docs / leaderboard / shop 公开页顶部都存在统一的“社区发现”轻入口，可把用户重新收口回 `/discover`。
- 从 `/discover` 进入榜单，再跳到 `/u/:id` 时，返回链路仍会优先保留原公开来源，不会在榜单或个人页之间丢失上下文。
- 从 `/discover` 进入 forum / docs / shop 详情时，返回动作会优先回到 `/discover`，不会退回专题默认列表。
- 移动端用户能先进入“内容分发页”，而不是被迫从桌面工作台语义进入社区。

## 6. 可跳过项

- 若本轮只改 changelog、规划或纯文档，可跳过整份清单。
- 若本轮只改 forum / docs / leaderboard / shop 的单一专题页，且未触达 `/discover`、`PublicEntry` 或公开分发页聚合逻辑，可跳过本清单。
- 若本轮只改公开分发页文案或纯样式，且未影响入口跳转或只读边界，可只抽查 `/discover` 首屏与每个区块各 1 条跳转。

## 7. 结论记录格式

```md
- 验收日期：2026-04-13
- 验收人：<name>
- 验收范围：公开社区分发页首批
- 执行入口：/discover、/forum、/docs、/leaderboard、/u/:id、/shop
- 结果：通过 / 阻塞
- 备注：<如有问题，记录区块、目标路由、视口尺寸与复现步骤>
```
