using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Serilog;

namespace Radish.Service;

/// <summary>组合当前权威来源的匿名公开发现流。</summary>
public sealed class PublicDiscoverService : IPublicDiscoverService
{
    private const int CursorVersion = 1;
    private const long PublicTenantId = 0;
    private const int MaxCursorLength = 2048;
    private const string PostPublicIdPrefix = "pst_";
    private static readonly TimeSpan PulseWindow = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkPattern = new(@"!?\[([^\]]+)\]\([^\)]+\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownSymbolPattern = new(@"[#>*_`~!\-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ControlCharacterPattern = new(@"\p{Cc}+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IPublicDiscoverRepository _repository;
    private readonly IPublicDiscoverChannelRepository _channelRepository;
    private readonly TimeProvider _timeProvider;

    public PublicDiscoverService(
        IPublicDiscoverRepository repository,
        IPublicDiscoverChannelRepository channelRepository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _channelRepository = channelRepository;
        _timeProvider = timeProvider;
    }

    public async Task<PublicDiscoverFeedVo> GetFeedAsync(string? cursor, int pageSize)
    {
        if (pageSize is < 1 or > 50)
        {
            throw new BusinessException(
                "公开发现页大小必须在 1 到 50 之间",
                StatusCodes.Status400BadRequest,
                "PublicDiscover.PageSizeInvalid",
                "error.public_discover.page_size_invalid");
        }

        var generatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var cursorState = DecodeCursor(cursor, generatedAtUtc);
        var snapshotCutoffUtc = cursorState?.SnapshotCutoffUtc ?? generatedAtUtc;
        var windowStartedAtUtc = snapshotCutoffUtc.Subtract(PulseWindow);
        var sourceWindow = new PublicDiscoverSourceWindow(
            PublicTenantId,
            snapshotCutoffUtc,
            cursorState?.LastOccurredAtUtc,
            cursorState?.LastKindOrder,
            cursorState?.LastSourceId,
            pageSize + 1);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var channelTask = _channelRepository.QueryChannelSummariesAsync(sourceWindow, windowStartedAtUtc);
            var memberActivityTask = _repository.QueryMemberActivitiesAsync(sourceWindow);
            var highlightedCommentTask = _repository.QueryHighlightedCommentsAsync(sourceWindow);
            var postTask = _repository.QueryPostsAsync(sourceWindow);
            var questionTask = _repository.QueryQuestionsAsync(sourceWindow);
            var channelPulseTask = _channelRepository.QueryPulseAsync(
                PublicTenantId,
                windowStartedAtUtc,
                snapshotCutoffUtc);
            var mainPulseTask = _repository.QueryPulseAsync(
                PublicTenantId,
                windowStartedAtUtc,
                snapshotCutoffUtc);

            await Task.WhenAll(
                channelTask,
                memberActivityTask,
                highlightedCommentTask,
                postTask,
                questionTask,
                channelPulseTask,
                mainPulseTask);

            var candidates = channelTask.Result
                .Concat(memberActivityTask.Result)
                .Concat(highlightedCommentTask.Result)
                .Concat(postTask.Result)
                .Concat(questionTask.Result)
                .Where(candidate => IsValidCandidate(candidate, sourceWindow))
                .OrderByDescending(candidate => NormalizeUtc(candidate.OccurredAtUtc))
                .ThenBy(candidate => PublicDiscoverKindOrder.FromKind(candidate.Kind))
                .ThenByDescending(candidate => candidate.SourceId)
                .Take(pageSize + 1)
                .ToList();
            var hasMore = candidates.Count > pageSize;
            var pageCandidates = candidates.Take(pageSize).ToList();
            var items = pageCandidates.Select(MapItem).ToList();
            string? nextCursor = null;
            if (hasMore && pageCandidates.Count > 0)
            {
                var last = pageCandidates[^1];
                nextCursor = EncodeCursor(new PublicDiscoverCursorState
                {
                    Version = CursorVersion,
                    SnapshotCutoffUtc = snapshotCutoffUtc,
                    LastOccurredAtUtc = NormalizeUtc(last.OccurredAtUtc),
                    LastKindOrder = PublicDiscoverKindOrder.FromKind(last.Kind),
                    LastSourceId = last.SourceId
                });
            }

            var channelPulse = channelPulseTask.Result;
            var mainPulse = mainPulseTask.Result;
            Log.Information(
                "公开发现流生成完成，TenantId={TenantId}, CursorVersion={CursorVersion}, PageSize={PageSize}, " +
                "ChannelCandidates={ChannelCandidates}, MemberCandidates={MemberCandidates}, " +
                "HighlightCandidates={HighlightCandidates}, PostCandidates={PostCandidates}, " +
                "QuestionCandidates={QuestionCandidates}, Returned={Returned}, HasMore={HasMore}, ElapsedMs={ElapsedMs}",
                PublicTenantId,
                cursorState?.Version ?? CursorVersion,
                pageSize,
                channelTask.Result.Count,
                memberActivityTask.Result.Count,
                highlightedCommentTask.Result.Count,
                postTask.Result.Count,
                questionTask.Result.Count,
                items.Count,
                hasMore,
                stopwatch.ElapsedMilliseconds);
            return new PublicDiscoverFeedVo
            {
                VoItems = items,
                VoPulse = new PublicDiscoverPulseVo
                {
                    VoWindowStartedAtUtc = windowStartedAtUtc,
                    VoWindowEndedAtUtc = snapshotCutoffUtc,
                    VoDiscoverableChannelCount = channelPulse.DiscoverableChannelCount,
                    VoEligibleItemCount = channelPulse.RecentChannelItemCount + mainPulse.EligibleItemCount,
                    VoKnowledgeContributionCount = mainPulse.KnowledgeContributionCount
                },
                VoNextCursor = nextCursor,
                VoHasMore = hasMore,
                VoGeneratedAtUtc = generatedAtUtc
            };
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Log.Error(
                exception,
                "公开发现来源查询失败，TenantId={TenantId}, CursorVersion={CursorVersion}, PageSize={PageSize}, ElapsedMs={ElapsedMs}",
                PublicTenantId,
                cursorState?.Version ?? CursorVersion,
                pageSize,
                stopwatch.ElapsedMilliseconds);
            throw new BusinessException(
                "公开发现内容暂时不可用，请稍后重试",
                StatusCodes.Status503ServiceUnavailable,
                "PublicDiscover.SourceUnavailable",
                "error.public_discover.source_unavailable");
        }
    }

    private static bool IsValidCandidate(
        PublicDiscoverSourceProjection candidate,
        PublicDiscoverSourceWindow window)
    {
        if (candidate.SourceId <= 0 ||
            string.IsNullOrWhiteSpace(candidate.Title) ||
            NormalizeUtc(candidate.OccurredAtUtc) > window.SnapshotCutoffUtc ||
            !HasValidTarget(candidate))
        {
            return false;
        }

        if (!window.LastOccurredAtUtc.HasValue ||
            !window.LastKindOrder.HasValue ||
            !window.LastSourceId.HasValue)
        {
            return true;
        }

        var occurredAtUtc = NormalizeUtc(candidate.OccurredAtUtc);
        var lastOccurredAtUtc = window.LastOccurredAtUtc.Value;
        var kindOrder = PublicDiscoverKindOrder.FromKind(candidate.Kind);
        return occurredAtUtc < lastOccurredAtUtc ||
               occurredAtUtc == lastOccurredAtUtc &&
               (kindOrder > window.LastKindOrder.Value ||
                kindOrder == window.LastKindOrder.Value && candidate.SourceId < window.LastSourceId.Value);
    }

    private static bool HasValidTarget(PublicDiscoverSourceProjection candidate)
    {
        return candidate.TargetKind switch
        {
            PublicDiscoverTargetKind.Messages => candidate.ChannelId is > 0,
            PublicDiscoverTargetKind.Docs => !string.IsNullOrWhiteSpace(candidate.DocumentSlug),
            PublicDiscoverTargetKind.ForumPost =>
                HasPostPublicIdFormat(candidate.PostPublicId) &&
                (candidate.Kind != PublicDiscoverItemKind.HighlightedComment || candidate.CommentId is > 0),
            _ => false
        };
    }

    private static PublicDiscoverItemVo MapItem(PublicDiscoverSourceProjection source)
    {
        var actor = User.HasPublicIdFormat(source.ActorPublicId)
            ? new PublicDiscoverActorVo
            {
                VoPublicId = source.ActorPublicId!.Trim().ToLowerInvariant(),
                VoDisplayName = NormalizePlainText(source.ActorDisplayName, 80, "成员")
            }
            : null;
        return new PublicDiscoverItemVo
        {
            VoKey = $"{source.Kind.ToString().ToLowerInvariant()}:{source.SourceId.ToString(CultureInfo.InvariantCulture)}",
            VoKind = source.Kind,
            VoOccurredAtUtc = NormalizeUtc(source.OccurredAtUtc),
            VoTitle = NormalizePlainText(source.Title, 160, "公开内容"),
            VoSummary = NormalizePlainText(source.Summary, 260, string.Empty),
            VoActor = actor,
            VoTarget = new PublicDiscoverTargetVo
            {
                VoKind = source.TargetKind,
                VoChannelId = source.ChannelId?.ToString(CultureInfo.InvariantCulture),
                VoDocumentSlug = NormalizeOptionalIdentifier(source.DocumentSlug),
                VoPostPublicId = NormalizeOptionalIdentifier(source.PostPublicId),
                VoCommentId = source.CommentId?.ToString(CultureInfo.InvariantCulture),
                VoRequiresAuthentication = source.RequiresAuthentication
            },
            VoPrimaryMetric = source.MetricKind.HasValue && source.MetricValue.HasValue
                ? new PublicDiscoverMetricVo
                {
                    VoKind = source.MetricKind.Value,
                    VoValue = Math.Max(0, source.MetricValue.Value)
                }
                : null
        };
    }

    private static PublicDiscoverCursorState? DecodeCursor(string? cursor, DateTime generatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        var normalized = cursor.Trim();
        if (normalized.Length > MaxCursorLength)
        {
            throw CursorInvalid();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PublicDiscoverCursorState>(
                DecodeBase64Url(normalized),
                CursorJsonOptions);
            if (payload == null ||
                payload.Version != CursorVersion ||
                payload.SnapshotCutoffUtc.Kind != DateTimeKind.Utc ||
                payload.LastOccurredAtUtc.Kind != DateTimeKind.Utc ||
                payload.SnapshotCutoffUtc == default ||
                payload.LastOccurredAtUtc == default ||
                payload.SnapshotCutoffUtc > generatedAtUtc.AddMinutes(5) ||
                payload.LastOccurredAtUtc > payload.SnapshotCutoffUtc ||
                payload.LastKindOrder is < PublicDiscoverKindOrder.ChannelSummary or > PublicDiscoverKindOrder.Question ||
                payload.LastSourceId <= 0)
            {
                throw CursorInvalid();
            }

            return payload;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch
        {
            throw CursorInvalid();
        }
    }

    private static string EncodeCursor(PublicDiscoverCursorState cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor, CursorJsonOptions);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException("Invalid Base64Url length.")
        };
        return Convert.FromBase64String(base64);
    }

    private static string NormalizePlainText(string? value, int maxTextElements, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = WebUtility.HtmlDecode(value);
        text = HtmlTagPattern.Replace(text, " ");
        text = MarkdownLinkPattern.Replace(text, "$1");
        text = MarkdownSymbolPattern.Replace(text, " ");
        text = ControlCharacterPattern.Replace(text, " ");
        text = WhitespacePattern.Replace(text, " ").Trim();
        return string.IsNullOrWhiteSpace(text)
            ? fallback
            : TruncateByTextElements(text, maxTextElements);
    }

    private static string TruncateByTextElements(string value, int maxTextElements)
    {
        var elementIndexes = StringInfo.ParseCombiningCharacters(value);
        if (elementIndexes.Length <= maxTextElements)
        {
            return value;
        }

        var boundary = elementIndexes[Math.Max(1, maxTextElements) - 1];
        return string.Concat(value.AsSpan(0, boundary), "…");
    }

    private static string? NormalizeOptionalIdentifier(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static bool HasPostPublicIdFormat(string? value)
    {
        var normalized = value?.Trim();
        return normalized is { Length: 36 } &&
               normalized.StartsWith(PostPublicIdPrefix, StringComparison.OrdinalIgnoreCase) &&
               normalized[PostPublicIdPrefix.Length..].All(Uri.IsHexDigit);
    }

    private static BusinessException CursorInvalid() => new(
        "公开发现游标无效，请刷新页面后重试",
        StatusCodes.Status400BadRequest,
        "PublicDiscover.CursorInvalid",
        "error.public_discover.cursor_invalid");

    private sealed class PublicDiscoverCursorState
    {
        public int Version { get; set; }

        public DateTime SnapshotCutoffUtc { get; set; }

        public DateTime LastOccurredAtUtc { get; set; }

        public int LastKindOrder { get; set; }

        public long LastSourceId { get; set; }
    }
}
