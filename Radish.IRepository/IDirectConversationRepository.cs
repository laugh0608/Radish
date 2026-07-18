using Radish.IRepository.Base;
using Radish.Model;

namespace Radish.IRepository;

/// <summary>一对一私聊会话创建结果</summary>
public sealed record DirectConversationCreateResult(
    DirectConversation Conversation,
    bool WasCreated);

/// <summary>一对一私聊仓储</summary>
public interface IDirectConversationRepository : IBaseRepository<DirectConversation>
{
    /// <summary>在 Chat 库单事务内幂等创建频道、两名成员和一对一关系。</summary>
    Task<DirectConversationCreateResult> CreateOrGetAsync(
        DirectConversation conversation,
        Channel channel,
        ChannelMember lowParticipant,
        ChannelMember highParticipant);
}
