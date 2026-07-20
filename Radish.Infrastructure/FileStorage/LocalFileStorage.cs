using Microsoft.Extensions.Options;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.OptionTool;
using SqlSugar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Serilog;

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

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _options = options.Value;

        // 统一相对于解决方案根目录解析，避免启动目录变化导致附件根路径漂移。
        var solutionRoot = AppPathTool.GetSolutionRootOrBasePath();
        _rootPath = Path.IsPathRooted(_options.Local.BasePath)
            ? Path.GetFullPath(_options.Local.BasePath)
            : Path.GetFullPath(Path.Combine(solutionRoot, _options.Local.BasePath));

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
        FileUploadOptionsDto? options = null)
    {
        string? writeTargetPath = null;

        try
        {
            options ??= new FileUploadOptionsDto();

            if (!AttachmentBusinessTypes.TryNormalize(options.BusinessType, out var businessType))
            {
                return FileUploadResult.Fail(
                    FileUploadFailureKind.InvalidBusinessType,
                    "不支持该附件业务类型");
            }

            // 根据业务类型获取文件大小限制
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var maxSize = FileStoragePolicy.GetMaxFileSize(_options, businessType, extension);

            // 验证文件大小
            if (stream.Length > maxSize)
            {
                return FileUploadResult.Fail(
                    FileUploadFailureKind.FileTooLarge,
                    $"文件大小超过限制（最大 {FormatFileSize(maxSize)}）",
                    FormatFileSize(maxSize));
            }

            // 验证文件类型
            if (!FileStoragePolicy.IsAllowedForBusinessType(
                    _options,
                    extension,
                    AttachmentBusinessTypes.RequiresImage(businessType)))
            {
                return FileUploadResult.Fail(
                    FileUploadFailureKind.UnsupportedType,
                    $"不支持的文件类型：{extension}");
            }

            // 验证文件头（Magic Number）防止扩展名伪装
            var isValidMagicNumber = await ValidateFileMagicNumberAsync(stream, extension);
            if (!isValidMagicNumber)
            {
                return FileUploadResult.Fail(
                    FileUploadFailureKind.ContentMismatch,
                    "文件内容与扩展名不匹配，可能是伪装文件");
            }

            // 生成唯一文件名（使用 Snowflake ID + 原始扩展名）
            var uniqueId = SnowFlakeSingle.instance.getID();
            var storedName = $"{uniqueId}{extension}";

            // 构建存储路径：{BusinessType}/{Year}/{Month}/{FileName}
            var now = DateTime.Now;
            var relativePath = Path.Combine(
                businessType,
                now.Year.ToString(),
                now.Month.ToString("D2"),
                storedName
            );

            // 标准化路径分隔符为正斜杠（URL 格式）
            relativePath = relativePath.Replace('\\', '/');

            var fullPath = ResolveContainedPath(relativePath);
            writeTargetPath = fullPath;

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
                fileSize: stream.Length,
                contentType: GetContentType(extension),
                fileHash: fileHash
            );
            result.ThumbnailPath = thumbnailPath;

            return result;
        }
        catch (Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(writeTargetPath) && File.Exists(writeTargetPath))
            {
                try
                {
                    File.Delete(writeTargetPath);
                }
                catch
                {
                    // 保留原始存储异常，上层会记录并按 StorageFailed 返回。
                }
            }

            Log.Error(exception, "本地附件存储失败：{FileName}", fileName);

            return FileUploadResult.Fail(
                FileUploadFailureKind.StorageFailed,
                "文件存储失败，请稍后重试");
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
            var fullPath = ResolveContainedPath(filePath);

            if (!File.Exists(fullPath))
            {
                return false;
            }

            await Task.Run(() => File.Delete(fullPath));

            // 删除缩略图（如果存在）
            var thumbnailPath = GenerateThumbnailPath(filePath);
            var fullThumbnailPath = ResolveContainedPath(thumbnailPath);
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
            var fullPath = ResolveContainedPath(filePath);

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

    #region Exists

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            var fullPath = ResolveContainedPath(filePath);
            return await Task.FromResult(File.Exists(fullPath));
        }
        catch
        {
            return false;
        }
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
            var fullPath = ResolveContainedPath(filePath);

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
        return ResolveContainedPath(relativePath);
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// 判断是否为图片文件
    /// </summary>
    private bool IsImageFile(string extension)
    {
        return FileStoragePolicy.IsImageExtension(_options, extension);
    }

    /// <summary>
    /// 验证文件头（Magic Number）是否与扩展名匹配
    /// </summary>
    /// <param name="stream">文件流</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>是否匹配</returns>
    private async Task<bool> ValidateFileMagicNumberAsync(Stream stream, string extension)
    {
        var originalPosition = stream.Position;
        try
        {
            if (extension is ".txt" or ".md")
            {
                return await ValidateUtf8TextAsync(stream);
            }

            if (extension is ".docx" or ".xlsx" or ".pptx")
            {
                return ValidateOfficeOpenXml(stream, extension);
            }

            stream.Position = 0;
            var header = new byte[16];
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));
            if (bytesRead == 0)
            {
                return false;
            }

            return extension switch
            {
                ".jpg" or ".jpeg" => StartsWith(header, bytesRead, [0xFF, 0xD8, 0xFF]),
                ".png" => StartsWith(header, bytesRead, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                ".gif" => StartsWith(header, bytesRead, [0x47, 0x49, 0x46, 0x38]),
                ".bmp" => StartsWith(header, bytesRead, [0x42, 0x4D]),
                ".webp" => StartsWith(header, bytesRead, [0x52, 0x49, 0x46, 0x46]) &&
                           MatchesAt(header, bytesRead, 8, [0x57, 0x45, 0x42, 0x50]),
                ".ico" => StartsWith(header, bytesRead, [0x00, 0x00, 0x01, 0x00]),
                ".pdf" => StartsWith(header, bytesRead, [0x25, 0x50, 0x44, 0x46]),
                ".doc" or ".xls" or ".ppt" => StartsWith(
                    header,
                    bytesRead,
                    [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
                ".mp4" or ".mov" => MatchesAt(header, bytesRead, 4, [0x66, 0x74, 0x79, 0x70]),
                ".avi" => StartsWith(header, bytesRead, [0x52, 0x49, 0x46, 0x46]) &&
                          MatchesAt(header, bytesRead, 8, [0x41, 0x56, 0x49, 0x20]),
                ".wmv" or ".wma" => StartsWith(
                    header,
                    bytesRead,
                    [0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11, 0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C]),
                ".flv" => StartsWith(header, bytesRead, [0x46, 0x4C, 0x56]),
                ".mkv" or ".webm" => StartsWith(header, bytesRead, [0x1A, 0x45, 0xDF, 0xA3]),
                ".mp3" => StartsWith(header, bytesRead, [0x49, 0x44, 0x33]) ||
                          (bytesRead >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0),
                ".wav" => StartsWith(header, bytesRead, [0x52, 0x49, 0x46, 0x46]) &&
                          MatchesAt(header, bytesRead, 8, [0x57, 0x41, 0x56, 0x45]),
                ".flac" => StartsWith(header, bytesRead, [0x66, 0x4C, 0x61, 0x43]),
                ".aac" => StartsWith(header, bytesRead, [0x41, 0x44, 0x49, 0x46]) ||
                          (bytesRead >= 2 && header[0] == 0xFF && (header[1] & 0xF6) == 0xF0),
                ".ogg" => StartsWith(header, bytesRead, [0x4F, 0x67, 0x67, 0x53]),
                _ => false
            };
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool StartsWith(byte[] content, int contentLength, byte[] signature)
    {
        return MatchesAt(content, contentLength, 0, signature);
    }

    private static bool MatchesAt(byte[] content, int contentLength, int offset, byte[] signature)
    {
        return contentLength >= offset + signature.Length &&
               content.AsSpan(offset, signature.Length).SequenceEqual(signature);
    }

    private static bool ValidateOfficeOpenXml(Stream stream, string extension)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var requiredPrefix = extension switch
        {
            ".docx" => "word/",
            ".xlsx" => "xl/",
            ".pptx" => "ppt/",
            _ => string.Empty
        };

        return archive.GetEntry("[Content_Types].xml") != null &&
               archive.Entries.Any(entry =>
                   entry.FullName.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> ValidateUtf8TextAsync(Stream stream)
    {
        stream.Position = 0;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(memory.ToArray());

        return !text.Any(character => character == '\0' ||
            (char.IsControl(character) && character is not ('\r' or '\n' or '\t')));
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
    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            // Images
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",

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

    private string ResolveContainedPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("文件路径必须是存储根目录内的相对路径");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var relativeToRoot = Path.GetRelativePath(_rootPath, fullPath);
        if (relativeToRoot.Equals("..", StringComparison.Ordinal) ||
            relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativeToRoot) ||
            relativeToRoot.Equals(".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("文件路径越出配置的存储根目录");
        }

        return fullPath;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    #endregion
}
