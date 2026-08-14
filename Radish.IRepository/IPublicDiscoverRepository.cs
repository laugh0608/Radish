using Radish.Model.ViewModels;

namespace Radish.IRepository;

public static class PublicDiscoverKindOrder
{
    public const int ChannelSummary = 0;
    public const int MemberActivity = 1;
    public const int HighlightedComment = 2;
    public const int Post = 3;
    public const int Question = 4;

    public static int FromKind(PublicDiscoverItemKind kind) => kind switch
    {
        PublicDiscoverItemKind.ChannelSummary => ChannelSummary,
        PublicDiscoverItemKind.MemberActivity => MemberActivity,
        PublicDiscoverItemKind.HighlightedComment => HighlightedComment,
        PublicDiscoverItemKind.Post => Post,
        PublicDiscoverItemKind.Question => Question,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的公开发现来源类型。")
    };
}

/// <summary>单次公开发现请求的稳定快照与 keyset 窗口。</summary>
public sealed record PublicDiscoverSourceWindow(
    long TenantId,
    DateTime SnapshotCutoffUtc,
    DateTime? LastOccurredAtUtc,
    int? LastKindOrder,
    long? LastSourceId,
    int Take);

/// <summary>公开发现源的统一最小投影；不包含任意私域正文。</summary>
public sealed class PublicDiscoverSourceProjection
{
    public long SourceId { get; set; }

    public PublicDiscoverItemKind Kind { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? ActorPublicId { get; set; }

    public string? ActorDisplayName { get; set; }

    public PublicDiscoverTargetKind TargetKind { get; set; }

    public long? ChannelId { get; set; }

    public string? DocumentSlug { get; set; }

    public string? PostPublicId { get; set; }

    public long? CommentId { get; set; }

    public bool RequiresAuthentication { get; set; }

    public PublicDiscoverMetricKind? MetricKind { get; set; }

    public long? MetricValue { get; set; }
}

/// <summary>Main 库近 24 小时公开来源计数。</summary>
public sealed record PublicDiscoverMainPulseCounts(
    long EligibleItemCount,
    long KnowledgeContributionCount);

/// <summary>Chat 库公开频道计数。</summary>
public sealed record PublicDiscoverChannelPulseCounts(
    long DiscoverableChannelCount,
    long RecentChannelItemCount);

/// <summary>Main 库公开发现来源的资格与 keyset 查询边界。</summary>
public interface IPublicDiscoverRepository
{
    Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryMemberActivitiesAsync(
        PublicDiscoverSourceWindow window);

    Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryHighlightedCommentsAsync(
        PublicDiscoverSourceWindow window);

    Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryPostsAsync(
        PublicDiscoverSourceWindow window);

    Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryQuestionsAsync(
        PublicDiscoverSourceWindow window);

    Task<PublicDiscoverMainPulseCounts> QueryPulseAsync(
        long tenantId,
        DateTime windowStartedAtUtc,
        DateTime windowEndedAtUtc);
}

/// <summary>Chat 库公开频道摘要的只读边界。</summary>
public interface IPublicDiscoverChannelRepository
{
    Task<IReadOnlyList<PublicDiscoverSourceProjection>> QueryChannelSummariesAsync(
        PublicDiscoverSourceWindow window,
        DateTime recentWindowStartedAtUtc);

    Task<PublicDiscoverChannelPulseCounts> QueryPulseAsync(
        long tenantId,
        DateTime windowStartedAtUtc,
        DateTime windowEndedAtUtc);
}
