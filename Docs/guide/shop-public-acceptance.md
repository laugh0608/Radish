# 公开商城浏览首批人工验收清单

> 适用范围：`Phase 2-2` 下公开商城浏览首批与公开内容壳层中的 `/shop`、`/shop/products`、`/shop/product/:productId` 直达入口。
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [前端设计文档](/frontend/design)
> - [人工验收模板](/guide/manual-acceptance-template)

## 1. 适用改动范围

- `Frontend/radish.client/src/public/` 下公开商城路由、状态同步或内容展示逻辑改动后
- `/shop`、`/shop/products`、`/shop/product/:productId` 的公开壳层布局、筛选、分页、详情阅读或返回链路改动后
- 商城公开读取接口、匿名详情读取边界或桌面商城到公开详情回跳口径改动后

## 2. 前置条件

- 已完成前端最小自动化验证：
  - `npm run type-check --workspace=radish.client`
  - `npm run build --workspace=radish.client`
- 已启动本地前端与相关宿主，能在浏览器直接访问：
  - `/shop`
  - `/shop/products`
  - `/shop/product/:productId`
- 准备至少一条可公开浏览的商品详情数据，以及至少一个有商品的分类
- 浏览器优先覆盖 `390 x 844` 与 `430 x 932` 两档窄屏视口

## 3. 联调资产

- 公开壳层入口：`Frontend/radish.client/src/public/PublicEntry.tsx`
- 公开商城路由：`Frontend/radish.client/src/public/shopRouteState.ts`
- 公开商城页面：`Frontend/radish.client/src/public/shop/PublicShopApp.tsx`
- 商城接口：`Frontend/radish.client/src/api/shop.ts`
- 桌面商城：`Frontend/radish.client/src/apps/shop/ShopApp.tsx`

说明：

- 本清单以公开壳层 UI / 路由 / 匿名读取边界人工验收为主，不要求额外新增 `HttpTest`
- 若本轮同时修改订单、背包、购买或桌面商城状态机，再并行补商城专题回归

## 4. 最小人工验收顺序

1. 直接打开 `/shop`，确认页面进入公开内容壳层，而不是先进入桌面 `Shell`。
2. 在窄屏下检查商城首页，确认分类区、推荐商品区、只读标识和顶部返回 `WebOS` 入口没有重叠、截断或横向撑破。
3. 从首页点击分类或“查看全部”，确认能进入 `/shop/products`，并正常加载分类筛选和商品列表。
4. 在 `/shop/products` 中切换分类、执行一次搜索、翻到第 `2` 页及之后任意一页，确认 `category / q / page` 会稳定回写到 URL，刷新后仍能恢复当前列表上下文。
5. 从列表点击商品进入 `/shop/product/:productId`，确认详情页只展示商品信息、说明和返回桌面工作台的导向，不会误弹购买确认、举报或登录提示。
6. 直接刷新 `/shop/product/:productId`，确认匿名详情读取仍然可用，不会因为前端带了 `withAuth` 或把外部 `productId` 当成 `number` 而失败。
7. 在详情页点击“进入 WebOS 工作台”或同类导向按钮，确认会返回桌面入口；再从浏览器回退，确认还能回到当前公开详情或商品列表上下文。
8. 人工抽查一个不存在或已失效的 `productId`，确认公开壳层会给出只读的“商品不可访问”状态，而不是跳回论坛、桌面或空白页。

## 5. 预期结果

- `/shop`、`/shop/products` 与 `/shop/product/:productId` 以公开内容壳层直达，不绕回桌面入口。
- 公开商城在手机上保持“首页可扫读、列表可筛选、详情可直达”的浏览节奏。
- 列表筛选、搜索和分页状态都能被 URL 保存，刷新后不会丢失上下文。
- 商品详情匿名读取口径和公开路由的外部 ID 字符串口径保持一致，不再依赖前端数值型 ID。
- 公开商城继续保持只读边界，不误带入购买确认、订单、背包、权益使用、举报或其他工作台能力。

## 6. 可跳过项

- 若本轮只改 changelog、规划或纯文档，可跳过整份清单。
- 若本轮只改桌面 `shop` 窗口，且未触达 `src/public/`、公开商城路由或匿名商品详情读取，可跳过本清单。
- 若本轮只改公开商城文案或纯样式，且未影响筛选、分页、详情直链或只读边界，可只抽查 `/shop`、`/shop/products` 和 1 个 `/shop/product/:productId`。

## 7. 结论记录格式

```md
- 验收日期：2026-04-12
- 验收人：<name>
- 验收范围：公开商城浏览首批
- 执行入口：/shop、/shop/products、/shop/product/:productId
- 结果：通过 / 阻塞
- 备注：<如有问题，记录商品 ID、分类、搜索词、视口尺寸与复现步骤>
```
