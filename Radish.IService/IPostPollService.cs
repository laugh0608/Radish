using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>帖子投票服务接口</summary>
public interface IPostPollService
{
    /// <summary>
    /// 按帖子获取投票详情
    /// </summary>
    /// <param name="postId">帖子 Id</param>
    /// <param name="viewerUserId">查看者用户 Id（可空）</param>
    /// <returns>投票结果</returns>
    Task<PollVoteResultVo> GetByPostIdAsync(long postId, long? viewerUserId = null);

    /// <summary>
    /// 提交投票
    /// </summary>
    /// <param name="userId">当前用户 Id</param>
    /// <param name="userName">当前用户名</param>
    /// <param name="request">投票请求</param>
    /// <returns>最新投票详情</returns>
    Task<PostPollVo> VoteAsync(long userId, string userName, VotePollDto request);
}
