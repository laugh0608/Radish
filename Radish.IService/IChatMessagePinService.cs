using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IChatMessagePinService
{
    Task<ChatMessagePinStateVo> GetStateAsync(long tenantId, long userId, long channelId);

    Task<ChatMessagePinMutationVo> SetAsync(
        long tenantId,
        long userId,
        string userName,
        bool canManageChannel,
        SetChatMessagePinDto request);
}
