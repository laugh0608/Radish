# 通知系统实时推送方案（SignalR / WebSocket 规划）

> 目标：将 `api/v1/User/GetUnreadMessageCount` 的前端轮询升级为服务端实时推送，降低无效请求与并发占用，并为后续“站内通知/私信/公告/积分变动提醒”等提供统一基础设施。
>
> 备注：本文为**规划文档**，不包含具体代码实现。

## 1. 背景与问题

### 1.1 当前现状（占位 + 轮询）

- 后端：`GetUnreadMessageCount()` 为占位实现，固定返回 `unreadCount = 0`。
  - 位置：`Radish.Api/Controllers/UserController.cs:614`
- 前端：Dock 使用轮询获取未读数。
  - 位置：`radish.client/src/desktop/Dock.tsx:172`
  - 行为：初始化拉取一次 + 每 30 秒拉取一次

### 1.2 轮询的主要问题

- **不优雅且浪费**：大量请求在“无变化”时仍持续触发。
- **并发与限流风险**：在线用户上升后，轮询会显著抬升 API QPS，并更容易触发速率限制。
- **体验限制**：30 秒粒度无法做到“秒级到达”。

## 2. 目标与范围

### 2.1 目标

- 以实时推送替代前端轮询，实现未读数“变化即到达”。
- 推送能力沉淀为统一基础设施，支持：评论/点赞/@提及通知、系统公告、积分变动提醒等。

### 2.2 分阶段范围（建议）

- **P0（最小闭环）**：仅实现“未读数推送”，替换轮询。
- **P1（可用通知系统）**：通知数据模型 + 通知列表 API + 已读/未读状态维护 + 推送。
- **P2（体验增强）**：多端同步、通知聚合、偏好设置、分组订阅、容量治理等。

## 3. 技术方案：优先使用 SignalR（WebSocket）

> Radish 在架构文档中已明确“实时交互统一使用 SignalR Hub 承载”，避免重复造轮子。

- 参考：[开发框架说明](/architecture/framework) 的“实时交互（SignalR）”章节
- 参考：[开发规范](/architecture/specifications) 的“SignalR 实时交互规范”章节

### 3.1 为什么不是纯轮询

- 轮询将“用户在线”转化为持续请求；推送将“状态变化”转化为下行消息。
- 对未读数这类“变化频率较低，但在线用户多”的场景，推送更贴合。

### 3.2 为什么不是原生 WebSocket

- SignalR 默认使用 WebSocket，并提供自动降级（SSE/Long Polling）、重连、分组、用户映射等成熟能力。
- .NET 生态里 SignalR 的工程化成本更低，更符合“先落地再演进”的策略。

## 4. 总体架构

### 4.1 服务端（Radish.Api）

- 新增 `NotificationHub`（名称示例），用于下行事件推送。
- 业务层（Service）在事件发生后，通过注入 `IHubContext<...>` 推送。
- 原则：**Controller 不直接推送**，推送属于业务编排的一部分。

### 4.2 网关（Radish.Gateway）

- Gateway 当前开启 WebSocket 主要用于 Vite HMR：`Radish.Gateway/Program.cs:110`
- 若未来通过 Gateway 访问 Hub：
  - 需要为 Hub 路径增加 YARP 路由（例如 `/hub/{**catch-all}` 或更具体的 `/hub/notification/{**catch-all}`）
  - 需要验证 WebSocket Upgrade 是否正常透传

### 4.3 客户端（radish.client）

- 新增 SignalR 连接封装（Hook 或模块），统一处理：
  - 连接创建
  - Token 附带
  - 自动重连
  - 订阅/退订
- Dock 订阅 `UnreadCountChanged` 并更新 UI。

## 5. 协议与事件设计（P0）

### 5.1 Hub 路径（建议）

- Hub URL：`/hub/notification`

> 最终路径可根据网关路由与现有路径风格调整，关键是保持稳定与可扩展。

### 5.2 下行事件（Server -> Client）

- `UnreadCountChanged`
  - payload：`{ unreadCount: number }`

### 5.3 鉴权策略

- Hub 仅允许已认证用户连接（与现有 `Client` Policy 一致）。
- 连接建立时附带 JWT Access Token（SignalR 支持 `accessTokenFactory`）。
- `OnConnectedAsync` 中进行：
  - 用户身份校验
  - 租户校验（若需要）
  - 将连接加入用户组：`user:{userId}`

### 5.4 前端实现细节（P0）

#### 5.4.1 SignalR 连接配置

**连接建立示例**：

```typescript
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';

// 创建连接
const connection = new HubConnectionBuilder()
  .withUrl('/hub/notification', {
    accessTokenFactory: () => localStorage.getItem('access_token') || ''
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])  // 重连策略：立即、2s、5s、10s、30s
  .configureLogging(LogLevel.Information)
  .build();

// 订阅事件
connection.on('UnreadCountChanged', (data: { unreadCount: number }) => {
  setUnreadMessages(data.unreadCount);
});

// 启动连接
await connection.start();
```

#### 5.4.2 Hook 封装建议

**位置**：`radish.client/src/shared/signalr/useNotificationHub.ts`

**功能**：
- 自动管理连接生命周期
- 处理重连逻辑
- 提供订阅/退订接口
- 连接状态管理

**使用示例**：

```typescript
// 在 Dock 组件中使用
const { unreadCount, connectionState } = useNotificationHub();

// 显示连接状态（可选）
{connectionState === 'Disconnected' && (
  <div className={styles.offlineIndicator}>离线</div>
)}
```

#### 5.4.3 UI 交互建议

**Dock Badge 更新**（已有）：
- 实时更新未读数
- 数字超过 99 显示为 "99+"

**Toast 提示**（新增）：
- 新通知到达时显示 Toast
- 3 秒后自动消失
- 点击跳转到通知详情

**通知中心**（P1）：
- 点击 Dock badge 展开通知列表
- 支持标记已读/全部已读
- 支持删除通知

#### 5.4.4 降级策略（兜底）

**连接失败时**：
- 降级为每 60 秒轮询一次（比原来 30 秒更低频）
- 显示"离线"状态提示
- 用户可手动刷新

**实现示例**：

```typescript
const [isFallbackMode, setIsFallbackMode] = useState(false);

useEffect(() => {
  if (connectionState === 'Disconnected') {
    setIsFallbackMode(true);
    // 启动低频轮询
    const timer = setInterval(() => {
      void fetchUnreadMessageCount();
    }, 60000);
    return () => clearInterval(timer);
  } else {
    setIsFallbackMode(false);
  }
}, [connectionState]);
```

## 6. 数据模型规划（P1：真实通知系统）

> P0 可以只做未读数推送（甚至先用模拟/占位数据），但一旦要做真实通知列表与已读状态，就需要落库。

### 6.1 核心表（建议）

#### 6.1.1 Notification（通知表）

```csharp
[SugarTable("Notification")]
public class Notification : RootEntityTKey<long>
{
    /// <summary>
    /// 租户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    /// <summary>
    /// 通知类型（CommentReplied / PostLiked / Mentioned / SystemAnnouncement / CoinBalanceChanged）
    /// </summary>
    [SugarColumn(ColumnDescription = "通知类型", Length = 50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 通知标题
    /// </summary>
    [SugarColumn(ColumnDescription = "通知标题", Length = 200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知内容
    /// </summary>
    [SugarColumn(ColumnDescription = "通知内容", Length = 1000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型（Post / Comment / User / System）
    /// </summary>
    [SugarColumn(ColumnDescription = "业务类型", Length = 50, IsNullable = true)]
    public string? BusinessType { get; set; }

    /// <summary>
    /// 业务ID（帖子ID、评论ID等）
    /// </summary>
    [SugarColumn(ColumnDescription = "业务ID", IsNullable = true)]
    public long? BusinessId { get; set; }

    /// <summary>
    /// 触发者ID（谁触发了这个通知）
    /// </summary>
    [SugarColumn(ColumnDescription = "触发者ID", IsNullable = true)]
    public long? TriggerId { get; set; }

    /// <summary>
    /// 触发者名称（冗余字段，避免 JOIN）
    /// </summary>
    [SugarColumn(ColumnDescription = "触发者名称", Length = 100, IsNullable = true)]
    public string? TriggerName { get; set; }

    /// <summary>
    /// 扩展数据（JSON 格式）
    /// </summary>
    [SugarColumn(ColumnDescription = "扩展数据", ColumnDataType = "text", IsNullable = true)]
    public string? ExtData { get; set; }

    // 索引
    [SugarIndex("idx_tenant_type", nameof(TenantId), OrderByType.Asc)]
    [SugarIndex("idx_created_at", nameof(CreateTime), OrderByType.Desc)]
}
```

#### 6.1.2 UserNotification（用户通知关系表）

```csharp
[SugarTable("UserNotification")]
public class UserNotification : RootEntityTKey<long>
{
    /// <summary>
    /// 租户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>
    /// 通知ID
    /// </summary>
    [SugarColumn(ColumnDescription = "通知ID")]
    public long NotificationId { get; set; }

    /// <summary>
    /// 是否已读
    /// </summary>
    [SugarColumn(ColumnDescription = "是否已读")]
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// 已读时间
    /// </summary>
    [SugarColumn(ColumnDescription = "已读时间", IsNullable = true)]
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// 推送状态（Created / Delivered / Failed）
    /// </summary>
    [SugarColumn(ColumnDescription = "推送状态", Length = 20)]
    public string DeliveryStatus { get; set; } = "Created";

    /// <summary>
    /// 推送时间
    /// </summary>
    [SugarColumn(ColumnDescription = "推送时间", IsNullable = true)]
    public DateTime? DeliveredAt { get; set; }

    // 索引
    [SugarIndex("idx_user_read", nameof(UserId), OrderByType.Asc)]
    [SugarIndex("idx_notification", nameof(NotificationId), OrderByType.Asc)]
    [SugarIndex("idx_created_at", nameof(CreateTime), OrderByType.Desc)]
}
```

### 6.2 未读数计算策略

#### 6.2.1 写扩散方案（推荐）

**优点**：
- 查询性能高（直接读缓存）
- 适合读多写少场景

**实现**：
- 每次通知生成时，更新接收者的未读计数（Redis + DB）
- 标记已读时，递减未读计数

**缓存 Key 设计**：
```
notification:unread:{tenantId}:{userId} -> 未读数（整数）
```

**一致性保障**：
- 定时任务（每小时）校验缓存与 DB 的一致性
- 不一致时以 DB 为准，重建缓存

#### 6.2.2 读计算方案（简单但慢）

**优点**：
- 实现简单
- 数据一致性强

**缺点**：
- 每次查询都需要 `COUNT()`
- 用户量大时性能差

**不推荐**用于生产环境。

### 6.3 ViewModel 设计

#### 6.3.1 NotificationVo

```csharp
public class NotificationVo
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? BusinessType { get; set; }
    public long? BusinessId { get; set; }
    public long? TriggerId { get; set; }
    public string? TriggerName { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 6.4 API 契约设计（P1）

#### 6.4.1 获取通知列表

```http
GET /api/v1/Notification/GetList
Authorization: Bearer {token}
Query Parameters:
  - pageIndex: int (默认 1)
  - pageSize: int (默认 20, 最大 100)
  - isRead: bool? (可选，筛选已读/未读)
  - type: string? (可选，筛选通知类型)

Response:
{
  "isSuccess": true,
  "responseData": {
    "page": 1,
    "pageSize": 20,
    "dataCount": 156,
    "pageCount": 8,
    "data": [
      {
        "id": 123,
        "type": "CommentReplied",
        "title": "您的评论收到了新回复",
        "content": "用户 @张三 回复了您的评论：很有道理！",
        "businessType": "Comment",
        "businessId": 456,
        "triggerId": 789,
        "triggerName": "张三",
        "isRead": false,
        "readAt": null,
        "createdAt": "2026-01-02T10:30:00Z"
      }
    ]
  }
}
```

#### 6.4.2 标记已读

```http
POST /api/v1/Notification/MarkAsRead
Authorization: Bearer {token}
Content-Type: application/json

Body:
{
  "notificationIds": [123, 456, 789]
}

Response:
{
  "isSuccess": true,
  "messageInfo": "已标记 3 条通知为已读"
}
```

#### 6.4.3 全部标记已读

```http
POST /api/v1/Notification/MarkAllAsRead
Authorization: Bearer {token}

Response:
{
  "isSuccess": true,
  "messageInfo": "已标记所有通知为已读"
}
```

#### 6.4.4 删除通知

```http
POST /api/v1/Notification/Delete
Authorization: Bearer {token}
Content-Type: application/json

Body:
{
  "notificationIds": [123, 456]
}

Response:
{
  "isSuccess": true,
  "messageInfo": "已删除 2 条通知"
}
```

#### 6.4.5 获取未读数（兜底）

```http
GET /api/v1/Notification/GetUnreadCount
Authorization: Bearer {token}

Response:
{
  "isSuccess": true,
  "responseData": {
    "unreadCount": 15
  }
}
```

## 7. 推送触发点（从业务事件出发）

### 7.1 业务事件与通知类型映射

| 业务事件 | 触发位置（Service/Controller） | 通知类型 | 接收者 | 优先级 | 备注 |
|---------|-------------------------------|---------|--------|--------|------|
| **评论被回复** | `CommentService.AddCommentAsync` | `CommentReplied` | 父评论作者 | P1 | 不通知自己回复自己 |
| **帖子被点赞** | `PostService.ToggleLikeAsync` | `PostLiked` | 帖子作者 | P1 | 取消点赞不通知 |
| **评论被点赞** | `CommentService.ToggleLikeAsync` | `CommentLiked` | 评论作者 | P1 | 取消点赞不通知 |
| **@ 提及** | `PostService.PublishPostAsync`<br>`CommentService.AddCommentAsync` | `Mentioned` | 被提及用户 | P2 | 需解析内容中的 @用户名 |
| **成为神评** | `CommentHighlightJob.ExecuteAsync` | `HighlightAchieved` | 评论作者 | P2 | 类型：GodComment |
| **成为沙发** | `CommentHighlightJob.ExecuteAsync` | `HighlightAchieved` | 评论作者 | P2 | 类型：Sofa |
| **积分变动** | `CoinService.*` | `CoinBalanceChanged` | 用户 | P2 | 参考萝卜币文档 |
| **系统公告** | `AnnouncementService.PublishAsync` | `SystemAnnouncement` | 全体用户/指定用户 | P2 | 支持定向推送 |

### 7.2 通知生成规则

#### 7.2.1 去重规则

**同一事件短时间内不重复通知**：

- 评论被回复：同一父评论下，同一回复者 5 分钟内只通知一次
- 帖子被点赞：同一帖子，同一点赞者 1 小时内只通知一次
- 评论被点赞：同一评论，同一点赞者 1 小时内只通知一次

**实现方式**：
```
缓存 Key: notification:dedup:{type}:{businessId}:{triggerId}
TTL: 5 分钟 / 1 小时
```

#### 7.2.2 自己不通知自己

- 用户回复自己的评论：不通知
- 用户点赞自己的帖子/评论：不通知
- 用户 @ 自己：不通知

#### 7.2.3 通知聚合（P2）

**场景**：同一帖子短时间内收到多次点赞

**策略**：
- 5 分钟内的多次点赞合并为一条通知
- 通知内容：`张三、李四等 5 人点赞了您的帖子`

### 7.3 实现原则

- **通知生成与推送由 Service 层完成**（不在 Controller）
- **推送消息尽量小**：优先推未读数变化，列表通过 API 拉取
- **异步化**：通知生成不阻塞主业务流程（使用后台任务或消息队列）

## 8. 性能与容量规划

### 8.1 容量估算模型

**假设条件**：
- 在线用户：10,000
- 每用户平均每小时收到通知：5 条
- 推送消息大小：200 字节

**估算结果**：
- **并发连接数**：10,000（每个在线用户 1 个 SignalR 连接）
- **每小时推送消息数**：50,000 条
- **每秒推送消息数**：~14 条（平均）
- **峰值推送消息数**：~100 条/秒（假设 7 倍峰值）
- **带宽需求**：~20 KB/s（平均），~140 KB/s（峰值）

**扩展性评估**：
- 单实例 ASP.NET Core 可支撑 10,000-50,000 并发连接
- Redis Backplane 单实例可支撑 100,000+ 连接
- 建议使用 Redis Sentinel 或 Cluster 保证高可用

### 8.2 限流与保护策略

#### 8.2.1 连接速率限制

**目标**：防止恶意反复连接

**策略**：
- 同一 IP 每分钟最多建立 10 个连接
- 同一用户每分钟最多建立 5 个连接

**实现**：
```csharp
// 在 OnConnectedAsync 中检查
var connectionKey = $"signalr:conn:{userId}:{DateTime.Now:yyyyMMddHHmm}";
var connectionCount = await _cache.IncrementAsync(connectionKey, 1, TimeSpan.FromMinutes(1));
if (connectionCount > 5)
{
    Context.Abort();
    throw new HubException("连接频率过高，请稍后再试");
}
```

#### 8.2.2 消息体大小限制

**目标**：避免大 payload 占用带宽与内存

**策略**：
- 单条推送消息最大 1 KB
- 超过限制时只推送未读数，不推送详细内容

#### 8.2.3 订阅范围限制

**目标**：禁止订阅其他用户的消息

**策略**：
- 用户只能订阅自己的通知组：`user:{userId}`
- Hub 方法中验证 `Context.UserIdentifier` 与请求参数一致

### 8.3 多实例部署配置

#### 8.3.1 Redis Backplane 配置

**安装依赖**：
```bash
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis
```

**配置示例**（`Program.cs`）：
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.EndPoints.Add("localhost", 6379);
        options.Configuration.Password = "your-redis-password";
        options.Configuration.ChannelPrefix = "radish:signalr:";
    });
```

#### 8.3.2 负载均衡配置

**Nginx 配置示例**：
```nginx
upstream radish_api {
    server api1.example.com:5100;
    server api2.example.com:5100;
    server api3.example.com:5100;
}

server {
    listen 443 ssl;
    server_name api.example.com;

    location /hub/ {
        proxy_pass http://radish_api;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_read_timeout 3600s;
    }
}
```

### 8.4 降级与容错策略

#### 8.4.1 降级策略

**触发条件**：
1. SignalR 连接失败
2. 推送失败率 > 10%
3. 消息积压 > 1000 条

**降级措施**：
1. **连接失败** → 降级为每 60 秒轮询一次（比原来 30 秒更低频）
2. **推送失败** → 记录到失败队列，定时重试（最多 3 次）
3. **消息积压** → 只推送未读数，不推送详细内容

#### 8.4.2 容错机制

**客户端断线重连后的补偿**：
```typescript
connection.onreconnected(async () => {
  // 重连后主动拉取最近 5 分钟的通知
  const lastFetchTime = Date.now() - 5 * 60 * 1000;
  await fetchNotificationsSince(lastFetchTime);
});
```

**服务端推送失败的补偿**：
- 推送失败时，标记 `DeliveryStatus = "Failed"`
- 用户下次连接时，补发所有 `Failed` 状态的通知
- 最多重试 3 次，超过后标记为 `Abandoned`

**通知生成与推送解耦**：
- 通知生成：同步写入 DB + 缓存
- 通知推送：异步通过后台任务或消息队列
- 即使推送失败，通知也已保存，用户可通过 API 拉取

## 9. 监控与运维

### 9.1 关键监控指标

| 指标 | 说明 | 告警阈值 | 监控方式 |
|-----|------|---------|---------|
| **在线连接数** | 当前 SignalR 连接数 | > 80% 容量 | Prometheus + Grafana |
| **推送成功率** | 推送成功 / 推送总数 | < 95% | 应用日志 + APM |
| **推送延迟** | 事件发生到推送到达的时间 | P95 > 3s | 自定义埋点 |
| **重连频率** | 每分钟重连次数 | > 100 次/分钟 | SignalR 日志 |
| **消息积压** | 待推送消息队列长度 | > 1000 | Redis 队列长度 |
| **未读数一致性** | 缓存与 DB 的差异率 | > 5% | 定时校验任务 |

### 9.2 日志记录

**关键日志点**：
1. 连接建立/断开（包含 UserId、ConnectionId、IP）
2. 推送成功/失败（包含 NotificationId、UserId、错误原因）
3. 降级触发（包含触发原因、降级策略）
4. 容量告警（包含当前连接数、推送 QPS）

**日志级别**：
- 连接建立/断开：Information
- 推送成功：Debug
- 推送失败：Warning
- 降级触发：Warning
- 容量告警：Error

### 9.3 运维工具

#### 9.3.1 管理后台功能（建议）

**通知管理**：
- 查看所有通知（分页、筛选）
- 手动推送系统公告
- 查看推送失败记录
- 重试失败推送

**连接管理**：
- 查看在线用户列表
- 强制断开指定连接
- 查看连接历史

**监控面板**：
- 实时连接数曲线
- 推送成功率曲线
- 推送延迟分布
- 错误日志聚合

#### 9.3.2 故障排查清单

**问题：用户收不到推送**

排查步骤：
1. 检查用户是否在线（SignalR 连接状态）
2. 检查通知是否生成（查询 `Notification` 表）
3. 检查推送状态（查询 `UserNotification.DeliveryStatus`）
4. 检查 Hub 日志（是否有推送失败记录）
5. 检查客户端日志（是否有连接断开记录）

**问题：推送延迟高**

排查步骤：
1. 检查 Redis Backplane 延迟（`redis-cli --latency`）
2. 检查服务器 CPU/内存使用率
3. 检查推送消息队列积压情况
4. 检查网络带宽使用情况

## 10. 测试策略

### 10.1 单元测试

**测试范围**：
- Hub 方法的鉴权测试
- 通知生成逻辑测试
- 未读数计算测试
- 去重规则测试

**示例**：
```csharp
[Fact]
public async Task NotificationService_ShouldNotNotifySelf()
{
    // Arrange
    var userId = 123;
    var commentId = 456;

    // Act
    await _notificationService.NotifyCommentReplied(commentId, userId, userId);

    // Assert
    var notifications = await _notificationRepository.QueryAsync(n => n.TriggerId == userId);
    notifications.Should().BeEmpty(); // 不应该通知自己
}
```

### 10.2 集成测试

**测试范围**：
- 客户端连接与订阅测试
- 推送消息接收测试
- 断线重连测试
- 降级策略测试

**示例**：
```csharp
[Fact]
public async Task SignalR_ShouldReceiveUnreadCountChanged()
{
    // Arrange
    var connection = await CreateTestConnection();
    var receivedCount = 0;

    connection.On<int>("UnreadCountChanged", count =>
    {
        receivedCount = count;
    });

    // Act
    await _notificationService.CreateNotificationAsync(testUserId, "Test");

    // Assert
    await Task.Delay(1000); // 等待推送
    receivedCount.Should().Be(1);
}
```

### 10.3 压力测试

**测试场景**：
1. **10,000 并发连接测试**
   - 工具：SignalR 压测工具 / JMeter
   - 目标：连接成功率 > 99%

2. **100 条/秒推送测试**
   - 工具：自定义压测脚本
   - 目标：推送延迟 P95 < 3s

3. **长时间稳定性测试**
   - 时长：24 小时
   - 目标：无内存泄漏、无连接泄漏

**压测脚本示例**（C#）：
```csharp
public async Task StressTest_10000Connections()
{
    var tasks = new List<Task>();
    for (int i = 0; i < 10000; i++)
    {
        tasks.Add(Task.Run(async () =>
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5000/hub/notification")
                .Build();

            await connection.StartAsync();
            await Task.Delay(TimeSpan.FromHours(1)); // 保持连接 1 小时
            await connection.StopAsync();
        }));
    }

    await Task.WhenAll(tasks);
}
```

## 11. 落地实施清单

### 11.1 P0（替换轮询）- 预计 3-5 天

**目标**：实现未读数实时推送，替换现有轮询机制

**任务清单**：
1. **后端 Hub 实现**
   - [ ] 创建 `NotificationHub.cs`（位置：`Radish.Api/Hubs/`）
   - [ ] 实现 `OnConnectedAsync`（鉴权、用户分组）
   - [ ] 实现 `OnDisconnectedAsync`（清理连接）
   - [ ] 定义 `UnreadCountChanged` 事件

2. **后端推送逻辑**
   - [ ] 创建 `INotificationPushService` 接口
   - [ ] 实现推送服务（注入 `IHubContext<NotificationHub>`）
   - [ ] 在 `Program.cs` 中注册 SignalR：`builder.Services.AddSignalR()`
   - [ ] 映射 Hub 端点：`app.MapHub<NotificationHub>("/hub/notification")`

3. **前端 Hook 封装**
   - [ ] 安装依赖：`npm install @microsoft/signalr --workspace=radish.client`
   - [ ] 创建 `useNotificationHub.ts`（位置：`radish.client/src/shared/signalr/`）
   - [ ] 实现连接管理、事件订阅、状态管理
   - [ ] 实现降级策略（连接失败时轮询）

4. **Dock 集成**
   - [ ] 替换现有轮询逻辑为 `useNotificationHub`
   - [ ] 订阅 `UnreadCountChanged` 事件
   - [ ] 更新 UI（badge 数字）
   - [ ] 添加连接状态指示器（可选）

5. **Gateway 路由配置**（如果通过 Gateway 访问）
   - [ ] 在 `Radish.Gateway/appsettings.json` 添加 Hub 路由
   - [ ] 验证 WebSocket Upgrade 正常透传

6. **测试验证**
   - [ ] 手动测试：连接建立、推送接收、断线重连
   - [ ] 压测：100 并发连接
   - [ ] 兼容性测试：Chrome、Firefox、Safari

### 11.2 P1（通知系统可用）- 预计 5-7 天

**目标**：实现完整的通知列表、已读状态、业务事件集成

**任务清单**：
1. **数据模型**
   - [ ] 创建 `Notification` 实体（位置：`Radish.Model/Models/`）
   - [ ] 创建 `UserNotification` 实体
   - [ ] 创建 `NotificationVo`（位置：`Radish.Model/ViewModels/`）
   - [ ] 配置 AutoMapper 映射

2. **Repository 层**
   - [ ] 创建 `INotificationRepository` 接口
   - [ ] 实现 `NotificationRepository`
   - [ ] 创建 `IUserNotificationRepository` 接口
   - [ ] 实现 `UserNotificationRepository`

3. **Service 层**
   - [ ] 创建 `INotificationService` 接口
   - [ ] 实现 `NotificationService`（通知生成、推送、已读）
   - [ ] 实现未读数缓存策略（Redis + DB）
   - [ ] 实现去重规则（缓存 Key）

4. **API 接口**
   - [ ] `GET /api/v1/Notification/GetList`（通知列表）
   - [ ] `POST /api/v1/Notification/MarkAsRead`（标记已读）
   - [ ] `POST /api/v1/Notification/MarkAllAsRead`（全部已读）
   - [ ] `POST /api/v1/Notification/Delete`（删除通知）
   - [ ] `GET /api/v1/Notification/GetUnreadCount`（兜底）

5. **业务事件集成**
   - [ ] 评论被回复：`CommentService.AddCommentAsync`
   - [ ] 帖子被点赞：`PostService.ToggleLikeAsync`
   - [ ] 评论被点赞：`CommentService.ToggleLikeAsync`
   - [ ] 成为神评/沙发：`CommentHighlightJob.ExecuteAsync`

6. **前端通知中心**
   - [ ] 创建通知列表组件
   - [ ] 实现标记已读功能
   - [ ] 实现删除功能
   - [ ] Toast 提示（新通知到达）

7. **测试**
   - [ ] 单元测试：通知生成、去重、已读
   - [ ] 集成测试：端到端推送流程
   - [ ] 压测：1000 并发连接 + 100 条/秒推送

### 11.3 P2（体验增强）- 预计 3-5 天

**目标**：多端同步、通知聚合、偏好设置、完整监控

**任务清单**：
1. **多端已读同步**
   - [ ] 实现跨设备已读状态同步
   - [ ] 推送已读状态变化事件

2. **通知聚合**
   - [ ] 实现同类通知合并逻辑
   - [ ] 优化通知内容展示

3. **用户偏好设置**
   - [ ] 创建通知偏好表
   - [ ] 实现偏好设置 API
   - [ ] 前端偏好设置页面

4. **监控与告警**
   - [ ] 集成 Prometheus 指标
   - [ ] 配置 Grafana 面板
   - [ ] 设置告警规则

5. **管理后台**
   - [ ] 通知管理页面
   - [ ] 连接管理页面
   - [ ] 监控面板

6. **压力测试与优化**
   - [ ] 10,000 并发连接测试
   - [ ] 24 小时稳定性测试
   - [ ] 性能优化（根据压测结果）

## 12. 与现有代码/文档的关联索引

- **未读数占位接口**：`Radish.Api/Controllers/UserController.cs:614`
- **Dock 轮询实现**：`radish.client/src/desktop/Dock.tsx:172`
- **SignalR 规划与规范**：
  - [开发框架说明](/architecture/framework)（"实时交互（SignalR）"章节）
  - `radish.docs/docs/architecture/specifications.md`（"SignalR 实时交互规范"章节）
- **Gateway WebSocket 现状**：`Radish.Gateway/Program.cs:110`
- **萝卜币实时推送示例**：`radish.docs/docs/guide/radish-coin-system.md`（第 16.9 节）
- **神评/沙发功能**：`radish.docs/docs/features/comment-highlight.md`

---

## 13. 待确认问题（建议在真正开工前定稿）

### 13.1 功能范围

- [ ] 通知系统是否需要"站内信/会话（私信）"能力？还是仅做"事件通知"？
- [ ] 未读计数是否要按分类拆分（例如 评论/点赞/@提及 分开计数）？
- [ ] 推送与拉取的职责边界：推送未读数 + 列表通过 API 拉取（推荐），还是推送完整通知内容？

### 13.2 多租户与隔离

- [ ] 通知是否跨租户隔离（默认应隔离）？
- [ ] 系统公告是否支持跨租户推送（例如平台级公告）？

### 13.3 性能与容量

- [ ] 预期在线用户峰值是多少？（影响容量规划）
- [ ] 是否需要支持多实例部署？（影响 Redis Backplane 配置）
- [ ] 通知保留时长是多久？（影响数据清理策略）

### 13.4 UI/UX

- [ ] 通知中心的展示位置：Dock 弹窗 / 独立窗口 / 侧边栏？
- [ ] Toast 提示的样式与交互：点击跳转 / 自动消失 / 可关闭？
- [ ] 是否需要通知声音提示？

### 13.5 运维与监控

- [ ] 是否需要管理后台的通知管理功能？
- [ ] 监控指标的告警接收方式：邮件 / 企业微信 / 钉钉？
- [ ] 日志保留时长与归档策略？

---

## 14. 参考资料

### 14.1 内部文档

- [开发框架说明](/architecture/framework) - SignalR 实时交互章节
- [开发规范](/architecture/specifications) - SignalR 实时交互规范
- [萝卜币系统设计](/guide/radish-coin-system) - 实时推送示例（第 16.9 节）
- [神评/沙发功能](/features/comment-highlight) - 通知触发点参考
- [Gateway 服务网关](/guide/gateway) - WebSocket 路由配置

### 14.2 外部资源

- [SignalR 官方文档](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [SignalR JavaScript 客户端](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
- [SignalR 扩展与 Redis Backplane](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane)
- [SignalR 性能调优](https://learn.microsoft.com/en-us/aspnet/core/signalr/performance)

---

**文档版本**：v1.0
**创建日期**：2026-01-02
**最后更新**：2026-01-02
**负责人**：待定
**审核状态**：待评审
