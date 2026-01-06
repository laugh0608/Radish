# 通知系统 API 文档

> **文档版本**：v1.0
> **创建日期**：2026-01-06
> **最后更新**：2026-01-06
> **关联文档**：[通知系统总体规划](/guide/notification-realtime)

本文档定义通知系统的 HTTP API 契约和 SignalR 事件协议。

## 1. 概述

### 1.1 基础信息

- **基础路径**：`/api/v1/Notification`
- **认证方式**：Bearer Token（JWT）
- **授权策略**：`Client`（需要已认证用户）

### 1.2 通用响应格式

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "操作成功",
  "responseData": { ... }
}
```

### 1.3 错误响应格式

```json
{
  "isSuccess": false,
  "statusCode": 400,
  "messageInfo": "错误描述",
  "responseData": null
}
```

### 1.4 常见错误码

| 状态码 | 说明 |
|-------|------|
| 200 | 成功 |
| 400 | 请求参数错误 |
| 401 | 未认证 |
| 403 | 权限不足 |
| 404 | 资源不存在 |
| 429 | 请求频率过高 |
| 500 | 服务器内部错误 |

---

## 2. HTTP API

### 2.1 获取未读数量

获取当前用户的未读通知数量。

**请求**

```http
GET /api/v1/Notification/UnreadCount
Authorization: Bearer {token}
```

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "获取成功",
  "responseData": {
    "unreadCount": 15
  }
}
```

**说明**

- 此接口作为 SignalR 连接失败时的兜底方案
- 数据优先从 Redis 缓存读取，缓存未命中时查询数据库
- 建议前端优先使用 SignalR 实时推送，避免频繁调用此接口

---

### 2.2 获取通知列表

分页获取当前用户的通知列表。

**请求**

```http
GET /api/v1/Notification/List
Authorization: Bearer {token}
```

**查询参数**

| 参数 | 类型 | 必填 | 默认值 | 说明 |
|-----|------|-----|-------|------|
| `pageIndex` | int | 否 | 1 | 页码（从 1 开始） |
| `pageSize` | int | 否 | 20 | 每页数量（最大 100） |
| `isRead` | bool? | 否 | null | 筛选已读/未读（null 表示全部） |
| `type` | string? | 否 | null | 筛选通知类型 |

**请求示例**

```http
GET /api/v1/Notification/List?pageIndex=1&pageSize=20&isRead=false
Authorization: Bearer {token}
```

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "获取成功",
  "responseData": {
    "page": 1,
    "pageSize": 20,
    "dataCount": 156,
    "pageCount": 8,
    "data": [
      {
        "id": 123456789,
        "type": "CommentReplied",
        "priority": 2,
        "title": "张三 回复了您的评论",
        "content": "张三 说：很有道理，学到了！",
        "businessType": "Comment",
        "businessId": 987654321,
        "triggerId": 111222333,
        "triggerName": "张三",
        "triggerAvatar": "/uploads/avatars/111222333.jpg",
        "isRead": false,
        "readAt": null,
        "link": "/forum/post/555666777#comment-987654321",
        "createdAt": "2026-01-06T10:30:00+08:00"
      },
      {
        "id": 123456788,
        "type": "PostLiked",
        "priority": 1,
        "title": "李四 点赞了您的帖子",
        "content": "您的帖子《如何学习 React》收到了一个赞",
        "businessType": "Post",
        "businessId": 555666777,
        "triggerId": 444555666,
        "triggerName": "李四",
        "triggerAvatar": "/uploads/avatars/444555666.jpg",
        "isRead": true,
        "readAt": "2026-01-06T11:00:00+08:00",
        "link": "/forum/post/555666777",
        "createdAt": "2026-01-06T09:15:00+08:00"
      }
    ]
  }
}
```

**通知类型说明**

| 类型 | 说明 | 优先级 |
|-----|------|--------|
| `CommentReplied` | 评论被回复 | 2 (Normal) |
| `PostLiked` | 帖子被点赞 | 1 (Low) |
| `CommentLiked` | 评论被点赞 | 1 (Low) |
| `Mentioned` | 被 @ 提及 | 3 (High) |
| `GodComment` | 成为神评 | 3 (High) |
| `Sofa` | 成为沙发 | 3 (High) |
| `CoinBalanceChanged` | 萝卜币余额变动 | 2 (Normal) |
| `LevelUp` | 等级提升 | 3 (High) |
| `SystemAnnouncement` | 系统公告 | 4 (Critical) |
| `AccountSecurity` | 账号安全 | 4 (Critical) |

---

### 2.3 获取通知详情

获取单条通知的详细信息。

**请求**

```http
GET /api/v1/Notification/{id}
Authorization: Bearer {token}
```

**路径参数**

| 参数 | 类型 | 说明 |
|-----|------|------|
| `id` | long | 通知 ID |

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "获取成功",
  "responseData": {
    "id": 123456789,
    "type": "CommentReplied",
    "priority": 2,
    "title": "张三 回复了您的评论",
    "content": "张三 说：很有道理，学到了！",
    "businessType": "Comment",
    "businessId": 987654321,
    "triggerId": 111222333,
    "triggerName": "张三",
    "triggerAvatar": "/uploads/avatars/111222333.jpg",
    "isRead": false,
    "readAt": null,
    "link": "/forum/post/555666777#comment-987654321",
    "extData": {
      "postId": 555666777,
      "postTitle": "如何学习 React",
      "commentContent": "很有道理，学到了！"
    },
    "createdAt": "2026-01-06T10:30:00+08:00"
  }
}
```

**错误响应**

```json
{
  "isSuccess": false,
  "statusCode": 404,
  "messageInfo": "通知不存在或已删除",
  "responseData": null
}
```

---

### 2.4 标记已读

标记指定通知为已读。

**请求**

```http
PUT /api/v1/Notification/MarkAsRead
Authorization: Bearer {token}
Content-Type: application/json
```

**请求体**

```json
{
  "notificationIds": [123456789, 123456788, 123456787]
}
```

| 字段 | 类型 | 必填 | 说明 |
|-----|------|-----|------|
| `notificationIds` | long[] | 是 | 通知 ID 列表（最多 100 个） |

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "已标记 3 条通知为已读",
  "responseData": {
    "markedCount": 3,
    "unreadCount": 12
  }
}
```

**说明**

- 已读的通知不会重复标记
- 返回的 `unreadCount` 是更新后的未读数量
- 标记成功后会通过 SignalR 推送 `NotificationRead` 事件到用户的其他端

---

### 2.5 标记全部已读

标记当前用户的所有通知为已读。

**请求**

```http
PUT /api/v1/Notification/MarkAllAsRead
Authorization: Bearer {token}
```

**查询参数（可选）**

| 参数 | 类型 | 必填 | 说明 |
|-----|------|-----|------|
| `type` | string? | 否 | 只标记指定类型的通知 |

**请求示例**

```http
# 标记全部
PUT /api/v1/Notification/MarkAllAsRead

# 只标记点赞类通知
PUT /api/v1/Notification/MarkAllAsRead?type=PostLiked
```

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "已标记所有通知为已读",
  "responseData": {
    "markedCount": 15,
    "unreadCount": 0
  }
}
```

---

### 2.6 删除通知

删除指定的通知（软删除）。

**请求**

```http
DELETE /api/v1/Notification/Delete
Authorization: Bearer {token}
Content-Type: application/json
```

**请求体**

```json
{
  "notificationIds": [123456789, 123456788]
}
```

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "已删除 2 条通知",
  "responseData": {
    "deletedCount": 2
  }
}
```

**说明**

- 使用软删除，不会真正从数据库移除
- 删除后不会影响未读数统计（已删除的通知不计入未读）
- 只能删除自己的通知

---

### 2.7 获取通知偏好设置

获取当前用户的通知偏好设置。

**请求**

```http
GET /api/v1/Notification/Settings
Authorization: Bearer {token}
```

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "获取成功",
  "responseData": {
    "settings": [
      {
        "notificationType": "CommentReplied",
        "isEnabled": true,
        "enableInApp": true,
        "enableEmail": false,
        "enableSound": true
      },
      {
        "notificationType": "PostLiked",
        "isEnabled": true,
        "enableInApp": true,
        "enableEmail": false,
        "enableSound": false
      },
      {
        "notificationType": "SystemAnnouncement",
        "isEnabled": true,
        "enableInApp": true,
        "enableEmail": true,
        "enableSound": true
      }
    ]
  }
}
```

---

### 2.8 更新通知偏好设置

更新当前用户的通知偏好设置。

**请求**

```http
PUT /api/v1/Notification/Settings
Authorization: Bearer {token}
Content-Type: application/json
```

**请求体**

```json
{
  "settings": [
    {
      "notificationType": "PostLiked",
      "isEnabled": false
    },
    {
      "notificationType": "CommentLiked",
      "enableSound": false
    }
  ]
}
```

| 字段 | 类型 | 必填 | 说明 |
|-----|------|-----|------|
| `settings` | array | 是 | 设置项列表 |
| `settings[].notificationType` | string | 是 | 通知类型 |
| `settings[].isEnabled` | bool? | 否 | 是否启用该类型通知 |
| `settings[].enableInApp` | bool? | 否 | 是否启用站内推送 |
| `settings[].enableEmail` | bool? | 否 | 是否启用邮件推送 |
| `settings[].enableSound` | bool? | 否 | 是否启用声音提示 |

**响应**

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "messageInfo": "设置已更新",
  "responseData": null
}
```

---

## 3. SignalR 协议

### 3.1 连接配置

**Hub 路径**

```
/hub/notification
```

**连接建立**

```typescript
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const connection = new HubConnectionBuilder()
  .withUrl('/hub/notification', {
    accessTokenFactory: () => getAccessToken()
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .configureLogging(LogLevel.Information)
  .build();

await connection.start();
```

### 3.2 服务端事件（Server → Client）

#### 3.2.1 UnreadCountChanged

未读数量变化时推送。

**事件名**：`UnreadCountChanged`

**Payload**

```typescript
interface UnreadCountChangedPayload {
  unreadCount: number;
}
```

**示例**

```json
{
  "unreadCount": 15
}
```

**触发时机**

- 新通知生成时
- 通知标记已读时
- 通知删除时
- 用户首次连接时（推送当前未读数）

---

#### 3.2.2 NotificationReceived

新通知到达时推送。

**事件名**：`NotificationReceived`

**Payload**

```typescript
interface NotificationReceivedPayload {
  id: number;
  type: string;
  priority: number;
  title: string;
  content: string;
  businessType: string | null;
  businessId: number | null;
  triggerId: number | null;
  triggerName: string | null;
  triggerAvatar: string | null;
  link: string | null;
  createdAt: string;
}
```

**示例**

```json
{
  "id": 123456789,
  "type": "CommentReplied",
  "priority": 2,
  "title": "张三 回复了您的评论",
  "content": "张三 说：很有道理，学到了！",
  "businessType": "Comment",
  "businessId": 987654321,
  "triggerId": 111222333,
  "triggerName": "张三",
  "triggerAvatar": "/uploads/avatars/111222333.jpg",
  "link": "/forum/post/555666777#comment-987654321",
  "createdAt": "2026-01-06T10:30:00+08:00"
}
```

---

#### 3.2.3 NotificationRead

其他端标记已读时推送（多端同步）。

**事件名**：`NotificationRead`

**Payload**

```typescript
interface NotificationReadPayload {
  notificationIds: number[];
}
```

**示例**

```json
{
  "notificationIds": [123456789, 123456788]
}
```

**说明**

- 当用户在某一端标记已读后，会推送到该用户的其他在线端
- 用于实现多端已读状态同步

---

#### 3.2.4 AllNotificationsRead

其他端标记全部已读时推送。

**事件名**：`AllNotificationsRead`

**Payload**

```typescript
interface AllNotificationsReadPayload {
  // 空对象
}
```

**示例**

```json
{}
```

---

### 3.3 客户端方法（Client → Server）

#### 3.3.1 MarkAsRead

客户端标记通知已读。

**方法名**：`MarkAsRead`

**参数**

| 参数 | 类型 | 说明 |
|-----|------|------|
| `notificationId` | long | 通知 ID |

**调用示例**

```typescript
await connection.invoke('MarkAsRead', 123456789);
```

**说明**

- 调用后服务端会更新数据库和缓存
- 会触发 `NotificationRead` 事件推送到用户的其他端

---

#### 3.3.2 MarkAllAsRead

客户端标记全部已读。

**方法名**：`MarkAllAsRead`

**参数**

无

**调用示例**

```typescript
await connection.invoke('MarkAllAsRead');
```

---

### 3.4 连接生命周期

#### 3.4.1 连接建立

```typescript
connection.onreconnecting((error) => {
  console.log('正在重连...', error);
  // 可以显示重连状态
});

connection.onreconnected((connectionId) => {
  console.log('重连成功', connectionId);
  // 重连后服务端会自动推送当前未读数
});

connection.onclose((error) => {
  console.log('连接关闭', error);
  // 可以启动降级轮询
});
```

#### 3.4.2 重连策略

```typescript
// 自动重连策略：立即、2s、5s、10s、30s
.withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
```

#### 3.4.3 断线补偿

```typescript
connection.onreconnected(async () => {
  // 重连成功后，主动拉取最近 5 分钟的通知
  const since = new Date(Date.now() - 5 * 60 * 1000).toISOString();
  await fetchRecentNotifications(since);
});
```

---

## 4. ViewModel 定义

### 4.1 NotificationVo

```csharp
namespace Radish.Model.ViewModels;

/// <summary>
/// 通知 ViewModel
/// </summary>
public class NotificationVo
{
    /// <summary>
    /// 通知 ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 通知类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 优先级（1-4）
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型
    /// </summary>
    public string? BusinessType { get; set; }

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? BusinessId { get; set; }

    /// <summary>
    /// 触发者 ID
    /// </summary>
    public long? TriggerId { get; set; }

    /// <summary>
    /// 触发者名称
    /// </summary>
    public string? TriggerName { get; set; }

    /// <summary>
    /// 触发者头像
    /// </summary>
    public string? TriggerAvatar { get; set; }

    /// <summary>
    /// 是否已读
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 已读时间
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// 跳转链接
    /// </summary>
    public string? Link { get; set; }

    /// <summary>
    /// 扩展数据
    /// </summary>
    public object? ExtData { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
```

### 4.2 NotificationListVo

```csharp
/// <summary>
/// 通知列表 ViewModel（分页）
/// </summary>
public class NotificationListVo
{
    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总数量
    /// </summary>
    public int DataCount { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 通知列表
    /// </summary>
    public List<NotificationVo> Data { get; set; } = new();
}
```

### 4.3 NotificationSettingVo

```csharp
/// <summary>
/// 通知偏好设置 ViewModel
/// </summary>
public class NotificationSettingVo
{
    /// <summary>
    /// 通知类型
    /// </summary>
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否启用站内推送
    /// </summary>
    public bool EnableInApp { get; set; }

    /// <summary>
    /// 是否启用邮件推送
    /// </summary>
    public bool EnableEmail { get; set; }

    /// <summary>
    /// 是否启用声音提示
    /// </summary>
    public bool EnableSound { get; set; }
}
```

---

## 5. 请求/响应 DTO

### 5.1 MarkAsReadDto

```csharp
/// <summary>
/// 标记已读请求
/// </summary>
public class MarkAsReadDto
{
    /// <summary>
    /// 通知 ID 列表
    /// </summary>
    [Required]
    [MaxLength(100, ErrorMessage = "一次最多标记 100 条通知")]
    public List<long> NotificationIds { get; set; } = new();
}
```

### 5.2 DeleteNotificationDto

```csharp
/// <summary>
/// 删除通知请求
/// </summary>
public class DeleteNotificationDto
{
    /// <summary>
    /// 通知 ID 列表
    /// </summary>
    [Required]
    [MaxLength(100, ErrorMessage = "一次最多删除 100 条通知")]
    public List<long> NotificationIds { get; set; } = new();
}
```

### 5.3 UpdateSettingsDto

```csharp
/// <summary>
/// 更新通知设置请求
/// </summary>
public class UpdateSettingsDto
{
    /// <summary>
    /// 设置项列表
    /// </summary>
    [Required]
    public List<NotificationSettingItemDto> Settings { get; set; } = new();
}

/// <summary>
/// 单个设置项
/// </summary>
public class NotificationSettingItemDto
{
    /// <summary>
    /// 通知类型
    /// </summary>
    [Required]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// 是否启用站内推送
    /// </summary>
    public bool? EnableInApp { get; set; }

    /// <summary>
    /// 是否启用邮件推送
    /// </summary>
    public bool? EnableEmail { get; set; }

    /// <summary>
    /// 是否启用声音提示
    /// </summary>
    public bool? EnableSound { get; set; }
}
```

---

## 6. TypeScript 类型定义

供前端使用的类型定义文件。

```typescript
// types/notification.ts

/** 通知类型 */
export type NotificationType =
  | 'CommentReplied'
  | 'PostLiked'
  | 'CommentLiked'
  | 'Mentioned'
  | 'GodComment'
  | 'Sofa'
  | 'CoinBalanceChanged'
  | 'LevelUp'
  | 'SystemAnnouncement'
  | 'AccountSecurity';

/** 通知优先级 */
export enum NotificationPriority {
  Low = 1,
  Normal = 2,
  High = 3,
  Critical = 4
}

/** 通知 ViewModel */
export interface NotificationVo {
  id: number;
  type: NotificationType;
  priority: NotificationPriority;
  title: string;
  content: string;
  businessType: string | null;
  businessId: number | null;
  triggerId: number | null;
  triggerName: string | null;
  triggerAvatar: string | null;
  isRead: boolean;
  readAt: string | null;
  link: string | null;
  extData: Record<string, unknown> | null;
  createdAt: string;
}

/** 通知列表响应 */
export interface NotificationListResponse {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: NotificationVo[];
}

/** 未读数响应 */
export interface UnreadCountResponse {
  unreadCount: number;
}

/** 标记已读响应 */
export interface MarkAsReadResponse {
  markedCount: number;
  unreadCount: number;
}

/** 删除通知响应 */
export interface DeleteNotificationResponse {
  deletedCount: number;
}

/** 通知设置项 */
export interface NotificationSettingVo {
  notificationType: NotificationType;
  isEnabled: boolean;
  enableInApp: boolean;
  enableEmail: boolean;
  enableSound: boolean;
}

/** 通知设置响应 */
export interface NotificationSettingsResponse {
  settings: NotificationSettingVo[];
}

// SignalR 事件 Payload

/** UnreadCountChanged 事件 */
export interface UnreadCountChangedPayload {
  unreadCount: number;
}

/** NotificationReceived 事件 */
export interface NotificationReceivedPayload {
  id: number;
  type: NotificationType;
  priority: NotificationPriority;
  title: string;
  content: string;
  businessType: string | null;
  businessId: number | null;
  triggerId: number | null;
  triggerName: string | null;
  triggerAvatar: string | null;
  link: string | null;
  createdAt: string;
}

/** NotificationRead 事件 */
export interface NotificationReadPayload {
  notificationIds: number[];
}

/** AllNotificationsRead 事件 */
export interface AllNotificationsReadPayload {
  // 空对象
}
```

---

## 7. HTTP 调试脚本

```http
### 获取未读数量
GET {{baseUrl}}/api/v1/Notification/UnreadCount
Authorization: Bearer {{accessToken}}

### 获取通知列表
GET {{baseUrl}}/api/v1/Notification/List?pageIndex=1&pageSize=20
Authorization: Bearer {{accessToken}}

### 获取未读通知列表
GET {{baseUrl}}/api/v1/Notification/List?pageIndex=1&pageSize=20&isRead=false
Authorization: Bearer {{accessToken}}

### 获取通知详情
GET {{baseUrl}}/api/v1/Notification/123456789
Authorization: Bearer {{accessToken}}

### 标记已读
PUT {{baseUrl}}/api/v1/Notification/MarkAsRead
Authorization: Bearer {{accessToken}}
Content-Type: application/json

{
  "notificationIds": [123456789, 123456788]
}

### 标记全部已读
PUT {{baseUrl}}/api/v1/Notification/MarkAllAsRead
Authorization: Bearer {{accessToken}}

### 删除通知
DELETE {{baseUrl}}/api/v1/Notification/Delete
Authorization: Bearer {{accessToken}}
Content-Type: application/json

{
  "notificationIds": [123456789]
}

### 获取通知设置
GET {{baseUrl}}/api/v1/Notification/Settings
Authorization: Bearer {{accessToken}}

### 更新通知设置
PUT {{baseUrl}}/api/v1/Notification/Settings
Authorization: Bearer {{accessToken}}
Content-Type: application/json

{
  "settings": [
    {
      "notificationType": "PostLiked",
      "enableSound": false
    }
  ]
}
```

---

**文档版本**：v1.0
**状态**：待实施
**关联文档**：
- [通知系统总体规划](/guide/notification-realtime)
- [通知系统实现细节](/guide/notification-implementation)
- [通知系统前端集成指南](/guide/notification-frontend)
