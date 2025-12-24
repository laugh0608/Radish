using Microsoft.Extensions.Options;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.IService;
using Serilog;

namespace Radish.Service;

/// <summary>
/// 上传限流服务实现
/// </summary>
public class UploadRateLimitService : IUploadRateLimitService
{
    private readonly ICaching _cache;
    private readonly UploadRateLimitOptions _options;

    public UploadRateLimitService(ICaching cache, IOptions<UploadRateLimitOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    /// <summary>
    /// 检查用户是否可以开始新的上传
    /// </summary>
    public async Task<(bool IsAllowed, string? ErrorMessage)> CheckUploadAllowedAsync(long userId, long fileSize)
    {
        // 如果限流未启用，直接允许
        if (!_options.Enable)
        {
            return (true, null);
        }

        // 1. 检查并发上传限制
        var concurrentCount = await GetConcurrentUploadCountAsync(userId);
        if (concurrentCount >= _options.MaxConcurrentUploads)
        {
            Log.Warning("[UploadRateLimit] 用户 {UserId} 达到并发上传限制: {Count}/{Max}",
                userId, concurrentCount, _options.MaxConcurrentUploads);
            return (false, $"您当前有 {concurrentCount} 个文件正在上传，已达到并发上传限制（最多 {_options.MaxConcurrentUploads} 个）");
        }

        // 2. 检查每分钟上传次数限制
        var uploadsThisMinute = await GetUploadsThisMinuteAsync(userId);
        if (uploadsThisMinute >= _options.MaxUploadsPerMinute)
        {
            Log.Warning("[UploadRateLimit] 用户 {UserId} 达到速率限制: {Count}/{Max} 次/分钟",
                userId, uploadsThisMinute, _options.MaxUploadsPerMinute);
            return (false, $"您本分钟已上传 {uploadsThisMinute} 个文件，已达到速率限制（最多 {_options.MaxUploadsPerMinute} 次/分钟），请稍后再试");
        }

        // 3. 检查每日上传大小限制
        var uploadedSizeToday = await GetUploadedSizeTodayAsync(userId);
        if (uploadedSizeToday + fileSize > _options.MaxDailyUploadSize)
        {
            var remainingSize = _options.MaxDailyUploadSize - uploadedSizeToday;
            Log.Warning("[UploadRateLimit] 用户 {UserId} 达到日上传大小限制: {Used}/{Max}",
                userId, FormatFileSize(uploadedSizeToday), FormatFileSize(_options.MaxDailyUploadSize));
            return (false, $"您今日已上传 {FormatFileSize(uploadedSizeToday)}，剩余配额 {FormatFileSize(remainingSize)}，无法上传此文件（{FormatFileSize(fileSize)}）");
        }

        return (true, null);
    }

    /// <summary>
    /// 记录上传开始
    /// </summary>
    public async Task RecordUploadStartAsync(long userId, string uploadId)
    {
        var key = GetConcurrentKey(userId);
        var value = await _cache.GetStringAsync(key) ?? "";
        var uploadIds = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (!uploadIds.Contains(uploadId))
        {
            uploadIds.Add(uploadId);
            await _cache.SetStringAsync(key, string.Join(',', uploadIds), TimeSpan.FromHours(1));
            Log.Information("[UploadRateLimit] 用户 {UserId} 开始上传: {UploadId}, 当前并发: {Count}",
                userId, uploadId, uploadIds.Count);
        }
    }

    /// <summary>
    /// 记录上传完成
    /// </summary>
    public async Task RecordUploadCompleteAsync(long userId, string uploadId, long fileSize)
    {
        // 1. 减少并发计数
        await RemoveConcurrentUploadAsync(userId, uploadId);

        // 2. 增加速率计数
        await IncrementRateCountAsync(userId);

        // 3. 增加日上传大小
        await IncrementDailySizeAsync(userId, fileSize);

        Log.Information("[UploadRateLimit] 用户 {UserId} 完成上传: {UploadId}, 大小: {Size}",
            userId, uploadId, FormatFileSize(fileSize));
    }

    /// <summary>
    /// 记录上传失败
    /// </summary>
    public async Task RecordUploadFailedAsync(long userId, string uploadId)
    {
        await RemoveConcurrentUploadAsync(userId, uploadId);
        Log.Information("[UploadRateLimit] 用户 {UserId} 上传失败: {UploadId}", userId, uploadId);
    }

    /// <summary>
    /// 获取用户上传统计信息
    /// </summary>
    public async Task<UploadStatistics> GetUploadStatisticsAsync(long userId)
    {
        var concurrentCount = await GetConcurrentUploadCountAsync(userId);
        var uploadsThisMinute = await GetUploadsThisMinuteAsync(userId);
        var uploadedSizeToday = await GetUploadedSizeTodayAsync(userId);

        return new UploadStatistics
        {
            CurrentConcurrentUploads = concurrentCount,
            UploadsThisMinute = uploadsThisMinute,
            UploadedSizeToday = uploadedSizeToday,
            MaxConcurrentUploads = _options.MaxConcurrentUploads,
            MaxUploadsPerMinute = _options.MaxUploadsPerMinute,
            MaxDailyUploadSize = _options.MaxDailyUploadSize
        };
    }

    /// <summary>
    /// 重置用户限流计数
    /// </summary>
    public async Task ResetUserLimitsAsync(long userId)
    {
        await _cache.RemoveAsync(GetConcurrentKey(userId));
        await _cache.RemoveAsync(GetRateKey(userId));
        await _cache.RemoveAsync(GetDailySizeKey(userId));
        Log.Information("[UploadRateLimit] 重置用户 {UserId} 的限流计数", userId);
    }

    #region 私有方法

    /// <summary>
    /// 获取当前并发上传数
    /// </summary>
    private async Task<int> GetConcurrentUploadCountAsync(long userId)
    {
        var key = GetConcurrentKey(userId);
        var value = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(value))
            return 0;

        var uploadIds = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
        return uploadIds.Length;
    }

    /// <summary>
    /// 移除并发上传记录
    /// </summary>
    private async Task RemoveConcurrentUploadAsync(long userId, string uploadId)
    {
        var key = GetConcurrentKey(userId);
        var value = await _cache.GetStringAsync(key) ?? "";
        var uploadIds = value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (uploadIds.Remove(uploadId))
        {
            if (uploadIds.Count > 0)
            {
                await _cache.SetStringAsync(key, string.Join(',', uploadIds), TimeSpan.FromHours(1));
            }
            else
            {
                await _cache.RemoveAsync(key);
            }
        }
    }

    /// <summary>
    /// 获取本分钟上传次数
    /// </summary>
    private async Task<int> GetUploadsThisMinuteAsync(long userId)
    {
        var key = GetRateKey(userId);
        var value = await _cache.GetStringAsync(key);
        return int.TryParse(value, out var count) ? count : 0;
    }

    /// <summary>
    /// 增加速率计数
    /// </summary>
    private async Task IncrementRateCountAsync(long userId)
    {
        var key = GetRateKey(userId);
        var value = await _cache.GetStringAsync(key);
        var count = int.TryParse(value, out var c) ? c : 0;
        count++;
        await _cache.SetStringAsync(key, count.ToString(), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// 获取今日已上传大小
    /// </summary>
    private async Task<long> GetUploadedSizeTodayAsync(long userId)
    {
        var key = GetDailySizeKey(userId);
        var value = await _cache.GetStringAsync(key);
        return long.TryParse(value, out var size) ? size : 0;
    }

    /// <summary>
    /// 增加日上传大小
    /// </summary>
    private async Task IncrementDailySizeAsync(long userId, long fileSize)
    {
        var key = GetDailySizeKey(userId);
        var value = await _cache.GetStringAsync(key);
        var size = long.TryParse(value, out var s) ? s : 0;
        size += fileSize;

        // 计算到今天结束的剩余时间
        var now = DateTime.Now;
        var endOfDay = now.Date.AddDays(1);
        var ttl = endOfDay - now;

        await _cache.SetStringAsync(key, size.ToString(), ttl);
    }

    /// <summary>
    /// 获取并发上传 Redis 键
    /// </summary>
    private string GetConcurrentKey(long userId) => $"upload:concurrent:{userId}";

    /// <summary>
    /// 获取速率限制 Redis 键（按分钟）
    /// </summary>
    private string GetRateKey(long userId)
    {
        var minute = DateTime.Now.ToString("yyyyMMddHHmm");
        return $"upload:rate:{userId}:{minute}";
    }

    /// <summary>
    /// 获取日上传大小 Redis 键
    /// </summary>
    private string GetDailySizeKey(long userId)
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        return $"upload:daily:{userId}:{date}";
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #endregion
}
