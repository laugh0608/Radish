using Radish.IRepository;
using Radish.IRepository.Base;
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
    public Task<CoinRewardResult> GrantLikeRewardAsync(long postId, long authorId, long likerId)
    {
        return GrantLikeRewardAsync(postId, authorId, likerId, DateTime.Today);
    }

    public async Task<CoinRewardResult> GrantLikeRewardAsync(long postId, long authorId, long likerId, DateTime rewardDate)
    {
        try
        {
            var today = rewardDate.Date;

            // 2. 检查点赞者今日奖励是否已达上限
            var likerLimitReached = await CheckDailyLikeRewardLimitAsync(likerId, today);

            // 3. 发放作者奖励 +2 胡萝卜
            var authorGrant = await _coinService.GrantCoinOnceAsync(
                userId: authorId,
                amount: LIKE_REWARD_AUTHOR,
                transactionType: "LIKE_REWARD",
                rewardBusinessKey: BuildDailyRewardKey("coin:post-like:author", authorId, "post", postId, today),
                businessType: "POST_LIKE",
                businessId: postId,
                remark: $"帖子被点赞奖励"
            );

            // 4. 发放点赞者奖励 +1 胡萝卜（如未达上限）
            CoinGrantOnceResult? likerGrant = null;
            if (!likerLimitReached)
            {
                likerGrant = await _coinService.GrantCoinOnceAsync(
                    userId: likerId,
                    amount: LIKE_REWARD_LIKER,
                    transactionType: "LIKE_REWARD",
                    rewardBusinessKey: BuildDailyRewardKey("coin:post-like:giver", likerId, "post", postId, today),
                    businessType: "POST_LIKE_ACTION",
                    businessId: postId,
                    remark: $"点赞互动奖励"
                );
            }

            if (!authorGrant.Granted && likerGrant?.Granted != true)
            {
                return CoinRewardResult.Failure("今日已发放过点赞奖励");
            }

            Log.Information("点赞奖励发放成功：帖子={PostId}, 作者={AuthorId} (+{AuthorReward}), 点赞者={LikerId} (+{LikerReward})",
                postId, authorId, LIKE_REWARD_AUTHOR, likerId, likerLimitReached ? 0 : LIKE_REWARD_LIKER);

            var transactionNo = authorGrant.Granted ? authorGrant.TransactionNo : likerGrant?.TransactionNo ?? authorGrant.TransactionNo;
            var amount = (authorGrant.Granted ? LIKE_REWARD_AUTHOR : 0) + (likerGrant?.Granted == true ? LIKE_REWARD_LIKER : 0);
            return CoinRewardResult.Success(transactionNo, amount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放点赞奖励失败：帖子={PostId}, 作者={AuthorId}, 点赞者={LikerId}",
                postId, authorId, likerId);
            throw;
        }
    }

    /// <summary>
    /// 发放评论点赞奖励
    /// </summary>
    public Task<CoinRewardResult> GrantCommentLikeRewardAsync(long commentId, long authorId, long likerId)
    {
        return GrantCommentLikeRewardAsync(commentId, authorId, likerId, DateTime.Today);
    }

    public async Task<CoinRewardResult> GrantCommentLikeRewardAsync(long commentId, long authorId, long likerId, DateTime rewardDate)
    {
        try
        {
            var today = rewardDate.Date;

            // 检查点赞者上限
            var likerLimitReached = await CheckDailyLikeRewardLimitAsync(likerId, today);

            // 发放作者奖励
            var authorGrant = await _coinService.GrantCoinOnceAsync(
                userId: authorId,
                amount: LIKE_REWARD_AUTHOR,
                transactionType: "LIKE_REWARD",
                rewardBusinessKey: BuildDailyRewardKey("coin:comment-like:author", authorId, "comment", commentId, today),
                businessType: "COMMENT_LIKE",
                businessId: commentId,
                remark: $"评论被点赞奖励"
            );

            // 发放点赞者奖励
            CoinGrantOnceResult? likerGrant = null;
            if (!likerLimitReached)
            {
                likerGrant = await _coinService.GrantCoinOnceAsync(
                    userId: likerId,
                    amount: LIKE_REWARD_LIKER,
                    transactionType: "LIKE_REWARD",
                    rewardBusinessKey: BuildDailyRewardKey("coin:comment-like:giver", likerId, "comment", commentId, today),
                    businessType: "COMMENT_LIKE_ACTION",
                    businessId: commentId,
                    remark: $"点赞评论互动奖励"
                );
            }

            if (!authorGrant.Granted && likerGrant?.Granted != true)
            {
                return CoinRewardResult.Failure("今日已发放过评论点赞奖励");
            }

            var transactionNo = authorGrant.Granted ? authorGrant.TransactionNo : likerGrant?.TransactionNo ?? authorGrant.TransactionNo;
            var amount = (authorGrant.Granted ? LIKE_REWARD_AUTHOR : 0) + (likerGrant?.Granted == true ? LIKE_REWARD_LIKER : 0);
            return CoinRewardResult.Success(transactionNo, amount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放评论点赞奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            throw;
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
            var grant = await _coinService.GrantCoinOnceAsync(
                userId: authorId,
                amount: COMMENT_REWARD,
                transactionType: "COMMENT_REWARD",
                rewardBusinessKey: $"coin:comment-create:author:{authorId}:comment:{commentId}",
                businessType: "COMMENT_POST",
                businessId: commentId,
                remark: $"发表评论奖励"
            );

            if (!grant.Granted)
            {
                return CoinRewardResult.Failure("评论奖励已发放过");
            }

            Log.Information("评论奖励发放成功：评论={CommentId}, 作者={AuthorId}, 金额={Amount}",
                commentId, authorId, COMMENT_REWARD);

            return CoinRewardResult.Success(grant.TransactionNo, COMMENT_REWARD);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放评论奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            throw;
        }
    }

    /// <summary>
    /// 发放评论被回复奖励
    /// </summary>
    public Task<CoinRewardResult> GrantCommentReplyRewardAsync(
        long parentCommentId,
        long parentAuthorId,
        long replyCommentId)
    {
        return GrantCommentReplyRewardAsync(parentCommentId, parentAuthorId, replyCommentId, DateTime.Today);
    }

    public async Task<CoinRewardResult> GrantCommentReplyRewardAsync(
        long parentCommentId,
        long parentAuthorId,
        long replyCommentId,
        DateTime rewardDate)
    {
        try
        {
            var today = rewardDate.Date;

            // 发放奖励 +1 胡萝卜
            var grant = await _coinService.GrantCoinOnceAsync(
                userId: parentAuthorId,
                amount: COMMENT_REPLY_REWARD,
                transactionType: "COMMENT_REWARD",
                rewardBusinessKey: BuildDailyRewardKey("coin:comment-reply:author", parentAuthorId, "comment", parentCommentId, today),
                businessType: "COMMENT_REPLY",
                businessId: parentCommentId,
                remark: $"评论被回复奖励"
            );

            return grant.Granted
                ? CoinRewardResult.Success(grant.TransactionNo, COMMENT_REPLY_REWARD)
                : CoinRewardResult.Failure("今日已发放过评论被回复奖励");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放评论被回复奖励失败：父评论={ParentCommentId}, 作者={ParentAuthorId}",
                parentCommentId, parentAuthorId);
            throw;
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

            var grant = await _coinService.GrantCoinOnceAsync(
                userId: authorId,
                amount: totalReward,
                transactionType: "HIGHLIGHT_REWARD",
                rewardBusinessKey: $"coin:highlight-base:god-comment:author:{authorId}:comment:{commentId}",
                businessType: "GOD_COMMENT",
                businessId: commentId,
                remark: $"神评奖励（基础 {GOD_COMMENT_BASE} + 点赞加成 {likeCount}×{GOD_COMMENT_LIKE_BONUS}）"
            );

            if (!grant.Granted)
            {
                return CoinRewardResult.Failure("该评论已发放过神评奖励");
            }

            Log.Information("神评奖励发放成功：评论={CommentId}, 作者={AuthorId}, 点赞数={LikeCount}, 奖励={TotalReward}",
                commentId, authorId, likeCount, totalReward);

            return CoinRewardResult.Success(grant.TransactionNo, totalReward);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放神评奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            throw;
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

            var grant = await _coinService.GrantCoinOnceAsync(
                userId: authorId,
                amount: totalReward,
                transactionType: "HIGHLIGHT_REWARD",
                rewardBusinessKey: $"coin:highlight-base:sofa:author:{authorId}:comment:{commentId}",
                businessType: "SOFA",
                businessId: commentId,
                remark: $"沙发奖励（基础 {SOFA_BASE} + 点赞加成 {likeCount}×{SOFA_LIKE_BONUS}）"
            );

            if (!grant.Granted)
            {
                return CoinRewardResult.Failure("该评论已发放过沙发奖励");
            }

            Log.Information("沙发奖励发放成功：评论={CommentId}, 作者={AuthorId}, 点赞数={LikeCount}, 奖励={TotalReward}",
                commentId, authorId, likeCount, totalReward);

            return CoinRewardResult.Success(grant.TransactionNo, totalReward);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放沙发奖励失败：评论={CommentId}, 作者={AuthorId}",
                commentId, authorId);
            throw;
        }
    }

    /// <summary>
    /// 发放点赞加成奖励（每日结算）
    /// </summary>
    public async Task<CoinRewardResult> GrantLikeBonusRewardAsync(
        long highlightId,
        long userId,
        int likeIncrement,
        string highlightType,
        int? likeCountAfter = null)
    {
        try
        {
            if (likeIncrement <= 0)
            {
                return CoinRewardResult.Failure("点赞增量必须大于 0");
            }

            if (!likeCountAfter.HasValue || likeCountAfter.Value <= 0)
            {
                return CoinRewardResult.Failure("点赞结算快照必须大于 0");
            }

            // 根据类型计算加成金额
            var normalizedHighlightType = NormalizeHighlightType(highlightType);
            var bonusPerLike = normalizedHighlightType == "GodComment" ? GOD_COMMENT_LIKE_BONUS : SOFA_LIKE_BONUS;
            var totalBonus = likeIncrement * bonusPerLike;

            var grant = await _coinService.GrantCoinOnceAsync(
                userId: userId,
                amount: totalBonus,
                transactionType: "HIGHLIGHT_REWARD",
                rewardBusinessKey: $"coin:highlight-like-bonus:{GetHighlightTypeKey(normalizedHighlightType)}:highlight:{highlightId}:to-like:{likeCountAfter.Value}",
                businessType: $"{normalizedHighlightType}_LIKE_BONUS",
                businessId: highlightId,
                remark: $"{normalizedHighlightType} 点赞加成奖励（新增 {likeIncrement} 个点赞）"
            );

            return grant.Granted
                ? CoinRewardResult.Success(grant.TransactionNo, totalBonus)
                : CoinRewardResult.Failure("点赞加成奖励已发放过");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放点赞加成奖励失败：HighlightId={HighlightId}, 类型={HighlightType}",
                highlightId, highlightType);
            throw;
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
            var normalizedHighlightType = NormalizeHighlightType(highlightType);
            var businessType = $"{normalizedHighlightType}_RETENTION_W{week}";

            // 根据类型确定保留奖励金额
            var retentionReward = normalizedHighlightType == "GodComment" ? GOD_COMMENT_RETENTION : SOFA_RETENTION;

            var grant = await _coinService.GrantCoinOnceAsync(
                userId: userId,
                amount: retentionReward,
                transactionType: "HIGHLIGHT_REWARD",
                rewardBusinessKey: $"coin:highlight-retention:{GetHighlightTypeKey(normalizedHighlightType)}:highlight:{highlightId}:week:{week}:author:{userId}",
                businessType: businessType,
                businessId: highlightId,
                remark: $"{normalizedHighlightType} 保留奖励（第 {week} 周）"
            );

            if (!grant.Granted)
            {
                Log.Debug("第 {Week} 周保留奖励已发放过：HighlightId={HighlightId}, Type={Type}",
                    week, highlightId, normalizedHighlightType);
                return CoinRewardResult.Failure($"第 {week} 周保留奖励已发放过");
            }

            Log.Information("{HighlightType} 保留奖励发放成功：HighlightId={HighlightId}, 用户={UserId}, 周数={Week}, 奖励={Amount}",
                normalizedHighlightType, highlightId, userId, week, retentionReward);

            return CoinRewardResult.Success(grant.TransactionNo, retentionReward);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发放保留奖励失败：HighlightId={HighlightId}, 类型={HighlightType}, 周数={Week}",
                highlightId, highlightType, week);
            throw;
        }
    }

    #endregion

    #region 防刷检查

    /// <summary>
    /// 检查用户今日点赞奖励是否已达上限
    /// </summary>
    public Task<bool> CheckDailyLikeRewardLimitAsync(long userId)
    {
        return CheckDailyLikeRewardLimitAsync(userId, DateTime.Today);
    }

    public async Task<bool> CheckDailyLikeRewardLimitAsync(long userId, DateTime rewardDate)
    {
        try
        {
            var today = rewardDate.Date;
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
            throw;
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

    private static string BuildDailyRewardKey(
        string prefix,
        long userId,
        string targetType,
        long targetId,
        DateTime date)
    {
        return $"{prefix}:{userId}:{targetType}:{targetId}:day:{date:yyyyMMdd}";
    }

    private static string NormalizeHighlightType(string highlightType)
    {
        return string.Equals(highlightType, "GodComment", StringComparison.OrdinalIgnoreCase)
            ? "GodComment"
            : "Sofa";
    }

    private static string GetHighlightTypeKey(string highlightType)
    {
        return string.Equals(highlightType, "GodComment", StringComparison.OrdinalIgnoreCase)
            ? "god-comment"
            : "sofa";
    }
}
