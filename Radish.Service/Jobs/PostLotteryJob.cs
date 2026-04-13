using Microsoft.Extensions.Logging;
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
        var safeBatchSize = Math.Clamp(batchSize, 1, 100);
        var now = DateTime.UtcNow;

        var (dueLotteries, _) = await _postLotteryRepository.QueryPageAsync(
            lottery => !lottery.IsDeleted &&
                       !lottery.IsDrawn &&
                       lottery.DrawTime != null &&
                       lottery.DrawTime <= now,
            1,
            safeBatchSize,
            lottery => lottery.DrawTime,
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
        foreach (var postId in duePostIds)
        {
            try
            {
                await _postLotteryService.AutoDrawByPostIdAsync(postId);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PostLotteryJob] 自动开奖失败：PostId={PostId}", postId);
            }
        }

        return successCount;
    }
}
