using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;

namespace Radish.Service;

/// <summary>聊天室统一频道访问策略</summary>
public sealed class ChatChannelAccessService : IChatChannelAccessService
{
    private readonly IBaseRepository<Channel> _channelRepository;
    private readonly IBaseRepository<ChannelMember> _memberRepository;
    private readonly IBaseRepository<DirectConversation> _directConversationRepository;
    private readonly IChannelMessageRepository _messageRepository;

    public ChatChannelAccessService(
        IBaseRepository<Channel> channelRepository,
        IBaseRepository<ChannelMember> memberRepository,
        IBaseRepository<DirectConversation> directConversationRepository,
        IChannelMessageRepository messageRepository)
    {
        _channelRepository = channelRepository;
        _memberRepository = memberRepository;
        _directConversationRepository = directConversationRepository;
        _messageRepository = messageRepository;
    }

    public async Task<ChatChannelAccessResult> GetAccessAsync(
        long tenantId,
        long userId,
        long channelId,
        bool canManageChannel = false)
    {
        if (userId <= 0 || channelId <= 0)
        {
            return ChatChannelAccessResult.Unavailable;
        }

        var channel = await _channelRepository.QueryFirstAsync(candidate =>
            candidate.Id == channelId && candidate.IsEnabled && !candidate.IsDeleted);
        if (channel == null || !IsTenantVisible(channel.TenantId, tenantId))
        {
            return ChatChannelAccessResult.Unavailable;
        }

        var member = await _memberRepository.QueryFirstAsync(candidate =>
            candidate.ChannelId == channelId && candidate.UserId == userId && !candidate.IsDeleted);

        if (channel.Type == ChannelType.Public)
        {
            return new ChatChannelAccessResult(true, channel.Type, true, true, true, true, false);
        }

        if (channel.Type == ChannelType.Announcement)
        {
            var canSend = canManageChannel || member?.Role is MemberRole.Moderator or MemberRole.Owner;
            return new ChatChannelAccessResult(true, channel.Type, true, canSend, true, true, false);
        }

        if (member == null)
        {
            return new ChatChannelAccessResult(true, channel.Type, false, false, false, false, false);
        }

        var directConversation = await _directConversationRepository.QueryFirstAsync(candidate =>
            candidate.ChannelId == channelId && !candidate.IsDeleted);
        if (directConversation == null)
        {
            // 既有 Private 频道没有一对一关系元数据时，继续按私有群组成员语义处理。
            return new ChatChannelAccessResult(true, channel.Type, true, true, true, true, false);
        }

        var isParticipant = directConversation.ParticipantLowUserId == userId ||
                            directConversation.ParticipantHighUserId == userId;
        if (!isParticipant || !IsTenantVisible(directConversation.TenantId, tenantId))
        {
            return new ChatChannelAccessResult(true, channel.Type, false, false, false, false, true);
        }

        var isAccepted = directConversation.RequestStatus == DirectConversationRequestStatus.Accepted;
        var isBlocked = directConversation.BlockedByUserId.HasValue;
        return new ChatChannelAccessResult(
            true,
            channel.Type,
            true,
            isAccepted && !isBlocked,
            true,
            isAccepted && !isBlocked,
            true);
    }

    public async Task<bool> CanAccessMessageAttachmentAsync(long tenantId, long userId, long messageId)
    {
        if (userId <= 0 || messageId <= 0)
        {
            return false;
        }

        var message = await _messageRepository.QueryFirstAsync(candidate =>
            candidate.Id == messageId && !candidate.IsDeleted);
        if (message == null || !IsTenantVisible(message.TenantId, tenantId))
        {
            return false;
        }

        var access = await GetAccessAsync(tenantId, userId, message.ChannelId);
        return access.CanView;
    }

    private static bool IsTenantVisible(long resourceTenantId, long requestTenantId)
    {
        return resourceTenantId == 0 || resourceTenantId == requestTenantId;
    }
}
