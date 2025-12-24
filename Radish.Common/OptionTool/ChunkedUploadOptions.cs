using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>
/// 分片上传配置选项
/// </summary>
public class ChunkedUploadOptions : IConfigurableOptions
{
    /// <summary>
    /// 是否启用分片上传
    /// </summary>
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 默认分片大小（字节）
    /// 默认 2MB
    /// </summary>
    public int DefaultChunkSize { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// 最小分片大小（字节）
    /// 默认 1MB
    /// </summary>
    public int MinChunkSize { get; set; } = 1 * 1024 * 1024;

    /// <summary>
    /// 最大分片大小（字节）
    /// 默认 10MB
    /// </summary>
    public int MaxChunkSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// 会话过期时间（小时）
    /// 默认 24 小时
    /// </summary>
    public int SessionExpirationHours { get; set; } = 24;

    /// <summary>
    /// 临时文件存储路径
    /// </summary>
    public string TempChunkPath { get; set; } = "DataBases/Temp/Chunks";
}
