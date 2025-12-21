namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 文件上传选项
/// </summary>
public class FileUploadOptions
{
    /// <summary>
    /// 原始文件名
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型（Post/Comment/Avatar/Document）
    /// </summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>
    /// 是否生成缩略图
    /// </summary>
    public bool GenerateThumbnail { get; set; } = true;

    /// <summary>
    /// 是否生成多尺寸
    /// </summary>
    public bool GenerateMultipleSizes { get; set; } = false;

    /// <summary>
    /// 是否添加水印
    /// </summary>
    public bool AddWatermark { get; set; } = false;

    /// <summary>
    /// 水印文本（如果添加水印）
    /// </summary>
    public string? WatermarkText { get; set; }

    /// <summary>
    /// 是否计算文件哈希
    /// </summary>
    public bool CalculateHash { get; set; } = true;

    /// <summary>
    /// 是否移除 EXIF 信息
    /// </summary>
    public bool RemoveExif { get; set; } = true;
}
