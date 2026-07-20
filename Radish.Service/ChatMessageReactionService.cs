using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.IRepository.Base;

namespace Radish.Service;

/// <summary>聊天消息回应权威服务。</summary>
public sealed class ChatMessageReactionService : IChatMessageReactionService
{
    private const int MaxBatchSize = 100;
    private static readonly TimeSpan OperationRetention = TimeSpan.FromDays(30);

    private readonly IChatMessageReactionRepository _reactionRepository;
    private readonly IChannelMessageRepository _messageRepository;
    private readonly IChatChannelAccessService _accessService;
    private readonly IBaseRepository<StickerGroup> _stickerGroupRepository;
    private readonly IBaseRepository<Sticker> _stickerRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly TimeProvider _timeProvider;

    public ChatMessageReactionService(
        IChatMessageReactionRepository reactionRepository,
        IChannelMessageRepository messageRepository,
        IChatChannelAccessService accessService,
        IBaseRepository<StickerGroup> stickerGroupRepository,
        IBaseRepository<Sticker> stickerRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        TimeProvider timeProvider)
    {
        _reactionRepository = reactionRepository;
        _messageRepository = messageRepository;
        _accessService = accessService;
        _stickerGroupRepository = stickerGroupRepository;
        _stickerRepository = stickerRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _timeProvider = timeProvider;
    }

    public async Task<List<ChatMessageReactionStateVo>> GetStatesAsync(
        long tenantId,
        long userId,
        GetChatMessageReactionStatesDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var messageIds = NormalizeMessageIds(request.MessageIds);
        var access = await RequireReadableChannelAsync(tenantId, userId, request.ChannelId);
        _ = access;

        var messages = await RequireMessagesAsync(tenantId, request.ChannelId, messageIds);
        return await BuildStatesAsync(messages, userId);
    }

    public async Task<ChatMessageReactionMutationVo> SetAsync(
        long tenantId,
        long userId,
        string userName,
        SetChatMessageReactionDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0 || request.ChannelId <= 0 || request.MessageId <= 0)
        {
            throw InvalidArgument();
        }

        var operationId = request.ClientOperationId?.Trim();
        if (operationId is null || operationId.Length is < 8 or > 100)
        {
            throw InvalidArgument();
        }

        var emojiType = NormalizeEmojiType(request.EmojiType);
        var emojiValue = NormalizeEmojiValue(emojiType, request.EmojiValue);
        var access = await RequireReadableChannelAsync(tenantId, userId, request.ChannelId);
        if (!access.CanReact)
        {
            throw new BusinessException(
                "当前会话状态不允许回应消息",
                StatusCodes.Status403Forbidden,
                "Chat.ReactionNotAllowed",
                "error.chat.reaction_not_allowed");
        }

        var messages = await RequireMessagesAsync(tenantId, request.ChannelId, [request.MessageId]);
        var message = messages[0];
        var stickerAttachmentId = emojiType == "sticker"
            ? await ResolveStickerAttachmentIdAsync(tenantId, emojiValue)
            : null;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? "System" : userName.Trim();

        ChatMessageReactionWriteResult result;
        try
        {
            result = await _reactionRepository.SetAsync(new ChatMessageReactionSetCommand(
                message.TenantId,
                request.ChannelId,
                request.MessageId,
                userId,
                normalizedUserName,
                emojiType,
                emojiValue,
                stickerAttachmentId,
                request.IsActive,
                operationId,
                now,
                now.Add(OperationRetention)));
        }
        catch (ChatMessageReactionIdempotencyConflictException)
        {
            throw new BusinessException(
                "operation ID 已用于其他回应请求",
                StatusCodes.Status409Conflict,
                "Chat.ReactionIdempotencyConflict",
                "error.chat.reaction_idempotency_conflict");
        }
        catch (ChatMessageReactionTargetUnavailableException)
        {
            throw TargetUnavailable();
        }
        catch (ChatMessageReactionLimitExceededException)
        {
            throw new BusinessException(
                "单条消息最多保留 10 种回应",
                StatusCodes.Status400BadRequest,
                "Chat.ReactionLimitExceeded",
                "error.chat.reaction_limit_exceeded");
        }
        catch (ChatMessageReactionConcurrentConflictException)
        {
            throw new BusinessException(
                "回应状态已被其他请求更新，请刷新后重试",
                StatusCodes.Status409Conflict,
                "Chat.ReactionConcurrentConflict",
                "error.chat.reaction_concurrent_conflict");
        }

        var currentMessages = await RequireMessagesAsync(tenantId, request.ChannelId, [request.MessageId]);
        var state = (await BuildStatesAsync(currentMessages, userId))[0];
        return new ChatMessageReactionMutationVo
        {
            VoState = state,
            VoChanged = result.Changed,
            VoReplayed = result.Replayed
        };
    }

    private async Task<ChatChannelAccessResult> RequireReadableChannelAsync(
        long tenantId,
        long userId,
        long channelId)
    {
        if (userId <= 0 || channelId <= 0)
        {
            throw InvalidArgument();
        }

        var access = await _accessService.GetAccessAsync(tenantId, userId, channelId);
        if (!access.CanView)
        {
            throw TargetUnavailable();
        }

        return access;
    }

    private async Task<List<ChannelMessage>> RequireMessagesAsync(
        long tenantId,
        long channelId,
        IReadOnlyCollection<long> messageIds)
    {
        var messages = await _messageRepository.QueryByIdsIncludingDeletedAsync(messageIds.ToList());
        var messageMap = messages.ToDictionary(message => message.Id);
        if (messageMap.Count != messageIds.Count || messageIds.Any(messageId =>
                !messageMap.TryGetValue(messageId, out var message) ||
                message.ChannelId != channelId ||
                message.IsDeleted ||
                message.TenantId != 0 && message.TenantId != tenantId))
        {
            throw TargetUnavailable();
        }

        return messageIds.Select(messageId => messageMap[messageId]).ToList();
    }

    private async Task<List<ChatMessageReactionStateVo>> BuildStatesAsync(
        IReadOnlyList<ChannelMessage> messages,
        long currentUserId)
    {
        var groupedByTenant = messages.GroupBy(message => message.TenantId);
        var reactionMap = new Dictionary<long, List<ChatMessageReaction>>();
        foreach (var group in groupedByTenant)
        {
            var reactions = await _reactionRepository.QueryActiveByMessageIdsAsync(
                group.Key,
                group.Select(message => message.Id).ToList());
            foreach (var reactionGroup in reactions.GroupBy(reaction => reaction.MessageId))
            {
                reactionMap[reactionGroup.Key] = reactionGroup.ToList();
            }
        }

        return messages.Select(message => new ChatMessageReactionStateVo
        {
            VoMessageId = message.Id,
            VoRevision = message.ReactionRevision,
            VoItems = BuildSummary(
                reactionMap.GetValueOrDefault(message.Id) ?? [],
                currentUserId)
        }).ToList();
    }

    private List<ReactionSummaryVo> BuildSummary(
        IReadOnlyCollection<ChatMessageReaction> reactions,
        long currentUserId)
    {
        return reactions
            .GroupBy(reaction => new { reaction.EmojiType, reaction.EmojiValue })
            .Select(group => new ReactionSummaryVo
            {
                VoEmojiType = group.Key.EmojiType,
                VoEmojiValue = group.Key.EmojiValue,
                VoCount = group.Count(),
                VoIsReacted = group.Any(reaction => reaction.UserId == currentUserId),
                VoThumbnailUrl = group
                    .Select(reaction => ResolveThumbnailUrl(reaction.StickerAttachmentId))
                    .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
            })
            .OrderByDescending(item => item.VoCount)
            .ThenBy(item => item.VoEmojiValue, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<long?> ResolveStickerAttachmentIdAsync(long tenantId, string emojiValue)
    {
        var segments = emojiValue.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            throw InvalidArgument();
        }

        var groupCode = segments[0];
        var stickerCode = segments[1];
        var group = await _stickerGroupRepository.QueryFirstAsync(candidate =>
            (candidate.TenantId == tenantId || candidate.TenantId == 0) &&
            candidate.Code == groupCode &&
            candidate.IsEnabled &&
            !candidate.IsDeleted);
        if (group == null)
        {
            throw StickerUnavailable();
        }

        var sticker = await _stickerRepository.QueryFirstAsync(candidate =>
            candidate.GroupId == group.Id &&
            candidate.Code == stickerCode &&
            candidate.IsEnabled &&
            !candidate.IsDeleted);
        if (sticker == null)
        {
            throw StickerUnavailable();
        }

        return sticker.AttachmentId;
    }

    private string? ResolveThumbnailUrl(long? attachmentId)
    {
        return attachmentId is > 0
            ? _attachmentUrlResolver.ResolveAttachmentUrl(attachmentId.Value, AttachmentUrlVariant.Thumbnail)
            : null;
    }

    private static List<long> NormalizeMessageIds(IReadOnlyCollection<long>? messageIds)
    {
        if (messageIds == null || messageIds.Count == 0 || messageIds.Count > MaxBatchSize)
        {
            throw InvalidArgument();
        }

        var normalized = messageIds.Where(id => id > 0).Distinct().ToList();
        if (normalized.Count != messageIds.Count)
        {
            throw InvalidArgument();
        }

        return normalized;
    }

    private static string NormalizeEmojiType(string? emojiType)
    {
        return emojiType?.Trim().ToLowerInvariant() switch
        {
            "unicode" => "unicode",
            "sticker" => "sticker",
            _ => throw InvalidArgument()
        };
    }

    private static string NormalizeEmojiValue(string emojiType, string? emojiValue)
    {
        var normalized = emojiValue?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 200)
        {
            throw InvalidArgument();
        }

        if (emojiType == "unicode")
        {
            return normalized;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2 || segments.Any(string.IsNullOrWhiteSpace))
        {
            throw InvalidArgument();
        }

        return $"{segments[0].ToLowerInvariant()}/{segments[1].ToLowerInvariant()}";
    }

    private static BusinessException InvalidArgument() => new(
        "消息回应请求无效",
        StatusCodes.Status400BadRequest,
        "Chat.ReactionInvalidArgument",
        "error.chat.reaction_invalid_argument");

    private static BusinessException TargetUnavailable() => new(
        "消息不存在或当前不可访问",
        StatusCodes.Status404NotFound,
        "Chat.ReactionTargetUnavailable",
        "error.chat.reaction_target_unavailable");

    private static BusinessException StickerUnavailable() => new(
        "表情不存在或不可用",
        StatusCodes.Status404NotFound,
        "Chat.ReactionStickerUnavailable",
        "error.chat.reaction_sticker_unavailable");
}
