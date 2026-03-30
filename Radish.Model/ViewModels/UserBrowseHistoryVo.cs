namespace Radish.Model.ViewModels;

/// <summary>用户浏览记录视图模型</summary>
public class UserBrowseHistoryVo
{
    /// <summary>记录 ID</summary>
    public long VoId { get; set; }

    /// <summary>目标类型</summary>
    public string VoTargetType { get; set; } = string.Empty;

    /// <summary>目标类型文案</summary>
    public string VoTargetTypeDisplay => VoTargetType switch
    {
        "Post" => "帖子",
        "Product" => "商品",
        "Wiki" => "文档",
        _ => VoTargetType
    };

    /// <summary>目标 ID</summary>
    public long VoTargetId { get; set; }

    /// <summary>目标 Slug</summary>
    public string? VoTargetSlug { get; set; }

    /// <summary>标题</summary>
    public string VoTitle { get; set; } = string.Empty;

    /// <summary>摘要</summary>
    public string? VoSummary { get; set; }

    /// <summary>封面附件快照 Id</summary>
    public long? VoCoverAttachmentId { get; set; }

    /// <summary>封面 URL</summary>
    public string? VoCoverImage { get; set; }

    /// <summary>路由</summary>
    public string? VoRoutePath { get; set; }

    /// <summary>浏览次数</summary>
    public int VoViewCount { get; set; }

    /// <summary>最后浏览时间</summary>
    public DateTime VoLastViewTime { get; set; }
}
