# 通知系统实现细节

> **文档版本**：v1.1
> **创建日期**：2026-01-06
> **最后更新**：2026-01-09
> **关联文档**：[通知系统总体规划](/guide/notification-realtime)

本文档包含通知系统的详细实现方案，包括数据模型、服务层设计、缓存策略、异步化方案等核心技术细节。

**实施状态**：M7 P1 阶段已完成（2026-01-09），P2 阶段进行中。

## 1. 数据模型设计

### 1.1 Notification（通知表）

```csharp
using SqlSugar;

namespace Radish.Model.Models;

/// <summary>
/// 通知实体
/// </summary>
[SugarTable("Notification")]
[SplitTable(SplitType.Month)]  // SqlSugar 按月分表
public class Notification : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>
    /// 租户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "租户ID")]
    public long TenantId { get; set; }

    /// <summary>
    /// 通知类型
    /// </summary>
    [SugarColumn(ColumnDescription = "通知类型", Length = 50)]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 通知优先级（1-低, 2-普通, 3-高, 4-紧急）
    /// </summary>
    [SugarColumn(ColumnDescription = "通知优先级")]
    public int Priority { get; set; } = 2;

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
    /// 触发者头像（冗余字段）
    /// </summary>
    [SugarColumn(ColumnDescription = "触发者头像", Length = 500, IsNullable = true)]
    public string? TriggerAvatar { get; set; }

    /// <summary>
    /// 扩展数据（JSON 格式）
    /// </summary>
    [SugarColumn(ColumnDescription = "扩展数据", ColumnDataType = "text", IsNullable = true)]
    public string? ExtData { get; set; }
}
```

### 1.2 UserNotification（用户通知关系表）

```csharp
using SqlSugar;

namespace Radish.Model.Models;

/// <summary>
/// 用户通知关系实体
/// </summary>
[SugarTable("UserNotification")]
public class UserNotification : RootEntityTKey<long>, ITenantEntity
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
    /// 推送状态（Created / Delivered / Failed / Abandoned）
    /// </summary>
    [SugarColumn(ColumnDescription = "推送状态", Length = 20)]
    public string DeliveryStatus { get; set; } = "Created";

    /// <summary>
    /// 推送时间
    /// </summary>
    [SugarColumn(ColumnDescription = "推送时间", IsNullable = true)]
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    [SugarColumn(ColumnDescription = "重试次数")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 最后重试时间
    /// </summary>
    [SugarColumn(ColumnDescription = "最后重试时间", IsNullable = true)]
    public DateTime? LastRetryAt { get; set; }

    /// <summary>
    /// 是否已删除（软删除）
    /// </summary>
    [SugarColumn(ColumnDescription = "是否已删除")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 删除时间
    /// </summary>
    [SugarColumn(ColumnDescription = "删除时间", IsNullable = true)]
    public DateTime? DeletedAt { get; set; }
}
```

### 1.3 NotificationSetting（用户通知偏好）

```csharp
using SqlSugar;

namespace Radish.Model.Models;

/// <summary>
/// 用户通知偏好设置
/// </summary>
[SugarTable("NotificationSetting")]
public class NotificationSetting : RootEntityTKey<long>, ITenantEntity
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
    /// 通知类型
    /// </summary>
    [SugarColumn(ColumnDescription = "通知类型", Length = 50)]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否启用站内推送
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用站内推送")]
    public bool EnableInApp { get; set; } = true;

    /// <summary>
    /// 是否启用邮件推送
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用邮件推送")]
    public bool EnableEmail { get; set; } = false;

    /// <summary>
    /// 是否启用声音提示
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用声音提示")]
    public bool EnableSound { get; set; } = true;
}
```

### 1.4 数据库索引设计

```csharp
// 在 DbSeed 或 Migration 中添加索引
public static void CreateNotificationIndexes(ISqlSugarClient db)
{
    // Notification 表索引
    db.DbMaintenance.CreateIndex("Notification",
        new[] { "TenantId", "CreateTime" },
        "idx_notification_tenant_time",
        isUnique: false);

    db.DbMaintenance.CreateIndex("Notification",
        new[] { "Type", "BusinessId" },
        "idx_notification_type_business",
        isUnique: false);

    // UserNotification 表索引（核心查询索引）
    db.DbMaintenance.CreateIndex("UserNotification",
        new[] { "UserId", "IsRead", "IsDeleted", "CreateTime" },
        "idx_user_notification_list",
        isUnique: false);

    db.DbMaintenance.CreateIndex("UserNotification",
        new[] { "UserId", "IsRead", "IsDeleted" },
        "idx_user_notification_unread",
        isUnique: false);

    db.DbMaintenance.CreateIndex("UserNotification",
        new[] { "NotificationId" },
        "idx_user_notification_nid",
        isUnique: false);

    db.DbMaintenance.CreateIndex("UserNotification",
        new[] { "DeliveryStatus", "RetryCount" },
        "idx_user_notification_retry",
        isUnique: false);

    // NotificationSetting 表索引
    db.DbMaintenance.CreateIndex("NotificationSetting",
        new[] { "UserId", "NotificationType" },
        "idx_notification_setting_user_type",
        isUnique: true);
}
```

### 1.5 按月分表策略

```csharp
// 分表查询示例
public async Task<List<Notification>> GetNotificationsByDateRange(
    long tenantId,
    DateTime startDate,
    DateTime endDate)
{
    return await _db.Queryable<Notification>()
        // SqlSugar 自动处理跨月查询，根据时间范围确定需要查询哪些分表
        .SplitTable(tabs => tabs.InTableNames(
            SplitTableHelper.GetTableNamesByDateRange(startDate, endDate, "Notification")))
        .Where(n => n.TenantId == tenantId)
        .Where(n => n.CreateTime >= startDate && n.CreateTime <= endDate)
        .OrderBy(n => n.CreateTime, OrderByType.Desc)
        .ToListAsync();
}

// 分表辅助类
public static class SplitTableHelper
{
    public static List<string> GetTableNamesByDateRange(
        DateTime startDate,
        DateTime endDate,
        string baseTableName)
    {
        var tables = new List<string>();
        var current = new DateTime(startDate.Year, startDate.Month, 1);
        var end = new DateTime(endDate.Year, endDate.Month, 1);

        while (current <= end)
        {
            tables.Add($"{baseTableName}_{current:yyyyMM}");
            current = current.AddMonths(1);
        }

        return tables;
    }
}
```

## 2. 通知类型与优先级

### 2.1 通知类型枚举

```csharp
namespace Radish.Model.Enums;

/// <summary>
/// 通知类型
/// </summary>
public static class NotificationType
{
    // === 互动类通知（P1）===
    /// <summary>评论被回复</summary>
    public const string CommentReplied = "CommentReplied";

    /// <summary>帖子被点赞</summary>
    public const string PostLiked = "PostLiked";

    /// <summary>评论被点赞</summary>
    public const string CommentLiked = "CommentLiked";

    /// <summary>被 @ 提及</summary>
    public const string Mentioned = "Mentioned";

    // === 成就类通知（P2）===
    /// <summary>成为神评</summary>
    public const string GodComment = "GodComment";

    /// <summary>成为沙发</summary>
    public const string Sofa = "Sofa";

    /// <summary>等级提升</summary>
    public const string LevelUp = "LevelUp";

    // === 积分类通知（P2）===
    /// <summary>萝卜币余额变动</summary>
    public const string CoinBalanceChanged = "CoinBalanceChanged";

    // === 系统类通知 ===
    /// <summary>系统公告</summary>
    public const string SystemAnnouncement = "SystemAnnouncement";

    /// <summary>账号安全</summary>
    public const string AccountSecurity = "AccountSecurity";
}

/// <summary>
/// 通知优先级
/// </summary>
public enum NotificationPriority
{
    /// <summary>低优先级（点赞）</summary>
    Low = 1,

    /// <summary>普通优先级（评论回复）</summary>
    Normal = 2,

    /// <summary>高优先级（@提及、神评/沙发）</summary>
    High = 3,

    /// <summary>紧急（系统公告、账号安全）</summary>
    Critical = 4
}
```

### 2.2 通知优先级配置

```csharp
// 通知类型与优先级映射
public static class NotificationPriorityMapping
{
    private static readonly Dictionary<string, NotificationPriority> _mapping = new()
    {
        // 低优先级
        [NotificationType.PostLiked] = NotificationPriority.Low,
        [NotificationType.CommentLiked] = NotificationPriority.Low,

        // 普通优先级
        [NotificationType.CommentReplied] = NotificationPriority.Normal,
        [NotificationType.CoinBalanceChanged] = NotificationPriority.Normal,

        // 高优先级
        [NotificationType.Mentioned] = NotificationPriority.High,
        [NotificationType.GodComment] = NotificationPriority.High,
        [NotificationType.Sofa] = NotificationPriority.High,
        [NotificationType.LevelUp] = NotificationPriority.High,

        // 紧急
        [NotificationType.SystemAnnouncement] = NotificationPriority.Critical,
        [NotificationType.AccountSecurity] = NotificationPriority.Critical
    };

    public static NotificationPriority GetPriority(string type)
    {
        return _mapping.TryGetValue(type, out var priority)
            ? priority
            : NotificationPriority.Normal;
    }
}
```

## 3. 通知模板系统

### 3.1 模板定义

```csharp
namespace Radish.Model.Models;

/// <summary>
/// 通知模板
/// </summary>
public class NotificationTemplate
{
    /// <summary>
    /// 通知类型
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 标题模板
    /// </summary>
    public string TitleTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 内容模板
    /// </summary>
    public string ContentTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 跳转链接模板
    /// </summary>
    public string? LinkTemplate { get; set; }
}
```

### 3.2 模板服务实现

```csharp
using System.Text.RegularExpressions;

namespace Radish.Service;

public interface INotificationTemplateService
{
    string RenderTitle(string type, Dictionary<string, object> parameters);
    string RenderContent(string type, Dictionary<string, object> parameters);
    string? RenderLink(string type, Dictionary<string, object> parameters);
}

public class NotificationTemplateService : INotificationTemplateService
{
    // 模板定义（也可以从配置或数据库加载）
    private static readonly Dictionary<string, NotificationTemplate> _templates = new()
    {
        [NotificationType.CommentReplied] = new NotificationTemplate
        {
            Type = NotificationType.CommentReplied,
            TitleTemplate = "{triggerName} 回复了您的评论",
            ContentTemplate = "{triggerName} 说：{content}",
            LinkTemplate = "/forum/post/{postId}#comment-{commentId}"
        },
        [NotificationType.PostLiked] = new NotificationTemplate
        {
            Type = NotificationType.PostLiked,
            TitleTemplate = "{triggerName} 点赞了您的帖子",
            ContentTemplate = "您的帖子《{postTitle}》收到了一个赞",
            LinkTemplate = "/forum/post/{postId}"
        },
        [NotificationType.CommentLiked] = new NotificationTemplate
        {
            Type = NotificationType.CommentLiked,
            TitleTemplate = "{triggerName} 点赞了您的评论",
            ContentTemplate = "您的评论收到了一个赞",
            LinkTemplate = "/forum/post/{postId}#comment-{commentId}"
        },
        [NotificationType.Mentioned] = new NotificationTemplate
        {
            Type = NotificationType.Mentioned,
            TitleTemplate = "{triggerName} 在{businessType}中提及了您",
            ContentTemplate = "{triggerName} 说：{content}",
            LinkTemplate = "/forum/post/{postId}#comment-{commentId}"
        },
        [NotificationType.GodComment] = new NotificationTemplate
        {
            Type = NotificationType.GodComment,
            TitleTemplate = "恭喜！您的评论成为神评",
            ContentTemplate = "您在《{postTitle}》下的评论获得了大量点赞，成为神评！获得 {reward} 胡萝卜奖励",
            LinkTemplate = "/forum/post/{postId}#comment-{commentId}"
        },
        [NotificationType.Sofa] = new NotificationTemplate
        {
            Type = NotificationType.Sofa,
            TitleTemplate = "恭喜！您抢到了沙发",
            ContentTemplate = "您在《{postTitle}》下抢到了沙发！获得 {reward} 胡萝卜奖励",
            LinkTemplate = "/forum/post/{postId}#comment-{commentId}"
        },
        [NotificationType.CoinBalanceChanged] = new NotificationTemplate
        {
            Type = NotificationType.CoinBalanceChanged,
            TitleTemplate = "萝卜币余额变动",
            ContentTemplate = "{reason}，{changeType} {amount} 胡萝卜，当前余额：{balance} 胡萝卜",
            LinkTemplate = "/profile/wallet"
        },
        [NotificationType.SystemAnnouncement] = new NotificationTemplate
        {
            Type = NotificationType.SystemAnnouncement,
            TitleTemplate = "【系统公告】{title}",
            ContentTemplate = "{content}",
            LinkTemplate = null
        }
    };

    public string RenderTitle(string type, Dictionary<string, object> parameters)
    {
        if (!_templates.TryGetValue(type, out var template))
        {
            return "您有一条新通知";
        }

        return RenderTemplate(template.TitleTemplate, parameters);
    }

    public string RenderContent(string type, Dictionary<string, object> parameters)
    {
        if (!_templates.TryGetValue(type, out var template))
        {
            return string.Empty;
        }

        var content = RenderTemplate(template.ContentTemplate, parameters);

        // 内容安全处理
        return SanitizeContent(content);
    }

    public string? RenderLink(string type, Dictionary<string, object> parameters)
    {
        if (!_templates.TryGetValue(type, out var template) || template.LinkTemplate == null)
        {
            return null;
        }

        return RenderTemplate(template.LinkTemplate, parameters);
    }

    private static string RenderTemplate(string template, Dictionary<string, object> parameters)
    {
        var result = template;

        foreach (var (key, value) in parameters)
        {
            result = result.Replace($"{{{key}}}", value?.ToString() ?? string.Empty);
        }

        // 移除未替换的占位符
        result = Regex.Replace(result, @"\{[^}]+\}", string.Empty);

        return result;
    }

    private static string SanitizeContent(string content)
    {
        // 1. XSS 防护
        content = System.Net.WebUtility.HtmlEncode(content);

        // 2. 敏感信息脱敏（手机号）
        content = Regex.Replace(content, @"1[3-9]\d{9}",
            m => m.Value.Substring(0, 3) + "****" + m.Value.Substring(7));

        // 3. 敏感信息脱敏（邮箱）
        content = Regex.Replace(content, @"(\w{3})\w+@(\w+\.\w+)", "$1***@$2");

        // 4. 长度限制
        if (content.Length > 1000)
        {
            content = content.Substring(0, 997) + "...";
        }

        return content;
    }
}
```

## 4. 未读数缓存策略

### 4.1 缓存服务接口

```csharp
namespace Radish.IService;

public interface INotificationCacheService
{
    /// <summary>
    /// 获取用户未读数
    /// </summary>
    Task<long> GetUnreadCountAsync(long userId);

    /// <summary>
    /// 增加用户未读数
    /// </summary>
    Task<long> IncrementUnreadCountAsync(long userId, int delta = 1);

    /// <summary>
    /// 减少用户未读数
    /// </summary>
    Task<long> DecrementUnreadCountAsync(long userId, int delta = 1);

    /// <summary>
    /// 设置用户未读数（用于缓存重建）
    /// </summary>
    Task SetUnreadCountAsync(long userId, long count);

    /// <summary>
    /// 删除用户未读数缓存
    /// </summary>
    Task RemoveUnreadCountAsync(long userId);

    /// <summary>
    /// 检查通知去重
    /// </summary>
    Task<bool> CheckDedupAsync(string type, long businessId, long triggerId);

    /// <summary>
    /// 设置通知去重标记
    /// </summary>
    Task SetDedupAsync(string type, long businessId, long triggerId, TimeSpan ttl);
}
```

### 4.2 缓存服务实现

```csharp
using Radish.Common.Cache;

namespace Radish.Service;

public class NotificationCacheService : INotificationCacheService
{
    private readonly ICaching _cache;
    private readonly ILogger<NotificationCacheService> _logger;

    // 缓存 Key 前缀
    private const string UnreadCountKeyPrefix = "notification:unread:";
    private const string DedupKeyPrefix = "notification:dedup:";

    // 未读数缓存过期时间（24 小时）
    private static readonly TimeSpan UnreadCountTTL = TimeSpan.FromHours(24);

    public NotificationCacheService(ICaching cache, ILogger<NotificationCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<long> GetUnreadCountAsync(long userId)
    {
        var key = $"{UnreadCountKeyPrefix}{userId}";

        try
        {
            var cached = await _cache.GetAsync<long?>(key);
            if (cached.HasValue)
            {
                return cached.Value;
            }

            // 缓存未命中，返回 -1 表示需要从数据库查询
            return -1;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "获取未读数缓存失败，UserId: {UserId}", userId);
            return -1;
        }
    }

    public async Task<long> IncrementUnreadCountAsync(long userId, int delta = 1)
    {
        var key = $"{UnreadCountKeyPrefix}{userId}";

        try
        {
            // Redis INCRBY 原子操作
            var newCount = await _cache.IncrementAsync(key, delta);

            // 设置/刷新过期时间
            await _cache.ExpireAsync(key, UnreadCountTTL);

            return newCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "增加未读数缓存失败，UserId: {UserId}", userId);
            return -1;
        }
    }

    public async Task<long> DecrementUnreadCountAsync(long userId, int delta = 1)
    {
        var key = $"{UnreadCountKeyPrefix}{userId}";

        try
        {
            // Redis DECRBY 原子操作
            var newCount = await _cache.IncrementAsync(key, -delta);

            // 确保不会为负数
            if (newCount < 0)
            {
                await _cache.SetAsync(key, 0L, UnreadCountTTL);
                return 0;
            }

            // 刷新过期时间
            await _cache.ExpireAsync(key, UnreadCountTTL);

            return newCount;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "减少未读数缓存失败，UserId: {UserId}", userId);
            return -1;
        }
    }

    public async Task SetUnreadCountAsync(long userId, long count)
    {
        var key = $"{UnreadCountKeyPrefix}{userId}";

        try
        {
            await _cache.SetAsync(key, count, UnreadCountTTL);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "设置未读数缓存失败，UserId: {UserId}", userId);
        }
    }

    public async Task RemoveUnreadCountAsync(long userId)
    {
        var key = $"{UnreadCountKeyPrefix}{userId}";

        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除未读数缓存失败，UserId: {UserId}", userId);
        }
    }

    public async Task<bool> CheckDedupAsync(string type, long businessId, long triggerId)
    {
        var key = $"{DedupKeyPrefix}{type}:{businessId}:{triggerId}";

        try
        {
            return await _cache.ExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查通知去重失败，Key: {Key}", key);
            return false;  // 去重检查失败时，默认允许发送
        }
    }

    public async Task SetDedupAsync(string type, long businessId, long triggerId, TimeSpan ttl)
    {
        var key = $"{DedupKeyPrefix}{type}:{businessId}:{triggerId}";

        try
        {
            await _cache.SetAsync(key, 1, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "设置通知去重标记失败，Key: {Key}", key);
        }
    }
}
```

### 4.3 缓存一致性校验任务

```csharp
using Hangfire;

namespace Radish.Service.Jobs;

/// <summary>
/// 未读数缓存一致性校验任务
/// </summary>
public class UnreadCountConsistencyJob
{
    private readonly INotificationCacheService _cacheService;
    private readonly IBaseRepository<UserNotification> _userNotificationRepository;
    private readonly ILogger<UnreadCountConsistencyJob> _logger;

    public UnreadCountConsistencyJob(
        INotificationCacheService cacheService,
        IBaseRepository<UserNotification> userNotificationRepository,
        ILogger<UnreadCountConsistencyJob> logger)
    {
        _cacheService = cacheService;
        _userNotificationRepository = userNotificationRepository;
        _logger = logger;
    }

    /// <summary>
    /// 执行一致性校验（每小时执行一次）
    /// </summary>
    [Queue("notification")]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("开始执行未读数缓存一致性校验");

        var startTime = DateTime.Now;
        var checkedCount = 0;
        var fixedCount = 0;

        try
        {
            // 获取最近 24 小时有通知的用户
            var yesterday = DateTime.Now.AddDays(-1);
            var activeUserIds = await _userNotificationRepository.Db
                .Queryable<UserNotification>()
                .Where(n => n.CreateTime >= yesterday)
                .Select(n => n.UserId)
                .Distinct()
                .ToListAsync();

            foreach (var userId in activeUserIds)
            {
                checkedCount++;

                // 从缓存获取未读数
                var cachedCount = await _cacheService.GetUnreadCountAsync(userId);

                // 从数据库查询实际未读数
                var actualCount = await _userNotificationRepository.Db
                    .Queryable<UserNotification>()
                    .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
                    .CountAsync();

                // 比较并修复
                if (cachedCount != actualCount)
                {
                    _logger.LogWarning(
                        "未读数不一致，UserId: {UserId}, 缓存: {Cached}, 实际: {Actual}",
                        userId, cachedCount, actualCount);

                    await _cacheService.SetUnreadCountAsync(userId, actualCount);
                    fixedCount++;
                }
            }

            var elapsed = DateTime.Now - startTime;
            _logger.LogInformation(
                "未读数缓存一致性校验完成，检查用户数: {Checked}, 修复数: {Fixed}, 耗时: {Elapsed}ms",
                checkedCount, fixedCount, elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未读数缓存一致性校验失败");
            throw;
        }
    }
}
```

## 5. 异步化方案

### 5.1 使用 Hangfire 后台任务

```csharp
namespace Radish.Service;

public class NotificationService : INotificationService
{
    private readonly IBackgroundJobClient _backgroundJobs;

    /// <summary>
    /// 发送通知（异步）
    /// </summary>
    public async Task SendNotificationAsync(NotificationCreateDto dto)
    {
        // 1. 立即保存通知到数据库（同步）
        var notification = await CreateNotificationAsync(dto);

        // 2. 异步推送通知（不阻塞主流程）
        _backgroundJobs.Enqueue<INotificationPushService>(
            service => service.PushNotificationAsync(notification.Id));
    }

    /// <summary>
    /// 批量发送通知（如系统公告）
    /// </summary>
    public async Task SendBatchNotificationAsync(NotificationCreateDto dto, List<long> userIds)
    {
        // 1. 批量创建通知记录
        var notificationIds = await CreateBatchNotificationsAsync(dto, userIds);

        // 2. 分批异步推送
        const int batchSize = 100;
        for (int i = 0; i < notificationIds.Count; i += batchSize)
        {
            var batch = notificationIds.Skip(i).Take(batchSize).ToList();
            _backgroundJobs.Enqueue<INotificationPushService>(
                service => service.PushBatchNotificationsAsync(batch));
        }
    }
}
```

### 5.2 使用 Channel（轻量级方案）

```csharp
using System.Threading.Channels;

namespace Radish.Service;

/// <summary>
/// 通知事件
/// </summary>
public record NotificationEvent
{
    public long NotificationId { get; init; }
    public long UserId { get; init; }
    public string Type { get; init; } = string.Empty;
}

/// <summary>
/// 通知 Channel 服务
/// </summary>
public class NotificationChannelService
{
    private readonly Channel<NotificationEvent> _channel;

    public NotificationChannelService()
    {
        // 创建无界 Channel（可以配置为有界以限制内存）
        _channel = Channel.CreateUnbounded<NotificationEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ChannelWriter<NotificationEvent> Writer => _channel.Writer;
    public ChannelReader<NotificationEvent> Reader => _channel.Reader;
}

/// <summary>
/// 通知推送后台服务
/// </summary>
public class NotificationPushBackgroundService : BackgroundService
{
    private readonly NotificationChannelService _channelService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationPushBackgroundService> _logger;

    public NotificationPushBackgroundService(
        NotificationChannelService channelService,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationPushBackgroundService> logger)
    {
        _channelService = channelService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

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
    private readonly INotificationCacheService _cacheService;
    private readonly NotificationDedupOptions _options;

    public NotificationDedupService(
        INotificationCacheService cacheService,
        IOptions<NotificationDedupOptions> options)
    {
        _cacheService = cacheService;
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
        var isDuplicate = await _cacheService.CheckDedupAsync(type, businessId, triggerId);
        if (isDuplicate)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 标记通知已发送（设置去重缓存）
    /// </summary>
    public async Task MarkAsSentAsync(string type, long businessId, long triggerId)
    {
        var ttl = GetDedupTTL(type);
        await _cacheService.SetDedupAsync(type, businessId, triggerId, ttl);
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

**P2 阶段（计划中）**：
- ⏳ NotificationCacheService（未读数缓存管理）
- ⏳ 集成通知到点赞功能（PostService、CommentService）
- ⏳ 前端 NotificationCenter 组件
- ⏳ 前端 NotificationList 页面

**P3 阶段（计划中）**：
- ⏳ NotificationTemplateService（通知模板）
- ⏳ NotificationDedupService（通知去重）
- ⏳ 推送失败重试机制
- ⏳ 未读数一致性检查任务

**P4 阶段（计划中）**：
- ⏳ 通知偏好设置功能
- ⏳ 按类型分组的未读数统计
- ⏳ 邮件推送渠道
- ⏳ 端到端测试和验收

### 12.3 下一步行动

**明日优先级**：
1. 实现 NotificationCacheService（Redis 缓存未读数）
2. 集成通知到点赞功能（PostLiked、CommentLiked）
3. 前端 NotificationCenter 组件（顶栏通知铃铛）

---

**文档版本**：v1.1
**最后更新**：2026-01-09
**状态**：P1 阶段已完成，P2 阶段进行中
**关联文档**：
- [通知系统总体规划](/guide/notification-realtime)
- [通知系统 API 文档](/guide/notification-api)
- [通知系统前端集成指南](/guide/notification-frontend)
