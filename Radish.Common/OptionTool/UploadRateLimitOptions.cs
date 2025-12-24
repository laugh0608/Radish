using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>
/// 上传限流配置选项
/// </summary>
public class UploadRateLimitOptions : IConfigurableOptions
{
    /// <summary>
    /// 是否启用上传限流
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 单用户最大并发上传数
    /// </summary>
    public int MaxConcurrentUploads { get; set; } = 5;

    /// <summary>
    /// 每分钟最多上传文件数
    /// </summary>
    public int MaxUploadsPerMinute { get; set; } = 20;

    /// <summary>
    /// 每日最大上传大小（字节）
    /// 默认 100MB = 100 * 1024 * 1024 = 104857600
    /// </summary>
    public long MaxDailyUploadSize { get; set; } = 104857600;
}
