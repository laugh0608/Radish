# 通知系统前端入口

> **状态**：当前实现导航
>
> **最后更新**：2026-07-21

正式 Web `/notifications` 是浏览器默认通知入口，WebOS `/desktop` 只做历史兼容，Flutter 只维护既有通知 MVP。当前代码入口：

- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/notifications/notificationInbox.ts`
- `Frontend/radish.client/src/apps/notification/NotificationCenter.tsx`
- `Frontend/radish.client/src/services/notificationHub.ts`
- `Frontend/radish.client/src/services/notificationInboxSync.ts`
- `Frontend/radish.client/src/stores/notificationStore.ts`
- `Frontend/radish.client/src/utils/notificationNavigation.ts`

当前页面消费服务端分类、分组、summary、偏好和结构化 target，覆盖分类 / 未读筛选、聚合触发者、分组已读、分类 / 全部已读、删除、偏好、cursor 过期、离线与目标失效。导航、Workbench、Dock、Web 壳层和 WebOS 共用服务端 summary；Store 不自行增减权威未读，也不扫描标题、正文或 `ExtData` 猜分类和目标。

服务端已经可能返回 Wiki `DocsAuthorDraft` 目标；当前 `@radish/http` target 联合类型和 `notificationNavigation.ts` 尚未承接该 kind，因此邀请 / 审核通知先保留 Knowledge 摘要但不承诺直接跳到 `/docs/edit/:id`。消费者接入前不得把 `documentId` 猜成公开 Slug，也不得绕过服务端失权结果。

内容治理使用 `Governance` 分类的 `ContentReportResolved / UserModerationChanged`。两类通知固定为 `TargetKind=None`：前端保留服务端安全摘要，不渲染 Console 案件链接，也不从模板参数拼接目标；举报结果从 `/me/reports` 读取，当前账号限制从 `GetMyModerationStatus / GetMyPublishPermission` 回源确认。

跨标签只广播当前账号 revision，SignalR、重新聚焦、恢复在线和 cursor 失效统一回源对账。详细边界与成组验收见 [F4-B 通知中心深化与通知治理](/features/notification-center-deepening)；旧目录、安装步骤和代码草稿见 [通知系统前端集成指南（历史）](/records/notification-legacy/notification-frontend)。
