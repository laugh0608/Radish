using Microsoft.AspNetCore.Http;

namespace Radish.Model.ViewModels;

/// <summary>
/// 分片上传会话 ViewModel
/// </summary>
public class UploadSessionVo
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件总大小（字节）
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 分片大小（字节）
    /// </summary>
    public int ChunkSize { get; set; }

    /// <summary>
    /// 总分片数
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// 已上传分片数
    /// </summary>
    public int UploadedChunks { get; set; }

    /// <summary>
    /// 已上传分片索引列表
    /// </summary>
    public List<int> UploadedChunkIndexes { get; set; } = new();

    /// <summary>
    /// 上传进度（0-100）
    /// </summary>
    public decimal Progress { get; set; }

    /// <summary>
    /// 会话状态
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 最终附件ID（完成后）
    /// </summary>
    public long? AttachmentId { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }
}

/// <summary>
/// 创建上传会话请求
/// </summary>
public class CreateUploadSessionDto
{
    /// <summary>
    /// 原始文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件总大小（字节）
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 文件 MIME 类型
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// 分片大小（字节，默认 2MB）
    /// </summary>
    public int ChunkSize { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// 业务类型
    /// </summary>
    public string BusinessType { get; set; } = "General";

    /// <summary>
    /// 业务ID（可选）
    /// </summary>
    public long? BusinessId { get; set; }
}

/// <summary>
/// 上传分片请求
/// </summary>
public class UploadChunkDto
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 分片索引（从0开始）
    /// </summary>
    public int ChunkIndex { get; set; }

    /// <summary>
    /// 分片数据
    /// </summary>
    public IFormFile ChunkData { get; set; } = null!;
}

/// <summary>
/// 合并分片请求
/// </summary>
public class MergeChunksDto
{
    /// <summary>
    /// 会话ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

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
    /// 水印文本
    /// </summary>
    public string? WatermarkText { get; set; }

    /// <summary>
    /// 是否移除 EXIF
    /// </summary>
    public bool RemoveExif { get; set; } = true;
}
