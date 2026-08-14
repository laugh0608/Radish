using Radish.IRepository;
using Radish.Model;

namespace Radish.Repository;

/// <summary>
/// 系统配置仓储
/// </summary>
public class SystemConfigRepository : ISystemConfigRepository
{
    private readonly TimeProvider _timeProvider;

    public SystemConfigRepository(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task<List<SystemConfigRecord>> GetAllAsync()
    {
        return SystemConfigStorageCoordinator.ReadAsync(state =>
            state.Configs.Select(SystemConfigStorageCoordinator.CloneConfig).ToList());
    }

    public Task<SystemConfigRecord?> GetByIdAsync(long id)
    {
        return SystemConfigStorageCoordinator.ReadAsync(state =>
        {
            var record = state.Configs.FirstOrDefault(item => item.Id == id);
            return record == null ? null : SystemConfigStorageCoordinator.CloneConfig(record);
        });
    }

    public Task<SystemConfigRecord?> GetByKeyAsync(string key)
    {
        return SystemConfigStorageCoordinator.ReadAsync(state =>
        {
            var record = state.Configs
                .Where(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.ModifyTime ?? item.CreateTime)
                .FirstOrDefault();
            return record == null ? null : SystemConfigStorageCoordinator.CloneConfig(record);
        });
    }

    public Task<SystemConfigMutationResult> ApplyMutationAsync(SystemConfigMutation mutation)
    {
        return SystemConfigStorageCoordinator.ApplyMutationAsync(
            mutation,
            _timeProvider.GetLocalNow().DateTime);
    }
}
