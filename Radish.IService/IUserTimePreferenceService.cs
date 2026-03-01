using Radish.IService.Base;
using Radish.Model.Models;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// 用户时间偏好服务
/// </summary>
public interface IUserTimePreferenceService : IBaseService<UserTimePreference, UserTimePreferenceVo>
{
    /// <summary>
    /// 根据用户 ID 获取时间偏好
    /// </summary>
    Task<UserTimePreferenceVo?> GetByUserIdAsync(long userId);

    /// <summary>
    /// 更新或创建用户时间偏好
    /// </summary>
    Task<UserTimePreferenceVo> UpsertAsync(long userId, long tenantId, string timeZoneId, string operatorName);
}
