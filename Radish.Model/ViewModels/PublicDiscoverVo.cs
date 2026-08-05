namespace Radish.Model.ViewModels;

/// <summary>统一公开发现流中的结构化来源类型。</summary>
public enum PublicDiscoverItemKind
{
    ChannelSummary = 1,
    MemberActivity = 2,
    HighlightedComment = 3,
    Post = 4,
    Question = 5
}

/// <summary>公开发现项的受控目标类型。</summary>
public enum PublicDiscoverTargetKind
{
    Messages = 1,
    Docs = 2,
    ForumPost = 3
}

/// <summary>公开发现项主指标的跨类型语义。</summary>
public enum PublicDiscoverMetricKind
{
    RecentReplies = 1,
    Likes = 2,
    Comments = 3,
    Answers = 4
}

/// <summary>公开发现流响应。</summary>
public sealed class PublicDiscoverFeedVo
{
    public IReadOnlyList<PublicDiscoverItemVo> VoItems { get; set; } = [];

    public PublicDiscoverPulseVo VoPulse { get; set; } = new();

    public string? VoNextCursor { get; set; }

    public bool VoHasMore { get; set; }

    public DateTime VoGeneratedAtUtc { get; set; }
}

/// <summary>公开发现流中的单个结构化条目。</summary>
public sealed class PublicDiscoverItemVo
{
    public string VoKey { get; set; } = string.Empty;

    public PublicDiscoverItemKind VoKind { get; set; }

    public DateTime VoOccurredAtUtc { get; set; }

    public string VoTitle { get; set; } = string.Empty;

    public string VoSummary { get; set; } = string.Empty;

    public PublicDiscoverActorVo? VoActor { get; set; }

    public PublicDiscoverTargetVo VoTarget { get; set; } = new();

    public PublicDiscoverMetricVo? VoPrimaryMetric { get; set; }
}

/// <summary>发现流允许公开的最小成员身份。</summary>
public sealed class PublicDiscoverActorVo
{
    public string VoPublicId { get; set; } = string.Empty;

    public string VoDisplayName { get; set; } = string.Empty;

    public string? VoAvatarThumbnailUrl { get; set; }
}

/// <summary>发现流条目的受控站内目标。</summary>
public sealed class PublicDiscoverTargetVo
{
    public PublicDiscoverTargetKind VoKind { get; set; }

    public string? VoChannelId { get; set; }

    public string? VoDocumentSlug { get; set; }

    public string? VoPostPublicId { get; set; }

    public string? VoCommentId { get; set; }

    public bool VoRequiresAuthentication { get; set; }
}

/// <summary>发现流条目的受控主指标。</summary>
public sealed class PublicDiscoverMetricVo
{
    public PublicDiscoverMetricKind VoKind { get; set; }

    public long VoValue { get; set; }
}

/// <summary>以当前读窗口为截止点重算的社区公开脉搏。</summary>
public sealed class PublicDiscoverPulseVo
{
    public DateTime VoWindowStartedAtUtc { get; set; }

    public DateTime VoWindowEndedAtUtc { get; set; }

    public long VoDiscoverableChannelCount { get; set; }

    public long VoEligibleItemCount { get; set; }

    public long VoKnowledgeContributionCount { get; set; }
}
