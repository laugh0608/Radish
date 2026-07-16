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
    /// 稳定失败类别，供业务层映射 HTTP 状态和对外错误码。
    /// </summary>
    public FileUploadFailureKind FailureKind { get; set; }

    /// <summary>
    /// 存储文件名（唯一标识）
    /// </summary>
    public string StoredName { get; set; } = string.Empty;

    /// <summary>
    /// 存储路径（相对路径）
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

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
    /// 由服务端根据已验证文件类型确定的 MIME 类型。
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// 可安全公开的本地化格式参数。
    /// </summary>
    public object[] MessageArguments { get; set; } = Array.Empty<object>();

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static FileUploadResult Ok(
        string storedName,
        string storagePath,
        long fileSize,
        string contentType,
        string? fileHash = null)
    {
        return new FileUploadResult
        {
            Success = true,
            StoredName = storedName,
            StoragePath = storagePath,
            FileSize = fileSize,
            ContentType = contentType,
            FileHash = fileHash
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static FileUploadResult Fail(
        FileUploadFailureKind failureKind,
        string errorMessage,
        params object[] messageArguments)
    {
        if (failureKind == FileUploadFailureKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "A failed upload must have a failure kind.");
        }

        return new FileUploadResult
        {
            Success = false,
            FailureKind = failureKind,
            ErrorMessage = errorMessage,
            MessageArguments = messageArguments ?? Array.Empty<object>()
        };
    }
}
