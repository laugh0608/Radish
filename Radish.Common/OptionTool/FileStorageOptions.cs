using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>文件存储配置选项</summary>
public sealed class FileStorageOptions : IConfigurableOptions
{
    /// <summary>存储根目录</summary>
    /// <remarks>相对于应用根目录的路径，默认为 DataBases/Uploads</remarks>
    public string RootPath { get; set; } = "DataBases/Uploads";

    /// <summary>访问 URL 前缀</summary>
    /// <remarks>用于生成文件访问 URL，例如：/uploads</remarks>
    public string UrlPrefix { get; set; } = "/uploads";

    /// <summary>文件大小限制（字节）</summary>
    /// <remarks>默认 10MB</remarks>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>允许的图片类型</summary>
    public List<string> AllowedImageTypes { get; set; } = new()
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg"
    };

    /// <summary>允许的文档类型</summary>
    public List<string> AllowedDocumentTypes { get; set; } = new()
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md"
    };

    /// <summary>允许的视频类型</summary>
    public List<string> AllowedVideoTypes { get; set; } = new()
    {
        ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm"
    };

    /// <summary>允许的音频类型</summary>
    public List<string> AllowedAudioTypes { get; set; } = new()
    {
        ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma"
    };
}
