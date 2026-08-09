using Radish.IRepository;
using Radish.Model;

namespace Radish.Repository;

/// <summary>
/// 系统设置变更审计仓储
/// </summary>
public class SystemConfigChangeLogRepository : ISystemConfigChangeLogRepository
{
    public Task<List<SystemConfigChangeLogRecord>> GetByKeyAsync(string key, int take)
    {
        return SystemConfigStorageCoordinator.ReadAsync(state =>
            state.ChangeLogs
                .Where(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreateTime)
                .ThenByDescending(item => item.Id)
                .Take(Math.Clamp(take, 1, 100))
                .Select(SystemConfigStorageCoordinator.CloneChangeLog)
                .ToList());
    }
}
