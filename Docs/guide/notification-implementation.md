# 通知系统实现说明

> **状态**：当前入口
>
> **最后更新**：2026-07-21

当前通知实现由 Message 库通知月表、`UserNotification`、`NotificationInboxGroup`、`NotificationInboxState`、`NotificationSetting`、稳定定义注册表、可靠 Outbox、专属仓储事务、HTTP Controller、revision SignalR Hub 和 client 通知 Store 组成。偏好在入箱前生效，聚合、分类、未读、容量和 cursor 均以数据库权威状态为准；Wiki 邀请与审核结果使用 Knowledge 分类和 `DocsAuthorDraft` 目标。

内容治理注册两类 Governance 通知：`ContentReportResolved` 为普通优先级，只向举报者提供精简 `resultCode`；`UserModerationChanged` 为高优先级，只向被处置用户提供 `actionType` 摘要。两者均使用 `TargetKind=None`，不得携带 Console 案件链接、举报者身份、内部证据、内部备注或管理员信息；账号限制真相继续由治理状态接口读取。

正式消费者使用 `GetInbox / GetInboxSummary / GetPreferences / UpdatePreferences`、分组写操作和 `NotificationInboxChanged`；缓存读改写、客户端关键字分类、泛化目标回退和虚假 delivery 状态已经退出正确性链路。旧 API 暂时保留给兼容消费者，不能用于新增页面。

当前开发与设计入口：

- [通知中心使用说明](/guide/notification-center)
- [内容治理系统说明](/guide/content-moderation)
- [F4-B 通知中心深化与通知治理](/features/notification-center-deepening)
- [本地验证基线](/guide/validation-baseline)

2026-01 至 2026-06 的数据模型、缓存、异步化和代码草稿已归档到 [通知系统实现细节（历史）](/records/notification-legacy/notification-implementation)。历史示例不再作为当前模型、目录或可靠性契约。
