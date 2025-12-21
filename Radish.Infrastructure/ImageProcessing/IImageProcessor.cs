namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 图片处理接口
/// </summary>
/// <remarks>
/// 定义统一的图片处理接口，支持多种实现（ImageSharp、Rust Native）
/// </remarks>
public interface IImageProcessor
{
    /// <summary>
    /// 生成缩略图
    /// </summary>
    /// <param name="sourceStream">源图片流</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="width">目标宽度</param>
    /// <param name="height">目标高度</param>
    /// <param name="quality">图片质量（1-100，默认 85）</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessResult> GenerateThumbnailAsync(
        Stream sourceStream,
        string outputPath,
        int width = 150,
        int height = 150,
        int quality = 85);

    /// <summary>
    /// 调整图片大小
    /// </summary>
    /// <param name="sourceStream">源图片流</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="width">目标宽度（0 表示按比例自动计算）</param>
    /// <param name="height">目标高度（0 表示按比例自动计算）</param>
    /// <param name="keepAspectRatio">是否保持宽高比</param>
    /// <param name="quality">图片质量（1-100，默认 85）</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessResult> ResizeAsync(
        Stream sourceStream,
        string outputPath,
        int width,
        int height,
        bool keepAspectRatio = true,
        int quality = 85);

    /// <summary>
    /// 压缩图片
    /// </summary>
    /// <param name="sourceStream">源图片流</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="quality">图片质量（1-100，默认 85）</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessResult> CompressAsync(
        Stream sourceStream,
        string outputPath,
        int quality = 85);

    /// <summary>
    /// 添加水印
    /// </summary>
    /// <param name="sourceStream">源图片流</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="options">水印配置选项</param>
    /// <returns>处理结果</returns>
    Task<ImageProcessResult> AddWatermarkAsync(
        Stream sourceStream,
        string outputPath,
        WatermarkOptions options);

    /// <summary>
    /// 移除 EXIF 信息
    /// </summary>
    /// <param name="sourceStream">源图片流</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <returns>是否成功移除</returns>
    Task<bool> RemoveExifAsync(
        Stream sourceStream,
        string outputPath);

    /// <summary>
    /// 生成多个尺寸的图片
    /// </summary>
    /// <param name="sourceStream">源图片流</param>
    /// <param name="baseOutputPath">基础输出路径（不含扩展名）</param>
    /// <param name="sizes">尺寸配置列表</param>
    /// <returns>处理结果列表</returns>
    Task<List<ImageProcessResult>> GenerateMultipleSizesAsync(
        Stream sourceStream,
        string baseOutputPath,
        List<ImageSize> sizes);

    /// <summary>
    /// 获取图片信息
    /// </summary>
    /// <param name="stream">图片流</param>
    /// <returns>图片信息</returns>
    Task<ImageInfo?> GetImageInfoAsync(Stream stream);

    /// <summary>
    /// 验证图片是否有效
    /// </summary>
    /// <param name="stream">图片流</param>
    /// <returns>是否为有效图片</returns>
    Task<bool> IsValidImageAsync(Stream stream);
}
