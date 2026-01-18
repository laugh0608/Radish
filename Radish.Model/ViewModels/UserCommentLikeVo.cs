using System;

namespace Radish.Model.ViewModels;

/// <summary>用户评论点赞视图模型</summary>
public class UserCommentLikeVo
{
    /// <summary>点赞记录ID</summary>
    public long VoId { get; set; }

    /// <summary>用户ID</summary>
    public long VoUserId { get; set; }

    /// <summary>评论ID</summary>
    public long VoCommentId { get; set; }

    /// <summary>帖子ID</summary>
    public long VoPostId { get; set; }

    /// <summary>点赞时间</summary>
    public DateTime VoLikedAt { get; set; }

    /// <summary>用户名（关联信息，按需加载）</summary>
    public string? VoUserName { get; set; }

    /// <summary>用户头像（关联信息，按需加载）</summary>
    public string? VoUserAvatar { get; set; }
}
