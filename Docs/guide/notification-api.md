# 通知系统 API 入口

> **状态**：当前事实入口
>
> **最后更新**：2026-07-18

当前 Controller 基础路径为 `/api/v1/Notification`，真实公开动作如下：

| 方法 | 路径 |
| --- | --- |
| `GET` | `/GetNotificationList` |
| `GET` | `/GetUnreadCount` |
| `PUT` | `/MarkAsRead` |
| `PUT` | `/MarkAllAsRead` |
| `DELETE` | `/DeleteNotification/{notificationId}` |

所有接口要求登录，LongId 在前端按字符串消费。当前 client 删除调用与 Controller 的 HTTP 方法 / 路径不一致，已登记到 F4-B 审计，不能继续复制旧调用。

F4-B 的收件箱、摘要、偏好、分组已读和 revision 契约见 [通知中心深化与通知治理](/features/notification-center-deepening)。旧接口在新消费者完成迁移前保留兼容。2026-01 至 2026-06 的完整接口草稿见 [通知系统 API 方案（历史）](/records/notification-legacy/notification-api)。
