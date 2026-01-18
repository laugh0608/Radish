namespace Radish.Model.DTOs;

/// <summary>评论点赞操作结果DTO</summary>
public class CommentLikeResultDto
{
    /// <summary>当前点赞状态（true=已点赞，false=已取消点赞）</summary>
    public bool IsLiked { get; set; }

    /// <summary>最新点赞总数</summary>
    public int LikeCount { get; set; }
}
