using Radish.Model;

namespace Radish.IRepository;

/// <summary>
/// 系统设置变更审计仓储接口
/// </summary>
public interface ISystemConfigChangeLogRepository
{
    Task<List<SystemConfigChangeLogRecord>> GetByKeyAsync(string key, int take);

    Task<SystemConfigChangeLogRecord> CreateAsync(SystemConfigChangeLogRecord record);
}
