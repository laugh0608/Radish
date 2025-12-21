namespace Radish.Infrastructure.ImageProcessing;

/// <summary>
/// 水印配置选项
/// </summary>
public class WatermarkOptions
{
    /// <summary>
    /// 水印文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 字体大小
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
    /// 水印颜色（十六进制，如 #FFFFFF）
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>
    /// 边距（像素）
    /// </summary>
    public int Padding { get; set; } = 10;
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
