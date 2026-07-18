# 通知系统实现说明

> **状态**：当前入口
>
> **最后更新**：2026-07-18

当前通知实现由 Message 库通知月表、`UserNotification`、`NotificationService`、可靠 Outbox、HTTP Controller、SignalR Hub 和 client 通知 Store 组成。现有实现可提供持久通知、列表、未读、已读、删除入口和部分目标回流，但偏好、服务端聚合、权威分类、容量与 revision 对账尚未实施。

当前开发与设计入口：

- [通知中心使用说明](/guide/notification-center)
- [F4-B 通知中心深化与通知治理](/features/notification-center-deepening)
- [本地验证基线](/guide/validation-baseline)

2026-01 至 2026-06 的数据模型、缓存、异步化和代码草稿已归档到 [通知系统实现细节（历史）](/records/notification-legacy/notification-implementation)。历史示例不再作为当前模型、目录或可靠性契约。
