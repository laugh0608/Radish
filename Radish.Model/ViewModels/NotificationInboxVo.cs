using Radish.Model;

namespace Radish.Model.ViewModels;

/// <summary>通知结构化目标。</summary>
public sealed class NotificationTargetVo
{
    public string VoKind { get; set; } = NotificationTargetKind.None;
    public string? VoPostId { get; set; }
    public string? VoPostPublicId { get; set; }
    public string? VoCommentId { get; set; }
    public string? VoChannelId { get; set; }
    public string? VoMessageId { get; set; }
    public string? VoUserId { get; set; }
    public string? VoUserPublicId { get; set; }
    public string? VoOrderId { get; set; }
    public string? VoBenefitId { get; set; }
    public string? VoDocumentSlug { get; set; }
    public string? VoGovernanceCaseId { get; set; }
    public string? VoUnavailableReason { get; set; }
}

/// <summary>通知收件箱权威摘要。</summary>
public sealed class NotificationInboxSummaryVo
{
    public long VoRevision { get; set; }
    public long VoUnreadGroupCount { get; set; }
    public long VoUnreadOccurrenceCount { get; set; }
    public Dictionary<string, long> VoUnreadGroupCountByCategory { get; set; } = new(StringComparer.Ordinal);
    public DateTime VoLastChangedAtUtc { get; set; }
}

/// <summary>通知收件箱分组列表项。</summary>
public sealed class NotificationInboxGroupVo
{
    public long VoGroupId { get; set; }
    public long VoLatestNotificationId { get; set; }
    public string VoCategory { get; set; } = string.Empty;
    public string VoKind { get; set; } = string.Empty;
    public string VoTitle { get; set; } = string.Empty;
    public string VoContent { get; set; } = string.Empty;
    public int VoPriority { get; set; }
    public long VoOccurrenceCount { get; set; }
    public long VoUnreadOccurrenceCount { get; set; }
    public long VoDistinctTriggerCount { get; set; }
    public bool VoIsRead => VoUnreadOccurrenceCount == 0;
    public DateTime VoFirstOccurredAtUtc { get; set; }
    public DateTime VoLastOccurredAtUtc { get; set; }
    public string? VoTriggerId { get; set; }
    public string? VoTriggerName { get; set; }
    public string? VoTriggerAvatar { get; set; }
    public NotificationTargetVo VoTarget { get; set; } = new();
}

/// <summary>基于 revision cursor 的通知收件箱页。</summary>
public sealed class NotificationInboxPageVo
{
    public List<NotificationInboxGroupVo> VoItems { get; set; } = [];
    public string? VoNextCursor { get; set; }
    public NotificationInboxSummaryVo VoSummary { get; set; } = new();
}

/// <summary>收件箱写操作结果。</summary>
public sealed class NotificationInboxMutationVo
{
    public int VoAffectedRows { get; set; }
    public NotificationInboxSummaryVo VoSummary { get; set; } = new();
}

/// <summary>用户可见通知分类偏好。</summary>
public sealed class NotificationPreferenceVo
{
    public string VoCategory { get; set; } = string.Empty;
    public bool VoInAppEnabled { get; set; }
    public bool VoRealtimePreviewEnabled { get; set; }
    public bool VoCanDisableInApp { get; set; }
    public bool VoCanDisableRealtimePreview { get; set; }
    public List<string> VoSupportedKinds { get; set; } = [];
}

/// <summary>SignalR 只发送的权威收件箱变化提示。</summary>
public sealed class NotificationInboxChangedVo
{
    public long VoRevision { get; set; }
    public long VoUnreadGroupCount { get; set; }
    public long VoUnreadOccurrenceCount { get; set; }
    public string VoReason { get; set; } = string.Empty;
    public long? VoLatestGroupId { get; set; }
    public bool VoRealtimePreviewAllowed { get; set; }
}
