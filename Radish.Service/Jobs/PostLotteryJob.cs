using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Radish.Common.LogTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using SqlSugar;

namespace Radish.Service.Jobs;

/// <summary>
/// 论坛抽奖自动开奖定时任务
/// </summary>
public class PostLotteryJob
{
    private readonly IBaseRepository<PostLottery> _postLotteryRepository;
    private readonly IPostLotteryService _postLotteryService;
    private readonly ILogger<PostLotteryJob> _logger;

    public PostLotteryJob(
        IBaseRepository<PostLottery> postLotteryRepository,
        IPostLotteryService postLotteryService,
        ILogger<PostLotteryJob> logger)
    {
        _postLotteryRepository = postLotteryRepository;
        _postLotteryService = postLotteryService;
        _logger = logger;
    }

    /// <summary>
    /// 扫描到期抽奖并自动开奖
    /// </summary>
    public async Task<int> ExecuteAutoDrawAsync(int batchSize = 20)
    {
        var started = Stopwatch.GetTimestamp();
        var safeBatchSize = Math.Clamp(batchSize, 1, 100);
        var now = DateTime.UtcNow;

        var (dueLotteries, _) = await _postLotteryRepository.QueryPageAsync(
            lottery => !lottery.IsDeleted &&
                       !lottery.IsDrawn &&
                       lottery.DrawTime != null &&
                       lottery.DrawTime <= now,
            1,
            safeBatchSize,
            lottery => lottery.DrawTime ?? DateTime.MaxValue,
            OrderByType.Asc);

        var duePostIds = dueLotteries
            .Select(lottery => lottery.PostId)
            .Distinct()
            .ToList();

        if (duePostIds.Count == 0)
        {
            return 0;
        }

        var successCount = 0;
        var failedCount = 0;
        string? failureKind = null;
        foreach (var postId in duePostIds)
        {
            try
            {
                await _postLotteryService.AutoDrawByPostIdAsync(postId);
                successCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                var kind = RuntimeFailureSummary.Classify(ex);
                failureKind = failureKind == null || failureKind == kind ? kind : "other";
            }
        }

        var properties = new Dictionary<string, object>
        {
            ["EventCode"] = failedCount > 0 ? "job.batch.failed" : "job.batch.completed",
            ["SourceCategory"] = "job", ["jobKind"] = "post-lottery",
            ["outcome"] = failedCount > 0 ? (successCount > 0 ? "partial" : "failed") : "succeeded"
        };
        if (failureKind != null) properties["failureKind"] = failureKind;
        using var scope = _logger.BeginScope(properties);
        // 单项异常已消费；扫描异常仍直接传播，由 Hangfire 处理。
        _logger.Log(failedCount > 0 ? LogLevel.Error : LogLevel.Information,
            "Lottery batch finished; processed={processedCount}; failed={failedCount}; duration={durationMs} ms",
            successCount, failedCount, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return successCount;
    }
}
