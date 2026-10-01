using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Common.TimeTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using SqlSugar;

namespace Radish.Service.Jobs;

/// <summary>
/// 神评/沙发统计定时任务
/// </summary>
/// <remarks>
/// 每天凌晨 1 点执行，统计前一天的神评和沙发，并发放点赞加成奖励
/// </remarks>
public class CommentHighlightJob
{
    private readonly IBaseRepository<Comment> _commentRepository;
    private readonly IBaseRepository<CommentHighlight> _highlightRepository;
    private readonly ICoinRewardService _coinRewardService;
    private readonly IExperienceService _experienceService;
    private readonly CommentHighlightOptions _highlightOptions;
    private readonly TimeProvider _timeProvider;
    private readonly BusinessCalendar _businessCalendar;

    public CommentHighlightJob(
        IBaseRepository<Comment> commentRepository,
        IBaseRepository<CommentHighlight> highlightRepository,
        ICoinRewardService coinRewardService,
        IExperienceService experienceService,
        IOptions<CommentHighlightOptions> highlightOptions,
        TimeProvider timeProvider,
        BusinessCalendar businessCalendar)
    {
        _commentRepository = commentRepository;
        _highlightRepository = highlightRepository;
        _coinRewardService = coinRewardService;
        _experienceService = experienceService;
        _highlightOptions = highlightOptions.Value;
        _timeProvider = timeProvider;
        _businessCalendar = businessCalendar;
    }

    /// <summary>
    /// 执行神评/沙发统计
    /// </summary>
    /// <param name="statDate">统计日期（默认为昨天）</param>
    /// <returns>统计结果（神评数量, 沙发数量）</returns>
    public async Task<(int godCommentCount, int sofaCount)> ExecuteAsync(DateTime? statDate = null)
    {
        var summary = new BusinessJobSummary("comment-highlight");
        // 默认统计昨天的数据
        var targetDate = statDate?.Date
            ?? _businessCalendar.GetCurrentDate().AddDays(-1).ToDateTime(TimeOnly.MinValue);

        // 1. 统计神评（父评论中点赞数最高的）
        var godCommentCount = await StatGodCommentsAsync(targetDate, summary);

        // 2. 统计沙发（每个父评论下子评论中点赞数最高的）
        var sofaCount = await StatSofasAsync(targetDate, summary);

        summary.Write();
        return (godCommentCount, sofaCount);
    }

    /// <summary>
    /// 统计神评（每个帖子的父评论中点赞数最高的）
    /// </summary>
    private async Task<int> StatGodCommentsAsync(DateTime statDate, BusinessJobSummary summary)
    {
        // 🚀 增量扫描优化：只查询最近 24 小时有活动的帖子
        // 逻辑：ModifyTime > yesterday（已修改的）OR (ModifyTime == null AND CreateTime > yesterday)（新创建且未修改的）
        var yesterday = GetUtcNow().AddDays(-1);

        var postsWithComments = await _commentRepository.QueryDistinctAsync(
            c => c.PostId,
            c => !c.IsDeleted && c.IsEnabled && c.ParentId == null
                && ((c.ModifyTime != null && c.ModifyTime > yesterday)
                    || (c.ModifyTime == null && c.CreateTime > yesterday)));

        if (!postsWithComments.Any())
        {
            return 0;
        }

        var godComments = new List<CommentHighlight>();

        // 遍历每个帖子，找出神评
        foreach (var postId in postsWithComments)
        {
            // 仅在父评论数量超过配置的最小值时生效
            var parentCommentCount = await _commentRepository.QueryCountAsync(
                c => c.PostId == postId && c.ParentId == null && !c.IsDeleted && c.IsEnabled);

            if (parentCommentCount <= _highlightOptions.MinParentCommentCount)
            {
                // 评论数量不足时，确保不保留旧的"当前神评"
                summary.UpdatedCount += await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
                    h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);

                continue;
            }

            // 查询该帖子的所有父评论，按点赞数降序、创建时间降序
            var (topComments, _) = await _commentRepository.QueryPageAsync(
                whereExpression: c => c.PostId == postId &&
                           c.ParentId == null &&
                           !c.IsDeleted &&
                           c.IsEnabled,
                pageIndex: 1,
                pageSize: 5,
                orderByExpression: c => c.LikeCount,
                orderByType: OrderByType.Desc,
                thenByExpression: c => c.CreateTime,
                thenByType: OrderByType.Desc);

            if (!topComments.Any())
            {
                continue;
            }

            // 检查是否需要追加新的神评
            var existingHighlight = await _highlightRepository.QueryFirstAsync(
                h => h.PostId == postId &&
                     h.HighlightType == 1 &&
                     h.IsCurrent);

            var currentTopComment = topComments.First();
            if (currentTopComment.LikeCount <= 0)
            {
                summary.UpdatedCount += await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
                    h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);
                continue;
            }

            // 如果当前神评与历史记录不同，或者点赞数有变化，则追加新记录
            bool shouldAdd = existingHighlight == null ||
                            existingHighlight.CommentId != currentTopComment.Id ||
                            existingHighlight.LikeCount != currentTopComment.LikeCount;

            if (shouldAdd)
            {
                // 计算点赞增量（用于发放加成奖励）
                int likeIncrement = 0;
                if (existingHighlight != null && existingHighlight.CommentId == currentTopComment.Id)
                {
                    // 同一评论，点赞数增长
                    likeIncrement = currentTopComment.LikeCount - existingHighlight.LikeCount;
                }

                // 将之前的记录标记为非当前
                if (existingHighlight != null)
                {
                    summary.UpdatedCount += await _highlightRepository.UpdateColumnsAsync(
                        it => new CommentHighlight { IsCurrent = false },
                        h => h.PostId == postId && h.HighlightType == 1 && h.IsCurrent);
                }

                // 添加新记录（可能有多个并列第一）
                var rank = 1;
                var topLikeCount = topComments.First().LikeCount;

                foreach (var comment in topComments)
                {
                    // 只记录点赞数最高的（可能有多个并列）
                    if (comment.LikeCount < topLikeCount)
                    {
                        break;
                    }

                    godComments.Add(new CommentHighlight
                    {
                        PostId = postId,
                        CommentId = comment.Id,
                        ParentCommentId = null,
                        HighlightType = 1, // 神评
                        StatDate = statDate,
                        LikeCount = comment.LikeCount,
                        Rank = rank,
                        ContentSnapshot = comment.Content,
                        AuthorId = comment.AuthorId,
                        AuthorName = comment.AuthorName,
                        IsCurrent = true,
                        TenantId = comment.TenantId,
                        CreateTime = GetUtcNow(),
                        CreateBy = "CommentHighlightJob"
                    });

                    rank++;
                }

                // 🎁 发放点赞加成奖励（仅当点赞数有增长时）
                if (likeIncrement > 0 && existingHighlight != null)
                {
                    // 发放萝卜币加成奖励
                    var rewardResult = await _coinRewardService.GrantLikeBonusRewardAsync(
                        existingHighlight.Id,
                        currentTopComment.AuthorId,
                        likeIncrement,
                        "GodComment",
                        currentTopComment.LikeCount);

                    if (rewardResult.IsSuccess)
                    {
                        summary.RewardCount++;
                    }
                }

                // 🎁 发放神评经验值奖励（首次成为神评时）
                if (existingHighlight == null || existingHighlight.CommentId != currentTopComment.Id)
                {
                    var expResult = await _experienceService.GrantExperienceOnceAsync(
                        userId: currentTopComment.AuthorId,
                        amount: 50,
                        expType: "GOD_COMMENT",
                        rewardBusinessKey: $"exp:highlight-base:god-comment:author:{currentTopComment.AuthorId}:comment:{currentTopComment.Id}",
                        businessType: "Comment",
                        businessId: currentTopComment.Id,
                        remark: "评论成为神评");

                    if (expResult.Granted)
                    {
                        summary.RewardCount++;
                    }
                    else if (expResult.AlreadyGranted)
                    {
                        summary.SkippedCount++;
                    }
                    else
                    {
                        summary.RejectedCount++;
                    }
                }
            }
        }

        // 批量插入
        if (godComments.Any())
        {
            await _highlightRepository.AddRangeAsync(godComments);
            summary.ProcessedCount += godComments.Count;
        }

        return godComments.Count;
    }

    /// <summary>
    /// 统计沙发（每个父评论下子评论中点赞数最高的）
    /// </summary>
    private async Task<int> StatSofasAsync(DateTime statDate, BusinessJobSummary summary)
    {
        // 🚀 增量扫描优化：只查询最近 24 小时有活动的子评论
        // 逻辑：ModifyTime > yesterday（已修改的）OR (ModifyTime == null AND CreateTime > yesterday)（新创建且未修改的）
        var yesterday = GetUtcNow().AddDays(-1);

        var parentsWithChildren = await _commentRepository.QueryDistinctAsync(
            c => c.ParentId,
            c => !c.IsDeleted && c.IsEnabled && c.ParentId != null
                && ((c.ModifyTime != null && c.ModifyTime > yesterday)
                    || (c.ModifyTime == null && c.CreateTime > yesterday)));

        // 过滤掉 null 值（ParentId 是可空类型）
        var validParents = parentsWithChildren.Where(p => p.HasValue).Select(p => p!.Value).ToList();

        if (!validParents.Any())
        {
            return 0;
        }

        var sofas = new List<CommentHighlight>();

        // 遍历每个父评论，找出沙发
        foreach (var parentId in validParents)
        {
            // 仅在子评论数量超过配置的最小值时生效
            var childCommentCount = await _commentRepository.QueryCountAsync(
                c => c.ParentId == parentId && !c.IsDeleted && c.IsEnabled);

            if (childCommentCount <= _highlightOptions.MinChildCommentCount)
            {
                // 子评论数量不足时，确保不保留旧的"当前沙发"
                summary.UpdatedCount += await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
                    h => h.ParentCommentId == parentId && h.HighlightType == 2 && h.IsCurrent);
                continue;
            }

            // 查询该父评论下的所有子评论，按点赞数降序、创建时间降序
            var (topChildren, _) = await _commentRepository.QueryPageAsync(
                whereExpression: c => c.ParentId == parentId &&
                           !c.IsDeleted &&
                           c.IsEnabled,
                pageIndex: 1,
                pageSize: 5,
                orderByExpression: c => c.LikeCount,
                orderByType: OrderByType.Desc,
                thenByExpression: c => c.CreateTime,
                thenByType: OrderByType.Desc);

            if (!topChildren.Any())
            {
                continue;
            }

            // 获取父评论信息（用于获取 PostId）
            var parentComment = await _commentRepository.QueryByIdAsync(parentId);
            if (parentComment == null) continue;

            // 检查是否需要追加新的沙发
            var existingHighlight = await _highlightRepository.QueryFirstAsync(
                h => h.ParentCommentId == parentId &&
                     h.HighlightType == 2 &&
                     h.IsCurrent);

            var currentTopChild = topChildren.First();
            if (currentTopChild.LikeCount <= 0)
            {
                summary.UpdatedCount += await _highlightRepository.UpdateColumnsAsync(
                    it => new CommentHighlight { IsCurrent = false },
                    h => h.ParentCommentId == parentId &&
                         h.HighlightType == 2 &&
                         h.IsCurrent);
                continue;
            }

            bool shouldAdd = existingHighlight == null ||
                            existingHighlight.CommentId != currentTopChild.Id ||
                            existingHighlight.LikeCount != currentTopChild.LikeCount;

            if (shouldAdd)
            {
                // 计算点赞增量（用于发放加成奖励）
                int likeIncrement = 0;
                if (existingHighlight != null && existingHighlight.CommentId == currentTopChild.Id)
                {
                    // 同一评论，点赞数增长
                    likeIncrement = currentTopChild.LikeCount - existingHighlight.LikeCount;
                }

                // 将之前的记录标记为非当前
                if (existingHighlight != null)
                {
                    summary.UpdatedCount += await _highlightRepository.UpdateColumnsAsync(
                        it => new CommentHighlight { IsCurrent = false },
                        h => h.ParentCommentId == parentId &&
                             h.HighlightType == 2 &&
                             h.IsCurrent);
                }

                // 添加新记录
                var rank = 1;
                var topLikeCount = topChildren.First().LikeCount;

                foreach (var child in topChildren)
                {
                    if (child.LikeCount < topLikeCount)
                    {
                        break;
                    }

                    sofas.Add(new CommentHighlight
                    {
                        PostId = parentComment.PostId,
                        CommentId = child.Id,
                        ParentCommentId = parentId,
                        HighlightType = 2, // 沙发
                        StatDate = statDate,
                        LikeCount = child.LikeCount,
                        Rank = rank,
                        ContentSnapshot = child.Content,
                        AuthorId = child.AuthorId,
                        AuthorName = child.AuthorName,
                        IsCurrent = true,
                        TenantId = child.TenantId,
                        CreateTime = GetUtcNow(),
                        CreateBy = "CommentHighlightJob"
                    });

                    rank++;
                }

                // 🎁 发放点赞加成奖励（仅当点赞数有增长时）
                if (likeIncrement > 0 && existingHighlight != null)
                {
                    // 发放萝卜币加成奖励
                    var rewardResult = await _coinRewardService.GrantLikeBonusRewardAsync(
                        existingHighlight.Id,
                        currentTopChild.AuthorId,
                        likeIncrement,
                        "Sofa",
                        currentTopChild.LikeCount);

                    if (rewardResult.IsSuccess)
                    {
                        summary.RewardCount++;
                    }
                }

                // 🎁 发放沙发经验值奖励（首次成为沙发时）
                if (existingHighlight == null || existingHighlight.CommentId != currentTopChild.Id)
                {
                    var expResult = await _experienceService.GrantExperienceOnceAsync(
                        userId: currentTopChild.AuthorId,
                        amount: 30,
                        expType: "SOFA_COMMENT",
                        rewardBusinessKey: $"exp:highlight-base:sofa:author:{currentTopChild.AuthorId}:comment:{currentTopChild.Id}",
                        businessType: "Comment",
                        businessId: currentTopChild.Id,
                        remark: "评论成为沙发");

                    if (expResult.Granted)
                    {
                        summary.RewardCount++;
                    }
                    else if (expResult.AlreadyGranted)
                    {
                        summary.SkippedCount++;
                    }
                    else
                    {
                        summary.RejectedCount++;
                    }
                }
            }
        }

        // 批量插入
        if (sofas.Any())
        {
            await _highlightRepository.AddRangeAsync(sofas);
            summary.ProcessedCount += sofas.Count;
        }

        return sofas.Count;
    }

    private DateTime GetUtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
