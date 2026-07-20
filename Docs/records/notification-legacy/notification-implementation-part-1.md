# 通知系统实现细节

> **文档版本**：v1.2
> **创建日期**：2026-01-06
> **最后更新**：2026-05-19
> **关联文档**：[通知系统总体规划](/guide/notification-realtime)

> **文档状态**：历史实现参考。本文混合早期方案、代码片段与阶段记录，不再作为当前通知数据模型、缓存、聚合或可靠性契约；F4-B 后续实施统一以 [通知中心深化与通知治理](/features/notification-center-deepening) 和仓库代码为准。本文不再继续追加新批次流水，触达旧实现时按专题逐步归档。

本文档包含通知系统的详细实现方案，包括数据模型、服务层设计、缓存策略、异步化方案等核心技术细节。

**历史实施状态**：M7 P1 阶段曾按本文口径标记完成（2026-01-10）。2026-07-18 审计确认 `NotificationCacheService` 的读改写和异常归零不能承担未读正确性，`NotificationDedupService` 也没有形成可靠事件与展示聚合契约；F4-B 采用数据库分组 / 摘要和 revision 对账，不再把 Redis 原子计数或 Backplane 作为当前前置。

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
    /// 扩展数据（JSON 格式）。内部手动序列化时，导航对象 ID 必须显式写成字符串。
    /// </summary>
    [SugarColumn(ColumnDescription = "扩展数据", ColumnDataType = "text", IsNullable = true)]
    public string? ExtData { get; set; }
}
```

> 实现约束：`ExtData` 是业务代码自行拼接 / 序列化的 JSON 字符串，不会自动经过 API 层全局 `long -> string` 转换器。forum / chat 导航载荷中的 `postId`、`commentId`、`channelId`、`messageId` 等字段必须在写入前转成字符串；forum 通知当前应在可取得时并行写入 `postPublicId`，客户端消费顺序为 `postPublicId` 优先、旧 `postId` 字符串 fallback，避免旧通知和旧客户端失效。

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

当前实现以 `ICaching` 为统一入口：部署态 `Redis.Enable=true` 时使用 Redis，本地默认使用内存缓存。未读数缓存键为 `notification:unread_count:{userId}`，TTL 为 `30` 分钟；增量和减量更新当前是普通读-改-写缓存操作，`UserNotification` 关系表仍是未读状态真值来源。Redis `INCRBY / DECRBY` 原子计数和缓存一致性校验任务保留为后续治理项。

### 4.1 当前缓存服务接口

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
    Task<long> IncrementUnreadCountAsync(long userId);

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
    Task ClearUnreadCountAsync(long userId);

    /// <summary>
    /// 刷新用户未读数缓存
    /// </summary>
    Task<long> RefreshUnreadCountAsync(long userId);
}
```

通知去重已经拆到 `NotificationDedupService`，缓存键格式为 `notification:dedup:{userId}:{notificationType}:{businessId}`，默认去重窗口为 `300` 秒。

### 4.2 早期 Redis 原子计数设计示例（后续增强）

下面代码块保留为后续 Redis 原子计数方案参考，不代表当前仓库代码。当前代码实现位于 `Radish.Service/NotificationCacheService.cs` 与 `Radish.Service/NotificationDedupService.cs`。

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
            // 后续增强可改为 Redis INCRBY 原子操作
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
            // 后续增强可改为 Redis DECRBY 原子操作
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

    public async Task ClearUnreadCountAsync(long userId)
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

    public async Task<bool> ShouldDedupAsync(string type, long businessId, long triggerId)
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

    public async Task RecordDedupKeyAsync(string type, long businessId, long triggerId, TimeSpan ttl)
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
