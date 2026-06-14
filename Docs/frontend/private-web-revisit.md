# 纯 Web 私域复访入口设计说明

> 状态：执行中
>
> 最后更新：2026-06-14（Asia/Shanghai）
>
> 本文说明纯 Web 登录态私域复访入口的当前设计边界。阶段进度、验证流水和日结记录不写入本文。

## 定位

纯 Web 私域复访入口用于让登录用户在普通浏览器中直达高价值私域场景，不需要先进入 WebOS `/desktop` 工作台。

当前已落地三个入口：

| 路由 | 主要任务 | 数据边界 | 不承担 |
| --- | --- | --- | --- |
| `/notifications` | 查看站内通知、标记已读、删除、从通知进入目标内容 | 通知列表、未读数、通知目标分流 | 完整通知偏好、通知聚合策略、系统通知栏推送 |
| `/me` | 查看我的状态、公开主页入口、成长 / 资产只读摘要、最近复访 | 公开资料、经验摘要、经验明细、胡萝卜余额、近期流水、最近浏览 | 完整资料编辑、完整钱包、转账、支付密码、安全设置、完整浏览历史中心 |
| `/messages` | 复访聊天频道、定位通知中的消息、从成员进入公开主页后返回消息 | 频道列表、消息历史、`channelId/messageId` 定位、Chat Hub | 完整聊天平台重构、私聊、消息搜索、Reaction、置顶、阅读回执、移动系统通知 |

这三类入口是登录态页面，不进入公开 sitemap，不输出 public head / canonical / JSON-LD，也不替代 WebOS `/desktop` 的完整工作台能力。

## 路由与登录恢复

| 入口 | 允许的 URL | 登录恢复 |
| --- | --- | --- |
| `/notifications` | 只允许无 query / hash 的 `/notifications` | 匿名访问时保存 `/notifications`，登录后回到通知列表 |
| `/me` | 只允许无 query / hash 的 `/me` | 匿名访问时保存 `/me`，登录后回到我的状态 |
| `/messages` | `/messages` 或 `/messages?channelId={id}&messageId={id}` | 匿名访问时保留合法频道 / 消息参数，登录后恢复定位 |

`channelId` 和 `messageId` 必须保持字符串口径，并通过聊天参数解析器校验。外部来源不得把 Snowflake ID 转成 JavaScript `number` 后再拼 URL。

## 通知目标分流

纯 Web `/notifications` 点击通知时使用统一目标解析：

| 通知来源 | 目标 |
| --- | --- |
| 聊天通知 `extData.app=chat`，带 `channelId/messageId` | `/messages?channelId=...&messageId=...` |
| Forum 通知 `extData` 带 `postPublicId/postId/commentId` | `/forum/post/:id`，并记录来源为通知 |
| `businessType=Post` | `/forum/post/:postId`，并记录来源为通知 |
| `businessType=User` 或关注通知 | `/u/:id`，并记录来源为通知 |
| `businessType=Order` | `/desktop?app=shop&orderId=...` |
| 暂无可定位详情的评论、点赞、提及 | `/forum` |

规则原则：

- 优先使用纯 Web 可承接的公开详情、公开个人页和消息复访入口。
- 订单、背包、完整钱包等仍依赖工作台的能力继续进入 `/desktop` 深链。
- 通知目标来源状态只保存在当前标签页的一次性来源转交中，不进入公开 URL、canonical、分享链接或 sitemap。

## 来源返回

私域入口进入公开页面时，需要保留“从哪里来”的返回语义。

| 来源 | 公开目标 | 返回文案 |
| --- | --- | --- |
| `/notifications` | `/forum/post/:id`、`/u/:id` | 返回通知中心 |
| `/me` | `/u/:id`、最近复访中的公开详情 | 返回我的状态 |
| `/messages` | `/u/:id` | 返回消息 |

普通点击使用来源转交；新开标签、复制链接和分享链接只保留公开 URL。

## 与 WebOS 的关系

WebOS `/desktop` 继续保留聊天、通知中心、个人中心、萝卜坑、商城和工作台级编辑能力。纯 Web 私域入口只迁移浏览器里最常用的复访能力：

- 通知：列表、已读、删除和目标分流。
- 我的：个人状态、公开主页、成长 / 资产只读摘要和最近复访。
- 消息：频道列表、会话复访、通知消息定位和基础发送。

后续若要迁移完整聊天、完整钱包、完整个人中心或通知偏好，必须重新评审产品边界和验证范围。

## 实现入口

前端入口：

- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/me/MeApp.tsx`
- `Frontend/radish.client/src/messages/MessagesApp.tsx`
- `Frontend/radish.client/src/bootstrap/entryRoute.ts`
- `Frontend/radish.client/src/services/authReturnPath.ts`
- `Frontend/radish.client/src/utils/notificationNavigation.ts`

复用能力：

- 通知列表复用 `Frontend/radish.client/src/apps/notification/NotificationCenter.tsx`。
- 消息入口复用 `Frontend/radish.client/src/apps/chat/ChatApp.tsx` 和 `ChatHub`。
- 公开来源返回复用 `publicRouteNavigation` 的一次性来源转交。

## 验证口径

改动这些入口时，按风险分层覆盖：

- 路由与登录恢复：`authReturnPath.test.ts`、`entryRoute.test.ts`。
- 通知分流：`notificationNavigation.test.ts`。
- 来源返回：`publicRouteNavigation.test.ts`、`realUsagePathContracts.test.ts`。
- 前端类型与构建：`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`。
- 真实页面 smoke：Gateway 下覆盖 PC `1920x1080` 与移动 `390x844 @ DPR 3`；涉及点击时结合直接导航和来源状态校验，不把 Chrome 插件事件派发不稳定直接判定为页面缺陷。
