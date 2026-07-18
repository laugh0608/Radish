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
    private readonly IBaseRepository<User> _userRepository;
    private readonly IChannelMessageRepository _messageRepository;

    public ChatChannelAccessService(
        IBaseRepository<Channel> channelRepository,
        IBaseRepository<ChannelMember> memberRepository,
        IBaseRepository<DirectConversation> directConversationRepository,
        IBaseRepository<User> userRepository,
        IChannelMessageRepository messageRepository)
    {
        _channelRepository = channelRepository;
        _memberRepository = memberRepository;
        _directConversationRepository = directConversationRepository;
        _userRepository = userRepository;
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
            var announcementCanSend = canManageChannel || member?.Role is MemberRole.Moderator or MemberRole.Owner;
            return new ChatChannelAccessResult(true, channel.Type, true, announcementCanSend, true, true, false);
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

        var peerUserId = directConversation.ParticipantLowUserId == userId
            ? directConversation.ParticipantHighUserId
            : directConversation.ParticipantLowUserId;
        var peerUser = await _userRepository.QueryFirstAsync(candidate =>
            candidate.Id == peerUserId &&
            candidate.TenantId == directConversation.TenantId &&
            candidate.IsEnable &&
            !candidate.IsDeleted);
        var isPeerAvailable = peerUser != null;
        var hasMessages = await _messageRepository.QueryExistsAsync(candidate =>
            candidate.ChannelId == channelId && !candidate.IsDeleted);
        var isRequester = directConversation.RequestedByUserId == userId;
        var isBlocked = directConversation.BlockedByUserId.HasValue;
        var canView = directConversation.RequestStatus switch
        {
            DirectConversationRequestStatus.Pending => isRequester || hasMessages,
            DirectConversationRequestStatus.Accepted => true,
            DirectConversationRequestStatus.Declined => true,
            _ => false
        };
        var canSend = directConversation.RequestStatus switch
        {
            DirectConversationRequestStatus.Pending => isRequester && !hasMessages,
            DirectConversationRequestStatus.Accepted => true,
            DirectConversationRequestStatus.Declined => false,
            _ => false
        };
        canSend = canSend && !isBlocked && isPeerAvailable;
        return new ChatChannelAccessResult(
            true,
            channel.Type,
            canView,
            canSend,
            canView,
            directConversation.RequestStatus == DirectConversationRequestStatus.Accepted && !isBlocked,
            true,
            directConversation.RequestStatus,
            directConversation.RequestedByUserId,
            directConversation.BlockedByUserId,
            peerUserId,
            directConversation.Id,
            hasMessages,
            isPeerAvailable);
    }

    public async Task<bool> CanAccessChatAttachmentAsync(
        long tenantId,
        long userId,
        long attachmentId,
        long? messageId = null)
    {
        if (userId <= 0 || attachmentId <= 0 || messageId is <= 0)
        {
            return false;
        }

        var message = await _messageRepository.QueryFirstAsync(candidate =>
            candidate.AttachmentId == attachmentId &&
            (!messageId.HasValue || candidate.Id == messageId.Value) &&
            !candidate.IsDeleted);
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
