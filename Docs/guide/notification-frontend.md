# 通知系统前端入口

> **状态**：当前实现导航
>
> **最后更新**：2026-07-18

正式 Web `/notifications` 是浏览器默认通知入口，WebOS `/desktop` 只做历史兼容，Flutter 只维护既有通知 MVP。当前代码入口：

- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/notifications/notificationActionQueue.ts`
- `Frontend/radish.client/src/apps/notification/NotificationCenter.tsx`
- `Frontend/radish.client/src/services/notificationHub.ts`
- `Frontend/radish.client/src/stores/notificationStore.ts`
- `Frontend/radish.client/src/utils/notificationNavigation.ts`

当前页面具有列表、已读、全部已读、删除入口、最近通知队列和部分目标回流；分类与队列指标仍基于已加载切片和客户端关键字，不能视为权威全量统计。

后续分类、偏好、聚合、结构化目标、PC / mobile 和断线追平以 [F4-B 通知中心深化与通知治理](/features/notification-center-deepening) 为准。旧目录、安装步骤、代码片段和 Smoke 草稿见 [通知系统前端集成指南（历史）](/records/notification-legacy/notification-frontend)。
