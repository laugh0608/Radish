# 公开榜单首批人工验收清单

> 适用范围：`Phase 2-2` 下公开榜单首批与公开内容壳层中的 `/leaderboard`、`/leaderboard/:type` 直达入口。
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [前端设计文档](/frontend/design)
> - [人工验收模板](/guide/manual-acceptance-template)

## 1. 适用改动范围

- `Frontend/radish.client/src/public/` 下公开榜单路由、状态同步或内容展示逻辑改动后
- `/leaderboard`、`/leaderboard/:type` 的移动阅读布局、分页、榜单切换或只读边界改动后
- 榜单项到公开个人页跳转、登录态识别或“我的排名”显示逻辑改动后

## 2. 前置条件

- 已完成前端最小自动化验证：
  - `npm run type-check --workspace=radish.client`
  - `npm run build --workspace=radish.client`
- 已启动本地前端与相关宿主，能在浏览器直接访问：
  - `/leaderboard`
  - `/leaderboard/experience`
  - `/leaderboard/hot-product`
  - `/u/:id`
- 准备至少两类榜单数据：
  - 一类用户排行榜
  - 一类商品排行榜
- 浏览器优先覆盖 `390 x 844` 与 `430 x 932` 两档窄屏视口
- 若要检查“我的排名”，需准备一个已登录且在用户榜单内有排名的账号

## 3. 联调资产

- 公开壳层入口：`Frontend/radish.client/src/public/PublicEntry.tsx`
- 公开榜单路由：`Frontend/radish.client/src/public/leaderboardRouteState.ts`
- 公开榜单页面：`Frontend/radish.client/src/public/leaderboard/PublicLeaderboardApp.tsx`
- 排行榜接口：`Frontend/radish.client/src/api/leaderboard.ts`
- 公开个人页：`Frontend/radish.client/src/public/profile/PublicProfileApp.tsx`

说明：

- 本清单以公开壳层 UI / 路由 / 登录态人工验收为主，不要求额外新增 `HttpTest`
- 若本轮同时修改排行榜接口、经验值统计或桌面排行榜，再并行补对应后端回归

## 4. 最小人工验收顺序

1. 直接打开 `/leaderboard`，确认页面进入公开内容壳层，而不是先进入桌面 `Shell`。
2. 在窄屏下检查首屏，确认标题、只读标识、榜单切换按钮与统计胶囊没有重叠、截断或横向撑破。
3. 切换至少两类用户榜单，再刷新页面，确认 URL 会同步落到 `/leaderboard/:type`，刷新后仍能恢复当前榜单。
4. 切到第 `2` 页及之后任意一页，确认分页按钮在窄屏下仍可点击，刷新页面后页码状态仍能恢复。
5. 在未登录状态打开用户榜单，确认不会误显示“我的排名”；在已登录状态打开同一榜单，确认“我的排名”只在用户榜单中出现，且商品榜单不会误显示该信息。
6. 点击一条用户榜单项，确认能跳到 `/u/:id`，而不是只能打开桌面 `profile` 窗口。
7. 打开 `/leaderboard/hot-product`，确认商品榜单当前只做公开展示，不会误跳商品详情、商城购买、订单或背包链路。
8. 再从公开个人页返回榜单或浏览器回退，确认不会异常跳回桌面入口，榜单上下文保持稳定。

## 5. 预期结果

- `/leaderboard` 与 `/leaderboard/:type` 以公开内容壳层直达，不绕回桌面入口。
- 公开榜单在手机上保持“榜单切换清晰、排行信息可扫读、分页可继续”的阅读节奏。
- 榜单类型和分页状态都能被 URL 保存，刷新后不会丢失上下文。
- 用户榜单可稳定跳到公开个人页，不依赖桌面窗口系统。
- 公开榜单继续保持只读边界，不误带入经验明细、商城详情、购买、订单或背包等工作台能力。

## 6. 可跳过项

- 若本轮只改 changelog、规划或纯文档，可跳过整份清单。
- 若本轮只改桌面 `leaderboard` 窗口，且未触达 `src/public/`、公开榜单路由或公开个人页跳转，可跳过本清单。
- 若本轮只改公开榜单文案或纯样式，且未影响类型切换、分页、登录态识别或跳转链路，可只抽查 1 个用户榜单和 1 个商品榜单。

## 7. 结论记录格式

```md
- 验收日期：2026-04-12
- 验收人：<name>
- 验收范围：公开榜单首批
- 执行入口：/leaderboard、/leaderboard/:type、/u/:id
- 结果：通过 / 阻塞
- 备注：<如有问题，记录榜单类型、账号状态、视口尺寸与复现步骤>
```
