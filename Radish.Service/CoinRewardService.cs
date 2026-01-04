using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Serilog;

namespace Radish.Service;

/// <summary>萝卜币奖励服务实现</summary>
public class CoinRewardService : ICoinRewardService
{
    private readonly ICoinService _coinService;
    private readonly IBaseRepository<CoinTransaction> _coinTransactionRepository;

    // 奖励金额常量
    private const long LIKE_REWARD_AUTHOR = 2;      // 被点赞奖励 +2 胡萝卜
    private const long LIKE_REWARD_LIKER = 1;       // 点赞者奖励 +1 胡萝卜
    private const long DAILY_LIKE_LIMIT = 50;       // 每日点赞奖励上限 50 胡萝卜
    private const long COMMENT_REWARD = 1;          // 评论奖励 +1 胡萝卜
    private const long COMMENT_REPLY_REWARD = 1;    // 评论被回复奖励 +1 胡萝卜

    // 神评/沙发奖励常量
    private const long GOD_COMMENT_BASE = 8;        // 神评基础奖励 +8 胡萝卜
    private const long GOD_COMMENT_LIKE_BONUS = 5;  // 神评点赞加成 +5 胡萝卜/点赞
    private const long GOD_COMMENT_RETENTION = 15;  // 神评保留奖励 +15 胡萝卜/周

    private const long SOFA_BASE = 5;               // 沙发基础奖励 +5 胡萝卜
    private const long SOFA_LIKE_BONUS = 3;         // 沙发点赞加成 +3 胡萝卜/点赞
    private const long SOFA_RETENTION = 10;         // 沙发保留奖励 +10 胡萝卜/周

    public CoinRewardService(
        ICoinService coinService,
        IBaseRepository<CoinTransaction> coinTransactionRepository)
    {
        _coinService = coinService;
        _coinTransactionRepository = coinTransactionRepository;
    }

    #region 点赞奖励

    /// <summary>
    /// 发放点赞奖励（帖子被点赞）
    /// </summary>
    public async Task<CoinRewardResult> GrantLikeRewardAsync(long postId, long authorId, long likerId)
    {
        try
        {
            var today = DateTime.Today;

            // 1. 检查是否已发放过（同一用户对同一帖子每日仅奖励一次）
            var alreadyGranted = await CheckRewardExistsAsync("POST_LIKE", postId, authorId, today);
            if (alreadyGranted)
            {
                Log.Debug("帖子 {PostId} 今日已向作者 {AuthorId} 发放过点赞奖励，跳过", postId, authorId);
                return CoinRewardResult.Failure("今日已发放过点赞奖励");
            }

            // 2. 检查点赞者今日奖励是否已达上限
            var likerLimitReached = await CheckDailyLikeRewardLimitAsync(likerId);

            // 3. 发放作者奖励 +2 胡萝卜
            var authorTxNo = await _coinService.GrantCoinAsync(
                userId: authorId,
                amount: LIKE_REWARD_AUTHOR,
                transactionType: "LIKE_REWARD",
                businessType: "POST_LIKE",
                businessId: postId,
                remark: $"帖子被点赞奖励"
            );

            // 4. 发放点赞者奖励 +1 胡萝卜（如未达上限）
            string? likerTxNo = null;
            if (!likerLimitReached)
            {
                likerTxNo = await _coinService.GrantCoinAsync(
                    userId: likerId,
                    amount: LIKE_REWARD_LIKER,
                    transactionType: "LIKE_REWARD",
                    businessType: "POST_LIKE_ACTION",
                    businessId: postId,
                    remark: $"点赞互动奖励"
                );
            }

            Log.Information("点赞奖励发放成功：帖子={PostId}, 作者={AuthorId} (+{AuthorReward}), 点赞者={LikerId} (+{LikerReward})",
                postId, authorId, LIKE_REWARD_AUTHOR, likerId, likerLimitReached ? 0 : LIKE_REWARD_LIKER);

            return CoinRewardResult.Success(authorTxNo, LIKE_REWARD_AUTHOR + (likerLimitReached ? 0 : LIKE_REWARD_LIKER));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放点赞奖励失败：帖子={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                postId, authorId, likerId);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发放评论点赞奖励
    /// </summary>
    public async Task<CoinRewardResult> GrantCommentLikeRewardAsync(long commentId, long authorId, long likerId)
    {
        try
        {
            var today = DateTime.Today;

            // 检查是否已发放过
            var alreadyGranted = await CheckRewardExistsAsync("COMMENT_LIKE", commentId, authorId, today);
            if (alreadyGranted)
            {
                return CoinRewardResult.Failure("今日已发放过评论点赞奖励");
            }

            // 检查点赞者上限
            var likerLimitReached = await CheckDailyLikeRewardLimitAsync(likerId);

            // 发放作者奖励
            var authorTxNo = await _coinService.GrantCoinAsync(
                userId: authorId,
                amount: LIKE_REWARD_AUTHOR,
                transactionType: "LIKE_REWARD",
                businessType: "COMMENT_LIKE",
                businessId: commentId,
                remark: $"评论被点赞奖励"
            );

            // 发放点赞者奖励
            if (!likerLimitReached)
            {
                await _coinService.GrantCoinAsync(
                    userId: likerId,
                    amount: LIKE_REWARD_LIKER,
                    transactionType: "LIKE_REWARD",
                    businessType: "COMMENT_LIKE_ACTION",
                    businessId: commentId,
                    remark: $"点赞评论互动奖励"
                );
            }

            return CoinRewardResult.Success(authorTxNo, LIKE_REWARD_AUTHOR + (likerLimitReached ? 0 : LIKE_REWARD_LIKER));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放评论点赞奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    #endregion

    #region 评论奖励

    /// <summary>
    /// 发放评论奖励
    /// </summary>
    public async Task<CoinRewardResult> GrantCommentRewardAsync(long commentId, long authorId, long postId)
    {
        try
        {
            // 发放评论奖励 +1 胡萝卜
            var txNo = await _coinService.GrantCoinAsync(
                userId: authorId,
                amount: COMMENT_REWARD,
                transactionType: "COMMENT_REWARD",
                businessType: "COMMENT_POST",
                businessId: commentId,
                remark: $"发表评论奖励"
            );

            Log.Information("评论奖励发放成功：评论={CommentId}, 作者={AuthorId}, 金额={Amount}",
                commentId, authorId, COMMENT_REWARD);

            return CoinRewardResult.Success(txNo, COMMENT_REWARD);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放评论奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发放评论被回复奖励
    /// </summary>
    public async Task<CoinRewardResult> GrantCommentReplyRewardAsync(
        long parentCommentId,
        long parentAuthorId,
        long replyCommentId)
    {
        try
        {
            var today = DateTime.Today;

            // 检查是否已发放过（每日一次）
            var alreadyGranted = await CheckRewardExistsAsync("COMMENT_REPLY", parentCommentId, parentAuthorId, today);
            if (alreadyGranted)
            {
                return CoinRewardResult.Failure("今日已发放过评论被回复奖励");
            }

            // 发放奖励 +1 胡萝卜
            var txNo = await _coinService.GrantCoinAsync(
                userId: parentAuthorId,
                amount: COMMENT_REPLY_REWARD,
                transactionType: "COMMENT_REWARD",
                businessType: "COMMENT_REPLY",
                businessId: parentCommentId,
                remark: $"评论被回复奖励"
            );

            return CoinRewardResult.Success(txNo, COMMENT_REPLY_REWARD);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放评论被回复奖励失败：父评论={ParentCommentId}, 作者={ParentAuthorId}",
                parentCommentId, parentAuthorId);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    #endregion

    #region 神评/沙发奖励

    /// <summary>
    /// 发放神评奖励
    /// </summary>
    public async Task<CoinRewardResult> GrantGodCommentRewardAsync(long commentId, long authorId, int likeCount)
    {
        try
        {
            // 计算总奖励：基础 + 点赞加成
            var totalReward = GOD_COMMENT_BASE + (likeCount * GOD_COMMENT_LIKE_BONUS);

            var txNo = await _coinService.GrantCoinAsync(
                userId: authorId,
                amount: totalReward,
                transactionType: "HIGHLIGHT_REWARD",
                businessType: "GOD_COMMENT",
                businessId: commentId,
                remark: $"神评奖励（基础 {GOD_COMMENT_BASE} + 点赞加成 {likeCount}×{GOD_COMMENT_LIKE_BONUS}）"
            );

            Log.Information("神评奖励发放成功：评论={CommentId}, 作者={AuthorId}, 点赞数={LikeCount}, 奖励={TotalReward}",
                commentId, authorId, likeCount, totalReward);

            return CoinRewardResult.Success(txNo, totalReward);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放神评奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发放沙发奖励
    /// </summary>
    public async Task<CoinRewardResult> GrantSofaRewardAsync(long commentId, long authorId, int likeCount)
    {
        try
        {
            // 计算总奖励：基础 + 点赞加成
            var totalReward = SOFA_BASE + (likeCount * SOFA_LIKE_BONUS);

            var txNo = await _coinService.GrantCoinAsync(
                userId: authorId,
                amount: totalReward,
                transactionType: "HIGHLIGHT_REWARD",
                businessType: "SOFA",
                businessId: commentId,
                remark: $"沙发奖励（基础 {SOFA_BASE} + 点赞加成 {likeCount}×{SOFA_LIKE_BONUS}）"
            );

            Log.Information("沙发奖励发放成功：评论={CommentId}, 作者={AuthorId}, 点赞数={LikeCount}, 奖励={TotalReward}",
                commentId, authorId, likeCount, totalReward);

            return CoinRewardResult.Success(txNo, totalReward);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放沙发奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发放点赞加成奖励（每日结算）
    /// </summary>
    public async Task<CoinRewardResult> GrantLikeBonusRewardAsync(
        long highlightId,
        long userId,
        int likeIncrement,
        string highlightType)
    {
        try
        {
            if (likeIncrement <= 0)
            {
                return CoinRewardResult.Failure("点赞增量必须大于 0");
            }

            // 根据类型计算加成金额
            var bonusPerLike = highlightType == "GodComment" ? GOD_COMMENT_LIKE_BONUS : SOFA_LIKE_BONUS;
            var totalBonus = likeIncrement * bonusPerLike;

            var txNo = await _coinService.GrantCoinAsync(
                userId: userId,
                amount: totalBonus,
                transactionType: "HIGHLIGHT_REWARD",
                businessType: $"{highlightType}_LIKE_BONUS",
                businessId: highlightId,
                remark: $"{highlightType} 点赞加成奖励（新增 {likeIncrement} 个点赞）"
            );

            return CoinRewardResult.Success(txNo, totalBonus);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放点赞加成奖励失败：HighlightId={HighlightId}, 类型={HighlightType}",
                highlightId, highlightType);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发放保留奖励（每周结算）
    /// </summary>
    public async Task<CoinRewardResult> GrantRetentionRewardAsync(
        long highlightId,
        long userId,
        int week,
        string highlightType)
    {
        try
        {
            if (week < 1 || week > 3)
            {
                return CoinRewardResult.Failure("保留周数必须在 1-3 之间");
            }

            // 检查是否已发放过该周的保留奖励
            var businessType = $"{highlightType}_RETENTION_W{week}";
            var alreadyGranted = await CheckRewardExistsAsync(businessType, highlightId, userId);
            if (alreadyGranted)
            {
                Log.Debug("第 {Week} 周保留奖励已发放过：HighlightId={HighlightId}, Type={Type}",
                    week, highlightId, highlightType);
                return CoinRewardResult.Failure($"第 {week} 周保留奖励已发放过");
            }

            // 根据类型确定保留奖励金额
            var retentionReward = highlightType == "GodComment" ? GOD_COMMENT_RETENTION : SOFA_RETENTION;

            var txNo = await _coinService.GrantCoinAsync(
                userId: userId,
                amount: retentionReward,
                transactionType: "HIGHLIGHT_REWARD",
                businessType: businessType,
                businessId: highlightId,
                remark: $"{highlightType} 保留奖励（第 {week} 周）"
            );

            Log.Information("{HighlightType} 保留奖励发放成功：HighlightId={HighlightId}, 用户={UserId}, 周数={Week}, 奖励={Amount}",
                highlightType, highlightId, userId, week, retentionReward);

            return CoinRewardResult.Success(txNo, retentionReward);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放保留奖励失败：HighlightId={HighlightId}, 类型={HighlightType}, 周数={Week}",
                highlightId, highlightType, week);
            return CoinRewardResult.Failure($"发放失败: {ex.Message}");
        }
    }

    #endregion

    #region 防刷检查

    /// <summary>
    /// 检查用户今日点赞奖励是否已达上限
    /// </summary>
    public async Task<bool> CheckDailyLikeRewardLimitAsync(long userId)
    {
        try
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 统计今日点赞奖励总额（仅统计点赞者获得的奖励）
            var todayRewards = await _coinTransactionRepository.QuerySumAsync(
                t => t.Amount,
                t => t.ToUserId == userId
                    && t.TransactionType == "LIKE_REWARD"
                    && (t.BusinessType == "POST_LIKE_ACTION" || t.BusinessType == "COMMENT_LIKE_ACTION")
                    && t.Status == "SUCCESS"
                    && t.CreateTime >= today
                    && t.CreateTime < tomorrow);

            var limitReached = todayRewards >= DAILY_LIKE_LIMIT;

            if (limitReached)
            {
                Log.Debug("用户 {UserId} 今日点赞奖励已达上限 {Limit}（已获得 {TodayRewards}）",
                    userId, DAILY_LIKE_LIMIT, todayRewards);
            }

            return limitReached;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查用户 {UserId} 今日点赞奖励上限失败", userId);
            return true; // 出错时默认返回已达上限，避免刷币
        }
    }

    /// <summary>
    /// 检查是否已发放过该奖励
    /// </summary>
    public async Task<bool> CheckRewardExistsAsync(
        string businessType,
        long businessId,
        long userId,
        DateTime? date = null)
    {
        try
        {
            // 构建查询条件
            System.Linq.Expressions.Expression<Func<CoinTransaction, bool>> predicate =
                t => t.BusinessType == businessType
                    && t.BusinessId == businessId
                    && t.ToUserId == userId
                    && t.Status == "SUCCESS";

            // 如果指定了日期，则检查当天是否已发放
            if (date.HasValue)
            {
                var startDate = date.Value.Date;
                var endDate = startDate.AddDays(1);

                var transactions = await _coinTransactionRepository.QueryAsync(predicate);
                var exists = transactions.Any(t => t.CreateTime >= startDate && t.CreateTime < endDate);
                return exists;
            }

            var result = await _coinTransactionRepository.QueryAsync(predicate);
            return result.Any();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查奖励是否已发放失败：BusinessType={BusinessType}, BusinessId={BusinessId}, UserId={UserId}",
                businessType, businessId, userId);
            return true; // 出错时默认返回已存在，避免重复发放
        }
    }

    #endregion
}
