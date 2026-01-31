using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.IService;

/// <summary>排行榜服务接口</summary>
/// <remarks>
/// 提供多类型排行榜的统一查询接口
/// </remarks>
public interface ILeaderboardService
{
    /// <summary>
    /// 获取排行榜数据
    /// </summary>
    /// <param name="type">排行榜类型</param>
    /// <param name="pageIndex">页码（从 1 开始）</param>
    /// <param name="pageSize">每页数量（默认 50，最大 100）</param>
    /// <param name="currentUserId">当前用户 ID（可选，用于标记当前用户）</param>
    /// <returns>分页的排行榜数据</returns>
    Task<PageModel<UnifiedLeaderboardItemVo>> GetLeaderboardAsync(
        LeaderboardType type,
        int pageIndex,
        int pageSize = 50,
        long? currentUserId = null);

    /// <summary>
    /// 获取用户在指定排行榜中的排名
    /// </summary>
    /// <param name="type">排行榜类型</param>
    /// <param name="userId">用户 ID</param>
    /// <returns>用户排名（0 表示未上榜）</returns>
    Task<int> GetUserRankAsync(LeaderboardType type, long userId);

    /// <summary>
    /// 获取所有排行榜类型
    /// </summary>
    /// <returns>排行榜类型列表</returns>
    Task<List<LeaderboardTypeVo>> GetLeaderboardTypesAsync();
}
