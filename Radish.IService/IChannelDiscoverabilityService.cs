using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IChannelDiscoverabilityService
{
    Task<PageModel<ChannelDiscoverabilityVo>> GetPageAsync(
        long tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        ChannelDiscoverVisibility? discoverVisibility,
        bool? isEnabled,
        bool includeDeleted);

    Task<ChannelDiscoverabilityVo> GetByIdAsync(long tenantId, long channelId);

    Task<PageModel<ChannelDiscoverVisibilityEventVo>> GetHistoryAsync(
        long tenantId,
        long channelId,
        int pageIndex,
        int pageSize);

    Task<ChannelDiscoverVisibilityMutationVo> UpdateVisibilityAsync(
        long tenantId,
        long channelId,
        long actorUserId,
        string actorName,
        UpdateChannelDiscoverVisibilityDto request);
}
