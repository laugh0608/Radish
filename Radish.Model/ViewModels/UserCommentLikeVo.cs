using System;

namespace Radish.Model.ViewModels;

/// <summary>用户评论点赞视图模型</summary>
public class UserCommentLikeVo
{
    /// <summary>点赞记录ID</summary>
    public long Id { get; set; }

    /// <summary>用户ID</summary>
    public long UserId { get; set; }

    /// <summary>评论ID</summary>
    public long CommentId { get; set; }

    /// <summary>帖子ID</summary>
    public long PostId { get; set; }

    /// <summary>点赞时间</summary>
    public DateTime LikedAt { get; set; }

    /// <summary>用户名（关联信息，按需加载）</summary>
    public string? UserName { get; set; }

    /// <summary>用户头像（关联信息，按需加载）</summary>
    public string? UserAvatar { get; set; }
}
