using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>
/// 文件存储配置选项（按设计文档规范）
/// </summary>
/// <remarks>
/// 支持可配置的存储后端切换：Local / MinIO / OSS
/// </remarks>
public sealed class FileStorageOptions : IConfigurableOptions
{
    /// <summary>存储类型</summary>
    /// <remarks>Local（本地文件系统）/ MinIO（MinIO对象存储）/ OSS（阿里云OSS）</remarks>
    public string Type { get; set; } = "Local";

    /// <summary>文件大小限制（字节）- 按业务类型区分</summary>
    public MaxFileSizeOptions MaxFileSize { get; set; } = new();

    /// <summary>允许的文件扩展名（白名单）- 按业务类型区分</summary>
    public AllowedExtensionsOptions AllowedExtensions { get; set; } = new();

    /// <summary>本地文件系统存储配置</summary>
    public LocalStorageOptions Local { get; set; } = new();

    /// <summary>MinIO 对象存储配置</summary>
    public MinIOStorageOptions MinIO { get; set; } = new();

    /// <summary>阿里云 OSS 配置</summary>
    public OSSStorageOptions OSS { get; set; } = new();

    /// <summary>图片处理配置</summary>
    public ImageProcessingOptions ImageProcessing { get; set; } = new();

    /// <summary>水印配置</summary>
    public WatermarkOptions Watermark { get; set; } = new();

    /// <summary>文件去重配置</summary>
    public DeduplicationOptions Deduplication { get; set; } = new();
}

/// <summary>文件大小限制配置</summary>
public sealed class MaxFileSizeOptions
{
    /// <summary>头像大小限制（字节）</summary>
    /// <remarks>默认 2MB</remarks>
    public long Avatar { get; set; } = 2 * 1024 * 1024;

    /// <summary>图片大小限制（字节）</summary>
    /// <remarks>默认 5MB</remarks>
    public long Image { get; set; } = 5 * 1024 * 1024;

    /// <summary>文档大小限制（字节）</summary>
    /// <remarks>默认 10MB</remarks>
    public long Document { get; set; } = 10 * 1024 * 1024;

    /// <summary>视频大小限制（字节）</summary>
    /// <remarks>默认 50MB</remarks>
    public long Video { get; set; } = 50 * 1024 * 1024;

    /// <summary>音频大小限制（字节）</summary>
    /// <remarks>默认 10MB</remarks>
    public long Audio { get; set; } = 10 * 1024 * 1024;
}

/// <summary>允许的文件扩展名配置</summary>
public sealed class AllowedExtensionsOptions
{
    /// <summary>允许的图片类型</summary>
    public List<string> Image { get; set; } = new() { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };

    /// <summary>允许的文档类型</summary>
    public List<string> Document { get; set; } = new() { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" };

    /// <summary>允许的视频类型</summary>
    public List<string> Video { get; set; } = new() { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm" };

    /// <summary>允许的音频类型</summary>
    public List<string> Audio { get; set; } = new() { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" };
}

/// <summary>本地存储配置</summary>
public sealed class LocalStorageOptions
{
    /// <summary>存储根目录</summary>
    /// <remarks>相对于项目根目录，例如：DataBases/Uploads</remarks>
    public string BasePath { get; set; } = "DataBases/Uploads";

    /// <summary>访问 URL 前缀</summary>
    /// <remarks>例如：/uploads</remarks>
    public string BaseUrl { get; set; } = "/uploads";
}

/// <summary>MinIO 存储配置</summary>
public sealed class MinIOStorageOptions
{
    /// <summary>MinIO 服务器地址</summary>
    /// <remarks>例如：localhost:9000</remarks>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>存储桶名称</summary>
    public string BucketName { get; set; } = "radish-uploads";

    /// <summary>访问密钥</summary>
    public string AccessKey { get; set; } = "admin";

    /// <summary>密钥</summary>
    public string SecretKey { get; set; } = "your_password_here";

    /// <summary>是否使用 SSL</summary>
    public bool UseSSL { get; set; } = false;
}

/// <summary>阿里云 OSS 存储配置</summary>
public sealed class OSSStorageOptions
{
    /// <summary>OSS 节点地址</summary>
    /// <remarks>例如：oss-cn-hangzhou.aliyuncs.com</remarks>
    public string Endpoint { get; set; } = "oss-cn-hangzhou.aliyuncs.com";

    /// <summary>存储桶名称</summary>
    public string BucketName { get; set; } = "radish-uploads";

    /// <summary>访问密钥 ID</summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>访问密钥密码</summary>
    public string AccessKeySecret { get; set; } = string.Empty;

    /// <summary>CDN 域名</summary>
    /// <remarks>例如：https://cdn.example.com</remarks>
    public string Domain { get; set; } = string.Empty;
}

/// <summary>图片处理配置</summary>
public sealed class ImageProcessingOptions
{
    /// <summary>是否使用 Rust 扩展</summary>
    /// <remarks>true: 使用 Rust 扩展, false: 使用 C# (ImageSharp)</remarks>
    public bool UseRustExtension { get; set; } = false;

    /// <summary>是否生成缩略图</summary>
    public bool GenerateThumbnail { get; set; } = true;

    /// <summary>缩略图尺寸</summary>
    public ImageSizeOptions ThumbnailSize { get; set; } = new() { Width = 150, Height = 150 };

    /// <summary>是否生成多尺寸</summary>
    /// <remarks>生成 Small/Medium/Large 三种尺寸</remarks>
    public bool GenerateMultipleSizes { get; set; } = false;

    /// <summary>多尺寸配置</summary>
    public MultipleSizesOptions Sizes { get; set; } = new();

    /// <summary>JPEG 压缩质量</summary>
    /// <remarks>范围 1-100，默认 85</remarks>
    public int CompressQuality { get; set; } = 85;

    /// <summary>是否移除 EXIF 信息</summary>
    /// <remarks>保护隐私，建议开启</remarks>
    public bool RemoveExif { get; set; } = true;
}

/// <summary>图片尺寸配置</summary>
public sealed class ImageSizeOptions
{
    /// <summary>宽度</summary>
    public int Width { get; set; }

    /// <summary>高度</summary>
    public int Height { get; set; }
}

/// <summary>多尺寸配置</summary>
public sealed class MultipleSizesOptions
{
    /// <summary>小图尺寸</summary>
    public ImageSizeOptions Small { get; set; } = new() { Width = 400, Height = 300 };

    /// <summary>中图尺寸</summary>
    public ImageSizeOptions Medium { get; set; } = new() { Width = 800, Height = 600 };

    /// <summary>大图尺寸</summary>
    public ImageSizeOptions Large { get; set; } = new() { Width = 1200, Height = 900 };
}

/// <summary>水印配置</summary>
public sealed class WatermarkOptions
{
    /// <summary>是否启用水印</summary>
    /// <remarks>全局开关，用户仍可选择是否添加</remarks>
    public bool Enable { get; set; } = false;

    /// <summary>水印类型</summary>
    /// <remarks>Text（文字水印）/ Image（图片水印）</remarks>
    public string Type { get; set; } = "Text";

    /// <summary>文字水印配置</summary>
    public TextWatermarkOptions Text { get; set; } = new();

    /// <summary>图片水印配置</summary>
    public ImageWatermarkOptions Image { get; set; } = new();
}

/// <summary>文字水印配置</summary>
public sealed class TextWatermarkOptions
{
    /// <summary>水印内容</summary>
    public string Content { get; set; } = "Radish";

    /// <summary>水印位置</summary>
    /// <remarks>TopLeft/TopRight/BottomLeft/BottomRight/Center</remarks>
    public string Position { get; set; } = "BottomRight";

    /// <summary>字体大小</summary>
    public int FontSize { get; set; } = 24;

    /// <summary>相对字体大小</summary>
    /// <remarks>相对图片宽度的百分比，例如 0.05 表示 5%</remarks>
    public double FontSizeRelative { get; set; } = 0.05;

    /// <summary>字体颜色</summary>
    /// <remarks>十六进制颜色值，例如：#FFFFFF</remarks>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>不透明度</summary>
    /// <remarks>范围 0-1，默认 0.5（50%透明）</remarks>
    public double Opacity { get; set; } = 0.5;

    /// <summary>字体配置</summary>
    public FontOptions Font { get; set; } = new();
}

/// <summary>字体配置</summary>
public sealed class FontOptions
{
    /// <summary>字体名称列表（按优先级排序）</summary>
    /// <remarks>
    /// 支持跨平台字体选择，会按顺序尝试加载第一个可用的字体。
    /// 预置常见字体：Windows (Arial/Calibri), macOS (San Francisco/Helvetica), Linux (DejaVu/Ubuntu/Noto)
    /// </remarks>
    public List<string> PreferredFonts { get; set; } = new()
    {
        // Windows 系统
        "Arial",
        "Calibri",
        "Microsoft Sans Serif",

        // macOS 系统
        "San Francisco",
        "Helvetica",
        "Helvetica Neue",

        // Linux 系统
        "DejaVu Sans",
        "Ubuntu",
        "Liberation Sans",
        "Noto Sans",

        // 通用备选
        "Sans",
        "System UI"
    };

    /// <summary>自定义字体文件路径</summary>
    /// <remarks>优先使用自定义字体，如果为空则使用系统字体</remarks>
    public string? CustomFontPath { get; set; }

    /// <summary>字体样式</summary>
    /// <remarks>Regular/Bold/Italic/BoldItalic</remarks>
    public string Style { get; set; } = "Regular";

    /// <summary>是否使用相对字体大小</summary>
    /// <remarks>true: 根据图片宽度计算, false: 使用固定像素值</remarks>
    public bool UseRelativeSize { get; set; } = true;

    /// <summary>最小字体大小（像素）</summary>
    /// <remarks>当使用相对大小时的最小值</remarks>
    public int MinFontSize { get; set; } = 12;

    /// <summary>最大字体大小（像素）</summary>
    /// <remarks>当使用相对大小时的最大值</remarks>
    public int MaxFontSize { get; set; } = 72;
}

/// <summary>图片水印配置</summary>
public sealed class ImageWatermarkOptions
{
    /// <summary>水印图片路径</summary>
    /// <remarks>例如：wwwroot/images/watermark.png</remarks>
    public string Path { get; set; } = "wwwroot/images/watermark.png";

    /// <summary>水印位置</summary>
    /// <remarks>TopLeft/TopRight/BottomLeft/BottomRight/Center</remarks>
    public string Position { get; set; } = "BottomRight";

    /// <summary>水印缩放比例</summary>
    /// <remarks>相对图片宽度的百分比，例如 0.1 表示 10%</remarks>
    public double Scale { get; set; } = 0.1;
}

/// <summary>文件去重配置</summary>
public sealed class DeduplicationOptions
{
    /// <summary>是否启用去重</summary>
    public bool Enable { get; set; } = true;

    /// <summary>哈希算法</summary>
    /// <remarks>MD5 / SHA256</remarks>
    public string HashAlgorithm { get; set; } = "SHA256";

    /// <summary>哈希计算是否使用 Rust 扩展</summary>
    /// <remarks>性能优化，计算密集型操作可使用 Rust</remarks>
    public bool UseRustExtension { get; set; } = false;
}
