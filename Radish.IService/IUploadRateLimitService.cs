namespace Radish.IService;

/// <summary>
/// 上传限流服务接口
/// </summary>
public interface IUploadRateLimitService
{
    /// <summary>
    /// 检查用户是否可以开始新的上传
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <returns>是否允许上传</returns>
    Task<(bool IsAllowed, string? ErrorMessage)> CheckUploadAllowedAsync(long userId, long fileSize);

    /// <summary>
    /// 记录上传开始（增加并发计数）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="uploadId">上传 ID（用于标识单次上传）</param>
    Task RecordUploadStartAsync(long userId, string uploadId);

    /// <summary>
    /// 记录上传完成（减少并发计数，增加速率和大小计数）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="uploadId">上传 ID</param>
    /// <param name="fileSize">文件大小（字节）</param>
    Task RecordUploadCompleteAsync(long userId, string uploadId, long fileSize);

    /// <summary>
    /// 记录上传失败（减少并发计数）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="uploadId">上传 ID</param>
    Task RecordUploadFailedAsync(long userId, string uploadId);

    /// <summary>
    /// 获取用户当前上传统计信息
    /// </summary>
    /// <param name="userId">用户 ID</param>
    Task<UploadStatistics> GetUploadStatisticsAsync(long userId);

    /// <summary>
    /// 重置用户限流计数（管理员功能）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    Task ResetUserLimitsAsync(long userId);
}

/// <summary>
/// 上传统计信息
/// </summary>
public class UploadStatistics
{
    /// <summary>当前并发上传数</summary>
    public int CurrentConcurrentUploads { get; set; }

    /// <summary>本分钟已上传文件数</summary>
    public int UploadsThisMinute { get; set; }

    /// <summary>今日已上传总大小（字节）</summary>
    public long UploadedSizeToday { get; set; }

    /// <summary>今日已上传总大小（格式化）</summary>
    public string UploadedSizeTodayFormatted => FormatFileSize(UploadedSizeToday);

    /// <summary>并发上传限制</summary>
    public int MaxConcurrentUploads { get; set; }

    /// <summary>每分钟上传限制</summary>
    public int MaxUploadsPerMinute { get; set; }

    /// <summary>每日上传大小限制（字节）</summary>
    public long MaxDailyUploadSize { get; set; }

    /// <summary>每日上传大小限制（格式化）</summary>
    public string MaxDailyUploadSizeFormatted => FormatFileSize(MaxDailyUploadSize);

    /// <summary>是否达到并发限制</summary>
    public bool IsConcurrentLimitReached => CurrentConcurrentUploads >= MaxConcurrentUploads;

    /// <summary>是否达到速率限制</summary>
    public bool IsRateLimitReached => UploadsThisMinute >= MaxUploadsPerMinute;

    /// <summary>是否达到日上传大小限制</summary>
    public bool IsDailySizeLimitReached => UploadedSizeToday >= MaxDailyUploadSize;

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
}
