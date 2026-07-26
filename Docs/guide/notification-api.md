# 通知系统 API 入口

> **状态**：当前事实入口
>
> **最后更新**：2026-07-25

当前 Controller 基础路径为 `/api/v1/Notification`，正式 Web 使用以下权威收件箱动作：

| 方法 | 路径 |
| --- | --- |
| `GET` | `/GetInbox?category=&onlyUnread=&cursor=&pageSize=` |
| `GET` | `/GetInboxSummary` |
| `PUT` | `/MarkInboxGroupsAsRead` |
| `PUT` | `/MarkAllAsRead`，请求体可指定 `category` |
| `DELETE` | `/DeleteInboxGroup/{groupId}` |
| `GET` | `/GetPreferences` |
| `PUT` | `/UpdatePreferences` |

所有接口要求 Client policy 登录态，LongId、revision 和计数在前端按字符串消费。分页 cursor 绑定 revision 和筛选条件；过期后返回结构化冲突，客户端刷新第一页，不伪装为连续分页。所有写操作返回最新权威 summary，客户端不得自行增减未读。

F4-G 后注册表新增 Knowledge 分类的 `WikiCollaboratorInvited / WikiReviewUpdated`，目标类型为 `DocsAuthorDraft`，使用 `voDocumentId / voDraftId` 字符串字段。服务端返回目标前重新校验文档、草稿、租户和 Owner / Accepted Editor 关系；失权或目标失效时返回不可用原因，不回退公开文档链接。

内容治理注册表包含 Governance 分类的 `ContentReportResolved / UserModerationChanged / ContentModerationDecisionAvailable / ContentModerationAppealUpdated`。前两类 `voTarget.voKind=None`，后两类分别使用 `GovernanceDecision / GovernanceAppeal`，只导航到当前用户自己的 `/me/appeals`；消费者不得构造 `/moderation` 或泄露举报者、申诉陈述和内部证据。举报、账号限制、申诉与纠正状态仍回源各自本人接口。

关系型通知由注册表的 `SuppressWhenInteractionBlocked` 明确标注，当前覆盖评论 / 轻回应、点赞、聊天提及、Direct 请求和关注。创建时若触发者与接收者任一方向存在有效 `UserBlock`，该接收者不会入箱；历史关系通知可被可靠任务标记 `SuppressedByUserBlock`，`GetInbox / GetInboxSummary` 和实时 revision 在任务完成前仍按 Main 当前关系裁剪。解除屏蔽不恢复或补发通知，Knowledge、Governance、Commerce、Growth、系统与账号安全通知不受该策略影响。

`GetNotificationList / GetUnreadCount / MarkAsRead / DeleteNotification` 仍为旧消费者与 Flutter 兼容保留，不是正式 Web 新开发入口；删除前必须单独审计消费者。完整契约见 [通知中心深化与通知治理](/features/notification-center-deepening)，2026-01 至 2026-06 草稿见 [通知系统 API 方案（历史）](/records/notification-legacy/notification-api)。
