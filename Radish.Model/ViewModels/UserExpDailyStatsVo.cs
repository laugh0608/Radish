namespace Radish.Model.ViewModels;

/// <summary>
/// 用户经验值每日统计视图模型
/// </summary>
public class UserExpDailyStatsVo
{
    /// <summary>
    /// 统计 ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 统计日期
    /// </summary>
    public DateTime StatDate { get; set; }

    /// <summary>
    /// 当日获得经验值总计
    /// </summary>
    public long ExpEarned { get; set; }

    /// <summary>
    /// 来自发帖的经验值
    /// </summary>
    public int ExpFromPost { get; set; }

    /// <summary>
    /// 来自评论的经验值
    /// </summary>
    public int ExpFromComment { get; set; }

    /// <summary>
    /// 来自点赞的经验值
    /// </summary>
    /// <remarks>包括点赞他人和被点赞</remarks>
    public int ExpFromLike { get; set; }

    /// <summary>
    /// 来自神评/沙发的经验值
    /// </summary>
    public int ExpFromHighlight { get; set; }

    /// <summary>
    /// 来自登录的经验值
    /// </summary>
    /// <remarks>包括每日登录和连续登录</remarks>
    public int ExpFromLogin { get; set; }

    /// <summary>
    /// 当日发帖数
    /// </summary>
    public int PostCount { get; set; }

    /// <summary>
    /// 当日评论数
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// 当日点赞数
    /// </summary>
    /// <remarks>点赞他人</remarks>
    public int LikeGivenCount { get; set; }

    /// <summary>
    /// 当日被点赞数
    /// </summary>
    /// <remarks>被他人点赞</remarks>
    public int LikeReceivedCount { get; set; }
}
