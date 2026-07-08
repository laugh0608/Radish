# 纯 Web 私域复访入口设计说明

> 状态：执行中
>
> 最后更新：2026-07-08（Asia/Shanghai）
>
> 本文说明纯 Web 登录态私域复访入口的当前设计边界。阶段进度、验证流水和日结记录不写入本文。

## 定位

纯 Web 私域复访入口用于让登录用户在普通浏览器中直达高价值私域场景，不需要先进入 WebOS `/desktop` 工作台。

当前已落地七组入口：

| 路由 | 主要任务 | 数据边界 | 不承担 |
| --- | --- | --- | --- |
| `/workbench` | 正式 Web 功能地图、继续处理队列、公开 / 私域 / 作者态入口承接 | 现有正式 Web 路由、静态任务入口、私域状态 rail | 推荐系统、全局待办 API、WebOS Dock、窗口系统 |
| `/notifications` | 查看站内通知、标记已读、删除、从通知进入目标内容 | 通知列表、未读数、通知目标分流 | 完整通知偏好、通知聚合策略、系统通知栏推送 |
| `/circle` | 查看关注动态、我的关注、我的粉丝，并从关系链进入公开帖子或公开个人页 | `UserFollow` 汇总、关注动态、关注 / 粉丝列表、`Post.PublicId`、`User.PublicId` | 推荐算法、短动态、转发 / 引用、私信、联邦协议 |
| `/me` | 查看我的状态、公开主页入口、资产入口，并在 P3-12-B2 承接完整个人中心子路径 | 公开资料、经验摘要、经验明细、胡萝卜余额、近期流水、我的内容、浏览历史、附件、经验详情 | 完整资料编辑、转账、支付密码、安全设置、资产风控、论坛作者态 |
| `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` | 查看商城订单、订单详情和背包；从公开商品详情购买成功后确认订单结果 | 当前用户订单、订单状态、商品快照、背包权益 / 消耗品、订单通知回流 | 公开分享、购物车、退款、权益激活、道具使用、Console 治理 |
| `/messages` | 复访聊天频道、按会话关系浏览聊天、定位通知中的消息、从成员进入公开主页后返回聊天 | 频道列表、会话分区、消息历史、`channelId/messageId` 定位、Chat Hub | 完整聊天平台重构、消息搜索、Reaction、置顶、阅读回执、移动系统通知 |
| `/pet` | 领取和照顾当前用户电子宠物、查看状态与最近流水 | 宠物主档、四类照顾动作、每日次数 / 冷却、最近状态流水 | 萝卜币消耗、商城物品、社区任务奖励、Console 数值配置、公开宠物名片默认展示 |

这些入口是登录态页面，不进入公开 sitemap，不输出 public head / canonical / JSON-LD，也不替代 WebOS `/desktop` 的完整工作台能力。

## 共享壳层与状态槽

私域复访入口已开始接入 `radish.client` 共享 Web shell：

- `WebShellHeader` 负责正式 Web PC header 与移动底栏；Public / Private 页面共用产品级主导航，PC 固定为 `发现 / 论坛 / 聊天 / 更多`，右侧为 `通知 / 登录注册或头像用户名`，移动底栏固定为 `发现 / 论坛 / 聊天 / 更多 / 我的`。
- `/me`、`/circle`、`/pet` 和 `/me/*` 归属“我的”；`/messages` 归属“聊天”；`/notifications` 归属右侧通知动作；订单 / 背包、Docs 作者态、论坛作者态、Console、规则、榜单、公开文档、公开商城和 `/desktop` 兼容入口归属“更多”。
- `WebStateSlot` 负责加载、空态、错误、权限限制、登录恢复和普通信息状态，不再让 `/me`、`/messages`、`/notifications`、`/circle`、`/pet` 等页面分别维护不同视觉的状态卡。
- 私域页面进入公开帖子、公开个人页或商品详情时，仍按来源转交规则保留“返回我的状态 / 消息 / 通知中心 / 我的圈子 / 电子宠物”等语义；共享壳层不改变公开 URL、canonical、分享链接或 sitemap。
- 移动端内容区需要保留 `--rx-mobile-shell-offset` 对应的底部空间，避免 floating tab 遮挡状态槽、列表末尾或关键动作。
- E8 后 `/workbench` 作为“更多”功能地图承接公开商城、榜单、Docs、规则和兼容入口；移动底栏不再按 public / private 分叉，不把低频入口挤入一级底栏。

如果后续某个私域页确实需要新的 header 或状态变体，先回到 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design) 确认适用范围，再进入代码。

## 路由与登录恢复

| 入口 | 允许的 URL | 登录恢复 |
| --- | --- | --- |
| `/workbench` | 只允许无 query / hash 的 `/workbench` | 当前作为功能地图可公开访问；进入私域子任务时由目标路由自行处理登录恢复 |
| `/notifications` | 只允许无 query / hash 的 `/notifications` | 匿名访问时保存 `/notifications`，登录后回到通知列表 |
| `/circle` | `/circle` 或 `/circle?tab=feed|following|followers&page={n}` | 匿名访问时保留合法 tab / page，登录后恢复到原圈子分页 |
| `/me` | `/me`、`/me/assets`、`/me/assets/transactions`；P3-12-B2 扩展 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 的合法 query | 匿名访问时保存合法 `/me` 子路径，登录后回到对应私域页面 |
| `/shop/*` 私域 | `/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`；公开商品购买意图使用 `/shop/product/:productId?intent=purchase` | 匿名访问时保存合法商城私域路径或购买意图，登录后回到订单 / 背包 / 商品购买上下文 |
| `/messages` | `/messages` 或 `/messages?channelId={id}&messageId={id}` | 匿名访问时保留合法频道 / 消息参数，登录后恢复定位 |
| `/pet` | 只允许无 query / hash 的 `/pet` | 匿名访问时保存 `/pet`，登录后回到电子宠物页面 |

`channelId` 和 `messageId` 必须保持字符串口径，并通过聊天参数解析器校验。外部来源不得把 Snowflake ID 转成 JavaScript `number` 后再拼 URL。
`/circle` 的 `tab/page` 必须通过 `circleRouteState` 规范化，非法参数回到 `feed` 与第一页；规范化替换不得清掉当前标签页已保存的公开来源返回状态。

## 通知目标分流

纯 Web `/notifications` 点击通知时使用统一目标解析：

| 通知来源 | 目标 |
| --- | --- |
| 聊天通知 `extData.app=chat`，带 `channelId/messageId` | `/messages?channelId=...&messageId=...` |
| Forum 通知 `extData` 带 `postPublicId/postId/commentId` | `/forum/post/:id`，并记录来源为通知 |
| `businessType=Post` | `/forum/post/:postId`，并记录来源为通知 |
| `businessType=User` 或关注通知 | `/u/:identifier`，并记录来源为通知 |
| `businessType=Order` | `/shop/order/:orderId`，缺少或非法订单 ID 时回 `/shop/orders` |
| 暂无可定位详情的评论、点赞、提及 | `/forum` |

规则原则：

- 优先使用纯 Web 可承接的公开详情、公开个人页和消息复访入口。
- 订单、背包和资产流水已进入正式 Web 私域路由；转账、支付口令、安全设置等高风险资产能力继续保留在后续专题或 WebOS 历史入口。
- 通知目标来源状态只保存在当前标签页的一次性来源转交中，不进入公开 URL、canonical、分享链接或 sitemap。

## 来源返回

私域入口进入公开页面时，需要保留“从哪里来”的返回语义。

| 来源 | 公开目标 | 返回文案 |
| --- | --- | --- |
| `/notifications` | `/forum/post/:id`、`/u/:identifier` | 返回通知中心 |
| `/circle` | `/forum/post/:id`、`/u/:identifier` | 返回我的圈子 |
| `/me` | `/u/:identifier`、最近复访中的公开详情 | 返回我的状态 |
| `/messages` | `/u/:identifier` | 返回消息 |
| `/pet` | 后续公开宠物名片或公开个人页 | 返回电子宠物 |

普通点击使用来源转交；新开标签、复制链接和分享链接只保留公开 URL。

## 与 WebOS 的关系

WebOS `/desktop` 继续保留聊天、通知中心、个人中心、萝卜坑、商城和工作台级编辑能力。纯 Web 私域入口只迁移浏览器里最常用的复访能力：

- 通知：列表、已读、删除和目标分流。
- 工作台：正式 Web 功能地图、继续处理队列和私域 / 作者态入口分流，不迁移 WebOS Dock 或窗口几何。
- 圈子：关注动态、我的关注、我的粉丝和进入公开帖子 / 公开个人页的来源返回。
- 我的：个人状态、公开主页、资产入口、我的内容、完整浏览历史、附件管理和经验详情；关注关系权威入口仍是 `/circle`。
- 商城：公开商品详情登录后继续购买、订单列表、订单详情和背包；WebOS 商城窗口继续保留历史深链。
- 消息：频道列表、`互相关注 / 陌生人私信 / 好友群组 / 公共频道` 会话分区、通知消息定位和基础发送；当前后端关系 / 群组字段未完全补齐时只按明确的频道类型归类，不用名称或描述猜测关系。
- 宠物：领取、命名、状态展示、四类照顾动作、每日次数 / 冷却展示和最近流水。

后续若要迁移完整聊天、转账、支付口令、安全设置、资料编辑深水区、通知偏好、宠物经济系统或公开宠物名片，必须重新评审产品边界和验证范围。

## 实现入口

前端入口：

- `Frontend/radish.client/src/components/web-shell/WebShellHeader.tsx`
- `Frontend/radish.client/src/components/web-shell/WebStateSlot.tsx`
- `Frontend/radish.client/src/workbench/WorkbenchApp.tsx`
- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/circle/CircleApp.tsx`
- `Frontend/radish.client/src/circle/circleRouteState.ts`
- `Frontend/radish.client/src/me/MeApp.tsx`
- `Frontend/radish.client/src/me/MeAssetsPage.tsx`
- `Frontend/radish.client/src/me/meRouteState.ts`
- `Frontend/radish.client/src/shop/ShopEntry.tsx`
- `Frontend/radish.client/src/shop/shopRouteState.ts`
- `Frontend/radish.client/src/public/shopRouteState.ts`
- `Frontend/radish.client/src/messages/MessagesApp.tsx`
- `Frontend/radish.client/src/pet/PetEntry.tsx`
- `Frontend/radish.client/src/pet/PetApp.tsx`
- `Frontend/radish.client/src/pet/petRouteState.ts`
- `Frontend/radish.client/src/bootstrap/entryRoute.ts`
- `Frontend/radish.client/src/services/authReturnPath.ts`
- `Frontend/radish.client/src/utils/notificationNavigation.ts`

复用能力：

- 通知列表复用 `Frontend/radish.client/src/apps/notification/NotificationCenter.tsx`。
- 圈子入口复用 `UserFollow` 关注关系接口，帖子入口优先使用 `Post.PublicId`，用户入口优先使用 `User.PublicId`。
- 消息入口复用 `Frontend/radish.client/src/apps/chat/ChatApp.tsx` 和 `ChatHub`。
- 公开来源返回复用 `publicRouteNavigation` 的一次性来源转交。
- 我的状态入口复用 `meRouteState`、`authReturnPath` 和正式 Web `/me/*` 子路径，不复用 WebOS `ProfileApp` 顶层窗口壳。
- 宠物接口统一通过 `Frontend/radish.client/src/api/pet.ts` 调用 `@radish/http`，展示派生集中在 `Frontend/radish.client/src/pet/petPresentation.ts`。

## 验证口径

改动这些入口时，按风险分层覆盖：

- 路由与登录恢复：`authReturnPath.test.ts`、`entryRoute.test.ts`。
- 我的状态与个人中心：`meRouteState.test.ts`、`authReturnPath.test.ts`、`entryRoute.test.ts`、`realUsagePathContracts.test.ts`。
- 商城私域与购买回流：`shopRouteState.test.ts`、`authReturnPath.test.ts`、`entryRoute.test.ts`、`notificationNavigation.test.ts`、`realUsagePathContracts.test.ts`。
- 圈子入口：`circleRouteState.test.ts`、`authReturnPath.test.ts`、`publicRouteNavigation.test.ts`。
- 电子宠物入口与展示派生：`authReturnPath.test.ts`、`entryRoute.test.ts`、`petPresentation.test.ts`。
- 通知分流：`notificationNavigation.test.ts`。
- 来源返回：`publicRouteNavigation.test.ts`、`realUsagePathContracts.test.ts`。
- 前端类型与构建：`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`。
- 真实页面 smoke：Gateway 下覆盖 PC `1920x1080` 与移动 `390x844 @ DPR 3`；涉及点击时结合直接导航和来源状态校验，不把 Chrome 插件事件派发不稳定直接判定为页面缺陷。
