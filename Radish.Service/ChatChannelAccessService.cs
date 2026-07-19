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
            return BuildAccessResult(channel, member, null, userId, canManageChannel, false, true);
        }

        if (channel.Type == ChannelType.Announcement)
        {
            return BuildAccessResult(channel, member, null, userId, canManageChannel, false, true);
        }

        var directConversation = member == null
            ? null
            : await _directConversationRepository.QueryFirstAsync(candidate =>
                candidate.ChannelId == channelId && !candidate.IsDeleted);
        var hasMessages = directConversation != null && await _messageRepository.QueryExistsAsync(candidate =>
            candidate.ChannelId == channelId && !candidate.IsDeleted);
        var peerAvailable = true;
        if (directConversation != null)
        {
            var peerUserId = directConversation.ParticipantLowUserId == userId
                ? directConversation.ParticipantHighUserId
                : directConversation.ParticipantLowUserId;
            peerAvailable = await _userRepository.QueryFirstAsync(candidate =>
                candidate.Id == peerUserId &&
                candidate.TenantId == directConversation.TenantId &&
                candidate.IsEnable &&
                !candidate.IsDeleted) != null;
        }

        return BuildAccessResult(
            channel,
            member,
            directConversation,
            userId,
            canManageChannel,
            hasMessages,
            peerAvailable,
            tenantId);
    }

    public async Task<IReadOnlyList<ReadableChatChannelSnapshotItem>> GetReadableChannelSnapshotAsync(
        long tenantId,
        long userId)
    {
        if (userId <= 0)
        {
            return [];
        }

        var channels = await _channelRepository.QueryAsync(channel =>
            channel.IsEnabled && !channel.IsDeleted &&
            (channel.TenantId == tenantId || channel.TenantId == 0));
        if (channels.Count == 0)
        {
            return [];
        }

        var channelIds = channels.Select(channel => channel.Id).Distinct().ToList();
        var members = await _memberRepository.QueryAsync(member =>
            channelIds.Contains(member.ChannelId) && member.UserId == userId && !member.IsDeleted);
        var memberMap = members.ToDictionary(member => member.ChannelId);
        var directConversations = await _directConversationRepository.QueryAsync(conversation =>
            channelIds.Contains(conversation.ChannelId) && !conversation.IsDeleted);
        var directMap = directConversations
            .GroupBy(conversation => conversation.ChannelId)
            .ToDictionary(group => group.Key, group => group.First());
        var directChannelIds = directConversations.Select(conversation => conversation.ChannelId).Distinct().ToList();
        var messageChannelIds = directChannelIds.Count == 0
            ? []
            : await _messageRepository.QueryDistinctAsync(
                message => message.ChannelId,
                message => directChannelIds.Contains(message.ChannelId) && !message.IsDeleted);
        var channelIdsWithMessages = messageChannelIds.ToHashSet();
        var peerUserIds = directConversations
            .Where(conversation =>
                conversation.ParticipantLowUserId == userId || conversation.ParticipantHighUserId == userId)
            .Select(conversation => conversation.ParticipantLowUserId == userId
                ? conversation.ParticipantHighUserId
                : conversation.ParticipantLowUserId)
            .Distinct()
            .ToList();
        var activePeerUserIds = peerUserIds.Count == 0
            ? []
            : (await _userRepository.QueryAsync(user =>
                peerUserIds.Contains(user.Id) &&
                user.TenantId == tenantId &&
                user.IsEnable &&
                !user.IsDeleted))
            .Select(user => user.Id)
            .ToHashSet();
        var result = new List<ReadableChatChannelSnapshotItem>(channels.Count);

        foreach (var channel in channels)
        {
            memberMap.TryGetValue(channel.Id, out var member);
            directMap.TryGetValue(channel.Id, out var conversation);
            var peerAvailable = conversation == null ||
                                (conversation.ParticipantLowUserId == userId
                                    ? activePeerUserIds.Contains(conversation.ParticipantHighUserId)
                                    : activePeerUserIds.Contains(conversation.ParticipantLowUserId));
            var access = BuildAccessResult(
                channel,
                member,
                conversation,
                userId,
                false,
                channelIdsWithMessages.Contains(channel.Id),
                peerAvailable,
                tenantId);
            if (access.CanView)
            {
                result.Add(new ReadableChatChannelSnapshotItem(
                    channel.Id,
                    channel.Name,
                    channel.IconEmoji,
                    channel.Type,
                    access));
            }
        }

        return result;
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

    private static ChatChannelAccessResult BuildAccessResult(
        Channel channel,
        ChannelMember? member,
        DirectConversation? directConversation,
        long userId,
        bool canManageChannel,
        bool hasMessages,
        bool peerAvailable,
        long? tenantId = null)
    {
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
            return new ChatChannelAccessResult(true, channel.Type, false, false, false, false, directConversation != null);
        }

        if (directConversation == null)
        {
            return new ChatChannelAccessResult(true, channel.Type, true, true, true, true, false);
        }

        var isParticipant = directConversation.ParticipantLowUserId == userId ||
                            directConversation.ParticipantHighUserId == userId;
        if (!isParticipant || tenantId.HasValue && !IsTenantVisible(directConversation.TenantId, tenantId.Value))
        {
            return new ChatChannelAccessResult(true, channel.Type, false, false, false, false, true);
        }

        var peerUserId = directConversation.ParticipantLowUserId == userId
            ? directConversation.ParticipantHighUserId
            : directConversation.ParticipantLowUserId;
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
        canSend = canSend && !isBlocked && peerAvailable;
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
            peerAvailable);
    }
}
