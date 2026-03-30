using Radish.Model;

namespace Radish.IRepository;

/// <summary>
/// 系统配置仓储接口
/// </summary>
public interface ISystemConfigRepository
{
    Task<List<SystemConfigRecord>> GetAllAsync();

    Task<SystemConfigRecord?> GetByIdAsync(long id);

    Task<SystemConfigRecord?> GetByKeyAsync(string key);

    Task<SystemConfigRecord> CreateAsync(SystemConfigRecord record);

    Task<SystemConfigRecord?> UpdateAsync(SystemConfigRecord record);

    Task<bool> DeleteAsync(long id);
}
