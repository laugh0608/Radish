using AutoMapper;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model.Models;
using Radish.Model.ViewModels;
using Radish.Service.Base;

namespace Radish.Service;

/// <summary>
/// 用户时间偏好服务实现
/// </summary>
public class UserTimePreferenceService : BaseService<UserTimePreference, UserTimePreferenceVo>, IUserTimePreferenceService
{
    private readonly IBaseRepository<UserTimePreference> _repository;

    public UserTimePreferenceService(
        IMapper mapper,
        IBaseRepository<UserTimePreference> baseRepository) : base(mapper, baseRepository)
    {
        _repository = baseRepository;
    }

    public async Task<UserTimePreferenceVo?> GetByUserIdAsync(long userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        return await QueryFirstAsync(x => x.UserId == userId);
    }

    public async Task<UserTimePreferenceVo> UpsertAsync(long userId, long tenantId, string timeZoneId, string operatorName)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("用户 ID 非法", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("时区不能为空", nameof(timeZoneId));
        }

        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();
        var existing = await _repository.QueryFirstAsync(x => x.UserId == userId);

        if (existing == null)
        {
            var entity = new UserTimePreference
            {
                UserId = userId,
                TenantId = tenantId,
                TimeZoneId = timeZoneId,
                CreateBy = normalizedOperator,
                ModifyBy = normalizedOperator,
                ModifyTime = DateTime.UtcNow
            };

            var id = await _repository.AddAsync(entity);
            entity.Id = id;
            return Mapper.Map<UserTimePreferenceVo>(entity);
        }

        existing.TimeZoneId = timeZoneId;
        existing.TenantId = tenantId;
        existing.ModifyBy = normalizedOperator;
        existing.ModifyTime = DateTime.UtcNow;

        await _repository.UpdateAsync(existing);
        return Mapper.Map<UserTimePreferenceVo>(existing);
    }
}
