using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>一对一私聊状态变更结果</summary>
public sealed record DirectConversationMutationResult(
    DirectConversationVo Conversation,
    bool Changed);

/// <summary>一对一私聊生命周期服务</summary>
public interface IDirectConversationService
{
    Task<DirectConversationMutationResult> GetOrCreateAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        string operatorName);

    Task<DirectConversationMutationResult> AcceptAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName);

    Task<DirectConversationMutationResult> DeclineAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName);

    Task<DirectConversationMutationResult> BlockAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName);

    Task<DirectConversationMutationResult> UnblockAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName);

    Task<DirectConversationMutationResult> SetArchivedAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        bool archived,
        string operatorName);

    /// <summary>批量装配当前用户视角下的一对一会话权威摘要。</summary>
    Task<IReadOnlyDictionary<long, DirectConversationVo>> GetChannelSummariesAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> channelIds);
}
