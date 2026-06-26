# 通知中心使用说明

> **文档版本**：v1.4
> **最后更新**：2026-06-21

## 架构定位

通知中心当前有三个承载面：

| 承载面 | 入口 | 定位 |
| --- | --- | --- |
| 纯 Web | `/notifications` | 普通浏览器登录态通知复访入口，承接通知列表、已读、删除和目标分流 |
| WebOS | `/desktop` 内的通知中心应用 | 历史工作台入口，继续保留窗口应用和 Dock 未读角标 |
| Flutter | 移动原生“通知”相关入口 | 承接移动端快速查看、单条已读和 forum 回流，不复刻完整通知中心 |

纯 Web `/notifications` 是当前浏览器默认推荐入口；WebOS 通知中心仍作为完整工作台能力保留。

### 访问入口

通知中心提供以下主要访问入口：

1. **纯 Web 路由**：访问 `/notifications`，未登录时进入 OIDC 登录并在登录后回到通知列表。
2. **桌面图标**：在 `/desktop` 中打开"通知中心"窗口应用。
3. **Dock 固定入口**：在 Dock 中点击通知图标快速打开通知中心，并显示未读角标。

## 位置与外观

### 桌面图标
- **图标**: `mdi:bell` (铃铛图标)
- **名称**: "通知中心"
- **位置**: 在桌面应用图标网格中显示

### Dock 固定入口
- **图标**: 铃铛图标，固定在 Dock 第一个位置
- **未读徽章**: 红色圆点或数字，显示在铃铛右上角
- **行为**: 点击打开通知中心应用窗口
- **固定特性**: 始终显示在 Dock 中，无论应用是否运行

### 纯 Web 页面
- **路由**: `/notifications`
- **头部**: 使用公共壳层头部，保留“社区发现 / 我的圈子 / 工作台”动作
- **登录恢复**: 匿名访问保存 `/notifications` 作为返回路径
- **目标分流**: 点击通知后优先进入纯 Web 目标；只有历史 WebOS 载荷或尚未迁移能力才回到 `/desktop` 深链

## Flutter 移动端轻量承接

Flutter 登录态壳层会读取当前用户最近站内通知，展示标题、内容、类型、已读状态和时间。

- forum 通知可根据通知载荷中的帖子和评论标识打开原生 forum detail，并在返回时回到打开通知前的 tab。
- 系统通知等不可跳通知保持只读展示，不伪造详情目标。
- 未读通知支持单条显式标记已读；成功后只更新本地该条状态，失败时在通知 sheet 内提示并保留原状态。
- Flutter 当前不提供全部已读、删除、通知设置、SignalR 实时同步或系统通知栏推送入口。
- WebOS 通知中心和纯 Web `/notifications` 仍是浏览器里的通知管理入口；Flutter 只承担移动端快速查看、单条已读和 forum 回流。

## 应用窗口

打开通知中心后，会在 WebOS 中显示一个独立窗口：

- **默认尺寸**: 800x700px
- **窗口类型**: 可拖拽、可调整大小的标准窗口
- **背景**: 浅灰色 (#f8f9fa)
- **顶部栏**: 显示"通知中心"标题和未读数统计

## 功能

### 1. 查看通知
- 显示最近 20 条通知
- 每条通知显示：
  - 类型图标（👍点赞、💬评论、⭐收藏等）
  - 标题和内容
  - 触发者头像和名称
  - 相对时间（如"5分钟前"）
  - 未读状态（蓝色左边框）

### 2. 标记已读
- **单条已读**: 点击通知右侧的"标记已读"按钮
- **全部已读**: 点击面板头部的"全部已读"按钮
- 已读通知会移除蓝色边框，未读数量会相应减少

### 3. 删除通知
- 点击通知右侧的"删除"按钮
- 删除后通知会从列表中移除

### 4. 查看更多
- 纯 Web 用户直接访问 `/notifications` 查看通知列表。
- WebOS Dock 或通知预览需要进入完整列表时，应打开通知中心窗口或跳转到 `/notifications`，不要再指向未实现页面。

## 通知目标跳转

纯 Web `/notifications` 的通知点击规则：

| 通知目标 | 跳转 |
| --- | --- |
| 聊天消息 / 提及，`extData` 带 `channelId/messageId` | `/messages?channelId=...&messageId=...` |
| forum 帖子 / 评论，`extData` 带 `postPublicId/postId/commentId` | `/forum/post/:id`，并记录“返回通知中心”来源 |
| 用户关注或用户目标 | `/u/:id`，并记录“返回通知中心”来源 |
| 订单目标 | `/shop/order/:orderId`；缺少合法订单 ID 时回 `/shop/orders` |
| 暂无可定位详情的互动通知 | `/forum` |

跳转来源状态保存在当前标签页的一次性来源转交中，不写入公开 URL、canonical、分享链接或 sitemap。完整规则见 [纯 Web 私域复访入口设计说明](/frontend/private-web-revisit)。

## 实时更新
- 当有新通知时，未读数量会自动增加
- 通过 SignalR WebSocket 实现实时推送
- 多端同步：在其他设备标记已读后，当前设备也会同步更新
- 所有访问入口（桌面图标、Dock、灵动岛）的未读数都会同步显示

## 触发通知的操作
以下操作会触发通知：
1. **点赞通知**: 其他用户点赞你的帖子或评论
2. **评论回复**: 其他用户回复你的评论
3. **系统通知**: 系统消息、公告等

## 调试信息
如果通知中心不显示，请检查：
1. 是否已登录（只有登录用户才能看到通知中心）
2. 浏览器控制台是否有错误信息
3. SignalR 连接状态（控制台会显示连接日志）
4. 网络请求是否成功（检查 Network 标签）

## 技术实现

### 组件位置
- **纯 Web 页面**: `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- **WebOS 应用组件**: `Frontend/radish.client/src/apps/notification/NotificationApp.tsx`
- **共享通知列表组件**: `Frontend/radish.client/src/apps/notification/NotificationCenter.tsx`
- **通知目标分流**: `Frontend/radish.client/src/utils/notificationNavigation.ts`
- **注册位置**: `Frontend/radish.client/src/desktop/AppRegistry.tsx`
- **Dock 集成**: `Frontend/radish.client/src/desktop/Dock.tsx`

### 状态管理
- 使用 `useNotificationStore` 管理通知列表和未读数
- 使用 `useNotificationHub` 管理 SignalR 实时连接
- 通知状态在所有访问入口间共享

### API 集成
- 初始加载：`GET /api/v1/Notification/GetList`
- 标记已读：`POST /api/v1/Notification/MarkAsRead`
- 全部已读：`POST /api/v1/Notification/MarkAllAsRead`
- 删除通知：`DELETE /api/v1/Notification/Delete/{id}`

### SignalR 实时推送
- Hub URL: `/hub/notification`
- 事件监听：
  - `UnreadCountChanged`: 未读数变化
  - `NewNotification`: 接收新通知
  - `NotificationRead`: 通知已读同步
  - `AllNotificationsRead`: 全部已读同步
