namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 图片处理结果
/// </summary>
public class ImageProcessResult
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
    /// 输出文件路径
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 处理后的文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 图片宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图片高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static ImageProcessResult Ok(string outputPath, long fileSize, int width, int height)
    {
        return new ImageProcessResult
        {
            Success = true,
            OutputPath = outputPath,
            FileSize = fileSize,
            Width = width,
            Height = height
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static ImageProcessResult Fail(string errorMessage)
    {
        return new ImageProcessResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
