namespace Radish.Model.ViewModels;

/// <summary>
/// 用户统计信息ViewModel
/// </summary>
public class UserStatsVo
{
    /// <summary>
    /// 帖子数量
    /// </summary>
    public int VoPostCount { get; set; }

    /// <summary>
    /// 评论数量
    /// </summary>
    public int VoCommentCount { get; set; }

    /// <summary>
    /// 总点赞数
    /// </summary>
    public int VoTotalLikeCount { get; set; }

    /// <summary>
    /// 帖子点赞数
    /// </summary>
    public int VoPostLikeCount { get; set; }

    /// <summary>
    /// 评论点赞数
    /// </summary>
    public int VoCommentLikeCount { get; set; }
}