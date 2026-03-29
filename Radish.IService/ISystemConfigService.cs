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

    Task<SystemConfigVo?> UpdateConfigAsync(long id, UpdateSystemConfigDto request);

    Task<bool> DeleteConfigAsync(long id);

    Task<PublicSiteSettingsVo> GetPublicSiteSettingsAsync();
}
