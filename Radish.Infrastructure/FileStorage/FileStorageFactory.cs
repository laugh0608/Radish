using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;

namespace Radish.Infrastructure.FileStorage;

/// <summary>
/// 文件存储工厂，根据配置动态创建存储实现
/// </summary>
/// <remarks>
/// 通过 FileStorage:Type 配置切换存储后端：
/// - Local: 本地文件系统存储（已实现）
/// - MinIO: MinIO 对象存储（未实现）
/// - OSS: 阿里云 OSS 存储（未实现）
/// </remarks>
public static class FileStorageFactory
{
    public static IFileStorage Create(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<FileStorageOptions>>();
        var logger = serviceProvider.GetService<ILogger<IFileStorage>>();

        var storageType = options.Value.Type?.ToLowerInvariant() switch
        {
            "local" => "Local",
            "minio" => "MinIO",
            "oss" => "OSS",
            null or "" => "Local",
            var unknown => throw new NotSupportedException($"不支持的存储类型: {unknown}")
        };

        logger?.LogInformation("正在使用 {StorageType} 存储后端", storageType);

        return storageType switch
        {
            "Local" => new LocalFileStorage(
                options,
                serviceProvider.GetRequiredService<IWebHostEnvironment>()),

            "MinIO" => throw new NotSupportedException(
                "当前未实现 MinIO 存储。请先实现 MinIO 存储适配器后再将 FileStorage:Type 设置为 MinIO。"),

            "OSS" => throw new NotSupportedException(
                "当前未实现 OSS 存储。请先实现 OSS 存储适配器后再将 FileStorage:Type 设置为 OSS。"),

            _ => throw new NotSupportedException($"不支持的存储类型: {storageType}")
        };
    }
}