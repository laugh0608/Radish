using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

/// <summary>Chat 历史消息检索编排、权限快照与 cursor 契约。</summary>
public sealed class ChatMessageSearchService : IChatMessageSearchService
{
    private const int CursorVersion = 1;
    private const int MinimumKeywordLength = 2;
    private const int MaximumKeywordLength = 100;
    private const int MaximumPageSize = 50;
    private const int SnippetRadius = 80;
    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IChannelMessageSearchRepository _searchRepository;
    private readonly IChatChannelAccessService _channelAccessService;
    private readonly IDirectConversationService _directConversationService;
    private readonly IBaseRepository<Channel> _channelRepository;
    private readonly IAttachmentUrlResolver _attachmentUrlResolver;
    private readonly ILogger<ChatMessageSearchService> _logger;

    public ChatMessageSearchService(
        IChannelMessageSearchRepository searchRepository,
        IChatChannelAccessService channelAccessService,
        IDirectConversationService directConversationService,
        IBaseRepository<Channel> channelRepository,
        IAttachmentUrlResolver attachmentUrlResolver,
        ILogger<ChatMessageSearchService> logger)
    {
        _searchRepository = searchRepository;
        _channelAccessService = channelAccessService;
        _directConversationService = directConversationService;
        _channelRepository = channelRepository;
        _attachmentUrlResolver = attachmentUrlResolver;
        _logger = logger;
    }

    public async Task<ChannelMessageSearchPageVo> SearchAsync(
        long tenantId,
        long userId,
        SearchChannelMessagesDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId <= 0)
        {
            throw new BusinessException(
                "当前账号无效",
                StatusCodes.Status401Unauthorized,
                "Auth.Unauthorized",
                "error.auth.unauthorized");
        }

        var normalizedKeyword = ChatMessageSearchTextNormalizer.Normalize(request.Keyword);
        if (normalizedKeyword == null ||
            normalizedKeyword.Length < MinimumKeywordLength ||
            normalizedKeyword.Length > MaximumKeywordLength)
        {
            throw ValidationError(
                "搜索关键词规范化后长度必须为 2-100 个字符",
                "Chat.SearchKeywordInvalid",
                "error.chat.search_keyword_invalid");
        }

        var fromUtc = NormalizeUtc(request.FromUtc);
        var toUtc = NormalizeUtc(request.ToUtc);
        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value >= toUtc.Value)
        {
            throw ValidationError(
                "搜索开始时间必须早于结束时间",
                "Chat.SearchTimeRangeInvalid",
                "error.chat.search_time_range_invalid");
        }

        var pageSize = request.PageSize;
        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw ValidationError(
                "pageSize 必须为 1-50",
                "Chat.SearchPageSizeInvalid",
                "error.chat.search_page_size_invalid");
        }

        var channels = await ResolveReadableChannelsAsync(tenantId, userId, request);
        var channelIds = channels.Keys.OrderBy(id => id).ToList();
        var visibleChannelSetHash = ComputeHash(string.Join(',', channelIds));
        var queryFingerprint = ComputeQueryFingerprint(
            tenantId,
            userId,
            request.Scope,
            request.ChannelId,
            normalizedKeyword,
            fromUtc,
            toUtc);
        var cursor = string.IsNullOrWhiteSpace(request.Cursor)
            ? null
            : DecodeCursor(request.Cursor);

        if (cursor != null &&
            (cursor.Version != CursorVersion ||
             !string.Equals(cursor.QueryFingerprint, queryFingerprint, StringComparison.Ordinal) ||
             !string.Equals(cursor.VisibleChannelSetHash, visibleChannelSetHash, StringComparison.Ordinal) ||
             cursor.SnapshotMaxMessageId < 0 ||
             cursor.LastMessageId <= 0))
        {
            throw CursorInvalid();
        }

        var snapshotMaxMessageId = cursor?.SnapshotMaxMessageId ??
                                   await _searchRepository.GetSnapshotMaxMessageIdAsync(tenantId, channelIds);
        var stopwatch = Stopwatch.StartNew();
        var messages = await _searchRepository.SearchAsync(new ChannelMessageSearchQuery(
            tenantId,
            channelIds,
            normalizedKeyword,
            fromUtc,
            toUtc,
            snapshotMaxMessageId,
            cursor?.LastCreateTimeUtc,
            cursor?.LastMessageId,
            pageSize + 1));
        var hasMore = messages.Count > pageSize;
        var pageMessages = messages.Take(pageSize).ToList();
        var directSummaries = await _directConversationService.GetChannelSummariesAsync(
            tenantId,
            userId,
            pageMessages.Select(message => message.ChannelId).Distinct().ToList());
        var items = pageMessages
            .Select(message => MapItem(message, normalizedKeyword, channels[message.ChannelId], directSummaries))
            .ToList();
        string? nextCursor = null;
        if (hasMore && pageMessages.Count > 0)
        {
            var last = pageMessages[^1];
            nextCursor = EncodeCursor(new SearchCursorPayload(
                CursorVersion,
                queryFingerprint,
                snapshotMaxMessageId,
                visibleChannelSetHash,
                last.CreateTime,
                last.Id));
        }

        _logger.LogInformation(
            "Chat search completed for scope {Scope}; page size {PageSize}, result count {ResultCount}, duration {DurationMs} ms",
            request.Scope,
            pageSize,
            items.Count,
            stopwatch.ElapsedMilliseconds);

        return new ChannelMessageSearchPageVo
        {
            VoItems = items,
            VoNextCursor = nextCursor,
            VoHasMore = hasMore
        };
    }

    private async Task<Dictionary<long, ReadableChatChannelSnapshotItem>> ResolveReadableChannelsAsync(
        long tenantId,
        long userId,
        SearchChannelMessagesDto request)
    {
        if (request.Scope == ChatMessageSearchScope.CurrentChannel)
        {
            if (request.ChannelId is not > 0)
            {
                throw ScopeInvalid();
            }

            var access = await _channelAccessService.GetAccessAsync(tenantId, userId, request.ChannelId.Value);
            if (!access.CanView)
            {
                if (!string.IsNullOrWhiteSpace(request.Cursor))
                {
                    throw CursorInvalid();
                }

                throw new BusinessException(
                    "会话不存在或当前账号无权访问",
                    StatusCodes.Status404NotFound,
                    "Chat.ChannelUnavailable",
                    "error.chat.channel_unavailable");
            }

            var channel = await _channelRepository.QueryFirstAsync(candidate =>
                candidate.Id == request.ChannelId.Value && candidate.IsEnabled && !candidate.IsDeleted);
            if (channel == null)
            {
                if (!string.IsNullOrWhiteSpace(request.Cursor))
                {
                    throw CursorInvalid();
                }

                throw new BusinessException(
                    "会话不存在或当前账号无权访问",
                    StatusCodes.Status404NotFound,
                    "Chat.ChannelUnavailable",
                    "error.chat.channel_unavailable");
            }

            return new Dictionary<long, ReadableChatChannelSnapshotItem>
            {
                [channel.Id] = new(
                    channel.Id,
                    channel.Name,
                    channel.IconEmoji,
                    channel.Type,
                    access)
            };
        }

        if (request.Scope != ChatMessageSearchScope.AllVisibleChannels || request.ChannelId.HasValue)
        {
            throw ScopeInvalid();
        }

        var snapshot = await _channelAccessService.GetReadableChannelSnapshotAsync(tenantId, userId);
        return snapshot.ToDictionary(channel => channel.ChannelId);
    }

    private ChannelMessageSearchItemVo MapItem(
        ChannelMessage message,
        string normalizedKeyword,
        ReadableChatChannelSnapshotItem channel,
        IReadOnlyDictionary<long, DirectConversationVo> directSummaries)
    {
        directSummaries.TryGetValue(message.ChannelId, out var direct);
        return new ChannelMessageSearchItemVo
        {
            VoChannelId = message.ChannelId,
            VoMessageId = message.Id,
            VoChannelDisplayName = direct?.VoPeerDisplayName ?? channel.ChannelName,
            VoChannelIcon = direct?.VoPeerAvatarUrl ?? channel.ChannelIcon,
            VoConversationKind = direct?.VoConversationKind ??
                                 (channel.ChannelType == ChannelType.Private ? "group" : "public"),
            VoPeerUserId = direct?.VoPeerUserId,
            VoPeerPublicId = direct?.VoPeerPublicId,
            VoPeerAvatarUrl = direct?.VoPeerAvatarUrl,
            VoSenderUserId = message.UserId,
            VoSenderDisplayName = User.NormalizeDisplayName(message.UserName, message.UserId),
            VoSenderAvatarUrl = message.UserAvatarAttachmentIdSnapshot.HasValue
                ? _attachmentUrlResolver.ResolveAttachmentUrl(message.UserAvatarAttachmentIdSnapshot.Value)
                : null,
            VoSnippet = BuildSnippet(message.Content, normalizedKeyword),
            VoCreateTime = message.CreateTime,
            VoMessageType = message.Type
        };
    }

    private static string BuildSnippet(string? content, string normalizedKeyword)
    {
        var visibleText = ChatMessageSearchTextNormalizer.ToVisibleText(content) ?? string.Empty;
        var normalizedVisibleText = visibleText.ToLowerInvariant();
        var matchIndex = normalizedVisibleText.IndexOf(normalizedKeyword, StringComparison.Ordinal);
        if (matchIndex < 0 || visibleText.Length <= SnippetRadius * 2)
        {
            return visibleText;
        }

        var start = Math.Max(0, matchIndex - SnippetRadius);
        var end = Math.Min(visibleText.Length, matchIndex + normalizedKeyword.Length + SnippetRadius);
        var snippet = visibleText[start..end];
        return $"{(start > 0 ? "…" : string.Empty)}{snippet}{(end < visibleText.Length ? "…" : string.Empty)}";
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind == DateTimeKind.Utc
            ? value.Value
            : value.Value.ToUniversalTime();
    }

    private static string ComputeQueryFingerprint(
        long tenantId,
        long userId,
        ChatMessageSearchScope scope,
        long? channelId,
        string normalizedKeyword,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var canonical = string.Join('|',
            tenantId.ToString(CultureInfo.InvariantCulture),
            userId.ToString(CultureInfo.InvariantCulture),
            ((int)scope).ToString(CultureInfo.InvariantCulture),
            channelId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            normalizedKeyword,
            fromUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
            toUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        return ComputeHash(canonical);
    }

    private static string ComputeHash(string source)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static string EncodeCursor(SearchCursorPayload cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, CursorJsonOptions);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static SearchCursorPayload DecodeCursor(string source)
    {
        try
        {
            var normalized = source.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight((normalized.Length + 3) / 4 * 4, '=');
            return JsonSerializer.Deserialize<SearchCursorPayload>(
                       Convert.FromBase64String(normalized),
                       CursorJsonOptions)
                   ?? throw CursorInvalid();
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or NotSupportedException)
        {
            throw CursorInvalid();
        }
    }

    private static BusinessException ScopeInvalid()
    {
        return ValidationError(
            "搜索范围与频道参数组合无效",
            "Chat.SearchScopeInvalid",
            "error.chat.search_scope_invalid");
    }

    private static BusinessException CursorInvalid()
    {
        return new BusinessException(
            "搜索条件、账号或会话权限已经变化，请重新搜索",
            StatusCodes.Status409Conflict,
            "Chat.SearchCursorInvalid",
            "error.chat.search_cursor_invalid");
    }

    private static BusinessException ValidationError(string message, string code, string messageKey)
    {
        return new BusinessException(message, StatusCodes.Status400BadRequest, code, messageKey);
    }

    private sealed record SearchCursorPayload(
        int Version,
        string QueryFingerprint,
        long SnapshotMaxMessageId,
        string VisibleChannelSetHash,
        DateTime LastCreateTimeUtc,
        long LastMessageId);
}
