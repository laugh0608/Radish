namespace Radish.Model.ViewModels;

/// <summary>
/// 评论视图模型
/// </summary>
public class CommentVo
{
    /// <summary>
    /// 评论 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 评论内容
    /// </summary>
    public string VoContent { get; set; } = string.Empty;

    /// <summary>
    /// 所属帖子 Id
    /// </summary>
    public long VoPostId { get; set; }

    /// <summary>
    /// 父评论 Id
    /// </summary>
    public long? VoParentId { get; set; }

    /// <summary>
    /// 根评论 Id
    /// </summary>
    public long? VoRootId { get; set; }

    /// <summary>
    /// 回复目标用户 Id
    /// </summary>
    public long? VoReplyToUserId { get; set; }

    /// <summary>
    /// 回复目标用户名称
    /// </summary>
    public string? VoReplyToUserName { get; set; }

    /// <summary>
    /// 层级深度
    /// </summary>
    public int VoLevel { get; set; }

    /// <summary>
    /// 作者 Id
    /// </summary>
    public long VoAuthorId { get; set; }

    /// <summary>
    /// 作者名称
    /// </summary>
    public string VoAuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 点赞次数
    /// </summary>
    public int VoLikeCount { get; set; }

    /// <summary>
    /// 回复次数
    /// </summary>
    public int VoReplyCount { get; set; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool VoIsTop { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? VoModifyTime { get; set; }

    /// <summary>
    /// 子评论列表（用于树形结构）
    /// </summary>
    public List<CommentVo>? VoChildren { get; set; }

    /// <summary>
    /// 子评论总数（用于懒加载显示，前端分页用）
    /// </summary>
    /// <remarks>需要在Service层动态计算并填充</remarks>
    public int? VoChildrenTotal { get; set; }

    /// <summary>
    /// 当前用户是否已点赞
    /// </summary>
    /// <remarks>需要在Service层动态填充，默认为false</remarks>
    public bool VoIsLiked { get; set; }

    /// <summary>
    /// 是否为当前神评（父评论中点赞数最高的）
    /// </summary>
    /// <remarks>需要在Service层动态填充，默认为false</remarks>
    public bool VoIsGodComment { get; set; }

    /// <summary>
    /// 是否为当前沙发（父评论下子评论中点赞数最高的）
    /// </summary>
    /// <remarks>需要在Service层动态填充，默认为false</remarks>
    public bool VoIsSofa { get; set; }

    /// <summary>
    /// 高亮排名（1=第一名，2=第二名，等等）
    /// </summary>
    /// <remarks>需要在Service层动态填充，仅当 IsGodComment 或 IsSofa 为 true 时有值</remarks>
    public int? VoHighlightRank { get; set; }
}
