namespace Radish.Model.ViewModels;

/// <summary>
/// 评论高亮重算结果
/// </summary>
public class CommentHighlightRecheckResultVo
{
    /// <summary>帖子 Id</summary>
    public long VoPostId { get; set; }

    /// <summary>父评论 Id，空表示神评重算</summary>
    public long? VoParentCommentId { get; set; }

    /// <summary>高亮类型：1=神评，2=沙发</summary>
    public int VoHighlightType { get; set; }

    /// <summary>本次是否改变当前高亮集合或快照</summary>
    public bool VoChanged { get; set; }

    /// <summary>当前高亮评论 Id</summary>
    public List<long> VoCurrentCommentIds { get; set; } = [];

    /// <summary>事件时间</summary>
    public DateTime VoEventTime { get; set; } = DateTime.UtcNow;

    public static CommentHighlightRecheckResultVo NoChange(long postId, long? parentCommentId, int highlightType)
    {
        return new CommentHighlightRecheckResultVo
        {
            VoPostId = postId,
            VoParentCommentId = parentCommentId,
            VoHighlightType = highlightType,
            VoChanged = false
        };
    }
}
