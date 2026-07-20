# 通知中心使用说明

> **文档版本**：v1.9
>
> **最后更新**：2026-07-20
>
> **当前状态**：F4-B 已完成并关闭。正式 Web、Workbench、导航角标与 WebOS 复用面均使用服务端权威收件箱、摘要、偏好、结构化目标和 revision 对账；Flutter 继续只维护既有 MVP。

## 架构定位

通知中心是社区事件的复访与处理入口，不是第二套私信箱。当前承载面：

| 承载面 | 入口 | 定位 |
| --- | --- | --- |
| 正式 Web | `/notifications` | PC / mobile 默认入口，承接权威分组、筛选、偏好、写操作和目标回流 |
| WebOS | `/desktop` 通知应用与 Dock | 复用同一 Store、summary 和结构化目标，只做历史兼容 |
| Flutter | 既有通知入口 | 继续使用旧兼容 API 提供最近通知、单条已读和 forum 回流，不扩完整通知页 |

匿名访问 `/notifications` 时进入 OIDC 登录，成功后返回原路径。未读角标以 `GetInboxSummary.voUnreadGroupCount` 为准，任何壳层都不得本地模拟增减。

## 正式 Web 功能

### 权威分组与筛选

- 通知按服务端 `NotificationCategory` 和稳定 `Kind` 展示，不扫描标题、正文、`businessType` 或 `ExtData` 猜分类。
- Knowledge 分类当前包含 Wiki 协作者邀请和审核结果；偏好仍由服务端注册表决定可见类型。
- 页面支持全部分类、具体分类和仅未读筛选。
- 点赞等高频事件由服务端形成一个通知分组，展示事件数、去重触发者数和安全摘要。
- 列表 cursor 绑定筛选条件和 revision；cursor 过期时刷新权威第一页并明确提示，不静默拼接旧页。
- 页面不展示昂贵或容易漂移的客户端推导总数。

### 已读与删除

- 单个或多个分组已读使用 `MarkInboxGroupsAsRead`。
- 当前分类或全部已读统一使用 `MarkAllAsRead`；请求体可携带服务端分类。
- 删除使用 `DeleteInboxGroup/{groupId}`，只软删除当前用户分组。
- 每个写操作都返回最新 summary；Store 应用返回值，不自行减少角标。
- 聚合组已读与新事件并发时，新事件必须保持未读，最终以服务端事务和 revision 为准。

### 通知偏好

- `/notifications` 可读取和更新当前注册表实际支持的分类偏好。
- `InAppEnabled` 控制后续事件是否入箱，`RealtimePreviewEnabled` 控制实时预览资格；历史通知不会因关闭偏好被追删。
- 强制分类由服务端返回 `VoCanDisableInApp / VoCanDisableRealtimePreview`，客户端不能伪造可关闭能力。
- 本专题不承诺邮件、短信、Web Push、声音或移动系统通知。

### 结构化目标

客户端只根据 `NotificationTargetVo.voKind` 和结构化 ID 构造站内路径：

| `voKind` | 正式目标 |
| --- | --- |
| `ForumPost` | `/forum/post/:publicId-or-id`，可附评论定位 |
| `ChatConversation` | `/messages?channelId=...&messageId=...` |
| `UserProfile` | `/u/:publicId-or-id` |
| `ShopOrder` | `/shop/order/:orderId` |
| `Inventory` | `/shop/inventory` |
| `Experience` | `/me/experience` |
| `DocsDocument` | `/docs/:slug` |
| `DocsAuthorDraft` | 携带文档与可选草稿 ID；服务端先复核 Owner / Accepted Editor 权限，当前消费端未识别该目标时只保留通知摘要 |
| `GovernanceCase` | 仅在存在正式受权目标时打开 |
| `None` | 保留摘要和失效原因，不渲染伪造链接 |

服务端返回目标前重新检查实体状态、所有权和 ACL；目标页面仍独立鉴权。历史 `ExtData` 只承担旧记录兼容，不是新消费者的导航依据。

## 实时与恢复

- `NotificationHub` 位于 `/hub/notification`，正式消费者只处理 `NotificationInboxChanged` revision 提示。
- 连接、重连、窗口重新聚焦、恢复在线和跨标签 revision 都触发 `GetInboxSummary` 对账；summary 领先当前列表时刷新当前筛选第一页。
- 重复或乱序 revision 被忽略；跨标签只传当前账号 ID 与 revision，不广播正文、目标或列表。
- 离线时保留已加载列表，恢复后回源；SignalR 失败不会把未读归零。
- 旧 `UnreadCountChanged` 仅作为兼容提示触发权威刷新，不直接修改 Store。
- 多 API 实例尚未配置 SignalR Backplane；这可能影响即时提示覆盖，但不影响 HTTP / revision 最终正确性。

## 页面状态与可访问性

- PC / mobile 均覆盖加载、空态、错误、离线、新通知提示、cursor 过期、长文本和目标失效。
- 固定界面文案使用 client 中英文资源；用户名称和用户内容保持原文，日期和数字按当前 locale 格式化。
- 分类、未读筛选、分组动作、偏好开关和恢复按钮均支持键盘与清晰焦点。
- 状态变化通过 `aria-live` 提示；未读、失效和禁用状态不只依赖颜色。
- 所有样式消费现有语义主题 token，不破坏正式 Web 或 WebOS 壳层布局。

## 代码与 API 入口

前端：

- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/notifications/notificationInbox.ts`
- `Frontend/radish.client/src/apps/notification/NotificationCenter.tsx`
- `Frontend/radish.client/src/services/notificationInboxSync.ts`
- `Frontend/radish.client/src/services/notificationHub.ts`
- `Frontend/radish.client/src/stores/notificationStore.ts`
- `Frontend/radish.client/src/utils/notificationNavigation.ts`

正式 API：

- `GET GetInbox / GetInboxSummary / GetPreferences`
- `PUT MarkInboxGroupsAsRead / MarkAllAsRead / UpdatePreferences`
- `DELETE DeleteInboxGroup/{groupId}`

旧 `GetNotificationList / GetUnreadCount / MarkAsRead / DeleteNotification` 暂时保留给兼容消费者。删除前必须单独审计 Flutter 和其他调用方，不与 F4-C 聊天搜索混合处理。

## 验证结论与剩余风险

2026-07-18 的 F4-B-D 已使用三个普通账号完成关注、评论 / 回复、点赞聚合、私信请求、商城购买、偏好、已读竞态、多标签、离线、cursor 和目标失效验收；`zh / en × 1920x1080 PC / 390x844 @ DPR 3 mobile` 与 WebOS 复用面通过。详细矩阵、migration、测试与清理结果见 [F4-B 通知中心深化与通知治理](/features/notification-center-deepening)。

剩余边界：

- 旧 API 尚未完成消费者删除审计。
- 历史 `ExtData` 不能保证全部转换为可用结构化目标，无法解析时保持 `None`。
- 多实例实时全覆盖需要依据部署拓扑另立 Backplane 专题。
- Flutter 不具备偏好、聚合管理、全部已读、删除或系统通知栏能力。

旧通知方案、安装步骤和早期代码片段只在 [通知历史方案索引](/records/notification-legacy/) 中保留，不作为当前实现依据。
