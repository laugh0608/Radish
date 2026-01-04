using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>萝卜币奖励服务接口（论坛集成）</summary>
/// <remarks>
/// 该接口专门用于论坛功能的萝卜币奖励发放，封装了具体的奖励规则和防刷机制。
/// 底层依赖 ICoinService 进行实际的余额操作。
/// </remarks>
public interface ICoinRewardService
{
    #region 点赞奖励

    /// <summary>
    /// 发放点赞奖励（帖子被点赞）
    /// </summary>
    /// <param name="postId">帖子 ID</param>
    /// <param name="authorId">帖子作者 ID</param>
    /// <param name="likerId">点赞者 ID</param>
    /// <returns>是否成功发放（如已发放过则返回 false）</returns>
    Task<CoinRewardResult> GrantLikeRewardAsync(long postId, long authorId, long likerId);

    /// <summary>
    /// 发放评论点赞奖励（评论被点赞）
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <param name="authorId">评论作者 ID</param>
    /// <param name="likerId">点赞者 ID</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantCommentLikeRewardAsync(long commentId, long authorId, long likerId);

    #endregion

    #region 评论奖励

    /// <summary>
    /// 发放评论奖励（发表评论）
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <param name="authorId">评论者 ID</param>
    /// <param name="postId">所属帖子 ID</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantCommentRewardAsync(long commentId, long authorId, long postId);

    /// <summary>
    /// 发放评论被回复奖励
    /// </summary>
    /// <param name="parentCommentId">被回复的评论 ID</param>
    /// <param name="parentAuthorId">被回复评论的作者 ID</param>
    /// <param name="replyCommentId">回复评论 ID</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantCommentReplyRewardAsync(
        long parentCommentId,
        long parentAuthorId,
        long replyCommentId);

    #endregion

    #region 神评/沙发奖励

    /// <summary>
    /// 发放神评奖励（成为神评时）
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <param name="authorId">评论作者 ID</param>
    /// <param name="likeCount">当前点赞数</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantGodCommentRewardAsync(long commentId, long authorId, int likeCount);

    /// <summary>
    /// 发放沙发奖励（成为沙发时）
    /// </summary>
    /// <param name="commentId">评论 ID</param>
    /// <param name="authorId">评论作者 ID</param>
    /// <param name="likeCount">当前点赞数</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantSofaRewardAsync(long commentId, long authorId, int likeCount);

    /// <summary>
    /// 发放点赞加成奖励（每日结算）
    /// </summary>
    /// <param name="highlightId">神评/沙发记录 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="likeIncrement">点赞增量</param>
    /// <param name="highlightType">类型（GodComment/Sofa）</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantLikeBonusRewardAsync(
        long highlightId,
        long userId,
        int likeIncrement,
        string highlightType);

    /// <summary>
    /// 发放保留奖励（每周结算）
    /// </summary>
    /// <param name="highlightId">神评/沙发记录 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="week">第几周（1-3）</param>
    /// <param name="highlightType">类型（GodComment/Sofa）</param>
    /// <returns>是否成功发放</returns>
    Task<CoinRewardResult> GrantRetentionRewardAsync(
        long highlightId,
        long userId,
        int week,
        string highlightType);

    #endregion

    #region 防刷检查

    /// <summary>
    /// 检查用户今日点赞奖励是否已达上限
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>是否已达上限</returns>
    Task<bool> CheckDailyLikeRewardLimitAsync(long userId);

    /// <summary>
    /// 检查是否已发放过该奖励（防重复）
    /// </summary>
    /// <param name="businessType">业务类型</param>
    /// <param name="businessId">业务 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="date">日期（可选，用于每日一次的检查）</param>
    /// <returns>是否已发放过</returns>
    Task<bool> CheckRewardExistsAsync(
        string businessType,
        long businessId,
        long userId,
        DateTime? date = null);

    #endregion
}

/// <summary>
/// 萝卜币奖励发放结果
/// </summary>
public class CoinRewardResult
{
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; set; }

    /// <summary>交易流水号（成功时返回）</summary>
    public string? TransactionNo { get; set; }

    /// <summary>发放金额</summary>
    public long Amount { get; set; }

    /// <summary>失败原因</summary>
    public string? FailureReason { get; set; }

    /// <summary>创建成功结果</summary>
    public static CoinRewardResult Success(string transactionNo, long amount) => new()
    {
        IsSuccess = true,
        TransactionNo = transactionNo,
        Amount = amount
    };

    /// <summary>创建失败结果</summary>
    public static CoinRewardResult Failure(string reason) => new()
    {
        IsSuccess = false,
        FailureReason = reason
    };
}
