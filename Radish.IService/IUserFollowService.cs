using Radish.IService.Base;
using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>用户关系链服务接口</summary>
public interface IUserFollowService : IBaseService<UserFollow, UserFollowVo>
{
    /// <summary>关注用户</summary>
    Task<bool> FollowAsync(long followerUserId, long targetUserId, long tenantId, string? operatorName);

    /// <summary>取消关注用户</summary>
    Task<bool> UnfollowAsync(long followerUserId, long targetUserId, string? operatorName);

    /// <summary>获取关注状态</summary>
    Task<UserFollowStatusVo> GetFollowStatusAsync(long currentUserId, long targetUserId);

    /// <summary>分页获取我的粉丝列表</summary>
    Task<VoPagedResult<UserFollowUserVo>> GetMyFollowersAsync(long userId, int pageIndex, int pageSize);

    /// <summary>分页获取我的关注列表</summary>
    Task<VoPagedResult<UserFollowUserVo>> GetMyFollowingAsync(long userId, int pageIndex, int pageSize);

    /// <summary>分页获取我的关注动态流</summary>
    Task<VoPagedResult<PostVo>> GetMyFollowingFeedAsync(long userId, int pageIndex, int pageSize);

    /// <summary>分页获取我的分发流（推荐/热门/最新）</summary>
    Task<VoPagedResult<PostVo>> GetMyDistributionFeedAsync(long userId, string streamType, int pageIndex, int pageSize);

    /// <summary>获取我的关系链汇总</summary>
    Task<UserFollowSummaryVo> GetMyFollowSummaryAsync(long userId);
}
