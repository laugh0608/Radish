# 通知系统 API 入口

> **状态**：当前事实入口
>
> **最后更新**：2026-07-21

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

内容治理注册表包含 Governance 分类的 `ContentReportResolved / UserModerationChanged`。前者要求模板参数 `resultCode`，后者要求 `actionType`；两者的 `voTarget.voKind` 均为 `None`，消费者只展示本地化摘要，不构造 `/moderation`、举报详情或用户资料链接。举报结果和账号状态仍分别回源本人举报接口与治理状态接口。

`GetNotificationList / GetUnreadCount / MarkAsRead / DeleteNotification` 仍为旧消费者与 Flutter 兼容保留，不是正式 Web 新开发入口；删除前必须单独审计消费者。完整契约见 [通知中心深化与通知治理](/features/notification-center-deepening)，2026-01 至 2026-06 草稿见 [通知系统 API 方案（历史）](/records/notification-legacy/notification-api)。
