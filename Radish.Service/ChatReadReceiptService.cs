using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>聊天轻量阅读回执的权限、聚合、游标和资料装配边界。</summary>
public sealed class ChatReadReceiptService : IChatReadReceiptService
{
    private const int CursorVersion = 1;
    private const int MaximumSummaryMessages = 20;
    private const int MaximumReaderPageSize = 50;
    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChatReadReceiptRepository _readReceiptRepository;
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IChatChannelAccessService _accessService;
    private readonly IChatService _chatService;
    private readonly IBaseRepository<User> _userRepository;
    private readonly IBaseRepository<Attachment> _attachmentRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly TimeProvider _timeProvider;

    public ChatReadReceiptService(
        IChatReadReceiptRepository readReceiptRepository,
        IChannelMessageRepository messageRepository,
        IChatChannelAccessService accessService,
        IChatService chatService,
        IBaseRepository<User> userRepository,
        IBaseRepository<Attachment> attachmentRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        TimeProvider timeProvider)
    {
        _readReceiptRepository = readReceiptRepository;
        _messageRepository = messageRepository;
        _accessService = accessService;
        _chatService = chatService;
        _userRepository = userRepository;
        _attachmentRepository = attachmentRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _timeProvider = timeProvider;
    }

    public async Task<ChannelReadStateAdvanceResult> AdvanceAsync(
        long tenantId,
        long userId,
        string userName,
        AdvanceChannelReadStateDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0 || request.ChannelId <= 0 || request.ReadThroughMessageId <= 0)
        {
            throw ReadStateInvalidArgument();
        }

        var access = await _accessService.GetAccessAsync(tenantId, userId, request.ChannelId);
        if (!access.CanView)
        {
            throw ReadTargetUnavailable();
        }

        var message = await _messageRepository.QueryFirstIncludingDeletedAsync(candidate =>
            candidate.Id == request.ReadThroughMessageId &&
            candidate.ChannelId == request.ChannelId);
        if (message == null || !IsTenantVisible(message.TenantId, tenantId))
        {
            throw ReadTargetUnavailable();
        }
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();
        AdvanceChatReadStateResult writeResult;
        try
        {
            writeResult = await _readReceiptRepository.AdvanceAsync(new AdvanceChatReadStateCommand(
                request.ChannelId,
                userId,
                tenantId,
                message.Id,
                access.ChannelType is ChannelType.Public or ChannelType.Announcement,
                normalizedUserName,
                _timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (ChatReadMemberUnavailableException)
        {
            throw ReadTargetUnavailable();
        }

        var unread = await _chatService.GetChannelUnreadStateAsync(tenantId, userId, request.ChannelId);
        var state = new ChannelReadStateVo
        {
            VoChannelId = request.ChannelId,
            VoLastReadMessageId = writeResult.LastReadMessageId,
            VoUnreadCount = unread.VoUnreadCount,
            VoHasMention = unread.VoHasMention,
            VoChanged = writeResult.Changed
        };
        return new ChannelReadStateAdvanceResult(
            state,
            writeResult.Changed && ResolveMode(access) != ChatReadReceiptModes.None);
    }

    public async Task<ChatReadReceiptSummariesVo> GetSummariesAsync(
        long tenantId,
        long userId,
        GetChatReadReceiptSummariesDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MessageIds == null)
        {
            throw ReceiptInvalidArgument();
        }

        var messageIds = request.MessageIds
            .Where(messageId => messageId > 0)
            .Distinct()
            .ToList();
        if (userId <= 0 || request.ChannelId <= 0 ||
            request.MessageIds.Any(messageId => messageId <= 0) ||
            messageIds.Count == 0 || messageIds.Count > MaximumSummaryMessages)
        {
            throw ReceiptInvalidArgument();
        }

        var access = await RequireReadableChannelAsync(tenantId, userId, request.ChannelId);
        var messages = await _messageRepository.QueryByIdsIncludingDeletedAsync(messageIds);
        if (messages.Count != messageIds.Count || messages.Any(message =>
                message.ChannelId != request.ChannelId ||
                message.IsDeleted ||
                !IsTenantVisible(message.TenantId, tenantId)))
        {
            throw ReceiptTargetUnavailable();
        }

        if (messages.Any(message => message.UserId != userId))
        {
            throw ReceiptMessageNotOwned();
        }

        var mode = ResolveMode(access);
        var result = new ChatReadReceiptSummariesVo
        {
            VoChannelId = request.ChannelId,
            VoMode = mode
        };
        if (mode == ChatReadReceiptModes.None)
        {
            return result;
        }

        if (mode == ChatReadReceiptModes.Direct)
        {
            var peerUserId = access.DirectPeerUserId ?? throw ReceiptTargetUnavailable();
            var peerCursor = await _readReceiptRepository.GetMemberReadCursorAsync(
                request.ChannelId,
                peerUserId);
            result.VoItems = messageIds.Select(messageId => new ChatReadReceiptSummaryItemVo
            {
                VoMessageId = messageId,
                VoPeerHasRead = peerCursor.HasValue && peerCursor.Value >= messageId
            }).ToList();
            return result;
        }

        var counts = await _readReceiptRepository.GetReadCountsAsync(
            request.ChannelId,
            userId,
            messageIds);
        var countMap = counts.ToDictionary(item => item.MessageId, item => item.ReadCount);
        result.VoItems = messageIds.Select(messageId => new ChatReadReceiptSummaryItemVo
        {
            VoMessageId = messageId,
            VoReadCount = countMap.GetValueOrDefault(messageId)
        }).ToList();
        return result;
    }

    public async Task<ChatReadReceiptReaderPageVo> GetReadersAsync(
        long tenantId,
        long userId,
        long channelId,
        long messageId,
        string? cursor,
        int pageSize)
    {
        if (userId <= 0 || channelId <= 0 || messageId <= 0 ||
            pageSize is < 1 or > MaximumReaderPageSize)
        {
            throw ReceiptInvalidArgument();
        }

        var access = await RequireReadableChannelAsync(tenantId, userId, channelId);
        if (access.ChannelType != ChannelType.Private || access.IsDirectConversation)
        {
            throw ReceiptNotSupported();
        }

        var message = await RequireReadableMessageAsync(
            tenantId,
            channelId,
            messageId,
            includeRecalled: false);
        if (message.UserId != userId)
        {
            throw ReceiptMessageNotOwned();
        }

        var decodedCursor = string.IsNullOrWhiteSpace(cursor) ? null : DecodeCursor(cursor);
        if (decodedCursor != null &&
            (decodedCursor.Version != CursorVersion ||
             decodedCursor.TenantId != tenantId ||
             decodedCursor.UserId != userId ||
             decodedCursor.ChannelId != channelId ||
             decodedCursor.MessageId != messageId ||
             decodedCursor.LastUserId <= 0))
        {
            throw ReceiptCursorInvalid();
        }

        var readerIds = await _readReceiptRepository.GetReaderUserIdsAsync(
            channelId,
            userId,
            messageId,
            message.CreateTime,
            decodedCursor?.LastUserId,
            pageSize + 1);
        var hasMore = readerIds.Count > pageSize;
        var pageReaderIds = readerIds.Take(pageSize).ToList();
        var users = pageReaderIds.Count == 0
            ? []
            : await _userRepository.QueryAsync(user =>
                pageReaderIds.Contains(user.Id) &&
                user.TenantId == tenantId &&
                !user.IsDeleted);
        var userMap = users.ToDictionary(user => user.Id);
        var avatarMap = await GetUserAvatarAttachmentMapAsync(pageReaderIds);
        var items = pageReaderIds.Select(readerUserId =>
        {
            userMap.TryGetValue(readerUserId, out var user);
            avatarMap.TryGetValue(readerUserId, out var avatarAttachmentId);
            return new ChatReadReceiptReaderVo
            {
                VoUserId = readerUserId,
                VoPublicId = user?.PublicId,
                VoPublicIndex = user?.PublicIndex,
                VoDisplayName = user == null
                    ? User.NormalizeDisplayName(null, readerUserId)
                    : User.BuildDisplayHandle(user.UserName, user.PublicIndex, user.Id) ??
                      User.NormalizeDisplayName(user.UserName, user.Id),
                VoAvatarAttachmentId = avatarAttachmentId > 0 ? avatarAttachmentId : null,
                VoAvatarUrl = avatarAttachmentId > 0
                    ? _attachmentUrlResolver.ResolveAttachmentUrl(avatarAttachmentId)
                    : null
            };
        }).ToList();

        return new ChatReadReceiptReaderPageVo
        {
            VoChannelId = channelId,
            VoMessageId = messageId,
            VoItems = items,
            VoHasMore = hasMore,
            VoNextCursor = hasMore && pageReaderIds.Count > 0
                ? EncodeCursor(new ReaderCursorPayload(
                    CursorVersion,
                    tenantId,
                    userId,
                    channelId,
                    messageId,
                    pageReaderIds[^1]))
                : null
        };
    }

    private async Task<ChatChannelAccessResult> RequireReadableChannelAsync(
        long tenantId,
        long userId,
        long channelId)
    {
        var access = await _accessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            throw ReceiptTargetUnavailable();
        }

        return access;
    }

    private async Task<ChannelMessage> RequireReadableMessageAsync(
        long tenantId,
        long channelId,
        long messageId,
        bool includeRecalled)
    {
        var message = await _messageRepository.QueryFirstIncludingDeletedAsync(candidate =>
            candidate.Id == messageId && candidate.ChannelId == channelId);
        if (message == null || !IsTenantVisible(message.TenantId, tenantId) ||
            !includeRecalled && message.IsDeleted)
        {
            throw ReceiptTargetUnavailable();
        }

        return message;
    }

    private async Task<Dictionary<long, long>> GetUserAvatarAttachmentMapAsync(
        IReadOnlyCollection<long> userIds)
    {
        var targetUserIds = userIds.Where(userId => userId > 0).Distinct().ToList();
        if (targetUserIds.Count == 0)
        {
            return [];
        }

        var attachments = await _attachmentRepository.QueryAsync(attachment =>
            attachment.BusinessType == "Avatar" &&
            attachment.BusinessId.HasValue &&
            targetUserIds.Contains(attachment.BusinessId.Value) &&
            attachment.IsEnabled &&
            !attachment.IsDeleted);
        return attachments
            .Where(attachment => attachment.BusinessId.HasValue)
            .GroupBy(attachment => attachment.BusinessId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(attachment => attachment.Id).First().Id);
    }

    private static string ResolveMode(ChatChannelAccessResult access)
    {
        if (access.ChannelType != ChannelType.Private)
        {
            return ChatReadReceiptModes.None;
        }

        if (!access.IsDirectConversation)
        {
            return ChatReadReceiptModes.PrivateGroup;
        }

        return access.DirectRequestStatus == DirectConversationRequestStatus.Accepted &&
               access.DirectBlockedByUserId == null &&
               access.IsPeerAvailable
            ? ChatReadReceiptModes.Direct
            : ChatReadReceiptModes.None;
    }

    private static bool IsTenantVisible(long resourceTenantId, long requestTenantId)
    {
        return resourceTenantId == 0 || resourceTenantId == requestTenantId;
    }

    private static string EncodeCursor(ReaderCursorPayload cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, CursorJsonOptions);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static ReaderCursorPayload DecodeCursor(string source)
    {
        try
        {
            var normalized = source.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            return JsonSerializer.Deserialize<ReaderCursorPayload>(
                       Convert.FromBase64String(normalized),
                       CursorJsonOptions)
                   ?? throw ReceiptCursorInvalid();
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or NotSupportedException)
        {
            throw ReceiptCursorInvalid();
        }
    }

    private static BusinessException ReadStateInvalidArgument() => new(
        "频道已读游标请求无效",
        StatusCodes.Status400BadRequest,
        "Chat.ReadStateInvalidArgument",
        "error.chat.read_state_invalid_argument");

    private static BusinessException ReadTargetUnavailable() => new(
        "消息不存在或当前不可访问",
        StatusCodes.Status404NotFound,
        "Chat.ReadTargetUnavailable",
        "error.chat.read_target_unavailable");

    private static BusinessException ReceiptInvalidArgument() => new(
        "消息阅读回执请求无效",
        StatusCodes.Status400BadRequest,
        "Chat.ReceiptInvalidArgument",
        "error.chat.receipt_invalid_argument");

    private static BusinessException ReceiptTargetUnavailable() => new(
        "消息不存在、已撤回或当前不可访问",
        StatusCodes.Status404NotFound,
        "Chat.ReceiptTargetUnavailable",
        "error.chat.receipt_target_unavailable");

    private static BusinessException ReceiptMessageNotOwned() => new(
        "只能读取当前账号自己所发消息的阅读回执",
        StatusCodes.Status403Forbidden,
        "Chat.ReceiptMessageNotOwned",
        "error.chat.receipt_message_not_owned");

    private static BusinessException ReceiptNotSupported() => new(
        "当前频道类型不提供读者详情",
        StatusCodes.Status400BadRequest,
        "Chat.ReceiptNotSupported",
        "error.chat.receipt_not_supported");

    private static BusinessException ReceiptCursorInvalid() => new(
        "读者分页条件、账号或会话权限已经变化，请重新打开读者列表",
        StatusCodes.Status409Conflict,
        "Chat.ReceiptCursorInvalid",
        "error.chat.receipt_cursor_invalid");

    private sealed record ReaderCursorPayload(
        int Version,
        long TenantId,
        long UserId,
        long ChannelId,
        long MessageId,
        long LastUserId);
}
