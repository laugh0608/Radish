namespace Radish.Model.DtoModels;

/// <summary>
/// 轻量附件资源信息
/// </summary>
public class AttachmentAssetDto
{
    /// <summary>
    /// 附件 ID
    /// </summary>
    public long AttachmentId { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// 上传者 ID
    /// </summary>
    public long UploaderId { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? BusinessId { get; set; }

    /// <summary>
    /// 原图地址
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图地址
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}
