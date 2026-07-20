using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface IChatMessageReactionService
{
    Task<List<ChatMessageReactionStateVo>> GetStatesAsync(
        long tenantId,
        long userId,
        GetChatMessageReactionStatesDto request);

    Task<ChatMessageReactionMutationVo> SetAsync(
        long tenantId,
        long userId,
        string userName,
        SetChatMessageReactionDto request);
}
