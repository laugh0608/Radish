namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 图片信息
/// </summary>
public class ImageInfo
{
    /// <summary>
    /// 图片宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图片高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 图片格式（如 JPEG, PNG, GIF）
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 是否包含 EXIF 信息
    /// </summary>
    public bool HasExif { get; set; }

    /// <summary>
    /// 色彩深度（位/像素）
    /// </summary>
    public int? BitsPerPixel { get; set; }
}
