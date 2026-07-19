using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>Chat 历史消息服务端权威检索。</summary>
public interface IChatMessageSearchService
{
    Task<ChannelMessageSearchPageVo> SearchAsync(
        long tenantId,
        long userId,
        SearchChannelMessagesDto request);
}
