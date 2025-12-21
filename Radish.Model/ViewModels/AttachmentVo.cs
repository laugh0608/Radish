namespace Radish.Model.ViewModels;

/// <summary>
/// 附件视图模型
/// </summary>
public class AttachmentVo
{
    /// <summary>
    /// 附件 Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// 文件扩展名
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 文件大小（格式化后，如 1.5MB）
    /// </summary>
    public string FileSizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// 存储类型（Local/MinIO/OSS）
    /// </summary>
    public string StorageType { get; set; } = "Local";

    /// <summary>
    /// 访问 URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图 URL
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// 上传者 ID
    /// </summary>
    public long UploaderId { get; set; }

    /// <summary>
    /// 上传者名称
    /// </summary>
    public string UploaderName { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型（Post/Comment/Avatar/Document）
    /// </summary>
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? BusinessId { get; set; }

    /// <summary>
    /// 是否公开
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// 下载次数
    /// </summary>
    public int DownloadCount { get; set; }

    /// <summary>
    /// 内容审核状态（Pending/Pass/Reject）
    /// </summary>
    public string? AuditStatus { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}
