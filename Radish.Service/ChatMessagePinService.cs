using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>聊天消息共享置顶权威服务。</summary>
public sealed class ChatMessagePinService : IChatMessagePinService
{
    private readonly IChatMessagePinRepository _pinRepository;
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IChatChannelAccessService _accessService;
    private readonly IChatService _chatService;
    private readonly TimeProvider _timeProvider;

    public ChatMessagePinService(
        IChatMessagePinRepository pinRepository,
        IChannelMessageRepository messageRepository,
        IChatChannelAccessService accessService,
        IChatService chatService,
        TimeProvider timeProvider)
    {
        _pinRepository = pinRepository;
        _messageRepository = messageRepository;
        _accessService = accessService;
        _chatService = chatService;
        _timeProvider = timeProvider;
    }

    public async Task<ChatMessagePinStateVo> GetStateAsync(long tenantId, long userId, long channelId)
    {
        await RequireReadableChannelAsync(tenantId, userId, channelId);
        return await BuildStateAsync(tenantId, userId, channelId);
    }

    public async Task<ChatMessagePinMutationVo> SetAsync(
        long tenantId,
        long userId,
        string userName,
        bool canManageChannel,
        SetChatMessagePinDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0 || request.ChannelId <= 0 || request.MessageId <= 0)
        {
            throw InvalidArgument();
        }

        var access = await RequireReadableChannelAsync(
            tenantId,
            userId,
            request.ChannelId,
            canManageChannel);
        if (!access.CanPinMessages)
        {
            throw new BusinessException(
                "当前账号无权管理该会话的消息置顶",
                StatusCodes.Status403Forbidden,
                "Chat.PinNotAllowed",
                "error.chat.pin_not_allowed");
        }

        var message = await _messageRepository.QueryFirstIncludingDeletedAsync(candidate =>
            candidate.Id == request.MessageId &&
            candidate.ChannelId == request.ChannelId);
        if (message == null || message.IsDeleted ||
            message.TenantId != 0 && message.TenantId != tenantId)
        {
            throw TargetUnavailable();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();
        ChatMessagePinWriteResult result;
        try
        {
            result = await _pinRepository.SetAsync(new ChatMessagePinSetCommand(
                message.TenantId,
                request.ChannelId,
                request.MessageId,
                userId,
                normalizedUserName,
                request.IsPinned,
                now));
        }
        catch (ChatMessagePinTargetUnavailableException)
        {
            throw TargetUnavailable();
        }
        catch (ChatMessagePinLimitExceededException)
        {
            throw new BusinessException(
                "每个频道最多保留 20 条置顶消息",
                StatusCodes.Status400BadRequest,
                "Chat.PinLimitExceeded",
                "error.chat.pin_limit_exceeded");
        }
        catch (ChatMessagePinConcurrentConflictException)
        {
            throw new BusinessException(
                "置顶状态已被其他请求更新，请刷新后重试",
                StatusCodes.Status409Conflict,
                "Chat.PinConcurrentConflict",
                "error.chat.pin_concurrent_conflict");
        }

        var state = await BuildStateAsync(tenantId, userId, request.ChannelId);
        if (state.VoRevision < result.Revision)
        {
            throw new InvalidOperationException("消息置顶权威快照 revision 落后于写入结果。");
        }

        return new ChatMessagePinMutationVo
        {
            VoState = state,
            VoChanged = result.Changed
        };
    }

    private async Task<ChatChannelAccessResult> RequireReadableChannelAsync(
        long tenantId,
        long userId,
        long channelId,
        bool canManageChannel = false)
    {
        if (userId <= 0 || channelId <= 0)
        {
            throw InvalidArgument();
        }

        var access = await _accessService.GetAccessAsync(
            tenantId,
            userId,
            channelId,
            canManageChannel);
        if (!access.CanView)
        {
            throw TargetUnavailable();
        }

        return access;
    }

    private async Task<ChatMessagePinStateVo> BuildStateAsync(
        long tenantId,
        long userId,
        long channelId)
    {
        var snapshot = await _pinRepository.GetSnapshotAsync(tenantId, channelId);
        if (snapshot == null)
        {
            throw TargetUnavailable();
        }

        if (snapshot.Items.Count == 0)
        {
            return new ChatMessagePinStateVo
            {
                VoChannelId = channelId,
                VoRevision = snapshot.Revision
            };
        }

        var messageIds = snapshot.Items.Select(pin => pin.MessageId).ToList();
        var messages = await _chatService.GetMessagesByIdsAsync(
            tenantId,
            userId,
            channelId,
            messageIds);
        if (messages.Count != messageIds.Count)
        {
            throw new InvalidOperationException("消息置顶状态引用了不可用消息。");
        }

        var messageMap = messages.ToDictionary(message => message.VoId);
        return new ChatMessagePinStateVo
        {
            VoChannelId = channelId,
            VoRevision = snapshot.Revision,
            VoItems = snapshot.Items.Select(pin => new ChatMessagePinVo
            {
                VoId = pin.Id,
                VoMessageId = pin.MessageId,
                VoMessage = messageMap[pin.MessageId],
                VoPinnedByUserId = pin.PinnedByUserId,
                VoPinnedByName = pin.PinnedByName,
                VoPinnedAt = pin.PinnedAt
            }).ToList()
        };
    }

    private static BusinessException InvalidArgument() => new(
        "消息置顶请求无效",
        StatusCodes.Status400BadRequest,
        "Chat.PinInvalidArgument",
        "error.chat.pin_invalid_argument");

    private static BusinessException TargetUnavailable() => new(
        "消息不存在或当前不可访问",
        StatusCodes.Status404NotFound,
        "Chat.PinTargetUnavailable",
        "error.chat.pin_target_unavailable");
}
