namespace Radish.Model.ViewModels;

/// <summary>
/// 评论视图模型
/// </summary>
public class CommentVo
{
    /// <summary>
    /// 评论 Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 评论内容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 所属帖子 Id
    /// </summary>
    public long PostId { get; set; }

    /// <summary>
    /// 父评论 Id
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 根评论 Id
    /// </summary>
    public long? RootId { get; set; }

    /// <summary>
    /// 回复目标用户 Id
    /// </summary>
    public long? ReplyToUserId { get; set; }

    /// <summary>
    /// 回复目标用户名称
    /// </summary>
    public string? ReplyToUserName { get; set; }

    /// <summary>
    /// 层级深度
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 作者 Id
    /// </summary>
    public long AuthorId { get; set; }

    /// <summary>
    /// 作者名称
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 点赞次数
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// 回复次数
    /// </summary>
    public int ReplyCount { get; set; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool IsTop { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? ModifyTime { get; set; }

    /// <summary>
    /// 子评论列表（用于树形结构）
    /// </summary>
    public List<CommentVo>? Children { get; set; }

    /// <summary>
    /// 子评论总数（用于懒加载显示，前端分页用）
    /// </summary>
    /// <remarks>需要在Service层动态计算并填充</remarks>
    public int? ChildrenTotal { get; set; }

    /// <summary>
    /// 当前用户是否已点赞
    /// </summary>
    /// <remarks>需要在Service层动态填充，默认为false</remarks>
    public bool IsLiked { get; set; }
}
