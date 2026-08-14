# F4-R R2-P03 Public 只读详情变体能力门禁实现

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：窄前端能力门禁已关闭；尚未修改 Pencil，尚未执行运行态 smoke
>
> 正式锚点：`/docs/:slug`、`/shop/product/:productId`、`/u/:identifier`、`/legal`

## 1. 结论

R2-P03 readiness 识别的正式商品举报与公开主页权威加载缺口已经关闭。商品详情现在复用既有 `Product` 举报链路；公开主页由主资料请求独立裁决存在性，统计和公开内容继续作为局部读取，不再以次要失败覆盖可读资料。

本批只修改 `radish.client` 的既有消费与呈现，没有新增或修改 API、权限、URL、LongId 形态、购买 intent、关系类型、实时能力、事务或存储边界。Docs 详情与 Legal 没有代码门禁，不做无关改动。

## 2. 正式商品举报

- `/shop/product/:productId` 在返回与分享旁增加举报次级动作，购买继续是唯一主动作。
- 匿名点击沿用正式 Forum 的 `report.loginRequired` toast，不新增 `intent=report`、登录回流参数或第二套举报页面。
- 登录态使用当前权威 `product.voId` 打开共享 `ContentReportModal`，固定 `targetType="Product"`；LongId 始终以字符串传递。
- 举报原因、500 字说明限制、统一 HTTP 客户端、结构化错误和治理事务继续由既有共享组件与服务端契约负责；购买资格、支付确认、幂等键和订单回流未改动。

## 3. 公开主页权威加载

- `getPublicProfile(route.userId)` 成为详情 loading、not found 与 unavailable 的唯一权威请求；结构化 `404` 不再与统计请求竞态。
- `getPublicUserStats(profileRouteIdentifier)` 使用独立请求代次、loading、失败与重试状态。统计失败只在统计区显示局部 unavailable，基础身份、宠物、公开帖子和公开评论仍可继续读取。
- 公开统计只在权威响应成功后显示真实计数，加载态使用明确进行中占位，不再把失败或未读取包装成零值。
- 粉丝数只在登录用户查看他人且 `GetFollowStatus` 成功后显示真实值；匿名、本人或关系读取失败时不再固定显示 `—`。
- 公开统计、帖子和评论 consumer 改用 `createApiResponseError`，保留现有 HTTP status、code、messageKey 与服务端消息，不改变响应结构。

## 4. 定向契约

`publicSeoStatic.test.ts` 新增并收紧以下守卫：

- 正式商品详情必须挂载共享 `ContentReportModal`，匿名必须被本地守卫，目标类型必须是 `Product`，目标 ID 必须保持 `LongId` 字符串。
- 举报不得新增 URL intent，既有购买回流仍由原测试覆盖。
- 公开主页不得重新把资料与统计放回 `Promise.all`；统计必须按规范化公开 identifier 独立读取并可局部重试。
- 统计失败不得伪造 `0`，未读取粉丝数不得伪装为权威 `—`；只有权威 `followStatus` 才能呈现粉丝数。
- 公开统计、帖子和评论失败必须提升为结构化 `ApiResponseError`。

## 5. 验证

- `node --test --test-isolation=none ./tests/publicSeoStatic.test.ts`：`42 / 42` 通过。
- `npm run test --workspace=radish.client`：`512 / 512` 通过。
- `npm run lint --workspace=radish.client`：通过，`0 warning`。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：production build 通过；仅保留既有大 chunk 提示。
- `git diff --check`：通过。

## 6. 下一步与停止线

R2-P03 代码能力门禁已经关闭。下一步需取得当前任务对 Pencil 的明确授权，再在唯一活动 `.pen` 中完成局部代表设计：只覆盖 PC 长内容、PC 商品 / 公开主页动作详情、Mobile `390px` 信息顺序和必要关键状态，不复制 Docs、Shop、Profile、Legal 四个完整路由。

本批未修改 Pencil，未启动服务或浏览器，也未执行 Gateway smoke。后续仍不得新增 API、权限、URL、购物车、退款、关注隐私、实时统计、法务 CMS 或新的移动壳层，不提前推进 R3 页面。
