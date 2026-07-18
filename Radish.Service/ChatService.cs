using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Radish.Common;
using Radish.Common.Exceptions;
using Radish.IRepository;
using AutoMapper;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service.Base;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.Service;

/// <summary>聊天室服务实现</summary>
public class ChatService : BaseService<Channel, ChannelVo>, IChatService
{
    private readonly IBaseRepository<Channel> _channelRepository;
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IBaseRepository<ChannelMember> _memberRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IChatPresenceService _chatPresenceService;
    private readonly IChatChannelAccessService _chatChannelAccessService;
    private readonly IDirectConversationService _directConversationService;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;

    public ChatService(
        IMapper mapper,
        IBaseRepository<Channel> baseRepository,
        IChannelMessageRepository messageRepository,
        IBaseRepository<ChannelMember> memberRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IBaseRepository<User> userRepository,
        IChatPresenceService chatPresenceService,
        INotificationService notificationService,
        IChatChannelAccessService chatChannelAccessService,
        IDirectConversationService directConversationService,
        IAttachmentUrlResolver attachmentUrlResolver)
        : base(mapper, baseRepository)
    {
        _channelRepository = baseRepository;
        _messageRepository = messageRepository;
        _memberRepository = memberRepository;
        _attachmentRepository = attachmentRepository;
        _userRepository = userRepository;
        _chatPresenceService = chatPresenceService;
        _chatChannelAccessService = chatChannelAccessService;
        _directConversationService = directConversationService;
        _attachmentUrlResolver = attachmentUrlResolver;
    }

    public async Task<List<ChannelVo>> GetChannelListAsync(
        long tenantId,
        long userId,
        ChatChannelListView view = ChatChannelListView.Active)
    {
        var channels = await _channelRepository.QueryWithOrderAsync(
            c => c.IsEnabled && !c.IsDeleted,
            c => c.Sort,
            OrderByType.Asc);

        if (channels.Count == 0)
        {
            return new List<ChannelVo>();
        }

        var channelIds = channels.Select(c => c.Id).ToList();
        var members = await _memberRepository.QueryAsync(m => m.UserId == userId && channelIds.Contains(m.ChannelId) && !m.IsDeleted);
        var memberMap = members.ToDictionary(m => m.ChannelId, m => m);
        var directSummaries = await _directConversationService.GetChannelSummariesAsync(tenantId, userId, channelIds);

        var result = new List<ChannelVo>(channels.Count);
        foreach (var channel in channels)
        {
            var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channel.Id);
            if (!access.CanView)
            {
                continue;
            }

            directSummaries.TryGetValue(channel.Id, out var directSummary);
            if (view == ChatChannelListView.Archived)
            {
                if (directSummary?.VoIsArchived != true)
                {
                    continue;
                }
            }
            else if (directSummary?.VoIsArchived == true)
            {
                continue;
            }

            var channelVo = Mapper.Map<ChannelVo>(channel);
            ApplyConversationMetadata(channelVo, access, directSummary);
            channelVo.VoLastMessage = await GetLastMessagePreviewAsync(channel.Id);

            memberMap.TryGetValue(channel.Id, out var member);
            var unreadState = await GetUnreadStateInternalAsync(channel.Id, userId, member?.LastReadMessageId);
            channelVo.VoUnreadCount = unreadState.VoUnreadCount;
            channelVo.VoHasMention = unreadState.VoHasMention;

            result.Add(channelVo);
        }

        return result;
    }

    public async Task<ChannelVo?> GetChannelDetailAsync(long tenantId, long userId, long channelId)
    {
        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            return null;
        }

        var channel = await _channelRepository.QueryFirstAsync(c => c.Id == channelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            return null;
        }

        var channelVo = Mapper.Map<ChannelVo>(channel);
        var directSummaries = await _directConversationService.GetChannelSummariesAsync(
            tenantId,
            userId,
            new[] { channelId });
        directSummaries.TryGetValue(channelId, out var directSummary);
        ApplyConversationMetadata(channelVo, access, directSummary);
        channelVo.VoLastMessage = await GetLastMessagePreviewAsync(channel.Id);

        var member = await _memberRepository.QueryFirstAsync(m => m.ChannelId == channel.Id && m.UserId == userId && !m.IsDeleted);
        var unreadState = await GetUnreadStateInternalAsync(channel.Id, userId, member?.LastReadMessageId);
        channelVo.VoUnreadCount = unreadState.VoUnreadCount;
        channelVo.VoHasMention = unreadState.VoHasMention;

        return channelVo;
    }

    public async Task<List<ChannelMessageVo>> GetHistoryAsync(
        long tenantId,
        long userId,
        long channelId,
        long? beforeMessageId,
        long? afterMessageId,
        int pageSize = 50)
    {
        if (channelId <= 0)
        {
            return new List<ChannelMessageVo>();
        }

        if (beforeMessageId.HasValue && beforeMessageId.Value > 0 && afterMessageId.HasValue && afterMessageId.Value > 0)
        {
            return new List<ChannelMessageVo>();
        }

        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            return new List<ChannelMessageVo>();
        }

        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var messages = afterMessageId.HasValue && afterMessageId.Value > 0
            ? await QueryChannelMessagesAsync(
                m => m.ChannelId == channelId && m.Id > afterMessageId.Value,
                safePageSize,
                OrderByType.Asc)
            : await QueryChannelMessagesAsync(
                beforeMessageId.HasValue && beforeMessageId.Value > 0
                    ? (System.Linq.Expressions.Expression<Func<ChannelMessage, bool>>)(m => m.ChannelId == channelId && m.Id < beforeMessageId.Value)
                    : m => m.ChannelId == channelId,
                safePageSize,
                OrderByType.Desc);

        return await MapChannelMessagesAsync(messages);
    }

    public async Task<ChannelMessageWindowVo?> GetMessageWindowAsync(
        long tenantId,
        long userId,
        long channelId,
        long messageId,
        int beforeCount = 25,
        int afterCount = 25)
    {
        if (channelId <= 0 || messageId <= 0)
        {
            return null;
        }

        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            return null;
        }

        var anchorMessage = await _messageRepository.QueryFirstIncludingDeletedAsync(m => m.ChannelId == channelId && m.Id == messageId);
        if (anchorMessage == null)
        {
            return null;
        }

        var safeBeforeCount = Math.Clamp(beforeCount, 0, 50);
        var safeAfterCount = Math.Clamp(afterCount, 0, 50);
        var olderMessages = safeBeforeCount > 0
            ? await QueryChannelMessagesAsync(
                m => m.ChannelId == channelId && m.Id < messageId,
                safeBeforeCount,
                OrderByType.Desc)
            : new List<ChannelMessage>();
        var newerMessages = safeAfterCount > 0
            ? await QueryChannelMessagesAsync(
                m => m.ChannelId == channelId && m.Id > messageId,
                safeAfterCount,
                OrderByType.Asc)
            : new List<ChannelMessage>();

        var combinedMessages = new List<ChannelMessage>(olderMessages.Count + 1 + newerMessages.Count);
        combinedMessages.AddRange(olderMessages);
        combinedMessages.Add(anchorMessage);
        combinedMessages.AddRange(newerMessages);

        var oldestLoadedMessageId = olderMessages.Count > 0 ? olderMessages[0].Id : anchorMessage.Id;
        var newestLoadedMessageId = newerMessages.Count > 0 ? newerMessages[^1].Id : anchorMessage.Id;

        return new ChannelMessageWindowVo
        {
            VoChannelId = channelId,
            VoAnchorMessageId = anchorMessage.Id,
            VoMessages = await MapChannelMessagesAsync(combinedMessages),
            VoHasMoreBefore = await _messageRepository.QueryExistsIncludingDeletedAsync(m => m.ChannelId == channelId && m.Id < oldestLoadedMessageId),
            VoHasMoreAfter = await _messageRepository.QueryExistsIncludingDeletedAsync(m => m.ChannelId == channelId && m.Id > newestLoadedMessageId)
        };
    }

    public async Task<ChatMessageSendResult> SendMessageAsync(
        long tenantId,
        long userId,
        string userName,
        SendChannelMessageDto request,
        bool canManageChannel = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ChannelId <= 0)
        {
            throw new ArgumentException("频道 Id 无效", nameof(request.ChannelId));
        }

        var access = await _chatChannelAccessService.GetAccessAsync(
            tenantId,
            userId,
            request.ChannelId,
            canManageChannel);
        if (!access.CanView)
        {
            throw CreateChannelSendDeniedException(access);
        }

        var channel = await _channelRepository.QueryFirstAsync(c =>
            c.Id == request.ChannelId && c.IsEnabled && !c.IsDeleted);
        if (channel == null)
        {
            throw CreateChannelSendDeniedException(ChatChannelAccessResult.Unavailable);
        }

        var normalizedContent = request.Content?.Trim();
        var normalizedClientRequestId = string.IsNullOrWhiteSpace(request.ClientRequestId)
            ? null
            : request.ClientRequestId.Trim();
        var normalizedReplyToId = request.ReplyToId is > 0 ? request.ReplyToId : null;
        var normalizedAttachmentId = request.AttachmentId is > 0 ? request.AttachmentId : null;

        if (request.Type is not MessageType.Text and not MessageType.Image)
        {
            throw new BusinessException(
                "当前接口只允许发送文本或图片消息",
                StatusCodes.Status400BadRequest,
                "Chat.MessageTypeUnavailable",
                "error.chat.message_type_unavailable");
        }

        if (request.Type == MessageType.Text && string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new ArgumentException("文本消息内容不能为空", nameof(request.Content));
        }

        if (request.Type == MessageType.Image && !normalizedAttachmentId.HasValue)
        {
            throw new ArgumentException("图片消息必须提供附件 ID", nameof(request.AttachmentId));
        }

        if (normalizedClientRequestId != null)
        {
            var existingMessage = await FindIdempotentMessageAsync(tenantId, userId, normalizedClientRequestId);
            if (existingMessage != null)
            {
                EnsureIdempotentRequestMatches(
                    existingMessage,
                    request.ChannelId,
                    request.Type,
                    normalizedContent,
                    normalizedReplyToId,
                    normalizedAttachmentId);
                await CompletePersistedMessageStateAsync(channel, existingMessage, userId, userName);
                return new ChatMessageSendResult(await MapSingleMessageAsync(existingMessage), false);
            }
        }

        if (access.IsDirectConversation)
        {
            ValidateDirectSendRequest(
                access,
                userId,
                request.Type,
                normalizedContent,
                normalizedReplyToId,
                normalizedAttachmentId);
        }
        else if (!access.CanSend)
        {
            throw CreateChannelSendDeniedException(access);
        }

        var normalizedUserName = await ResolveChatDisplayNameAsync(userId, userName);
        var userAvatarAttachmentId = await GetCurrentUserAvatarAttachmentIdAsync(userId);
        var senderAvatarUrl = ResolveAttachmentUrl(userAvatarAttachmentId);

        ChannelMessage? replyToMessage = null;
        if (normalizedReplyToId.HasValue)
        {
            replyToMessage = await _messageRepository.QueryFirstAsync(m =>
                m.Id == normalizedReplyToId.Value && m.ChannelId == request.ChannelId);
            if (replyToMessage == null)
            {
                throw new BusinessException(
                    "引用消息不存在或不属于当前会话",
                    StatusCodes.Status400BadRequest,
                    "Chat.ReplyUnavailable",
                    "error.chat.reply_unavailable");
            }
        }

        Attachment? attachment = null;
        if (normalizedAttachmentId.HasValue)
        {
            attachment = await GetOwnedUnboundChatAttachmentAsync(normalizedAttachmentId.Value, userId);
        }

        var message = new ChannelMessage
        {
            ChannelId = request.ChannelId,
            UserId = userId,
            UserName = normalizedUserName,
            ClientRequestId = normalizedClientRequestId,
            UserAvatarAttachmentIdSnapshot = userAvatarAttachmentId,
            Type = request.Type,
            Content = normalizedContent,
            ReplyToId = normalizedReplyToId,
            AttachmentId = normalizedAttachmentId,
            TenantId = tenantId,
            CreateTime = DateTime.UtcNow,
            IsDeleted = false
        };

        var messageId = SnowFlakeSingle.Instance.NextId();
        message.Id = messageId;
        var occurredAtUtc = message.CreateTime;
        var outboxDrafts = new List<ReliableOutboxDraft>();
        var isPendingDirectRequest = access.IsDirectConversation &&
                                     access.DirectRequestStatus == DirectConversationRequestStatus.Pending;
        var mentionNotification = isPendingDirectRequest
            ? null
            : await BuildMentionNotificationAsync(
                tenantId,
                messageId,
                request.ChannelId,
                channel.Name,
                userId,
                normalizedUserName,
                senderAvatarUrl,
                normalizedContent,
                occurredAtUtc);
        if (mentionNotification != null)
        {
            outboxDrafts.Add(new ReliableOutboxDraft(
                ReliableOutboxSources.Chat,
                tenantId,
                ReliableTaskTypes.NotificationRequested,
                1,
                $"task:notification:chat-mention:message:{messageId}",
                "ChannelMessage",
                messageId.ToString(),
                JsonSerializer.Serialize(new NotificationRequestedTaskPayload(mentionNotification)),
                occurredAtUtc));
        }

        if (isPendingDirectRequest && access.DirectPeerUserId.HasValue)
        {
            var requestNotification = BuildDirectRequestNotification(
                tenantId,
                messageId,
                request.ChannelId,
                userId,
                normalizedUserName,
                senderAvatarUrl,
                access.DirectPeerUserId.Value,
                occurredAtUtc);
            outboxDrafts.Add(new ReliableOutboxDraft(
                ReliableOutboxSources.Chat,
                tenantId,
                ReliableTaskTypes.NotificationRequested,
                1,
                $"task:notification:chat-request:message:{messageId}",
                "ChannelMessage",
                messageId.ToString(),
                JsonSerializer.Serialize(new NotificationRequestedTaskPayload(requestNotification)),
                occurredAtUtc));
        }

        if (attachment != null)
        {
            outboxDrafts.Add(new ReliableOutboxDraft(
                ReliableOutboxSources.Chat,
                tenantId,
                ReliableTaskTypes.ChatAttachmentBinding,
                1,
                $"task:chat-attachment-bind:message:{messageId}",
                "ChannelMessage",
                messageId.ToString(),
                JsonSerializer.Serialize(new ChatAttachmentBindingTaskPayload(
                    tenantId,
                    messageId,
                    attachment.Id,
                    userId,
                    normalizedUserName)),
                occurredAtUtc));
        }

        try
        {
            await _messageRepository.AddWithEffectsAsync(
                message,
                outboxDrafts,
                access.DirectPeerUserId,
                isPendingDirectRequest ? access.DirectConversationId : null);
        }
        catch (Exception exception)
        {
            if (normalizedClientRequestId != null)
            {
                var existingMessage = await FindIdempotentMessageAsync(tenantId, userId, normalizedClientRequestId);
                if (existingMessage != null)
                {
                    EnsureIdempotentRequestMatches(
                        existingMessage,
                        request.ChannelId,
                        request.Type,
                        normalizedContent,
                        normalizedReplyToId,
                        normalizedAttachmentId);
                    await CompletePersistedMessageStateAsync(channel, existingMessage, userId, normalizedUserName);
                    return new ChatMessageSendResult(await MapSingleMessageAsync(existingMessage), false);
                }
            }

            if (exception is DirectConversationRequestClaimException)
            {
                throw new BusinessException(
                    "陌生私聊请求已经发送过首条消息",
                    StatusCodes.Status409Conflict,
                    "Chat.DirectRequestAlreadySent",
                    "error.chat.direct_request_already_sent");
            }

            if (normalizedAttachmentId.HasValue &&
                await _messageRepository.QueryExistsAsync(candidate =>
                    candidate.AttachmentId == normalizedAttachmentId.Value && !candidate.IsDeleted))
            {
                throw new BusinessException(
                    "Chat 附件已被其他消息引用",
                    StatusCodes.Status409Conflict,
                    "Chat.AttachmentBindingConflict",
                    "error.chat.attachment_binding_conflict");
            }

            throw;
        }

        await CompletePersistedMessageStateAsync(channel, message, userId, normalizedUserName);
        var changedUserIds = access.DirectPeerUserId.HasValue
            ? new[] { access.DirectPeerUserId.Value }
            : Array.Empty<long>();
        return new ChatMessageSendResult(
            await MapSingleMessageAsync(message, replyToMessage),
            true,
            changedUserIds);
    }

    private async Task<ChannelMessage?> FindIdempotentMessageAsync(
        long tenantId,
        long userId,
        string clientRequestId)
    {
        return await _messageRepository.QueryFirstIncludingDeletedAsync(message =>
            message.TenantId == tenantId &&
            message.UserId == userId &&
            message.ClientRequestId == clientRequestId);
    }

    private static void EnsureIdempotentRequestMatches(
        ChannelMessage existingMessage,
        long channelId,
        MessageType type,
        string? content,
        long? replyToId,
        long? attachmentId)
    {
        if (existingMessage.ChannelId == channelId &&
            existingMessage.Type == type &&
            string.Equals(existingMessage.Content, content, StringComparison.Ordinal) &&
            existingMessage.ReplyToId == replyToId &&
            existingMessage.AttachmentId == attachmentId)
        {
            return;
        }

        throw new BusinessException(
            "clientRequestId 已用于不同的消息请求",
            StatusCodes.Status409Conflict,
            "Chat.MessageIdempotencyConflict",
            "error.chat.message_idempotency_conflict");
    }

    private async Task<Attachment> GetOwnedUnboundChatAttachmentAsync(long attachmentId, long userId)
    {
        var attachment = await _attachmentRepository.QueryFirstAsync(candidate =>
            candidate.Id == attachmentId && candidate.IsEnabled && !candidate.IsDeleted);
        if (attachment == null ||
            attachment.UploaderId != userId ||
            attachment.BusinessType != AttachmentBusinessTypes.Chat ||
            string.IsNullOrWhiteSpace(attachment.MimeType) ||
            !attachment.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            attachment.BusinessId.HasValue)
        {
            throw new BusinessException(
                "Chat 附件不存在、无权使用或已绑定",
                StatusCodes.Status400BadRequest,
                "Chat.AttachmentUnavailable",
                "error.chat.attachment_unavailable");
        }

        return attachment;
    }

    private async Task CompletePersistedMessageStateAsync(
        Channel channel,
        ChannelMessage message,
        long userId,
        string userName)
    {
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? message.UserName : userName.Trim();
        if (!channel.LastMessageId.HasValue || channel.LastMessageId.Value < message.Id)
        {
            channel.LastMessageId = message.Id;
            channel.LastMessageTime = message.CreateTime;
            channel.ModifyTime = DateTime.UtcNow;
            channel.ModifyBy = normalizedUserName;
            channel.ModifyId = userId;
            await _channelRepository.UpdateAsync(channel);
        }

        await EnsureMemberAndUpdateReadStateAsync(
            channel.Id,
            userId,
            message.TenantId,
            normalizedUserName,
            message.Id);
    }

    private async Task<ChannelMessageVo> MapSingleMessageAsync(
        ChannelMessage message,
        ChannelMessage? knownReply = null)
    {
        var replyMap = new Dictionary<long, ChannelMessage>();
        if (knownReply != null)
        {
            replyMap[knownReply.Id] = knownReply;
        }
        else if (message.ReplyToId.HasValue)
        {
            var reply = await _messageRepository.QueryFirstIncludingDeletedAsync(candidate =>
                candidate.Id == message.ReplyToId.Value && candidate.ChannelId == message.ChannelId);
            if (reply != null)
            {
                replyMap[reply.Id] = reply;
            }
        }

        var avatarMap = await GetUserAvatarAttachmentMapAsync(
            new[] { message.UserId }.Concat(replyMap.Values.Select(reply => reply.UserId)));
        return MapMessageVo(message, replyMap, avatarMap);
    }

    private static BusinessException CreateChannelSendDeniedException(ChatChannelAccessResult access)
    {
        var statusCode = access.Exists && access.ChannelType == ChannelType.Announcement
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status404NotFound;
        return new BusinessException(
            statusCode == StatusCodes.Status403Forbidden ? "当前用户无权在该频道发言" : "频道不存在或无权访问",
            statusCode,
            statusCode == StatusCodes.Status403Forbidden ? "Chat.SendForbidden" : "Chat.ChannelUnavailable",
            statusCode == StatusCodes.Status403Forbidden ? "error.chat.send_forbidden" : "error.chat.channel_unavailable");
    }

    private static void ValidateDirectSendRequest(
        ChatChannelAccessResult access,
        long userId,
        MessageType messageType,
        string? content,
        long? replyToId,
        long? attachmentId)
    {
        if (access.DirectBlockedByUserId.HasValue)
        {
            throw new BusinessException(
                "会话已被阻断",
                StatusCodes.Status409Conflict,
                "Chat.DirectBlocked",
                "error.chat.direct_blocked");
        }

        if (!access.IsPeerAvailable)
        {
            throw new BusinessException(
                "对方账号当前不可用",
                StatusCodes.Status409Conflict,
                "Chat.DirectPeerUnavailable",
                "error.chat.direct_peer_unavailable");
        }

        switch (access.DirectRequestStatus)
        {
            case DirectConversationRequestStatus.Pending:
                if (access.DirectRequestedByUserId != userId)
                {
                    throw new BusinessException(
                        "接受私聊请求后才能发送消息",
                        StatusCodes.Status409Conflict,
                        "Chat.DirectNotAccepted",
                        "error.chat.direct_not_accepted");
                }

                if (access.HasMessages)
                {
                    throw new BusinessException(
                        "陌生私聊请求已经发送过首条消息",
                        StatusCodes.Status409Conflict,
                        "Chat.DirectRequestAlreadySent",
                        "error.chat.direct_request_already_sent");
                }

                if (messageType != MessageType.Text ||
                    replyToId.HasValue ||
                    attachmentId.HasValue ||
                    ParseMentionedUserIds(content).Count > 0)
                {
                    throw new BusinessException(
                        "陌生私聊请求只允许发送一条纯文本消息",
                        StatusCodes.Status400BadRequest,
                        "Chat.DirectRequestTextOnly",
                        "error.chat.direct_request_text_only");
                }

                break;
            case DirectConversationRequestStatus.Declined:
                throw new BusinessException(
                    "私聊请求已被拒绝",
                    StatusCodes.Status409Conflict,
                    "Chat.DirectRequestDeclined",
                    "error.chat.direct_request_declined");
            case DirectConversationRequestStatus.Accepted:
                break;
            default:
                throw new BusinessException(
                    "当前会话状态不能发送消息",
                    StatusCodes.Status409Conflict,
                    "Chat.DirectNotAccepted",
                    "error.chat.direct_not_accepted");
        }

        if (!access.CanSend)
        {
            throw new BusinessException(
                "当前会话状态不能发送消息",
                StatusCodes.Status409Conflict,
                "Chat.DirectNotAccepted",
                "error.chat.direct_not_accepted");
        }
    }

    private static CreateNotificationDto BuildDirectRequestNotification(
        long tenantId,
        long messageId,
        long channelId,
        long senderUserId,
        string senderUserName,
        string? senderAvatarUrl,
        long receiverUserId,
        DateTime occurredAtUtc)
    {
        return new CreateNotificationDto
        {
            NotificationId = SnowFlakeSingle.Instance.NextId(),
            BusinessKey = $"notification:chat-request:message:{messageId}",
            Type = NotificationType.DirectMessageRequested,
            Title = "新的私信请求",
            Content = $"{senderUserName} 向你发送了私信请求",
            Priority = (int)NotificationPriority.Normal,
            BusinessType = "ChannelMessage",
            BusinessId = messageId,
            TriggerId = senderUserId,
            TriggerName = senderUserName,
            TriggerAvatar = senderAvatarUrl,
            ReceiverUserIds = [receiverUserId],
            TenantId = tenantId,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["actorName"] = senderUserName
            },
            TargetKind = NotificationTargetKind.ChatConversation,
            Target = new NotificationTargetData
            {
                ChannelId = channelId,
                MessageId = messageId
            },
            OccurredAtUtc = occurredAtUtc
        };
    }

    private static void ApplyConversationMetadata(
        ChannelVo channel,
        ChatChannelAccessResult access,
        DirectConversationVo? directConversation)
    {
        channel.VoCanSend = access.CanSend;
        if (directConversation == null)
        {
            channel.VoConversationKind = channel.VoType == ChannelType.Private ? "group" : "public";
            return;
        }

        channel.VoConversationKind = directConversation.VoConversationKind;
        channel.VoPeerUserId = directConversation.VoPeerUserId;
        channel.VoPeerPublicId = directConversation.VoPeerPublicId;
        channel.VoPeerDisplayName = directConversation.VoPeerDisplayName;
        channel.VoPeerAvatarUrl = directConversation.VoPeerAvatarUrl;
        channel.VoDirectRequestStatus = directConversation.VoDirectRequestStatus;
        channel.VoCanSend = directConversation.VoCanSend;
        channel.VoCanAccept = directConversation.VoCanAccept;
        channel.VoCanDecline = directConversation.VoCanDecline;
        channel.VoCanBlock = directConversation.VoCanBlock;
        channel.VoCanUnblock = directConversation.VoCanUnblock;
        channel.VoIsBlockedByCurrentUser = directConversation.VoIsBlockedByCurrentUser;
        channel.VoIsArchived = directConversation.VoIsArchived;
        channel.VoIsPeerAvailable = directConversation.VoIsPeerAvailable;
    }

    private async Task<List<ChannelMessage>> QueryChannelMessagesAsync(
        System.Linq.Expressions.Expression<Func<ChannelMessage, bool>> whereExpression,
        int pageSize,
        OrderByType orderByType)
    {
        if (pageSize <= 0)
        {
            return new List<ChannelMessage>();
        }

        var (messages, _) = await _messageRepository.QueryPageIncludingDeletedAsync(
            whereExpression,
            1,
            pageSize,
            m => m.Id,
            orderByType);

        if (messages.Count == 0)
        {
            return new List<ChannelMessage>();
        }

        return messages
            .OrderBy(m => m.Id)
            .ToList();
    }

    private async Task<List<ChannelMessageVo>> MapChannelMessagesAsync(IReadOnlyCollection<ChannelMessage> messages)
    {
        if (messages.Count == 0)
        {
            return new List<ChannelMessageVo>();
        }

        var orderedMessages = messages
            .OrderBy(m => m.Id)
            .ToList();
        var replyIds = orderedMessages
            .Where(m => m.ReplyToId.HasValue)
            .Select(m => m.ReplyToId!.Value)
            .Distinct()
            .ToList();
        var replyMap = replyIds.Count > 0
            ? (await _messageRepository.QueryByIdsIncludingDeletedAsync(replyIds)).ToDictionary(m => m.Id, m => m)
            : new Dictionary<long, ChannelMessage>();
        var avatarMap = await GetUserAvatarAttachmentMapAsync(orderedMessages.Select(m => m.UserId)
            .Concat(replyMap.Values.Select(m => m.UserId)));

        return orderedMessages
            .Select(message => MapMessageVo(message, replyMap, avatarMap))
            .ToList();
    }

    public async Task<long?> RecallMessageAsync(long tenantId, long userId, string userName, long messageId, bool canRecallOthers)
    {
        if (messageId <= 0)
        {
            return null;
        }

        var message = await _messageRepository.QueryFirstIncludingDeletedAsync(m => m.Id == messageId);
        if (message == null)
        {
            return null;
        }

        var access = await _chatChannelAccessService.GetAccessAsync(
            tenantId,
            userId,
            message.ChannelId,
            canRecallOthers);
        if (!access.CanView)
        {
            return null;
        }

        if (message.IsDeleted)
        {
            return message.ChannelId;
        }

        var isOwner = message.UserId == userId;
        if (!isOwner && !canRecallOthers)
        {
            return null;
        }

        if (!canRecallOthers && message.CreateTime < DateTime.UtcNow.AddMinutes(-30))
        {
            return null;
        }

        var normalizedOperator = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();
        var affected = await _messageRepository.UpdateColumnsAsync(
            m => new ChannelMessage
            {
                IsDeleted = true,
                DeletedAt = DateTime.UtcNow,
                DeletedBy = normalizedOperator
            },
            m => m.Id == messageId && !m.IsDeleted);

        return affected > 0 ? message.ChannelId : null;
    }

    public async Task JoinChannelAsync(long tenantId, long userId, long channelId, string operatorName)
    {
        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanJoinRealtime)
        {
            throw new BusinessException(
                "频道不存在或无权访问",
                StatusCodes.Status404NotFound,
                "Chat.ChannelUnavailable",
                "error.chat.channel_unavailable");
        }

        await EnsureMemberAndUpdateReadStateAsync(channelId, userId, tenantId, operatorName, null);
    }

    public Task LeaveChannelAsync(long tenantId, long userId, long channelId)
    {
        _ = tenantId;
        _ = userId;
        _ = channelId;
        return Task.CompletedTask;
    }

    public async Task<ChannelUnreadStateVo> MarkChannelAsReadAsync(long tenantId, long userId, long channelId, string operatorName)
    {
        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            return new ChannelUnreadStateVo
            {
                VoChannelId = channelId,
                VoUnreadCount = 0,
                VoHasMention = false
            };
        }

        var (latestMessages, _) = await _messageRepository.QueryPageAsync(
            m => m.ChannelId == channelId && !m.IsDeleted,
            1,
            1,
            m => m.Id,
            OrderByType.Desc);

        var latestMessageId = latestMessages.FirstOrDefault()?.Id;
        await EnsureMemberAndUpdateReadStateAsync(channelId, userId, tenantId, operatorName, latestMessageId);

        return await GetChannelUnreadStateAsync(tenantId, userId, channelId);
    }

    public async Task<ChannelUnreadStateVo> GetChannelUnreadStateAsync(long tenantId, long userId, long channelId)
    {
        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            return new ChannelUnreadStateVo
            {
                VoChannelId = channelId,
                VoUnreadCount = 0,
                VoHasMention = false
            };
        }

        var member = await _memberRepository.QueryFirstAsync(m => m.ChannelId == channelId && m.UserId == userId && !m.IsDeleted);
        return await GetUnreadStateInternalAsync(channelId, userId, member?.LastReadMessageId);
    }

    public async Task<List<long>> GetChannelAudienceUserIdsAsync(long tenantId, long channelId)
    {
        _ = tenantId;

        var userIds = await _memberRepository.QueryDistinctAsync(
            m => m.UserId,
            m => m.ChannelId == channelId && !m.IsDeleted);

        return userIds
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();
    }

    public async Task<List<ChannelMemberVo>> GetOnlineMembersAsync(long tenantId, long userId, long channelId)
    {
        var access = await _chatChannelAccessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanViewMembers)
        {
            return new List<ChannelMemberVo>();
        }

        var onlineUserIds = _chatPresenceService.GetOnlineUserIds(tenantId, channelId);
        if (onlineUserIds.Count == 0)
        {
            return new List<ChannelMemberVo>();
        }

        var users = await _userRepository.QueryAsync(u => onlineUserIds.Contains(u.Id) && !u.IsDeleted);
        var avatarAttachmentMap = await GetUserAvatarAttachmentMapAsync(users.Select(u => u.Id));
        return users
            .OrderBy(u => BuildChatDisplayName(u, null))
            .Select(u =>
            {
                var hasAvatarAttachment = avatarAttachmentMap.TryGetValue(u.Id, out var avatarAttachmentId);
                var displayName = BuildChatDisplayName(u, null);

                return new ChannelMemberVo
                {
                    VoUserId = u.Id,
                    VoUserName = displayName,
                    VoUserAvatarAttachmentId = hasAvatarAttachment ? avatarAttachmentId : null,
                    VoUserAvatarUrl = hasAvatarAttachment
                        ? ResolveAttachmentUrl(avatarAttachmentId)
                        : null,
                    VoIsOnline = true
                };
            })
            .ToList();
    }

    private async Task<string> ResolveChatDisplayNameAsync(long userId, string fallback)
    {
        if (userId > 0)
        {
            var user = await _userRepository.QueryFirstAsync(u => u.Id == userId && !u.IsDeleted);
            if (user != null)
            {
                return BuildChatDisplayName(user, fallback);
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Unknown" : fallback.Trim();
    }

    private static string BuildChatDisplayName(User user, string? fallback)
    {
        return User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id)
            ?? User.NormalizeDisplayName(user.UserName, user.Id)
            ?? (string.IsNullOrWhiteSpace(fallback) ? $"User-{user.Id}" : fallback!.Trim());
    }

    private async Task<ChannelMessageVo?> GetLastMessagePreviewAsync(long channelId)
    {
        var (messages, _) = await _messageRepository.QueryPageAsync(
            m => m.ChannelId == channelId,
            1,
            1,
            m => m.Id,
            OrderByType.Desc);

        var message = messages.FirstOrDefault();
        if (message == null)
        {
            return null;
        }

        var avatarMap = await GetUserAvatarAttachmentMapAsync(new[] { message.UserId });
        return MapMessageVo(message, null, avatarMap);
    }

    private async Task<ChannelUnreadStateVo> GetUnreadStateInternalAsync(long channelId, long userId, long? lastReadMessageId)
    {
        var hasReadCursor = lastReadMessageId.HasValue && lastReadMessageId.Value > 0;

        var unreadCount = hasReadCursor
            ? await _messageRepository.QueryCountAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.Id > lastReadMessageId!.Value && m.UserId != userId)
            : await _messageRepository.QueryCountAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.UserId != userId);

        var hasMention = false;
        if (unreadCount > 0)
        {
            var mentionToken = $"({userId})";
            hasMention = hasReadCursor
                ? await _messageRepository.QueryExistsAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.Id > lastReadMessageId!.Value && m.UserId != userId && m.Content != null && m.Content.Contains(mentionToken))
                : await _messageRepository.QueryExistsAsync(m => m.ChannelId == channelId && !m.IsDeleted && m.UserId != userId && m.Content != null && m.Content.Contains(mentionToken));
        }

        return new ChannelUnreadStateVo
        {
            VoChannelId = channelId,
            VoUnreadCount = unreadCount,
            VoHasMention = hasMention
        };
    }

    private async Task EnsureMemberAndUpdateReadStateAsync(long channelId, long userId, long tenantId, string operatorName, long? lastReadMessageId)
    {
        var normalizedOperator = string.IsNullOrWhiteSpace(operatorName) ? "System" : operatorName.Trim();

        var member = await _memberRepository.QueryFirstAsync(m => m.ChannelId == channelId && m.UserId == userId);
        if (member == null)
        {
            await _memberRepository.AddAsync(new ChannelMember
            {
                ChannelId = channelId,
                UserId = userId,
                Role = MemberRole.Member,
                LastReadMessageId = lastReadMessageId,
                JoinedAt = DateTime.UtcNow,
                ArchivedAt = null,
                TenantId = tenantId,
                CreateTime = DateTime.UtcNow,
                CreateBy = normalizedOperator,
                CreateId = userId,
                IsDeleted = false
            });

            return;
        }

        if (lastReadMessageId.HasValue)
        {
            member.LastReadMessageId = lastReadMessageId;
        }

        if (member.IsDeleted)
        {
            member.IsDeleted = false;
            member.DeletedAt = null;
            member.DeletedBy = null;
        }

        member.ArchivedAt = null;
        member.ModifyTime = DateTime.UtcNow;
        member.ModifyBy = normalizedOperator;
        member.ModifyId = userId;

        await _memberRepository.UpdateAsync(member);
    }

    private async Task<Dictionary<long, long>> GetUserAvatarAttachmentMapAsync(IEnumerable<long> userIds)
    {
        var targetUserIds = userIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (targetUserIds.Count == 0)
        {
            return new Dictionary<long, long>();
        }

        var attachments = await _attachmentRepository.QueryAsync(a =>
            a.BusinessType == "Avatar"
            && a.BusinessId.HasValue
            && targetUserIds.Contains(a.BusinessId.Value)
            && a.IsEnabled
            && !a.IsDeleted);

        return attachments
            .Where(a => a.BusinessId.HasValue)
            .GroupBy(a => a.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.Id)
                    .First());
    }

    private async Task<long?> GetCurrentUserAvatarAttachmentIdAsync(long userId)
    {
        if (userId <= 0)
        {
            return null;
        }

        var avatarMap = await GetUserAvatarAttachmentMapAsync(new[] { userId });
        return avatarMap.TryGetValue(userId, out var avatarAttachmentId) ? avatarAttachmentId : null;
    }

    private async Task<CreateNotificationDto?> BuildMentionNotificationAsync(
        long tenantId,
        long messageId,
        long channelId,
        string channelName,
        long senderUserId,
        string senderUserName,
        string? senderAvatarUrl,
        string? content,
        DateTime occurredAtUtc)
    {
        var mentionedUserIds = ParseMentionedUserIds(content)
            .Where(userId => userId > 0 && userId != senderUserId)
            .Distinct()
            .ToList();

        if (mentionedUserIds.Count == 0)
        {
            return null;
        }

        var receivers = await _userRepository.QueryAsync(u =>
            mentionedUserIds.Contains(u.Id)
            && u.TenantId == tenantId
            && u.IsEnable
            && !u.IsDeleted);
        var receiverUserIds = receivers
            .Select(u => u.Id)
            .Distinct()
            .ToList();

        var visibleReceiverUserIds = new List<long>(receiverUserIds.Count);
        foreach (var receiverUserId in receiverUserIds)
        {
            var access = await _chatChannelAccessService.GetAccessAsync(tenantId, receiverUserId, channelId);
            if (access.CanView)
            {
                visibleReceiverUserIds.Add(receiverUserId);
            }
        }

        if (visibleReceiverUserIds.Count == 0)
        {
            return null;
        }

        var notificationId = SnowFlakeSingle.Instance.NextId();
        return new CreateNotificationDto
        {
            NotificationId = notificationId,
            BusinessKey = $"notification:chat-mention:message:{messageId}",
            Type = NotificationType.ChatMentioned,
            Title = "聊天室提及",
            Content = $"{senderUserName} 在频道「{channelName}」中提到了你",
            Priority = (int)NotificationPriority.High,
            BusinessType = "ChannelMessage",
            BusinessId = messageId,
            TriggerId = senderUserId,
            TriggerName = senderUserName,
            TriggerAvatar = senderAvatarUrl,
            ReceiverUserIds = visibleReceiverUserIds,
            TenantId = tenantId,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["actorName"] = senderUserName,
                ["channelName"] = channelName
            },
            TargetKind = NotificationTargetKind.ChatConversation,
            Target = new NotificationTargetData
            {
                ChannelId = channelId,
                MessageId = messageId
            },
            OccurredAtUtc = occurredAtUtc
        };
    }

    private static List<long> ParseMentionedUserIds(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<long>();
        }

        return Regex.Matches(content, @"@\[[^\]]+\]\((\d+)\)")
            .Select(match => long.TryParse(match.Groups[1].Value, out var userId) ? userId : 0)
            .Where(userId => userId > 0)
            .Distinct()
            .ToList();
    }

    private ChannelMessageVo MapMessageVo(
        ChannelMessage message,
        Dictionary<long, ChannelMessage>? replyMap,
        Dictionary<long, long>? avatarMap = null)
    {
        var messageVo = Mapper.Map<ChannelMessageVo>(message);
        messageVo.VoUserAvatarAttachmentId = message.UserAvatarAttachmentIdSnapshot;
        messageVo.VoUserAvatarUrl = ResolveAttachmentUrl(message.UserAvatarAttachmentIdSnapshot);
        messageVo.VoImageUrl = ResolveAttachmentUrl(message.AttachmentId);
        messageVo.VoImageThumbnailUrl = ResolveAttachmentUrl(message.AttachmentId, AttachmentUrlVariant.Thumbnail);

        if (string.IsNullOrWhiteSpace(messageVo.VoUserAvatarUrl)
            && avatarMap != null
            && avatarMap.TryGetValue(message.UserId, out var avatarAttachmentId))
        {
            messageVo.VoUserAvatarAttachmentId = avatarAttachmentId;
            messageVo.VoUserAvatarUrl = ResolveAttachmentUrl(avatarAttachmentId);
        }

        if (message.ReplyToId.HasValue && replyMap != null && replyMap.TryGetValue(message.ReplyToId.Value, out var replyMessage))
        {
            messageVo.VoReplyTo = Mapper.Map<ChannelMessageVo>(replyMessage);
            messageVo.VoReplyTo.VoUserAvatarAttachmentId = replyMessage.UserAvatarAttachmentIdSnapshot;
            messageVo.VoReplyTo.VoUserAvatarUrl = ResolveAttachmentUrl(replyMessage.UserAvatarAttachmentIdSnapshot);
            messageVo.VoReplyTo.VoImageUrl = ResolveAttachmentUrl(replyMessage.AttachmentId);
            messageVo.VoReplyTo.VoImageThumbnailUrl = ResolveAttachmentUrl(replyMessage.AttachmentId, AttachmentUrlVariant.Thumbnail);

            if (string.IsNullOrWhiteSpace(messageVo.VoReplyTo.VoUserAvatarUrl)
                && avatarMap != null
                && avatarMap.TryGetValue(replyMessage.UserId, out var replyAvatarAttachmentId))
            {
                messageVo.VoReplyTo.VoUserAvatarAttachmentId = replyAvatarAttachmentId;
                messageVo.VoReplyTo.VoUserAvatarUrl = ResolveAttachmentUrl(replyAvatarAttachmentId);
            }
        }

        messageVo.VoIsRecalled = message.IsDeleted;
        if (message.IsDeleted)
        {
            messageVo.VoContent = null;
            messageVo.VoImageUrl = null;
            messageVo.VoImageThumbnailUrl = null;
        }

        if (messageVo.VoReplyTo != null)
        {
            messageVo.VoReplyTo.VoIsRecalled = messageVo.VoReplyTo.VoIsRecalled || messageVo.VoReplyTo.VoContent == null;
        }

        return messageVo;
    }

    private string? ResolveAttachmentUrl(long? attachmentId, AttachmentUrlVariant variant = AttachmentUrlVariant.Original)
    {
        if (!attachmentId.HasValue || attachmentId.Value <= 0)
        {
            return null;
        }

        return _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value, variant);
    }

}
