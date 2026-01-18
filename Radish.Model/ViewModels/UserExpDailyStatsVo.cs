namespace Radish.Model.ViewModels;

/// <summary>
/// 用户经验值每日统计视图模型
/// </summary>
public class UserExpDailyStatsVo
{
    /// <summary>
    /// 统计 ID
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 统计日期
    /// </summary>
    public DateTime VoStatDate { get; set; }

    /// <summary>
    /// 当日获得经验值总计
    /// </summary>
    public long VoExpEarned { get; set; }

    /// <summary>
    /// 来自发帖的经验值
    /// </summary>
    public int VoExpFromPost { get; set; }

    /// <summary>
    /// 来自评论的经验值
    /// </summary>
    public int VoExpFromComment { get; set; }

    /// <summary>
    /// 来自点赞的经验值
    /// </summary>
    /// <remarks>包括点赞他人和被点赞</remarks>
    public int VoExpFromLike { get; set; }

    /// <summary>
    /// 来自神评/沙发的经验值
    /// </summary>
    public int VoExpFromHighlight { get; set; }

    /// <summary>
    /// 来自登录的经验值
    /// </summary>
    /// <remarks>包括每日登录和连续登录</remarks>
    public int VoExpFromLogin { get; set; }

    /// <summary>
    /// 当日发帖数
    /// </summary>
    public int VoPostCount { get; set; }

    /// <summary>
    /// 当日评论数
    /// </summary>
    public int VoCommentCount { get; set; }

    /// <summary>
    /// 当日点赞数
    /// </summary>
    /// <remarks>点赞他人</remarks>
    public int VoLikeGivenCount { get; set; }

    /// <summary>
    /// 当日被点赞数
    /// </summary>
    /// <remarks>被他人点赞</remarks>
    public int VoLikeReceivedCount { get; set; }
}
