using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Serilog;

namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 基于 Rust Native 的图片处理实现
/// </summary>
/// <remarks>
/// 通过 P/Invoke 调用 Rust 编写的高性能图片处理库
/// </remarks>
public class RustImageProcessor : IImageProcessor
{
    private readonly ImageProcessingOptions _options;
    private readonly TextWatermarkOptions _textWatermarkOptions;
    private readonly CSharpImageProcessor _fallbackProcessor;

    // Rust 库名称（根据平台自动选择）
    private const string LibraryName = "radish_lib";

    public RustImageProcessor(IOptions<FileStorageOptions> fileStorageOptions)
    {
        var options = fileStorageOptions.Value;
        _options = options.ImageProcessing;
        _textWatermarkOptions = options.Watermark?.Text ?? new TextWatermarkOptions();

        // 创建 C# 实现作为后备方案
        _fallbackProcessor = new CSharpImageProcessor(fileStorageOptions);
    }

    #region Rust FFI Declarations

    /// <summary>
    /// 添加文字水印（Rust 实现）
    /// </summary>
    /// <returns>0: 成功, -1: 失败</returns>
    [DllImport(LibraryName, EntryPoint = "add_text_watermark", CallingConvention = CallingConvention.Cdecl)]
    private static extern int AddTextWatermarkNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
        uint fontSize,
        float opacity,
        byte position
    );

    /// <summary>
    /// 计算文件 SHA256 哈希（Rust 实现）
    /// </summary>
    /// <returns>0: 成功, -1: 失败, -2: 缓冲区太小</returns>
    [DllImport(LibraryName, EntryPoint = "calculate_file_sha256", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateFileSha256Native(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filePath,
        [MarshalAs(UnmanagedType.LPStr)] StringBuilder hashOutput,
        int outputLen
    );

    #endregion

    #region IImageProcessor Implementation

    /// <summary>
    /// 生成缩略图（使用 C# 实现）
    /// </summary>
    public Task<ImageProcessResult> GenerateThumbnailAsync(
        Stream sourceStream,
        string outputPath,
        int width = 150,
        int height = 150,
        int quality = 85)
    {
        // 缩略图生成使用 C# 实现（Rust 版本可以后续添加）
        return _fallbackProcessor.GenerateThumbnailAsync(sourceStream, outputPath, width, height, quality);
    }

    /// <summary>
    /// 调整图片大小（使用 C# 实现）
    /// </summary>
    public Task<ImageProcessResult> ResizeAsync(
        Stream sourceStream,
        string outputPath,
        int width,
        int height,
        bool keepAspectRatio = true,
        int quality = 85)
    {
        // 图片缩放使用 C# 实现（Rust 版本可以后续添加）
        return _fallbackProcessor.ResizeAsync(sourceStream, outputPath, width, height, keepAspectRatio, quality);
    }

    /// <summary>
    /// 压缩图片（使用 C# 实现）
    /// </summary>
    public Task<ImageProcessResult> CompressAsync(
        Stream sourceStream,
        string outputPath,
        int quality = 85)
    {
        // 图片压缩使用 C# 实现（Rust 版本可以后续添加）
        return _fallbackProcessor.CompressAsync(sourceStream, outputPath, quality);
    }

    /// <summary>
    /// 添加水印（使用 Rust 实现）
    /// </summary>
    public async Task<ImageProcessResult> AddWatermarkAsync(
        Stream sourceStream,
        string outputPath,
        WatermarkOptions options)
    {
        try
        {
            // 只支持文字水印
            if (options.Type != WatermarkType.Text)
            {
                Log.Warning("Rust processor only supports text watermark, falling back to C# implementation");
                return await _fallbackProcessor.AddWatermarkAsync(sourceStream, outputPath, options);
            }

            // 保存源图片到临时文件（Rust 需要文件路径）
            var tempInputPath = Path.Combine(Path.GetTempPath(), $"radish_input_{Guid.NewGuid()}.tmp");
            try
            {
                sourceStream.Position = 0;
                await using (var fileStream = File.Create(tempInputPath))
                {
                    await sourceStream.CopyToAsync(fileStream);
                }

                // 确保输出目录存在
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 调用 Rust 函数
                var result = AddTextWatermarkNative(
                    tempInputPath,
                    outputPath,
                    options.Text,
                    (uint)options.FontSize,
                    options.Opacity,
                    (byte)options.Position
                );

                if (result != 0)
                {
                    Log.Error("Rust watermark failed with code {Code}, falling back to C# implementation", result);
                    return await _fallbackProcessor.AddWatermarkAsync(sourceStream, outputPath, options);
                }

                // 获取输出文件信息
                var fileInfo = new FileInfo(outputPath);
                if (!fileInfo.Exists)
                {
                    return ImageProcessResult.Fail("Rust watermark succeeded but output file not found");
                }

                // 获取图片尺寸（使用 C# ImageSharp）
                var imageInfo = await _fallbackProcessor.GetImageInfoAsync(File.OpenRead(outputPath));

                return ImageProcessResult.Ok(
                    outputPath: outputPath,
                    fileSize: fileInfo.Length,
                    width: imageInfo?.Width ?? 0,
                    height: imageInfo?.Height ?? 0
                );
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tempInputPath))
                {
                    try
                    {
                        File.Delete(tempInputPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to delete temporary file: {Path}", tempInputPath);
                    }
                }
            }
        }
        catch (DllNotFoundException ex)
        {
            Log.Warning(ex, "Rust library not found, falling back to C# implementation");
            return await _fallbackProcessor.AddWatermarkAsync(sourceStream, outputPath, options);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Rust watermark error, falling back to C# implementation");
            return await _fallbackProcessor.AddWatermarkAsync(sourceStream, outputPath, options);
        }
    }

    /// <summary>
    /// 移除 EXIF 信息（使用 C# 实现）
    /// </summary>
    public Task<bool> RemoveExifAsync(Stream sourceStream, string outputPath)
    {
        // EXIF 移除使用 C# 实现
        return _fallbackProcessor.RemoveExifAsync(sourceStream, outputPath);
    }

    /// <summary>
    /// 生成多个尺寸的图片（使用 C# 实现）
    /// </summary>
    public Task<List<ImageProcessResult>> GenerateMultipleSizesAsync(
        Stream sourceStream,
        string baseOutputPath,
        List<ImageSize> sizes)
    {
        // 多尺寸生成使用 C# 实现
        return _fallbackProcessor.GenerateMultipleSizesAsync(sourceStream, baseOutputPath, sizes);
    }

    /// <summary>
    /// 获取图片信息（使用 C# 实现）
    /// </summary>
    public Task<ImageInfo?> GetImageInfoAsync(Stream stream)
    {
        return _fallbackProcessor.GetImageInfoAsync(stream);
    }

    /// <summary>
    /// 验证图片是否有效（使用 C# 实现）
    /// </summary>
    public Task<bool> IsValidImageAsync(Stream stream)
    {
        return _fallbackProcessor.IsValidImageAsync(stream);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// 计算文件 SHA256 哈希（Rust 实现）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>SHA256 哈希值（小写十六进制）</returns>
    public static string? CalculateFileSha256(string filePath)
    {
        try
        {
            var buffer = new StringBuilder(65); // SHA256 = 64 chars + null terminator
            var result = CalculateFileSha256Native(filePath, buffer, buffer.Capacity);

            if (result == 0)
            {
                return buffer.ToString();
            }

            Log.Warning("Rust hash calculation failed with code {Code}", result);
            return null;
        }
        catch (DllNotFoundException ex)
        {
            Log.Warning(ex, "Rust library not found for hash calculation");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Rust hash calculation error");
            return null;
        }
    }

    /// <summary>
    /// 检查 Rust 库是否可用
    /// </summary>
    public static bool IsRustLibraryAvailable()
    {
        try
        {
            // 尝试调用一个简单的函数来检测库是否存在
            var testPath = Path.GetTempFileName();
            File.WriteAllText(testPath, "test");

            var buffer = new StringBuilder(65);
            var result = CalculateFileSha256Native(testPath, buffer, buffer.Capacity);

            File.Delete(testPath);

            return result == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
