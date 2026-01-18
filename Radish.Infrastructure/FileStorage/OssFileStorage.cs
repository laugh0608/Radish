using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 阿里云 OSS 存储实现（占位符）
/// </summary>
/// <remarks>
/// TODO: 实现完整的 OSS 存储功能
/// - 依赖：Aliyun.OSS 或兼容 SDK
/// - 功能：上传、删除、下载、URL 生成、文件存在检查
/// - 配置：从 FileStorageOptions.OSS 读取连接参数
/// </remarks>
public class OssFileStorage : IFileStorage
{
    private readonly OSSStorageOptions _options;
    private readonly ILogger<OssFileStorage> _logger;

    public OssFileStorage(IOptions<FileStorageOptions> options, ILogger<OssFileStorage> logger)
    {
        _options = options.Value.OSS;
        _logger = logger;
    }

    public Task<FileUploadResult> UploadAsync(
        Stream stream,
        string fileName,
        string contentType,
        FileUploadOptionsDto? options = null)
    {
        throw new NotImplementedException("OSS 存储上传功能尚未实现");
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        throw new NotImplementedException("OSS 存储删除功能尚未实现");
    }

    public Task<Stream?> DownloadAsync(string filePath)
    {
        throw new NotImplementedException("OSS 存储下载功能尚未实现");
    }

    public string GetFileUrl(string filePath)
    {
        if (!string.IsNullOrWhiteSpace(_options.Domain))
        {
            return $"{_options.Domain.TrimEnd('/')}/{filePath.TrimStart('/')}";
        }

        // TODO: 没有 CDN 域名时，按 endpoint/bucket 拼出可访问地址（需要根据 OSS Endpoint 规则调整）
        return $"https://{_options.BucketName}.{_options.Endpoint}/{filePath.TrimStart('/')}";
    }

    public Task<bool> ExistsAsync(string filePath)
    {
        throw new NotImplementedException("OSS 存储存在检查功能尚未实现");
    }

    public Task<FileStorageInfo?> GetFileInfoAsync(string filePath)
    {
        throw new NotImplementedException("OSS 存储文件信息获取功能尚未实现");
    }

    public string GetFullPath(string relativePath)
    {
        // OSS 是对象存储，没有本地文件系统路径概念
        return GetFileUrl(relativePath);
    }
}