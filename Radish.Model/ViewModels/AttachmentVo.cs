namespace Radish.Model.ViewModels;

/// <summary>
/// 附件视图模型
/// </summary>
public class AttachmentVo
{
    /// <summary>
    /// 附件 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string VoOriginalName { get; set; } = string.Empty;

    /// <summary>
    /// 文件扩展名
    /// </summary>
    public string VoExtension { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long VoFileSize { get; set; }

    /// <summary>
    /// 文件大小（格式化后，如 1.5MB）
    /// </summary>
    public string VoFileSizeFormatted { get; set; } = string.Empty;

    /// <summary>
    /// MIME 类型
    /// </summary>
    public string VoMimeType { get; set; } = string.Empty;

    /// <summary>
    /// 存储类型（Local/MinIO/OSS）
    /// </summary>
    public string VoStorageType { get; set; } = "Local";

    /// <summary>
    /// 访问 URL
    /// </summary>
    public string VoUrl { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图 URL
    /// </summary>
    public string? VoThumbnailUrl { get; set; }

    /// <summary>
    /// 上传者 ID
    /// </summary>
    public long VoUploaderId { get; set; }

    /// <summary>
    /// 上传者名称
    /// </summary>
    public string VoUploaderName { get; set; } = string.Empty;

    /// <summary>
    /// 业务类型（Post/Comment/Avatar/Document）
    /// </summary>
    public string VoBusinessType { get; set; } = string.Empty;

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? VoBusinessId { get; set; }

    /// <summary>
    /// 是否公开
    /// </summary>
    public bool VoIsPublic { get; set; } = true;

    /// <summary>
    /// 下载次数
    /// </summary>
    public int VoDownloadCount { get; set; }

    /// <summary>
    /// 内容审核状态（Pending/Pass/Reject）
    /// </summary>
    public string? VoAuditStatus { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? VoRemark { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }
}
