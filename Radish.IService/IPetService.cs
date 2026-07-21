using Radish.IService.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>电子宠物服务接口</summary>
public interface IPetService : IBaseService<PetProfile, PetProfileVo>
{
    Task<PetProfileVo?> GetMyPetAsync(long userId);

    Task<PetPublicCardVo?> GetPublicCardAsync(long userId, long tenantId);

    Task<PetProfileVo> ClaimAsync(long userId, string operatorName, long tenantId, PetClaimDto request);

    Task<PetProfileVo> UpdateProfileAsync(long userId, string operatorName, UpdatePetProfileDto request);

    Task<PetCareResultVo> CareAsync(long userId, string operatorName, PetCareDto request);

    Task<VoPagedResult<PetStatLogVo>> GetMyLogsAsync(long userId, int pageIndex, int pageSize);
}
