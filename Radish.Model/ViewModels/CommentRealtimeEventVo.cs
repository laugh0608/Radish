namespace Radish.Model.ViewModels;

/// <summary>
/// 评论实时事件载荷
/// </summary>
public class CommentRealtimeEventVo
{
    /// <summary>帖子 Id</summary>
    public long VoPostId { get; set; }

    /// <summary>评论 Id</summary>
    public long VoCommentId { get; set; }

    /// <summary>父评论 Id</summary>
    public long? VoParentCommentId { get; set; }

    /// <summary>根评论 Id</summary>
    public long? VoRootCommentId { get; set; }

    /// <summary>评论详情</summary>
    public CommentVo? VoComment { get; set; }

    /// <summary>点赞数</summary>
    public int? VoLikeCount { get; set; }

    /// <summary>事件时间</summary>
    public DateTime VoEventTime { get; set; } = DateTime.UtcNow;
}
