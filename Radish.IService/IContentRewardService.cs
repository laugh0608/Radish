using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IContentRewardService
{
    Task<ContentRewardMutationVo> CreateAsync(
        CreateContentRewardDto request,
        long currentUserId,
        string currentUserName,
        long tenantId);

    Task<ContentRewardTargetPageVo> GetTargetRewardsAsync(
        string targetType,
        long targetId,
        long currentUserId,
        long tenantId,
        int pageIndex,
        int pageSize);

    Task<IReadOnlyList<ContentRewardTargetStateVo>> GetTargetStatesAsync(
        GetContentRewardTargetStatesDto request,
        long currentUserId,
        long tenantId);
}
