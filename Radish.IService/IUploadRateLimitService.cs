namespace Radish.IService;

/// <summary>
/// 上传限流服务接口
/// </summary>
public interface IUploadRateLimitService
{
    /// <summary>
    /// 原子检查并预留一次上传的并发与日容量；成功取得预留即计入本分钟上传尝试次数。
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="uploadId">上传 ID（用于标识单次上传）</param>
    /// <param name="fileSize">文件大小（字节）</param>
    /// <param name="reservationLifetime">预留有效期；普通上传默认 1 小时，分片上传应传入会话有效期。</param>
    /// <returns>是否成功取得上传预留</returns>
    Task<UploadRateLimitCheckResult> AcquireUploadAsync(
        long userId,
        string uploadId,
        long fileSize,
        TimeSpan? reservationLifetime = null);

    /// <summary>
    /// 原子结算已成功的上传：释放并发预留，并将预留容量计入预留时所属业务日期的已用配额。
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="uploadId">上传 ID</param>
    Task CompleteUploadAsync(long userId, string uploadId);

    /// <summary>
    /// 原子释放失败或取消的上传预留，不消费日容量；已发生的分钟尝试次数保留。
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="uploadId">上传 ID</param>
    Task FailUploadAsync(long userId, string uploadId);

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
/// 上传限流拒绝原因。
/// </summary>
public enum UploadRateLimitFailureKind
{
    ConcurrentUploads,
    UploadFrequency,
    DailyUploadSize
}

/// <summary>
/// 上传限流检查结果。
/// </summary>
public sealed class UploadRateLimitCheckResult
{
    public bool IsAllowed { get; init; }

    public string? ErrorMessage { get; init; }

    public UploadRateLimitFailureKind? FailureKind { get; init; }

    public object[] MessageArguments { get; init; } = Array.Empty<object>();

    public static UploadRateLimitCheckResult Allowed() => new() { IsAllowed = true };

    public static UploadRateLimitCheckResult Rejected(
        string errorMessage,
        UploadRateLimitFailureKind failureKind,
        params object[] messageArguments)
    {
        return new UploadRateLimitCheckResult
        {
            IsAllowed = false,
            ErrorMessage = errorMessage,
            FailureKind = failureKind,
            MessageArguments = messageArguments ?? Array.Empty<object>()
        };
    }

    public void Deconstruct(out bool isAllowed, out string? errorMessage)
    {
        isAllowed = IsAllowed;
        errorMessage = ErrorMessage;
    }
}

/// <summary>
/// 上传统计信息
/// </summary>
public class UploadStatistics
{
    /// <summary>当前并发上传数</summary>
    public int CurrentConcurrentUploads { get; set; }

    /// <summary>本分钟已获准发起的上传尝试数（含进行中和最终失败的上传）</summary>
    public int UploadsThisMinute { get; set; }

    /// <summary>今日已上传总大小（字节）</summary>
    public long UploadedSizeToday { get; set; }

    /// <summary>今日正在上传且已预留的大小（字节）</summary>
    public long ReservedUploadSizeToday { get; set; }

    /// <summary>今日已用与预留容量合计（字节）</summary>
    public long OccupiedUploadSizeToday => UploadedSizeToday + ReservedUploadSizeToday;

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
    public bool IsDailySizeLimitReached => OccupiedUploadSizeToday >= MaxDailyUploadSize;

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
