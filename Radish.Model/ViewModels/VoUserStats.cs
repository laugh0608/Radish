namespace Radish.Model.ViewModels;

/// <summary>
/// 用户统计信息ViewModel
/// </summary>
public class VoUserStats
{
    /// <summary>
    /// 帖子数量
    /// </summary>
    public int PostCount { get; set; }

    /// <summary>
    /// 评论数量
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// 总点赞数
    /// </summary>
    public int TotalLikeCount { get; set; }

    /// <summary>
    /// 帖子点赞数
    /// </summary>
    public int PostLikeCount { get; set; }

    /// <summary>
    /// 评论点赞数
    /// </summary>
    public int CommentLikeCount { get; set; }
}