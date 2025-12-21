using Radish.Model.DTOs;

namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 文件存储接口
/// </summary>
/// <remarks>
/// 定义统一的文件存储接口，支持多种存储实现（本地文件系统、MinIO、OSS等）
/// </remarks>
public interface IFileStorage
{
    /// <summary>
    /// 上传文件
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="fileName">文件名</param>
    /// <param name="contentType">MIME 类型</param>
    /// <param name="options">上传选项</param>
    /// <returns>上传结果</returns>
    Task<FileUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        FileUploadOptions? options = null);

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="filePath">文件路径（相对路径）</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteAsync(string filePath);

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="filePath">文件路径（相对路径）</param>
    /// <returns>文件流</returns>
    Task<Stream?> DownloadAsync(string filePath);

    /// <summary>
    /// 获取文件访问 URL
    /// </summary>
    /// <param name="filePath">文件路径（相对路径）</param>
    /// <returns>访问 URL</returns>
    string GetFileUrl(string filePath);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="filePath">文件路径（相对路径）</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string filePath);

    /// <summary>
    /// 获取文件信息
    /// </summary>
    /// <param name="filePath">文件路径（相对路径）</param>
    /// <returns>文件信息（文件大小、修改时间等）</returns>
    Task<FileStorageInfo?> GetFileInfoAsync(string filePath);

    /// <summary>
    /// 获取文件的完整物理路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>完整物理路径</returns>
    string GetFullPath(string relativePath);
}

/// <summary>
/// 文件存储信息
/// </summary>
public class FileStorageInfo
{
    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; }

    /// <summary>
    /// 内容类型
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
}
