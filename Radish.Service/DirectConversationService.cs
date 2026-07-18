using Microsoft.AspNetCore.Http;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Service;

/// <summary>一对一私聊生命周期服务</summary>
public sealed class DirectConversationService : IDirectConversationService
{
    private readonly IDirectConversationRepository _conversationRepository;
    private readonly IBaseRepository<ChannelMember> _memberRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<UserFollow> _userFollowRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

    public DirectConversationService(
        IDirectConversationRepository conversationRepository,
        IBaseRepository<ChannelMember> memberRepository,
        IBaseRepository<User> userRepository,
        IBaseRepository<UserFollow> userFollowRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentUrlResolver attachmentUrlResolver)
    {
        _conversationRepository = conversationRepository;
        _memberRepository = memberRepository;
        _userRepository = userRepository;
        _userFollowRepository = userFollowRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    public async Task<DirectConversationMutationResult> GetOrCreateAsync(
        long tenantId,
        long currentUserId,
        long targetUserId,
        string operatorName)
    {
        if (currentUserId <= 0 || targetUserId <= 0)
        {
            throw ValidationError("目标用户 Id 无效", "Chat.DirectTargetInvalid", "error.chat.direct_target_invalid");
        }

        if (currentUserId == targetUserId)
        {
            throw ValidationError("不能与自己建立私聊", "Chat.DirectSelfNotAllowed", "error.chat.direct_self_not_allowed");
        }

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var targetUser = await _userRepository.QueryFirstAsync(user =>
            user.Id == targetUserId &&
            user.TenantId == normalizedTenantId &&
            user.IsEnable &&
            !user.IsDeleted);
        if (targetUser == null)
        {
            throw NotFoundError("目标用户不存在或不可用", "Chat.DirectTargetUnavailable", "error.chat.direct_target_unavailable");
        }

        var participantLowUserId = Math.Min(currentUserId, targetUserId);
        var participantHighUserId = Math.Max(currentUserId, targetUserId);
        var isMutualFollow = await IsMutualFollowAsync(normalizedTenantId, currentUserId, targetUserId);
        var now = DateTime.UtcNow;
        var normalizedOperator = NormalizeOperator(operatorName);
        var channelId = SnowFlakeSingle.Instance.NextId();
        var conversation = new DirectConversation
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            ChannelId = channelId,
            ParticipantLowUserId = participantLowUserId,
            ParticipantHighUserId = participantHighUserId,
            RequestedByUserId = currentUserId,
            RequestStatus = isMutualFollow
                ? DirectConversationRequestStatus.Accepted
                : DirectConversationRequestStatus.Pending,
            AcceptedAt = isMutualFollow ? now : null,
            TenantId = normalizedTenantId,
            CreateTime = now,
            CreateBy = normalizedOperator,
            CreateId = currentUserId
        };
        var channel = new Channel
        {
            Id = channelId,
            Name = "Direct conversation",
            Slug = $"direct-{channelId}",
            Description = null,
            IconEmoji = "💬",
            Type = ChannelType.Private,
            IsEnabled = true,
            Sort = 0,
            TenantId = normalizedTenantId,
            CreateTime = now,
            CreateBy = normalizedOperator,
            CreateId = currentUserId
        };
        var lowParticipant = CreateMember(
            channelId,
            participantLowUserId,
            normalizedTenantId,
            normalizedOperator,
            currentUserId,
            now);
        var highParticipant = CreateMember(
            channelId,
            participantHighUserId,
            normalizedTenantId,
            normalizedOperator,
            currentUserId,
            now);

        var createResult = await _conversationRepository.CreateOrGetAsync(
            conversation,
            channel,
            lowParticipant,
            highParticipant);
        var summary = await GetRequiredSummaryAsync(normalizedTenantId, currentUserId, createResult.Conversation.ChannelId);
        summary.VoWasCreated = createResult.WasCreated;
        return new DirectConversationMutationResult(summary, createResult.WasCreated);
    }

    public async Task<DirectConversationMutationResult> AcceptAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName)
    {
        var conversation = await GetParticipantConversationAsync(tenantId, currentUserId, channelId);
        EnsureRequestReceiver(conversation, currentUserId);
        EnsureNotBlocked(conversation);
        if (conversation.RequestStatus == DirectConversationRequestStatus.Accepted)
        {
            return await UnchangedResultAsync(tenantId, currentUserId, channelId);
        }
        EnsureVisibleToRequestReceiver(conversation, currentUserId);

        var summary = await GetRequiredSummaryAsync(tenantId, currentUserId, channelId);
        if (!summary.VoIsPeerAvailable)
        {
            throw ConflictError("对方账号当前不可用", "Chat.DirectPeerUnavailable", "error.chat.direct_peer_unavailable");
        }

        var now = DateTime.UtcNow;
        var affected = await _conversationRepository.UpdateColumnsAsync(
            item => new DirectConversation
            {
                RequestStatus = DirectConversationRequestStatus.Accepted,
                AcceptedAt = now,
                DeclinedAt = null,
                ModifyTime = now,
                ModifyBy = NormalizeOperator(operatorName),
                ModifyId = currentUserId
            },
            item => item.Id == conversation.Id &&
                    item.BlockedByUserId == null &&
                    item.RequestedByUserId != currentUserId &&
                    item.RequestStatus != DirectConversationRequestStatus.Accepted &&
                    !item.IsDeleted);

        return await BuildMutationResultAsync(tenantId, currentUserId, channelId, affected > 0);
    }

    public async Task<DirectConversationMutationResult> DeclineAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName)
    {
        var conversation = await GetParticipantConversationAsync(tenantId, currentUserId, channelId);
        EnsureRequestReceiver(conversation, currentUserId);
        EnsureNotBlocked(conversation);
        if (conversation.RequestStatus == DirectConversationRequestStatus.Declined)
        {
            return await UnchangedResultAsync(tenantId, currentUserId, channelId);
        }
        EnsureVisibleToRequestReceiver(conversation, currentUserId);

        if (conversation.RequestStatus != DirectConversationRequestStatus.Pending)
        {
            throw ConflictError("当前会话状态不能拒绝", "Chat.DirectActionConflict", "error.chat.direct_action_conflict");
        }

        var now = DateTime.UtcNow;
        var affected = await _conversationRepository.UpdateColumnsAsync(
            item => new DirectConversation
            {
                RequestStatus = DirectConversationRequestStatus.Declined,
                DeclinedAt = now,
                AcceptedAt = null,
                ModifyTime = now,
                ModifyBy = NormalizeOperator(operatorName),
                ModifyId = currentUserId
            },
            item => item.Id == conversation.Id &&
                    item.BlockedByUserId == null &&
                    item.RequestedByUserId != currentUserId &&
                    item.RequestStatus == DirectConversationRequestStatus.Pending &&
                    !item.IsDeleted);

        return await BuildMutationResultAsync(tenantId, currentUserId, channelId, affected > 0);
    }

    public async Task<DirectConversationMutationResult> BlockAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName)
    {
        var conversation = await GetParticipantConversationAsync(tenantId, currentUserId, channelId);
        EnsureVisibleToRequestReceiver(conversation, currentUserId);
        if (conversation.BlockedByUserId == currentUserId)
        {
            return await UnchangedResultAsync(tenantId, currentUserId, channelId);
        }

        if (conversation.BlockedByUserId.HasValue)
        {
            throw ConflictError("会话已被对方阻断", "Chat.DirectBlockedByPeer", "error.chat.direct_blocked_by_peer");
        }

        var now = DateTime.UtcNow;
        var affected = await _conversationRepository.UpdateColumnsAsync(
            item => new DirectConversation
            {
                BlockedByUserId = currentUserId,
                BlockedAt = now,
                ModifyTime = now,
                ModifyBy = NormalizeOperator(operatorName),
                ModifyId = currentUserId
            },
            item => item.Id == conversation.Id && item.BlockedByUserId == null && !item.IsDeleted);

        return await BuildMutationResultAsync(tenantId, currentUserId, channelId, affected > 0);
    }

    public async Task<DirectConversationMutationResult> UnblockAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        string operatorName)
    {
        var conversation = await GetParticipantConversationAsync(tenantId, currentUserId, channelId);
        if (!conversation.BlockedByUserId.HasValue)
        {
            return await UnchangedResultAsync(tenantId, currentUserId, channelId);
        }

        if (conversation.BlockedByUserId != currentUserId)
        {
            throw ForbiddenError("只有执行阻断的一方可以解除", "Chat.DirectUnblockForbidden", "error.chat.direct_unblock_forbidden");
        }

        var now = DateTime.UtcNow;
        var affected = await _conversationRepository.UpdateColumnsAsync(
            item => new DirectConversation
            {
                BlockedByUserId = null,
                BlockedAt = null,
                ModifyTime = now,
                ModifyBy = NormalizeOperator(operatorName),
                ModifyId = currentUserId
            },
            item => item.Id == conversation.Id && item.BlockedByUserId == currentUserId && !item.IsDeleted);

        return await BuildMutationResultAsync(tenantId, currentUserId, channelId, affected > 0);
    }

    public async Task<DirectConversationMutationResult> SetArchivedAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        bool archived,
        string operatorName)
    {
        var conversation = await GetParticipantConversationAsync(tenantId, currentUserId, channelId);
        EnsureVisibleToRequestReceiver(conversation, currentUserId);
        var member = await _memberRepository.QueryFirstAsync(item =>
            item.ChannelId == channelId && item.UserId == currentUserId && !item.IsDeleted);
        if (member == null)
        {
            throw NotFoundError("会话不存在或无权访问", "Chat.ChannelUnavailable", "error.chat.channel_unavailable");
        }

        if (member.ArchivedAt.HasValue == archived)
        {
            return await UnchangedResultAsync(tenantId, currentUserId, channelId);
        }

        var now = DateTime.UtcNow;
        var affected = await _memberRepository.UpdateColumnsAsync(
            item => new ChannelMember
            {
                ArchivedAt = archived ? now : null,
                ModifyTime = now,
                ModifyBy = NormalizeOperator(operatorName),
                ModifyId = currentUserId
            },
            item => item.Id == member.Id &&
                    (archived ? item.ArchivedAt == null : item.ArchivedAt != null) &&
                    !item.IsDeleted);

        return await BuildMutationResultAsync(tenantId, currentUserId, channelId, affected > 0);
    }

    public async Task<IReadOnlyDictionary<long, DirectConversationVo>> GetChannelSummariesAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> channelIds)
    {
        var normalizedChannelIds = channelIds.Where(id => id > 0).Distinct().ToList();
        if (currentUserId <= 0 || normalizedChannelIds.Count == 0)
        {
            return new Dictionary<long, DirectConversationVo>();
        }

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var conversations = await _conversationRepository.QueryAsync(item =>
            normalizedChannelIds.Contains(item.ChannelId) &&
            item.TenantId == normalizedTenantId &&
            (item.ParticipantLowUserId == currentUserId || item.ParticipantHighUserId == currentUserId) &&
            !item.IsDeleted);
        if (conversations.Count == 0)
        {
            return new Dictionary<long, DirectConversationVo>();
        }

        var directChannelIds = conversations.Select(item => item.ChannelId).Distinct().ToList();
        var members = await _memberRepository.QueryAsync(item =>
            directChannelIds.Contains(item.ChannelId) && item.UserId == currentUserId && !item.IsDeleted);
        var memberMap = members.ToDictionary(item => item.ChannelId);
        var peerUserIds = conversations
            .Select(item => GetPeerUserId(item, currentUserId))
            .Distinct()
            .ToList();
        var users = await _userRepository.QueryAsync(user =>
            peerUserIds.Contains(user.Id) && user.TenantId == normalizedTenantId && !user.IsDeleted);
        var userMap = users.ToDictionary(user => user.Id);
        var activeUserIds = users.Where(user => user.IsEnable).Select(user => user.Id).ToHashSet();
        var mutualPeerIds = await GetMutualPeerIdsAsync(normalizedTenantId, currentUserId, peerUserIds);
        var avatarMap = await GetAvatarUrlMapAsync(peerUserIds);
        var result = new Dictionary<long, DirectConversationVo>(conversations.Count);

        foreach (var conversation in conversations)
        {
            var peerUserId = GetPeerUserId(conversation, currentUserId);
            userMap.TryGetValue(peerUserId, out var peerUser);
            memberMap.TryGetValue(conversation.ChannelId, out var member);
            var isRequestReceiver = conversation.RequestedByUserId != currentUserId;
            var isBlockedByCurrentUser = conversation.BlockedByUserId == currentUserId;
            var isPeerAvailable = activeUserIds.Contains(peerUserId);
            var canSend = !conversation.BlockedByUserId.HasValue &&
                          isPeerAvailable &&
                          (conversation.RequestStatus == DirectConversationRequestStatus.Accepted ||
                           conversation.RequestStatus == DirectConversationRequestStatus.Pending &&
                           conversation.RequestedByUserId == currentUserId &&
                           !conversation.RequestMessageId.HasValue);
            result[conversation.ChannelId] = new DirectConversationVo
            {
                VoChannelId = conversation.ChannelId,
                VoConversationKind = mutualPeerIds.Contains(peerUserId) ? "mutual" : "stranger",
                VoPeerUserId = peerUserId,
                VoPeerPublicId = peerUser != null && User.HasPublicIdFormat(peerUser.PublicId)
                    ? peerUser.PublicId!.Trim().ToLowerInvariant()
                    : null,
                VoPeerDisplayName = peerUser == null
                    ? string.Empty
                    : User.BuildDisplayHandle(peerUser.UserName, peerUser.PublicIndex, peerUser.Id)
                      ?? User.NormalizeDisplayName(peerUser.UserName, peerUser.Id),
                VoPeerAvatarUrl = avatarMap.GetValueOrDefault(peerUserId),
                VoDirectRequestStatus = ToStatusToken(conversation.RequestStatus),
                VoCanSend = canSend,
                VoCanAccept = isPeerAvailable &&
                              isRequestReceiver &&
                              !conversation.BlockedByUserId.HasValue &&
                              conversation.RequestStatus is DirectConversationRequestStatus.Pending or DirectConversationRequestStatus.Declined,
                VoCanDecline = isRequestReceiver &&
                               !conversation.BlockedByUserId.HasValue &&
                               conversation.RequestStatus == DirectConversationRequestStatus.Pending,
                VoCanBlock = !conversation.BlockedByUserId.HasValue,
                VoCanUnblock = isBlockedByCurrentUser,
                VoIsBlockedByCurrentUser = isBlockedByCurrentUser,
                VoIsArchived = member?.ArchivedAt.HasValue == true,
                VoIsPeerAvailable = isPeerAvailable
            };
        }

        return result;
    }

    private async Task<DirectConversationMutationResult> BuildMutationResultAsync(
        long tenantId,
        long currentUserId,
        long channelId,
        bool changed)
    {
        var summary = await GetRequiredSummaryAsync(tenantId, currentUserId, channelId);
        return new DirectConversationMutationResult(summary, changed);
    }

    private async Task<DirectConversationMutationResult> UnchangedResultAsync(
        long tenantId,
        long currentUserId,
        long channelId)
    {
        return new DirectConversationMutationResult(
            await GetRequiredSummaryAsync(tenantId, currentUserId, channelId),
            false);
    }

    private async Task<DirectConversationVo> GetRequiredSummaryAsync(
        long tenantId,
        long currentUserId,
        long channelId)
    {
        var summaries = await GetChannelSummariesAsync(tenantId, currentUserId, new[] { channelId });
        if (!summaries.TryGetValue(channelId, out var summary))
        {
            throw NotFoundError("会话不存在或无权访问", "Chat.ChannelUnavailable", "error.chat.channel_unavailable");
        }

        return summary;
    }

    private async Task<DirectConversation> GetParticipantConversationAsync(
        long tenantId,
        long currentUserId,
        long channelId)
    {
        if (currentUserId <= 0 || channelId <= 0)
        {
            throw NotFoundError("会话不存在或无权访问", "Chat.ChannelUnavailable", "error.chat.channel_unavailable");
        }

        var normalizedTenantId = NormalizeTenantId(tenantId);
        var conversation = await _conversationRepository.QueryFirstAsync(item =>
            item.ChannelId == channelId &&
            item.TenantId == normalizedTenantId &&
            (item.ParticipantLowUserId == currentUserId || item.ParticipantHighUserId == currentUserId) &&
            !item.IsDeleted);
        return conversation
               ?? throw NotFoundError("会话不存在或无权访问", "Chat.ChannelUnavailable", "error.chat.channel_unavailable");
    }

    private async Task<bool> IsMutualFollowAsync(long tenantId, long currentUserId, long peerUserId)
    {
        var currentFollowsPeer = await _userFollowRepository.QueryExistsAsync(item =>
            item.TenantId == tenantId &&
            item.FollowerUserId == currentUserId &&
            item.FollowingUserId == peerUserId &&
            !item.IsDeleted);
        if (!currentFollowsPeer)
        {
            return false;
        }

        return await _userFollowRepository.QueryExistsAsync(item =>
            item.TenantId == tenantId &&
            item.FollowerUserId == peerUserId &&
            item.FollowingUserId == currentUserId &&
            !item.IsDeleted);
    }

    private async Task<HashSet<long>> GetMutualPeerIdsAsync(
        long tenantId,
        long currentUserId,
        IReadOnlyCollection<long> peerUserIds)
    {
        if (peerUserIds.Count == 0)
        {
            return new HashSet<long>();
        }

        var relations = await _userFollowRepository.QueryAsync(item =>
            item.TenantId == tenantId &&
            !item.IsDeleted &&
            ((item.FollowerUserId == currentUserId && peerUserIds.Contains(item.FollowingUserId)) ||
             (item.FollowingUserId == currentUserId && peerUserIds.Contains(item.FollowerUserId))));
        var following = relations
            .Where(item => item.FollowerUserId == currentUserId)
            .Select(item => item.FollowingUserId)
            .ToHashSet();
        var followers = relations
            .Where(item => item.FollowingUserId == currentUserId)
            .Select(item => item.FollowerUserId)
            .ToHashSet();
        following.IntersectWith(followers);
        return following;
    }

    private async Task<Dictionary<long, string>> GetAvatarUrlMapAsync(IReadOnlyCollection<long> userIds)
    {
        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachment.BusinessType == AttachmentBusinessTypes.Avatar &&
            attachment.BusinessId.HasValue &&
            userIds.Contains(attachment.BusinessId.Value) &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        return attachments
            .Where(attachment => attachment.BusinessId.HasValue)
            .GroupBy(attachment => attachment.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => _attachmentUrlResolver.ResolveAttachmentUrl(
                    group.OrderByDescending(attachment => attachment.Id).First().Id));
    }

    private static ChannelMember CreateMember(
        long channelId,
        long userId,
        long tenantId,
        string operatorName,
        long operatorId,
        DateTime now)
    {
        return new ChannelMember
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            ChannelId = channelId,
            UserId = userId,
            Role = MemberRole.Member,
            JoinedAt = now,
            TenantId = tenantId,
            CreateTime = now,
            CreateBy = operatorName,
            CreateId = operatorId
        };
    }

    private static long GetPeerUserId(DirectConversation conversation, long currentUserId)
    {
        return conversation.ParticipantLowUserId == currentUserId
            ? conversation.ParticipantHighUserId
            : conversation.ParticipantLowUserId;
    }

    private static void EnsureRequestReceiver(DirectConversation conversation, long currentUserId)
    {
        if (conversation.RequestedByUserId == currentUserId)
        {
            throw ForbiddenError("只有私聊请求接收人可以执行此操作", "Chat.DirectReceiverRequired", "error.chat.direct_receiver_required");
        }
    }

    private static void EnsureNotBlocked(DirectConversation conversation)
    {
        if (conversation.BlockedByUserId.HasValue)
        {
            throw ConflictError("会话已被阻断", "Chat.DirectBlocked", "error.chat.direct_blocked");
        }
    }

    private static void EnsureVisibleToRequestReceiver(DirectConversation conversation, long currentUserId)
    {
        if (conversation.RequestStatus == DirectConversationRequestStatus.Pending &&
            conversation.RequestedByUserId != currentUserId &&
            !conversation.RequestMessageId.HasValue)
        {
            throw NotFoundError(
                "会话不存在或无权访问",
                "Chat.ChannelUnavailable",
                "error.chat.channel_unavailable");
        }
    }

    private static string ToStatusToken(DirectConversationRequestStatus status)
    {
        return status switch
        {
            DirectConversationRequestStatus.Pending => "pending",
            DirectConversationRequestStatus.Accepted => "accepted",
            DirectConversationRequestStatus.Declined => "declined",
            _ => "pending"
        };
    }

    private static long NormalizeTenantId(long tenantId) => tenantId > 0 ? tenantId : 0;

    private static string NormalizeOperator(string operatorName) =>
        string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();

    private static BusinessException ValidationError(string message, string code, string messageKey) =>
        new(message, StatusCodes.Status400BadRequest, code, messageKey);

    private static BusinessException NotFoundError(string message, string code, string messageKey) =>
        new(message, StatusCodes.Status404NotFound, code, messageKey);

    private static BusinessException ForbiddenError(string message, string code, string messageKey) =>
        new(message, StatusCodes.Status403Forbidden, code, messageKey);

    private static BusinessException ConflictError(string message, string code, string messageKey) =>
        new(message, StatusCodes.Status409Conflict, code, messageKey);
}
