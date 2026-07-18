    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("通知推送后台服务启动");

        await foreach (var evt in _channelService.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pushService = scope.ServiceProvider.GetRequiredService<INotificationPushService>();

                await pushService.PushNotificationAsync(evt.NotificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "推送通知失败，NotificationId: {Id}", evt.NotificationId);
            }
        }

        _logger.LogInformation("通知推送后台服务停止");
    }
}
```

### 5.3 推荐方案选择

| 方案 | 优点 | 缺点 | 适用场景 |
|-----|------|------|---------|
| **Hangfire**（推荐） | 持久化、可重试、可监控、已有依赖 | 稍重 | 生产环境、需要可靠性 |
| **Channel** | 轻量、高性能、无额外依赖 | 内存中、重启丢失 | 开发环境、实时性要求高 |
| **消息队列**（RabbitMQ） | 分布式、高可用 | 引入新依赖 | 大规模、多服务 |

**建议**：P0/P1 阶段使用 Hangfire，P2 阶段根据实际需求评估是否引入消息队列。

## 6. 推送失败重试机制

### 6.1 重试配置

```csharp
namespace Radish.Common.OptionTool;

/// <summary>
/// 通知重试配置
/// </summary>
public class NotificationRetryOptions : IConfigurableOptions
{
    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（秒）- 指数退避
    /// </summary>
    public int[] RetryDelaySeconds { get; set; } = { 5, 30, 300 };

    /// <summary>
    /// 重试任务 Cron 表达式（每 5 分钟）
    /// </summary>
    public string RetryCron { get; set; } = "*/5 * * * *";
}
```

### 6.2 重试任务实现

```csharp
using Hangfire;

namespace Radish.Service.Jobs;

/// <summary>
/// 通知推送重试任务
/// </summary>
public class NotificationRetryJob
{
    private readonly IBaseRepository<UserNotification> _repository;
    private readonly INotificationPushService _pushService;
    private readonly NotificationRetryOptions _options;
    private readonly ILogger<NotificationRetryJob> _logger;

    public NotificationRetryJob(
        IBaseRepository<UserNotification> repository,
        INotificationPushService pushService,
        IOptions<NotificationRetryOptions> options,
        ILogger<NotificationRetryJob> logger)
    {
        _repository = repository;
        _pushService = pushService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 执行重试任务
    /// </summary>
    [Queue("notification-retry")]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("开始执行通知推送重试任务");

        var retriedCount = 0;
        var successCount = 0;
        var abandonedCount = 0;

        try
        {
            // 获取需要重试的通知
            var failedNotifications = await _repository.Db
                .Queryable<UserNotification>()
                .Where(n => n.DeliveryStatus == "Failed")
                .Where(n => n.RetryCount < _options.MaxRetryCount)
                .OrderBy(n => n.CreateTime)
                .Take(100)
                .ToListAsync();

            foreach (var notification in failedNotifications)
            {
                // 检查重试间隔
                var retryDelay = GetRetryDelay(notification.RetryCount);
                if (notification.LastRetryAt.HasValue &&
                    DateTime.Now - notification.LastRetryAt.Value < retryDelay)
                {
                    continue;  // 还未到重试时间
                }

                retriedCount++;

                try
                {
                    // 尝试推送
                    await _pushService.PushNotificationAsync(notification.NotificationId);

                    // 更新状态为已送达
                    notification.DeliveryStatus = "Delivered";
                    notification.DeliveredAt = DateTime.Now;
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "重试推送失败，NotificationId: {Id}, RetryCount: {Count}",
                        notification.NotificationId, notification.RetryCount + 1);

                    notification.RetryCount++;
                    notification.LastRetryAt = DateTime.Now;

                    // 超过最大重试次数，标记为放弃
                    if (notification.RetryCount >= _options.MaxRetryCount)
                    {
                        notification.DeliveryStatus = "Abandoned";
                        abandonedCount++;

                        // 可选：发送告警
                        _logger.LogError(
                            "通知推送最终失败，NotificationId: {Id}, UserId: {UserId}",
                            notification.NotificationId, notification.UserId);
                    }
                }

                await _repository.UpdateAsync(notification);
            }

            _logger.LogInformation(
                "通知推送重试任务完成，重试: {Retried}, 成功: {Success}, 放弃: {Abandoned}",
                retriedCount, successCount, abandonedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知推送重试任务执行失败");
            throw;
        }
    }

    private TimeSpan GetRetryDelay(int retryCount)
    {
        if (retryCount >= _options.RetryDelaySeconds.Length)
        {
            return TimeSpan.FromSeconds(_options.RetryDelaySeconds[^1]);
        }

        return TimeSpan.FromSeconds(_options.RetryDelaySeconds[retryCount]);
    }
}
```

## 7. 通知去重规则

### 7.1 去重策略配置

```csharp
namespace Radish.Common.OptionTool;

/// <summary>
/// 通知去重配置
/// </summary>
public class NotificationDedupOptions : IConfigurableOptions
{
    /// <summary>
    /// 评论回复去重时间（分钟）
    /// </summary>
    public int CommentReplyDedupMinutes { get; set; } = 5;

    /// <summary>
    /// 点赞去重时间（分钟）
    /// </summary>
    public int LikeDedupMinutes { get; set; } = 60;

    /// <summary>
    /// 提及去重时间（分钟）
    /// </summary>
    public int MentionDedupMinutes { get; set; } = 30;
}
```

### 7.2 去重服务实现

```csharp
namespace Radish.Service;

public class NotificationDedupService : INotificationDedupService
{
    private readonly ICaching _caching;
    private readonly NotificationDedupOptions _options;

    public NotificationDedupService(
        ICaching caching,
        IOptions<NotificationDedupOptions> options)
    {
        _caching = caching;
        _options = options.Value;
    }

    /// <summary>
    /// 检查是否可以发送通知（去重检查）
    /// </summary>
    public async Task<bool> CanSendNotificationAsync(
        string type,
        long businessId,
        long triggerId,
        long receiverId)
    {
        // 1. 自己不通知自己
        if (triggerId == receiverId)
        {
            return false;
        }

        // 2. 检查缓存去重
        var dedupKey = BuildDedupKey(receiverId, type, businessId);
        var isDuplicate = await _caching.ExistsAsync(dedupKey);
        if (isDuplicate)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 标记通知已发送（设置去重缓存）
    /// </summary>
    public async Task MarkAsSentAsync(string type, long businessId, long receiverId)
    {
        var ttl = GetDedupTTL(type);
        await _caching.SetStringAsync(BuildDedupKey(receiverId, type, businessId), "1", ttl);
    }

    private static string BuildDedupKey(long userId, string type, long businessId)
    {
        return $"notification:dedup:{userId}:{type}:{businessId}";
    }

    private TimeSpan GetDedupTTL(string type)
    {
        return type switch
        {
            NotificationType.CommentReplied => TimeSpan.FromMinutes(_options.CommentReplyDedupMinutes),
            NotificationType.PostLiked => TimeSpan.FromMinutes(_options.LikeDedupMinutes),
            NotificationType.CommentLiked => TimeSpan.FromMinutes(_options.LikeDedupMinutes),
            NotificationType.Mentioned => TimeSpan.FromMinutes(_options.MentionDedupMinutes),
            _ => TimeSpan.FromMinutes(5)  // 默认 5 分钟
        };
    }
}
```

## 8. 配置文件示例

### 8.1 appsettings.json

```json
{
  "Notification": {
    "Enable": true,

    "SignalR": {
      "HubPath": "/hub/notification",
      "KeepAliveIntervalSeconds": 15,
      "ClientTimeoutSeconds": 30,
      "EnableDetailedErrors": false,
      "MaximumReceiveMessageSize": 32768
    },

    "Push": {
      "MaxPayloadSize": 1024,
      "BatchSize": 100,
      "EnableCompression": true
    },

    "Retry": {
      "MaxRetryCount": 3,
      "RetryDelaySeconds": [5, 30, 300],
      "RetryCron": "*/5 * * * *"
    },

    "Dedup": {
      "CommentReplyDedupMinutes": 5,
      "LikeDedupMinutes": 60,
      "MentionDedupMinutes": 30
    },

    "Cache": {
      "UnreadCountTTLHours": 24,
      "ConsistencyCheckCron": "0 * * * *"
    },

    "Retention": {
      "DaysToKeep": 90,
      "AutoCleanup": true,
      "CleanupCron": "0 2 * * *"
    },

    "RateLimit": {
      "ConnectionsPerUserPerMinute": 5,
      "ConnectionsPerIpPerMinute": 10,
      "NotificationsPerUserPerHour": 100
    }
  }
}
```

### 8.2 配置绑定

```csharp
// Program.cs 中绑定配置
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection("Notification"));
builder.Services.Configure<NotificationRetryOptions>(
    builder.Configuration.GetSection("Notification:Retry"));
builder.Services.Configure<NotificationDedupOptions>(
    builder.Configuration.GetSection("Notification:Dedup"));
```

## 9. SignalR Hub 实现

### 9.1 NotificationHub

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Radish.Api.Hubs;

/// <summary>
/// 通知 SignalR Hub
/// </summary>
[Authorize(Policy = "Client")]
public class NotificationHub : Hub
{
    private readonly INotificationCacheService _cacheService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        INotificationCacheService cacheService,
        ILogger<NotificationHub> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// 连接建立时
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        _logger.LogInformation(
            "SignalR 连接建立，UserId: {UserId}, ConnectionId: {ConnectionId}",
            userId, connectionId);

        // 将连接加入用户组
        await Groups.AddToGroupAsync(connectionId, $"user:{userId}");

        // 推送当前未读数
        var unreadCount = await _cacheService.GetUnreadCountAsync(userId);
        if (unreadCount >= 0)
        {
            await Clients.Caller.SendAsync("UnreadCountChanged", new { unreadCount });
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// 连接断开时
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "SignalR 连接异常断开，UserId: {UserId}, ConnectionId: {ConnectionId}",
                userId, connectionId);
        }
        else
        {
            _logger.LogInformation(
                "SignalR 连接正常断开，UserId: {UserId}, ConnectionId: {ConnectionId}",
                userId, connectionId);
        }

        // 从用户组移除
        await Groups.RemoveFromGroupAsync(connectionId, $"user:{userId}");

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// 客户端标记通知已读
    /// </summary>
    public async Task MarkAsRead(long notificationId)
    {
        var userId = GetUserId();

        _logger.LogDebug(
            "客户端标记已读，UserId: {UserId}, NotificationId: {NotificationId}",
            userId, notificationId);

        // 通知其他端（多端同步）
        await Clients.OthersInGroup($"user:{userId}")
            .SendAsync("NotificationRead", new { notificationIds = new[] { notificationId } });
    }

    /// <summary>
    /// 客户端标记全部已读
    /// </summary>
    public async Task MarkAllAsRead()
    {
        var userId = GetUserId();

        _logger.LogDebug("客户端标记全部已读，UserId: {UserId}", userId);

        // 通知其他端
        await Clients.OthersInGroup($"user:{userId}")
            .SendAsync("AllNotificationsRead", new { });
    }

    private long GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst("jti")?.Value;

        if (long.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        throw new HubException("无法获取用户ID");
    }
}
```

### 9.2 推送服务实现

```csharp
using Microsoft.AspNetCore.SignalR;

namespace Radish.Service;

public interface INotificationPushService
{
    Task PushNotificationAsync(long notificationId);
    Task PushBatchNotificationsAsync(List<long> notificationIds);
    Task PushUnreadCountAsync(long userId);
}

public class NotificationPushService : INotificationPushService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IBaseRepository<Notification> _notificationRepository;
    private readonly IBaseRepository<UserNotification> _userNotificationRepository;
    private readonly INotificationCacheService _cacheService;
    private readonly ILogger<NotificationPushService> _logger;

    public NotificationPushService(
        IHubContext<NotificationHub> hubContext,
        IBaseRepository<Notification> notificationRepository,
        IBaseRepository<UserNotification> userNotificationRepository,
        INotificationCacheService cacheService,
        ILogger<NotificationPushService> logger)
    {
        _hubContext = hubContext;
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task PushNotificationAsync(long notificationId)
    {
        try
        {
            // 获取用户通知关系
            var userNotifications = await _userNotificationRepository.QueryAsync(
                n => n.NotificationId == notificationId && n.DeliveryStatus == "Created");

            foreach (var userNotification in userNotifications)
            {
                await PushToUserAsync(userNotification);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送通知失败，NotificationId: {Id}", notificationId);
            throw;
        }
    }

    public async Task PushBatchNotificationsAsync(List<long> notificationIds)
    {
        foreach (var notificationId in notificationIds)
        {
            try
            {
                await PushNotificationAsync(notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "批量推送中单条失败，NotificationId: {Id}", notificationId);
            }
        }
    }

    public async Task PushUnreadCountAsync(long userId)
    {
        try
        {
            var unreadCount = await _cacheService.GetUnreadCountAsync(userId);

            if (unreadCount < 0)
            {
                // 缓存未命中，从数据库查询
                unreadCount = await _userNotificationRepository.Db
                    .Queryable<UserNotification>()
                    .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
                    .CountAsync();

                // 写入缓存
                await _cacheService.SetUnreadCountAsync(userId, unreadCount);
            }

            await _hubContext.Clients
                .Group($"user:{userId}")
                .SendAsync("UnreadCountChanged", new { unreadCount });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送未读数失败，UserId: {UserId}", userId);
        }
    }

    private async Task PushToUserAsync(UserNotification userNotification)
    {
        try
        {
            // 获取通知内容
            var notification = await _notificationRepository.QueryByIdAsync(
                userNotification.NotificationId);

            if (notification == null)
            {
                _logger.LogWarning(
                    "通知不存在，NotificationId: {Id}",
                    userNotification.NotificationId);
                return;
            }

            // 推送通知内容
            await _hubContext.Clients
                .Group($"user:{userNotification.UserId}")
                .SendAsync("NotificationReceived", new
                {
                    id = notification.Id,
                    type = notification.Type,
                    priority = notification.Priority,
                    title = notification.Title,
                    content = notification.Content,
                    businessType = notification.BusinessType,
                    businessId = notification.BusinessId,
                    triggerId = notification.TriggerId,
                    triggerName = notification.TriggerName,
                    triggerAvatar = notification.TriggerAvatar,
                    createdAt = notification.CreateTime
                });

            // 推送未读数更新
            await PushUnreadCountAsync(userNotification.UserId);

            // 更新推送状态
            userNotification.DeliveryStatus = "Delivered";
            userNotification.DeliveredAt = DateTime.Now;
            await _userNotificationRepository.UpdateAsync(userNotification);

            _logger.LogDebug(
                "推送成功，UserId: {UserId}, NotificationId: {NotificationId}",
                userNotification.UserId, userNotification.NotificationId);
        }
        catch (Exception ex)
        {
            // 更新为失败状态
            userNotification.DeliveryStatus = "Failed";
            userNotification.RetryCount++;
            userNotification.LastRetryAt = DateTime.Now;
            await _userNotificationRepository.UpdateAsync(userNotification);

            _logger.LogWarning(ex,
                "推送失败，UserId: {UserId}, NotificationId: {NotificationId}",
                userNotification.UserId, userNotification.NotificationId);

            throw;
        }
    }
}
```

## 10. Gateway 路由配置

### 10.1 YARP 路由配置

```json
// Radish.Gateway/appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "notification-hub": {
        "ClusterId": "api",
        "Match": {
          "Path": "/hub/notification/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "" }
        ]
      }
    }
  }
}
```

### 10.2 WebSocket 配置

```csharp
// Radish.Gateway/Program.cs
var app = builder.Build();

// 启用 WebSocket（SignalR 需要）
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
});

app.MapReverseProxy();
```

## 11. 服务注册

### 11.1 Program.cs 配置

```csharp
// Radish.Api/Program.cs

// 注册 SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
});

// 多实例部署时添加 Redis Backplane
if (builder.Configuration.GetValue<bool>("Redis:Enable"))
{
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(options =>
        {
            options.Configuration.EndPoints.Add(
                builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379");
            options.Configuration.ChannelPrefix = "radish:signalr:";
        });
}

// 注册通知相关服务
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationPushService, NotificationPushService>();
builder.Services.AddScoped<INotificationCacheService, NotificationCacheService>();
builder.Services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
builder.Services.AddScoped<INotificationDedupService, NotificationDedupService>();

// 注册后台服务（如果使用 Channel 方案）
// builder.Services.AddSingleton<NotificationChannelService>();
// builder.Services.AddHostedService<NotificationPushBackgroundService>();

// 配置绑定
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection("Notification"));
builder.Services.Configure<NotificationRetryOptions>(
    builder.Configuration.GetSection("Notification:Retry"));
builder.Services.Configure<NotificationDedupOptions>(
    builder.Configuration.GetSection("Notification:Dedup"));

var app = builder.Build();

// 映射 Hub 端点
app.MapHub<NotificationHub>("/hub/notification");
```

### 11.2 Hangfire 任务注册

```csharp
// 在 Hangfire 配置中添加任务
RecurringJob.AddOrUpdate<NotificationRetryJob>(
    "notification-retry",
    job => job.ExecuteAsync(),
    "*/5 * * * *");  // 每 5 分钟

RecurringJob.AddOrUpdate<UnreadCountConsistencyJob>(
    "unread-count-consistency",
    job => job.ExecuteAsync(),
    "0 * * * *");  // 每小时
```

---

## 12. 实施进度

### 12.1 M7 P1 阶段（✅ 已完成 - 2026-01-09）

**核心功能**：
- ✅ 数据模型（Notification、UserNotification、NotificationSetting）
- ✅ ViewModel 和 DTO（NotificationVo、CreateNotificationDto 等）
- ✅ NotificationService 核心业务逻辑
- ✅ NotificationPushService SignalR 推送服务
- ✅ NotificationController API 接口
- ✅ AutoMapper 映射配置
- ✅ 集成到 CommentService（评论回复通知）
- ✅ 独立消息数据库架构（Radish.Message.db）

**数据库架构**：
```
DataBases/
├── Radish.db              # 主业务数据库
├── Radish.Log.db          # 日志数据库
├── Radish.Message.db      # 消息数据库 (NEW!)
│   ├── Notification_20260101  (按月分表)
│   ├── UserNotification
│   └── NotificationSetting
```

**技术亮点**：
- 按月分表（`Notification_{year}{month}{day}`）
- 软删除模式（`IsDeleted` 标记）
- 异步推送（`Task.Run` 避免阻塞主流程）
- 性能隔离（独立数据库，不影响核心业务）

### 12.2 待实施功能

**P2 阶段（已完成首批，后续增强待规划）**：
- ✅ NotificationCacheService（统一缓存层未读数缓存管理）
- ✅ NotificationDedupService（通知去重）
- ✅ 集成通知到点赞功能（PostService、CommentService）
- ✅ 前端 NotificationCenter 组件
- ✅ 前端 NotificationList 页面
- ⏳ Redis 原子未读计数与缓存一致性巡检

**P3 阶段（计划中）**：
- ⏳ NotificationTemplateService（通知模板）
- ⏳ 推送失败重试机制
- ⏳ 未读数一致性检查任务

**P4 阶段（计划中）**：
- ⏳ 通知偏好设置功能
- ⏳ 按类型分组的未读数统计
- ⏳ 邮件推送渠道
- ⏳ 端到端测试和验收

### 12.3 下一步行动

**明日优先级**：
1. 视多实例和高并发情况评估 Redis 原子未读计数。
2. 评估缓存一致性巡检任务是否需要回拉。
3. 评估多实例 SignalR Redis Backplane。

---

**文档版本**：v1.1
**最后更新**：2026-05-19
**状态**：P1 / P2 首批已完成，Redis 原子计数与一致性巡检后置
**关联文档**：
- [通知系统总体规划](/guide/notification-realtime)
- [通知系统 API 文档](/guide/notification-api)
- [通知系统前端集成指南](/guide/notification-frontend)
