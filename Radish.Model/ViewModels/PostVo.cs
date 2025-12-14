namespace Radish.Model.ViewModels;

/// <summary>
/// 帖子视图模型
/// </summary>
public class PostVo
{
    /// <summary>
    /// 帖子 Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 帖子标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 帖子摘要
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 帖子内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型（markdown/html/text）
    /// </summary>
    public string ContentType { get; set; } = "markdown";

    /// <summary>
    /// 封面图片
    /// </summary>
    public string? CoverImage { get; set; }

    /// <summary>
    /// 作者 Id
    /// </summary>
    public long AuthorId { get; set; }

    /// <summary>
    /// 作者名称
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 分类 Id
    /// </summary>
    public long CategoryId { get; set; }

    /// <summary>
    /// 分类名称（冗余字段，便于前端显示）
    /// </summary>
    public string? CategoryName { get; set; }

    /// <summary>
    /// 标签列表（逗号分隔的标签名）
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool IsTop { get; set; }

    /// <summary>
    /// 是否精华
    /// </summary>
    public bool IsEssence { get; set; }

    /// <summary>
    /// 是否锁定
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>
    /// 浏览次数
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// 点赞次数
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// 评论次数
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// 收藏次数
    /// </summary>
    public int CollectCount { get; set; }

    /// <summary>
    /// 分享次数
    /// </summary>
    public int ShareCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? ModifyTime { get; set; }
}
