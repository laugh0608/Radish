using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>帖子抽奖服务接口</summary>
public interface IPostLotteryService
{
    /// <summary>按帖子获取抽奖详情</summary>
    Task<LotteryResultVo> GetByPostIdAsync(long postId, long? viewerUserId = null);

    /// <summary>手动开奖</summary>
    Task<PostLotteryVo> DrawAsync(long postId, long userId, string userName);
}
