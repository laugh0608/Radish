namespace Radish.Infrastructure.ImageProcessing;

/// <summary>图片内容识别、受限处理与显式编码；不关闭调用方流。</summary>
public interface IImageProcessor
{
    Task<ImageProcessResult> GenerateThumbnailAsync(Stream sourceStream, string outputPath,
        int width = 150, int height = 150, int quality = 85,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.FirstFrame,
        CancellationToken cancellationToken = default);

    Task<ImageProcessResult> ResizeAsync(Stream sourceStream, string outputPath,
        int width, int height, bool keepAspectRatio = true, int quality = 85,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default);

    Task<ImageProcessResult> CompressAsync(Stream sourceStream, string outputPath, int quality = 85,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default);

    Task<ImageProcessResult> AddWatermarkAsync(Stream sourceStream, string outputPath, WatermarkOptions options,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default);

    Task<ImageProcessResult> RemoveExifAsync(Stream sourceStream, string outputPath,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default);

    /// <summary>基础路径包含扩展名；整批成功才提交，结果按 sizes 顺序返回。</summary>
    Task<List<ImageProcessResult>> GenerateMultipleSizesAsync(Stream sourceStream, string baseOutputPath,
        List<ImageSize> sizes, CancellationToken cancellationToken = default);

    Task<ImageInfo?> GetImageInfoAsync(Stream stream, CancellationToken cancellationToken = default);
    Task<bool> IsValidImageAsync(Stream stream, CancellationToken cancellationToken = default);
}
