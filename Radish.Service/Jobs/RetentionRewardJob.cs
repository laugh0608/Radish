using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Serilog;

namespace Radish.Service.Jobs;

/// <summary>
/// 神评/沙发保留奖励定时任务
/// </summary>
/// <remarks>
/// 每周执行一次，为保留的神评和沙发发放保留奖励
/// - 神评保留奖励：+15 胡萝卜/周（最多3周）
/// - 沙发保留奖励：+10 胡萝卜/周（最多3周）
/// </remarks>
public class RetentionRewardJob
{
    private readonly IBaseRepository<CommentHighlight> _highlightRepository;
    private readonly ICoinRewardService _coinRewardService;

    public RetentionRewardJob(
        IBaseRepository<CommentHighlight> highlightRepository,
        ICoinRewardService coinRewardService)
    {
        _highlightRepository = highlightRepository;
        _coinRewardService = coinRewardService;
    }

    /// <summary>
    /// 执行保留奖励发放
    /// </summary>
    /// <returns>发放结果（神评数量, 沙发数量）</returns>
    public async Task<(int godCommentCount, int sofaCount)> ExecuteAsync()
    {
        try
        {
            Log.Information("[RetentionReward] 开始执行保留奖励发放");

            // 1. 发放神评保留奖励
            var godCommentCount = await GrantGodCommentRetentionRewardAsync();

            // 2. 发放沙发保留奖励
            var sofaCount = await GrantSofaRetentionRewardAsync();

            Log.Information("[RetentionReward] 保留奖励发放完成，神评：{GodCount} 个，沙发：{SofaCount} 个",
                godCommentCount, sofaCount);

            return (godCommentCount, sofaCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RetentionReward] 执行保留奖励发放时发生异常");
            return (0, 0);
        }
    }

    /// <summary>
    /// 发放神评保留奖励
    /// </summary>
    private async Task<int> GrantGodCommentRetentionRewardAsync()
    {
        try
        {
            // 查询所有当前的神评（IsCurrent = true）
            var currentGodComments = await _highlightRepository.QueryAsync(
                h => h.HighlightType == 1 && h.IsCurrent);

            if (!currentGodComments.Any())
            {
                Log.Information("[RetentionReward] 没有找到当前的神评");
                return 0;
            }

            Log.Information("[RetentionReward] 找到 {Count} 个当前神评", currentGodComments.Count);

            var rewardCount = 0;

            foreach (var highlight in currentGodComments)
            {
                // 计算已保留的完整周数（从创建时间到现在）
                var totalWeeks = CalculateWeeksRetained(highlight.CreateTime);

                // 最多发放3周的保留奖励
                var maxWeeks = Math.Min(totalWeeks, 3);

                // 逐周检查并发放奖励（发放所有未发放的周）
                for (int week = 1; week <= maxWeeks; week++)
                {
                    try
                    {
                        var result = await _coinRewardService.GrantRetentionRewardAsync(
                            highlight.Id,
                            highlight.AuthorId,
                            week,
                            "GodComment");

                        if (result.IsSuccess)
                        {
                            rewardCount++;
                            Log.Information("[RetentionReward] 神评保留奖励发放成功：HighlightId={HighlightId}, AuthorId={AuthorId}, Week={Week}, Amount={Amount}",
                                highlight.Id, highlight.AuthorId, week, result.Amount);
                        }
                        else if (result.FailureReason?.Contains("已发放过") == false)
                        {
                            // 只记录非重复的失败
                            Log.Warning("[RetentionReward] 神评保留奖励发放失败：HighlightId={HighlightId}, Week={Week}, Reason={Reason}",
                                highlight.Id, week, result.FailureReason);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[RetentionReward] 发放神评保留奖励时发生异常：HighlightId={HighlightId}, Week={Week}",
                            highlight.Id, week);
                    }
                }

                if (totalWeeks > 3)
                {
                    Log.Debug("[RetentionReward] 神评已保留超过3周：HighlightId={HighlightId}, TotalWeeks={Weeks}",
                        highlight.Id, totalWeeks);
                }
            }

            return rewardCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RetentionReward] 发放神评保留奖励时发生异常");
            return 0;
        }
    }

    /// <summary>
    /// 发放沙发保留奖励
    /// </summary>
    private async Task<int> GrantSofaRetentionRewardAsync()
    {
        try
        {
            // 查询所有当前的沙发（IsCurrent = true）
            var currentSofas = await _highlightRepository.QueryAsync(
                h => h.HighlightType == 2 && h.IsCurrent);

            if (!currentSofas.Any())
            {
                Log.Information("[RetentionReward] 没有找到当前的沙发");
                return 0;
            }

            Log.Information("[RetentionReward] 找到 {Count} 个当前沙发", currentSofas.Count);

            var rewardCount = 0;

            foreach (var highlight in currentSofas)
            {
                // 计算已保留的完整周数（从创建时间到现在）
                var totalWeeks = CalculateWeeksRetained(highlight.CreateTime);

                // 最多发放3周的保留奖励
                var maxWeeks = Math.Min(totalWeeks, 3);

                // 逐周检查并发放奖励（发放所有未发放的周）
                for (int week = 1; week <= maxWeeks; week++)
                {
                    try
                    {
                        var result = await _coinRewardService.GrantRetentionRewardAsync(
                            highlight.Id,
                            highlight.AuthorId,
                            week,
                            "Sofa");

                        if (result.IsSuccess)
                        {
                            rewardCount++;
                            Log.Information("[RetentionReward] 沙发保留奖励发放成功：HighlightId={HighlightId}, AuthorId={AuthorId}, Week={Week}, Amount={Amount}",
                                highlight.Id, highlight.AuthorId, week, result.Amount);
                        }
                        else if (result.FailureReason?.Contains("已发放过") == false)
                        {
                            // 只记录非重复的失败
                            Log.Warning("[RetentionReward] 沙发保留奖励发放失败：HighlightId={HighlightId}, Week={Week}, Reason={Reason}",
                                highlight.Id, week, result.FailureReason);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[RetentionReward] 发放沙发保留奖励时发生异常：HighlightId={HighlightId}, Week={Week}",
                            highlight.Id, week);
                    }
                }

                if (totalWeeks > 3)
                {
                    Log.Debug("[RetentionReward] 沙发已保留超过3周：HighlightId={HighlightId}, TotalWeeks={Weeks}",
                        highlight.Id, totalWeeks);
                }
            }

            return rewardCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[RetentionReward] 发放沙发保留奖励时发生异常");
            return 0;
        }
    }

    /// <summary>
    /// 计算保留周数（从创建时间到现在）
    /// </summary>
    /// <param name="createTime">神评/沙发创建时间</param>
    /// <returns>保留周数（1-based）</returns>
    private int CalculateWeeksRetained(DateTime createTime)
    {
        var now = DateTime.Now;
        var timeSpan = now - createTime;

        // 计算完整的周数（7天为1周）
        var weeks = (int)(timeSpan.TotalDays / 7);

        // 返回第几周（1-based）
        // 例如：0-6天 = 第0周（不发放），7-13天 = 第1周，14-20天 = 第2周，21-27天 = 第3周
        return weeks;
    }
}
