using Microsoft.Extensions.Logging;
using Radish.Common.CacheTool;
using Radish.Common.LogTool;
using Radish.IService;

namespace Radish.Service;

/// <summary>
/// 通知去重服务实现
/// </summary>
/// <remarks>
/// 使用缓存实现通知去重逻辑，防止短时间内重复推送
/// 去重策略：
/// - 基于 {userId}:{notificationType}:{businessId} 生成去重键
/// - 默认去重窗口：5 分钟
/// - 适用场景：点赞通知（防止同一用户多次点赞/取消点赞产生多条通知）
/// </remarks>
public class NotificationDedupService : INotificationDedupService
{
    private readonly ICaching _caching;
    private readonly ILogger<NotificationDedupService> _logger;

    // 去重键前缀
    private const string DedupKeyPrefix = "notification:dedup:";

    public NotificationDedupService(
        ICaching caching,
        ILogger<NotificationDedupService> logger)
    {
        _caching = caching;
        _logger = logger;
    }

    /// <summary>
    /// 检查通知是否应该被去重
    /// </summary>
    public async Task<bool> ShouldDedupAsync(long userId, string notificationType, long businessId)
    {
        try
        {
            var dedupKey = GetDedupKey(userId, notificationType, businessId);

            // 检查缓存中是否存在去重键
            var exists = await _caching.ExistsAsync(dedupKey);

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(1, "DedupCheckFailed"), "Notification deduplication cache operation failed; failure={failureKind}.",
                RuntimeFailureSummary.Classify(ex));

            // 失败时不去重，确保通知可以发送
            return false;
        }
    }

    /// <summary>
    /// 记录通知去重键
    /// </summary>
    public async Task RecordDedupKeyAsync(long userId, string notificationType, long businessId, int windowSeconds = 300)
    {
        try
        {
            var dedupKey = GetDedupKey(userId, notificationType, businessId);
            var expiration = TimeSpan.FromSeconds(windowSeconds);

            // 写入去重键，设置过期时间
            await _caching.SetStringAsync(dedupKey, "1", expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(2, "DedupRecordFailed"), "Notification deduplication cache operation failed; failure={failureKind}.",
                RuntimeFailureSummary.Classify(ex));
        }
    }

    /// <summary>
    /// 清除通知去重键
    /// </summary>
    public async Task ClearDedupKeyAsync(long userId, string notificationType, long businessId)
    {
        try
        {
            var dedupKey = GetDedupKey(userId, notificationType, businessId);
            await _caching.RemoveAsync(dedupKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(3, "DedupClearFailed"), "Notification deduplication cache operation failed; failure={failureKind}.",
                RuntimeFailureSummary.Classify(ex));
        }
    }

    /// <summary>
    /// 生成去重键
    /// </summary>
    private static string GetDedupKey(long userId, string notificationType, long businessId)
    {
        return $"{DedupKeyPrefix}{userId}:{notificationType}:{businessId}";
    }
}
