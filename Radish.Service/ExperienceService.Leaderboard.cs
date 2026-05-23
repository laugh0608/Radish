using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;
using SqlSugar;

namespace Radish.Service;

public partial class ExperienceService
{
    #region 排行榜

    /// <summary>
    /// 获取等级排行榜
    /// </summary>
    public async Task<PageModel<LeaderboardItemVo>> GetLeaderboardAsync(
        int pageIndex,
        int pageSize = 50,
        long? currentUserId = null)
    {
        try
        {
            // 限制每页数量
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 50;
            if (pageIndex < 1) pageIndex = 1;
            var now = DateTime.Now;

            // 查询排行榜（按 TotalExp 降序，CurrentLevel 降序）
            var (pagedData, totalCount) = await _userExpRepository.QueryPageAsync(
                whereExpression: e =>
                    (!e.ExpFrozen || (e.FrozenUntil != null && e.FrozenUntil <= now)) &&
                    e.UserId > 0 &&
                    !e.IsDeleted,
                pageIndex: pageIndex,
                pageSize: pageSize,
                orderByExpression: e => e.TotalExp,
                orderByType: OrderByType.Desc
            );

            // 获取用户信息
            var userIds = pagedData.Select(e => e.UserId).ToList();
            var users = await _userRepository.QueryAsync(u => userIds.Contains(u.Id));
            var userDict = users.ToDictionary(u => u.Id);

            // 获取等级配置
            var levels = pagedData.Select(e => e.CurrentLevel).Distinct().ToList();
            var levelConfigs = await _levelConfigRepository.QueryAsync(l => levels.Contains(l.Level));
            var levelConfigDict = levelConfigs.ToDictionary(l => l.Level);

            // 计算起始排名
            var startRank = (pageIndex - 1) * pageSize + 1;

            // 映射为 LeaderboardItemVo
            var leaderboard = new List<LeaderboardItemVo>();
            var rank = startRank;
            for (int i = 0; i < pagedData.Count; i++)
            {
                var exp = pagedData[i];
                if (!userDict.TryGetValue(exp.UserId, out var user))
                {
                    Log.Warning("排行榜跳过不存在的用户：userId={UserId}", exp.UserId);
                    continue;
                }

                var item = new LeaderboardItemVo
                {
                    VoRank = rank,
                    VoUserId = exp.UserId,
                    VoUserName = user.UserName,
                    VoCurrentLevel = exp.CurrentLevel,
                    VoCurrentLevelName = levelConfigDict.ContainsKey(exp.CurrentLevel)
                        ? levelConfigDict[exp.CurrentLevel].LevelName
                        : $"Lv.{exp.CurrentLevel}",
                    VoThemeColor = levelConfigDict.ContainsKey(exp.CurrentLevel)
                        ? levelConfigDict[exp.CurrentLevel].ThemeColor
                        : "#9E9E9E",
                    VoTotalExp = exp.TotalExp,
                    VoIsCurrentUser = currentUserId.HasValue && exp.UserId == currentUserId.Value
                };

                leaderboard.Add(item);
                rank++;
            }

            var pageCount = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new PageModel<LeaderboardItemVo>
            {
                Page = pageIndex,
                PageSize = pageSize,
                DataCount = totalCount,
                PageCount = pageCount,
                Data = leaderboard
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取排行榜失败");
            throw;
        }
    }

    /// <summary>
    /// 获取用户排名
    /// </summary>
    public async Task<int> GetUserRankAsync(long userId)
    {
        try
        {
            if (userId <= 0)
            {
                return 0;
            }

            var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
            if (userExp == null)
            {
                return 0;
            }

            userExp = await NormalizeFreezeStateAsync(userExp);
            if (IsFreezeActive(userExp, DateTime.Now))
            {
                return 0; // 未上榜或仍处于冻结中
            }

            var now = DateTime.Now;

            // 统计比该用户经验值高的用户数量
            var higherCount = await _userExpRepository.QueryCountAsync(
                e =>
                    (!e.ExpFrozen || (e.FrozenUntil != null && e.FrozenUntil <= now)) &&
                    e.UserId > 0 &&
                    e.TotalExp > userExp.TotalExp &&
                    !e.IsDeleted
            );

            return (int)higherCount + 1; // 排名 = 比自己高的数量 + 1
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 排名失败", userId);
            return 0;
        }
    }

    #endregion
}
