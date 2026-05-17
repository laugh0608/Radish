namespace Radish.Model.ViewModels;

/// <summary>公开详情页 HTML head 快照 VO。</summary>
public class PublicHeadSnapshotVo
{
    /// <summary>页面标题</summary>
    public string VoTitle { get; set; } = string.Empty;

    /// <summary>页面描述</summary>
    public string VoDescription { get; set; } = string.Empty;

    /// <summary>规范化访问路径</summary>
    public string VoCanonicalPath { get; set; } = string.Empty;

    /// <summary>规范化访问地址</summary>
    public string VoCanonicalUrl { get; set; } = string.Empty;

    /// <summary>Open Graph 类型</summary>
    public string VoOpenGraphType { get; set; } = "article";

    /// <summary>Open Graph 图片地址</summary>
    public string? VoImageUrl { get; set; }

    /// <summary>发布时间</summary>
    public DateTime? VoPublishedAt { get; set; }

    /// <summary>最后更新时间</summary>
    public DateTime? VoModifiedAt { get; set; }

    /// <summary>已序列化的 JSON-LD 字符串</summary>
    public string VoJsonLd { get; set; } = string.Empty;
}
