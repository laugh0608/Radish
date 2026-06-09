namespace Radish.Model.ViewModels;

/// <summary>
/// 评论输入状态实时事件载荷
/// </summary>
public class CommentTypingEventVo
{
    /// <summary>帖子 Id</summary>
    public long VoPostId { get; set; }

    /// <summary>正在编辑的评论 Id，空表示新评论</summary>
    public long? VoCommentId { get; set; }

    /// <summary>用户 Id</summary>
    public long VoUserId { get; set; }

    /// <summary>用户名称</summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>事件时间</summary>
    public DateTime VoEventTime { get; set; } = DateTime.UtcNow;
}
