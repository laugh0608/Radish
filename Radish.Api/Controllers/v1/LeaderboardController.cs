using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers.v1;

/// <summary>
/// 排行榜控制器
/// </summary>
/// <remarks>
/// 提供多类型排行榜的统一查询接口
/// </remarks>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[ApiVersion(1)]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public LeaderboardController(
        ILeaderboardService leaderboardService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _leaderboardService = leaderboardService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>
    /// 获取排行榜数据
    /// </summary>
    /// <param name="type">排行榜类型</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 50，最大 100）</param>
    /// <returns>分页的排行榜数据</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<PageModel<UnifiedLeaderboardItemVo>>> GetLeaderboard(
        [FromQuery] int type = (int)LeaderboardType.Experience,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50)
    {
        var leaderboardType = (LeaderboardType)type;
        if (!LeaderboardPublicPolicy.IsPublicType(leaderboardType))
        {
            return MessageModel<PageModel<UnifiedLeaderboardItemVo>>.Message(
                false,
                "该排行榜类型不提供公开访问",
                default,
                LeaderboardErrorCodes.TypeUnavailable,
                LeaderboardErrorCodes.ResolveMessageKey(LeaderboardErrorCodes.TypeUnavailable));
        }

        var currentUserId = GetCurrentUserId();
        var result = await _leaderboardService.GetLeaderboardAsync(
            leaderboardType,
            pageIndex,
            pageSize,
            currentUserId > 0 ? currentUserId : null);

        return MessageModel<PageModel<UnifiedLeaderboardItemVo>>.Success("查询成功", result);
    }

    /// <summary>
    /// 获取当前用户在指定排行榜中的排名
    /// </summary>
    /// <param name="type">排行榜类型</param>
    /// <returns>用户排名（0 表示未上榜）</returns>
    [HttpGet]
    public async Task<MessageModel<int>> GetMyRank(
        [FromQuery] int type = (int)LeaderboardType.Experience)
    {
        var leaderboardType = (LeaderboardType)type;
        if (!LeaderboardPublicPolicy.IsPublicType(leaderboardType))
        {
            return MessageModel<int>.Message(
                false,
                "该排行榜类型不提供公开访问",
                0,
                LeaderboardErrorCodes.TypeUnavailable,
                LeaderboardErrorCodes.ResolveMessageKey(LeaderboardErrorCodes.TypeUnavailable));
        }

        if (!LeaderboardPublicPolicy.SupportsUserRank(leaderboardType))
        {
            return MessageModel<int>.Message(
                false,
                "该排行榜类型不支持用户个人排名",
                0,
                LeaderboardErrorCodes.UserRankUnavailable,
                LeaderboardErrorCodes.ResolveMessageKey(LeaderboardErrorCodes.UserRankUnavailable));
        }

        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<int>.Message(false, "未登录", 0);
        }

        var rank = await _leaderboardService.GetUserRankAsync(leaderboardType, userId);
        return MessageModel<int>.Success("查询成功", rank);
    }

    /// <summary>
    /// 获取所有排行榜类型
    /// </summary>
    /// <returns>排行榜类型列表</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<MessageModel<List<LeaderboardTypeVo>>> GetTypes()
    {
        var result = await _leaderboardService.GetLeaderboardTypesAsync();
        return MessageModel<List<LeaderboardTypeVo>>.Success("查询成功", result);
    }

    #region 私有辅助方法

    /// <summary>
    /// 获取当前用户 ID
    /// </summary>
    private long GetCurrentUserId() => Current.UserId;

    #endregion
}
