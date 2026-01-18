namespace Radish.Model.ViewModels;

/// <summary>
/// 帖子视图模型
/// </summary>
public class PostVo
{
    /// <summary>
    /// 帖子 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 帖子标题
    /// </summary>
    public string VoTitle { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string VoSlug { get; set; } = string.Empty;

    /// <summary>
    /// 帖子摘要
    /// </summary>
    public string? VoSummary { get; set; }

    /// <summary>
    /// 帖子内容
    /// </summary>
    public string VoContent { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型（markdown/html/text）
    /// </summary>
    public string VoContentType { get; set; } = "markdown";

    /// <summary>
    /// 封面图片
    /// </summary>
    public string? VoCoverImage { get; set; }

    /// <summary>
    /// 作者 Id
    /// </summary>
    public long VoAuthorId { get; set; }

    /// <summary>
    /// 作者名称
    /// </summary>
    public string VoAuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 分类 Id
    /// </summary>
    public long VoCategoryId { get; set; }

    /// <summary>
    /// 分类名称（冗余字段，便于前端显示）
    /// </summary>
    public string? VoCategoryName { get; set; }

    /// <summary>
    /// 标签列表（逗号分隔的标签名）
    /// </summary>
    public string? VoTags { get; set; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool VoIsTop { get; set; }

    /// <summary>
    /// 是否精华
    /// </summary>
    public bool VoIsEssence { get; set; }

    /// <summary>
    /// 是否锁定
    /// </summary>
    public bool VoIsLocked { get; set; }

    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool VoIsPublished { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? VoPublishTime { get; set; }

    /// <summary>
    /// 浏览次数
    /// </summary>
    public int VoViewCount { get; set; }

    /// <summary>
    /// 点赞次数
    /// </summary>
    public int VoLikeCount { get; set; }

    /// <summary>
    /// 评论次数
    /// </summary>
    public int VoCommentCount { get; set; }

    /// <summary>
    /// 收藏次数
    /// </summary>
    public int VoCollectCount { get; set; }

    /// <summary>
    /// 分享次数
    /// </summary>
    public int VoShareCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? VoModifyTime { get; set; }
}
