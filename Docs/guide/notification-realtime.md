# 通知系统实时同步说明

> **状态**：当前入口
>
> **最后更新**：2026-07-18

当前 `NotificationHub` 位于 `/hub/notification`，登录连接加入 `user:{userId}` 组；正式权威事件为 `NotificationInboxChanged`，携带 revision、未读分组数、未读事件数、变更原因和可选最新分组 ID。数据库与 HTTP API 是持久化事实；SignalR 只加速变化感知，连接或推送成功不能解释为用户已经看到通知。

当前恢复契约：

- 连接成功、重连、页面重新聚焦和恢复在线均拉取 `GetInboxSummary`；revision 领先时刷新当前筛选第一页。
- 重复或乱序 revision 被 Store 拒绝，不在客户端增减计数。
- Hub 不提供已读或删除写命令，也不推送完整本地化通知对象；正式写操作只走 HTTP。
- 多标签页只按账号隔离广播 revision，不广播正文、目标或本地列表。
- 旧 `UnreadCountChanged` 暂时只用于兼容并触发权威对账，不再直接修改计数；删除前需另做消费者审计。
- 多实例 Backplane 不在当前正式范围；即使实时提示未覆盖全部实例，HTTP / revision 对账仍保证最终正确。

详细设计与验收见 [通知中心深化与通知治理](/features/notification-center-deepening)。历史 P0 / P1 / P2 方案原文见 [通知系统实时推送方案（历史）](/records/notification-legacy/notification-realtime)。
