namespace Radish.Model.ViewModels;

/// <summary>
/// 评论精确定位信息
/// </summary>
public class CommentNavigationVo
{
    /// <summary>
    /// 目标评论 Id
    /// </summary>
    public long VoCommentId { get; set; }

    /// <summary>
    /// 所属帖子 Id
    /// </summary>
    public long VoPostId { get; set; }

    /// <summary>
    /// 根评论 Id
    /// </summary>
    public long VoRootCommentId { get; set; }

    /// <summary>
    /// 父评论 Id（顶级评论时为空）
    /// </summary>
    public long? VoParentCommentId { get; set; }

    /// <summary>
    /// 是否为根评论
    /// </summary>
    public bool VoIsRootComment { get; set; }

    /// <summary>
    /// 根评论所在页码（默认排序）
    /// </summary>
    public int VoRootPageIndex { get; set; }

    /// <summary>
    /// 子评论所在页码（仅子评论有效）
    /// </summary>
    public int? VoChildPageIndex { get; set; }
}
