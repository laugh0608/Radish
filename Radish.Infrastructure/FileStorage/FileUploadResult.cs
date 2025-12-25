namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 文件上传结果
/// </summary>
public class FileUploadResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 存储文件名（唯一标识）
    /// </summary>
    public string StoredName { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径（相对路径）
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// 访问 URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 缩略图路径
    /// </summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 文件哈希值（SHA256）
    /// </summary>
    public string? FileHash { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static FileUploadResult Ok(string storedName, string storagePath, string url, long fileSize, string? fileHash = null)
    {
        return new FileUploadResult
        {
            Success = true,
            StoredName = storedName,
            StoragePath = storagePath,
            Url = url,
            FileSize = fileSize,
            FileHash = fileHash
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static FileUploadResult Fail(string errorMessage)
    {
        return new FileUploadResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
