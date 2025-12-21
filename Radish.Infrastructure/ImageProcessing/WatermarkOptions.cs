namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 水印配置选项
/// </summary>
public class WatermarkOptions
{
    /// <summary>
    /// 水印类型
    /// </summary>
    public WatermarkType Type { get; set; } = WatermarkType.Text;

    /// <summary>
    /// 水印文本（文字水印）
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 水印图片路径（图片水印）
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// 字体大小（文字水印）
    /// </summary>
    public int FontSize { get; set; } = 24;

    /// <summary>
    /// 不透明度（0.0 - 1.0）
    /// </summary>
    public float Opacity { get; set; } = 0.5f;

    /// <summary>
    /// 水印位置
    /// </summary>
    public WatermarkPosition Position { get; set; } = WatermarkPosition.BottomRight;

    /// <summary>
    /// 水印颜色（十六进制，如 #FFFFFF，仅用于文字水印）
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// 边距（像素）
    /// </summary>
    public int Padding { get; set; } = 10;

    /// <summary>
    /// 图片水印缩放比例（相对于原图宽度，0.0 - 1.0）
    /// </summary>
    public float Scale { get; set; } = 0.1f;
}

/// <summary>
/// 水印类型枚举
/// </summary>
public enum WatermarkType
{
    /// <summary>文字水印</summary>
    Text = 0,

    /// <summary>图片水印</summary>
    Image = 1
}

/// <summary>
/// 水印位置枚举
/// </summary>
public enum WatermarkPosition
{
    /// <summary>左上角</summary>
    TopLeft = 0,

    /// <summary>右上角</summary>
    TopRight = 1,

    /// <summary>左下角</summary>
    BottomLeft = 2,

    /// <summary>右下角</summary>
    BottomRight = 3,

    /// <summary>居中</summary>
    Center = 4
}
