using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
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
[Authorize(Policy = "Client")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LeaderboardController(
        ILeaderboardService leaderboardService,
        IHttpContextAccessor httpContextAccessor)
    {
        _leaderboardService = leaderboardService;
        _httpContextAccessor = httpContextAccessor;
    }

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
        [FromQuery] LeaderboardType type = LeaderboardType.Experience,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _leaderboardService.GetLeaderboardAsync(
            type,
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
    public async Task<MessageModel<int>> GetMyRank([FromQuery] LeaderboardType type = LeaderboardType.Experience)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return MessageModel<int>.Message(false, "未登录", 0);
        }

        var rank = await _leaderboardService.GetUserRankAsync(type, userId);
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
    private long GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return 0;
        }

        // 尝试从 sub claim 获取（OIDC 标准）
        var subClaim = httpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(subClaim) && long.TryParse(subClaim, out var userId))
        {
            return userId;
        }

        // 尝试从 jti claim 获取（兼容）
        var jtiClaim = httpContext.User.FindFirst("jti")?.Value;
        if (!string.IsNullOrEmpty(jtiClaim) && long.TryParse(jtiClaim, out userId))
        {
            return userId;
        }

        return 0;
    }

    #endregion
}
