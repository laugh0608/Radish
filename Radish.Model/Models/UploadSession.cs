using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.Models;

/// <summary>
/// 分片上传会话
/// </summary>
[SugarTable("UploadSession")]
public class UploadSession : RootEntityTKey<long>
{
    /// <summary>
    /// 会话ID（GUID）
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名
    /// </summary>
    [SugarColumn(Length = 255, IsNullable = false)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 文件总大小（字节）
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// 文件 MIME 类型
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? MimeType { get; set; }

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
    /// 已上传分片索引列表（JSON 数组）
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? UploadedChunkIndexes { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string BusinessType { get; set; } = string.Empty;

    /// <summary>
    /// 业务ID（可选）
    /// </summary>
    public long? BusinessId { get; set; }

    /// <summary>
    /// 上传用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 上传用户名
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 会话状态（Uploading/Completed/Failed/Cancelled）
    /// </summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string Status { get; set; } = "Uploading";

    /// <summary>
    /// 最终附件ID（完成后）
    /// </summary>
    public long? AttachmentId { get; set; }

    /// <summary>
    /// 过期时间（默认24小时）
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }
}
