using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// 系统配置服务接口
/// </summary>
public interface ISystemConfigService
{
    Task<List<SystemConfigVo>> GetSystemConfigsAsync();

    Task<List<string>> GetConfigCategoriesAsync();

    Task<SystemConfigVo?> GetConfigByIdAsync(long id);

    Task<SystemConfigVo> CreateConfigAsync(CreateSystemConfigDto request);

    Task<SystemConfigVo?> UpdateConfigAsync(long id, UpdateSystemConfigDto request, SystemConfigChangeContext? context = null);

    Task<SystemConfigVo?> RestoreConfigDefaultAsync(long id, RestoreSystemConfigDefaultDto? request = null, SystemConfigChangeContext? context = null);

    Task<bool> DeleteConfigAsync(long id, SystemConfigChangeContext? context = null);

    Task<List<SystemConfigChangeLogVo>?> GetConfigChangeLogsAsync(long id, int take = 20);

    Task<PublicSiteSettingsVo> GetPublicSiteSettingsAsync();
}
