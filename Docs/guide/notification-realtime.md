# 通知系统实时同步说明

> **状态**：当前入口
>
> **最后更新**：2026-07-18

当前 `NotificationHub` 位于 `/hub/notification`，登录连接加入 `user:{userId}` 组，并使用 `UnreadCountChanged / NewNotification / NotificationRead / AllNotificationsRead` 提供 best-effort 在线提示。数据库通知和 HTTP API 才是持久化事实；SignalR 连接、推送成功或内存 Store 状态不能解释为用户已经看到通知。

当前已知边界：

- 重连只推未读总数，不自动对账完整列表。
- Hub 的已读方法只广播、不落库，正式写操作必须走 HTTP。
- 完整通知对象按连接推送，尚未形成稳定语言和 revision 契约。
- 多实例 Backplane 不在当前正式范围。

F4-B 将实时链路收口为带 revision 的收件箱变化提示，详细设计见 [通知中心深化与通知治理](/features/notification-center-deepening)。历史 P0 / P1 / P2 方案原文见 [通知系统实时推送方案（历史）](/records/notification-legacy/notification-realtime)。
