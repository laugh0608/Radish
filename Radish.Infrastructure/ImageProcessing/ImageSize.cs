namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 图片尺寸配置
/// </summary>
public class ImageSize
{
    /// <summary>
    /// 尺寸名称（如 small, medium, large, thumbnail）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 目标宽度（0 表示按比例自动计算）
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 目标高度（0 表示按比例自动计算）
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 图片质量（1-100，仅适用于 JPEG）
    /// </summary>
    public int Quality { get; set; } = 85;

    /// <summary>
    /// 是否保持宽高比
    /// </summary>
    public bool KeepAspectRatio { get; set; } = true;

    /// <summary>
    /// 缩略图预设（150x150）
    /// </summary>
    public static ImageSize Thumbnail => new()
    {
        Name = "thumbnail",
        Width = 150,
        Height = 150,
        Quality = 85,
        KeepAspectRatio = true
    };

    /// <summary>
    /// 小图预设（400x0，保持比例）
    /// </summary>
    public static ImageSize Small => new()
    {
        Name = "small",
        Width = 400,
        Height = 0,
        Quality = 85,
        KeepAspectRatio = true
    };

    /// <summary>
    /// 中图预设（800x0，保持比例）
    /// </summary>
    public static ImageSize Medium => new()
    {
        Name = "medium",
        Width = 800,
        Height = 0,
        Quality = 85,
        KeepAspectRatio = true
    };

    /// <summary>
    /// 大图预设（1200x0，保持比例）
    /// </summary>
    public static ImageSize Large => new()
    {
        Name = "large",
        Width = 1200,
        Height = 0,
        Quality = 85,
        KeepAspectRatio = true
    };
}
