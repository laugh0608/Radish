using Radish.Model.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Radish.Common;
using Radish.Common.OptionTool;
using SqlSugar;
using System.Security.Cryptography;
using Radish.Model.ViewModels;

namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 本地文件存储实现
/// </summary>
/// <remarks>
/// 将文件存储在服务器本地文件系统中
/// 目录结构：{RootPath}/{BusinessType}/{Year}/{Month}/{UniqueFileName}
/// </remarks>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    private readonly string _rootPath;
    private readonly IWebHostEnvironment _environment;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;

        // 将相对路径转换为绝对路径
        _rootPath = Path.IsPathRooted(_options.Local.BasePath)
            ? _options.Local.BasePath
            : Path.Combine(_environment.ContentRootPath, _options.Local.BasePath);

        // 确保根目录存在
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    #region Upload

    /// <summary>
    /// 上传文件
    /// </summary>
    public async Task<FileUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        FileUploadOptionsDto? options = null)
    {
        try
        {
            options ??= new FileUploadOptionsDto();

            // 根据业务类型获取文件大小限制
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var maxSize = GetMaxFileSizeByType(options.BusinessType, extension);

            // 验证文件大小
            if (stream.Length > maxSize)
            {
                return FileUploadResult.Fail($"文件大小超过限制（最大 {maxSize / 1024 / 1024}MB）");
            }

            // 验证文件类型
            if (!IsAllowedFileType(extension))
            {
                return FileUploadResult.Fail($"不支持的文件类型：{extension}");
            }

            // 验证文件头（Magic Number）防止扩展名伪装
            var isValidMagicNumber = await ValidateFileMagicNumberAsync(stream, extension);
            if (!isValidMagicNumber)
            {
                return FileUploadResult.Fail($"文件内容与扩展名不匹配，可能是伪装文件");
            }

            // 生成唯一文件名（使用 Snowflake ID + 原始扩展名）
            var uniqueId = SnowFlakeSingle.instance.getID();
            var storedName = $"{uniqueId}{extension}";

            // 构建存储路径：{BusinessType}/{Year}/{Month}/{FileName}
            var now = DateTime.Now;
            var businessType = string.IsNullOrWhiteSpace(options.BusinessType) ? "General" : options.BusinessType;
            var relativePath = Path.Combine(
                businessType,
                now.Year.ToString(),
                now.Month.ToString("D2"),
                storedName
            );

            // 标准化路径分隔符为正斜杠（URL 格式）
            relativePath = relativePath.Replace('\\', '/');

            var fullPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // 确保目录存在
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 计算文件哈希（如果需要）
            string? fileHash = null;
            if (options.CalculateHash)
            {
                stream.Position = 0;
                fileHash = await CalculateFileHashAsync(stream);
            }

            // 保存文件
            stream.Position = 0;
            await using (var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fileStream);
            }

            // 生成访问 URL
            var url = GetFileUrl(relativePath);

            // 生成缩略图路径（如果需要）
            string? thumbnailPath = null;
            if (options.GenerateThumbnail && IsImageFile(extension))
            {
                thumbnailPath = GenerateThumbnailPath(relativePath);
                // 注意：实际的缩略图生成将在 IImageProcessor 中完成
            }

            var result = FileUploadResult.Ok(
                storedName: storedName,
                storagePath: relativePath,
                url: url,
                fileSize: stream.Length,
                fileHash: fileHash
            );
            result.ThumbnailPath = thumbnailPath;

            return result;
        }
        catch (Exception ex)
        {
            return FileUploadResult.Fail($"文件上传失败：{ex.Message}");
        }
    }

    #endregion

    #region Delete

    /// <summary>
    /// 删除文件
    /// </summary>
    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);

            if (!File.Exists(fullPath))
            {
                return false;
            }

            await Task.Run(() => File.Delete(fullPath));

            // 删除缩略图（如果存在）
            var thumbnailPath = GenerateThumbnailPath(filePath);
            var fullThumbnailPath = Path.Combine(_rootPath, thumbnailPath);
            if (File.Exists(fullThumbnailPath))
            {
                await Task.Run(() => File.Delete(fullThumbnailPath));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Download

    /// <summary>
    /// 下载文件
    /// </summary>
    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var memoryStream = new MemoryStream();
            await using (var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(memoryStream);
            }

            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region GetFileUrl

    /// <summary>
    /// 获取文件访问 URL
    /// </summary>
    public string GetFileUrl(string filePath)
    {
        // 将 Windows 路径分隔符替换为 URL 分隔符
        var urlPath = filePath.Replace('\\', '/');
        return $"{_options.Local.BaseUrl}/{urlPath}";
    }

    #endregion

    #region Exists

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string filePath)
    {
        var fullPath = Path.Combine(_rootPath, filePath);
        return await Task.FromResult(File.Exists(fullPath));
    }

    #endregion

    #region GetFileInfo

    /// <summary>
    /// 获取文件信息
    /// </summary>
    public async Task<FileStorageInfo?> GetFileInfoAsync(string filePath)
    {
        try
        {
            var fullPath = Path.Combine(_rootPath, filePath);

            if (!File.Exists(fullPath))
            {
                return null;
            }

            var fileInfo = new FileInfo(fullPath);

            // 根据扩展名推断 MIME 类型
            var contentType = GetContentType(fileInfo.Extension);

            return await Task.FromResult(new FileStorageInfo
            {
                FilePath = filePath,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                ContentType = contentType
            });
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region GetFullPath

    /// <summary>
    /// 获取文件的完整物理路径
    /// </summary>
    public string GetFullPath(string relativePath)
    {
        return Path.Combine(_rootPath, relativePath);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// 验证文件类型是否允许
    /// </summary>
    private bool IsAllowedFileType(string extension)
    {
        return _options.AllowedExtensions.Image.Contains(extension) ||
               _options.AllowedExtensions.Document.Contains(extension) ||
               _options.AllowedExtensions.Video.Contains(extension) ||
               _options.AllowedExtensions.Audio.Contains(extension);
    }

    /// <summary>
    /// 判断是否为图片文件
    /// </summary>
    private bool IsImageFile(string extension)
    {
        return _options.AllowedExtensions.Image.Contains(extension);
    }

    /// <summary>
    /// 验证文件头（Magic Number）是否与扩展名匹配
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>是否匹配</returns>
    private async Task<bool> ValidateFileMagicNumberAsync(Stream stream, string extension)
    {
        // 文件签名字典（Magic Numbers）
        var fileSignatures = new Dictionary<string, byte[][]>
        {
            // 图片格式
            [".jpg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },
            [".png"] = new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
            [".gif"] = new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } }, // GIF8
            [".bmp"] = new[] { new byte[] { 0x42, 0x4D } }, // BM
            [".webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }, // RIFF (需要进一步检查 WEBP)

            // 文档格式
            [".pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }, // %PDF
            [".doc"] = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } }, // OLE2
            [".docx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, // ZIP (Office Open XML)
            [".xlsx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, // ZIP
            [".pptx"] = new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } }, // ZIP
            [".xls"] = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } }, // OLE2
            [".ppt"] = new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } }, // OLE2

            // 文本格式（通常没有固定签名，跳过检查）
            [".txt"] = Array.Empty<byte[]>(),
            [".md"] = Array.Empty<byte[]>(),
            [".svg"] = Array.Empty<byte[]>() // SVG 是 XML 文本
        };

        // 如果扩展名不在字典中，默认通过（不检查）
        if (!fileSignatures.ContainsKey(extension))
        {
            return true;
        }

        var signatures = fileSignatures[extension];

        // 如果没有签名定义（如文本文件），跳过检查
        if (signatures.Length == 0)
        {
            return true;
        }

        // 读取文件头（最多 8 字节）
        var headerBytes = new byte[8];
        var originalPosition = stream.Position;
        stream.Position = 0;

        var bytesRead = await stream.ReadAsync(headerBytes, 0, headerBytes.Length);
        stream.Position = originalPosition; // 恢复原始位置

        if (bytesRead == 0)
        {
            return false;
        }

        // 检查是否匹配任一签名
        foreach (var signature in signatures)
        {
            if (bytesRead >= signature.Length)
            {
                var matches = true;
                for (int i = 0; i < signature.Length; i++)
                {
                    if (headerBytes[i] != signature[i])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 根据业务类型和文件扩展名获取文件大小限制
    /// </summary>
    /// <param name="businessType">业务类型（Avatar/Post/Document等）</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>文件大小限制（字节）</returns>
    private long GetMaxFileSizeByType(string businessType, string extension)
    {
        // 如果明确指定了 Avatar，使用头像限制
        if (businessType?.Equals("Avatar", StringComparison.OrdinalIgnoreCase) == true)
        {
            return _options.MaxFileSize.Avatar;
        }

        // 根据文件扩展名判断文件类型
        if (_options.AllowedExtensions.Image.Contains(extension))
        {
            return _options.MaxFileSize.Image;
        }

        if (_options.AllowedExtensions.Document.Contains(extension))
        {
            return _options.MaxFileSize.Document;
        }

        if (_options.AllowedExtensions.Video.Contains(extension))
        {
            return _options.MaxFileSize.Video;
        }

        if (_options.AllowedExtensions.Audio.Contains(extension))
        {
            return _options.MaxFileSize.Audio;
        }

        // 默认使用文档限制（10MB）
        return _options.MaxFileSize.Document;
    }

    /// <summary>
    /// 生成缩略图路径
    /// </summary>
    /// <remarks>
    /// 在原文件名后添加 _thumb 后缀
    /// 例如：Post/2025/12/123456.jpg -> Post/2025/12/123456_thumb.jpg
    /// </remarks>
    private string GenerateThumbnailPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);

        var thumbnailFileName = $"{fileName}_thumb{extension}";
        var thumbnailPath = Path.Combine(directory, thumbnailFileName);

        // 标准化路径分隔符为正斜杠（URL 格式）
        return thumbnailPath.Replace('\\', '/');
    }

    /// <summary>
    /// 计算文件 SHA256 哈希值
    /// </summary>
    private async Task<string> CalculateFileHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 根据文件扩展名获取 MIME 类型
    /// </summary>
    private string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",

            // Documents
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".md" => "text/markdown",

            // Videos
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".flv" => "video/x-flv",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",

            // Audios
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".wma" => "audio/x-ms-wma",

            // Default
            _ => "application/octet-stream"
        };
    }

    #endregion
}
