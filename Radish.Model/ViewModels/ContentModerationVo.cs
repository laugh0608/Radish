namespace Radish.Model.ViewModels;

/// <summary>举报单视图模型</summary>
public class ContentReportVo
{
    /// <summary>举报单 ID</summary>
    public long VoId { get; set; }

    /// <summary>举报目标类型（Post/Comment）</summary>
    public string VoTargetType { get; set; } = "Unknown";

    /// <summary>举报目标内容 ID</summary>
    public long VoTargetContentId { get; set; }

    /// <summary>举报目标快照所属帖子 ID</summary>
    public long? VoTargetSnapshotPostId { get; set; }

    /// <summary>举报目标快照所属频道 ID</summary>
    public long? VoTargetSnapshotChannelId { get; set; }

    /// <summary>举报目标快照标题</summary>
    public string? VoTargetSnapshotTitle { get; set; }

    /// <summary>举报目标快照摘要</summary>
    public string? VoTargetSnapshotSummary { get; set; }

    /// <summary>被举报用户 ID</summary>
    public long VoTargetUserId { get; set; }

    /// <summary>被举报用户名</summary>
    public string? VoTargetUserName { get; set; }

    /// <summary>举报人用户 ID</summary>
    public long VoReporterUserId { get; set; }

    /// <summary>举报人用户名</summary>
    public string VoReporterUserName { get; set; } = string.Empty;

    /// <summary>举报原因类型</summary>
    public string VoReasonType { get; set; } = string.Empty;

    /// <summary>举报补充说明</summary>
    public string? VoReasonDetail { get; set; }

    /// <summary>处理状态（Pending/Approved/Rejected）</summary>
    public string VoStatus { get; set; } = "Pending";

    /// <summary>审核动作（None/Mute/Ban）</summary>
    public string VoReviewActionType { get; set; } = "None";

    /// <summary>审核动作时长（小时）</summary>
    public int? VoReviewDurationHours { get; set; }

    /// <summary>审核备注</summary>
    public string? VoReviewRemark { get; set; }

    /// <summary>审核人 ID</summary>
    public long? VoReviewedById { get; set; }

    /// <summary>审核人用户名</summary>
    public string? VoReviewedByName { get; set; }

    /// <summary>审核时间</summary>
    public DateTime? VoReviewedAt { get; set; }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}

/// <summary>审核队列项视图模型</summary>
public class ContentReportQueueItemVo
{
    /// <summary>举报单 ID</summary>
    public long VoReportId { get; set; }

    /// <summary>举报目标类型（Post/Comment）</summary>
    public string VoTargetType { get; set; } = "Unknown";

    /// <summary>举报目标内容 ID</summary>
    public long VoTargetContentId { get; set; }

    /// <summary>论坛目标帖子 ID</summary>
    public long? VoTargetPostId { get; set; }

    /// <summary>论坛目标评论 ID</summary>
    public long? VoTargetCommentId { get; set; }

    /// <summary>聊天室目标频道 ID</summary>
    public long? VoTargetChannelId { get; set; }

    /// <summary>聊天室目标消息 ID</summary>
    public long? VoTargetMessageId { get; set; }

    /// <summary>目标导航状态（Ready/Fallback/Unavailable/Unsupported）</summary>
    public string VoTargetNavigationStatus { get; set; } = "Unavailable";

    /// <summary>目标导航状态说明</summary>
    public string? VoTargetNavigationMessage { get; set; }

    /// <summary>目标快照标题</summary>
    public string? VoTargetSnapshotTitle { get; set; }

    /// <summary>目标快照摘要</summary>
    public string? VoTargetSnapshotSummary { get; set; }

    /// <summary>被举报用户 ID</summary>
    public long VoTargetUserId { get; set; }

    /// <summary>被举报用户名</summary>
    public string? VoTargetUserName { get; set; }

    /// <summary>举报人用户 ID</summary>
    public long VoReporterUserId { get; set; }

    /// <summary>举报人用户名</summary>
    public string VoReporterUserName { get; set; } = string.Empty;

    /// <summary>举报原因类型</summary>
    public string VoReasonType { get; set; } = string.Empty;

    /// <summary>举报补充说明</summary>
    public string? VoReasonDetail { get; set; }

    /// <summary>处理状态（Pending/Approved/Rejected）</summary>
    public string VoStatus { get; set; } = "Pending";

    /// <summary>审核动作（None/Mute/Ban）</summary>
    public string VoReviewActionType { get; set; } = "None";

    /// <summary>审核动作时长（小时）</summary>
    public int? VoReviewDurationHours { get; set; }

    /// <summary>审核备注</summary>
    public string? VoReviewRemark { get; set; }

    /// <summary>审核人 ID</summary>
    public long? VoReviewedById { get; set; }

    /// <summary>审核人用户名</summary>
    public string? VoReviewedByName { get; set; }

    /// <summary>审核时间</summary>
    public DateTime? VoReviewedAt { get; set; }

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}

/// <summary>用户治理动作记录视图模型</summary>
public class UserModerationActionVo
{
    /// <summary>动作记录 ID</summary>
    public long VoActionId { get; set; }

    /// <summary>目标用户 ID</summary>
    public long VoTargetUserId { get; set; }

    /// <summary>目标用户名</summary>
    public string? VoTargetUserName { get; set; }

    /// <summary>动作类型</summary>
    public string VoActionType { get; set; } = "None";

    /// <summary>动作原因</summary>
    public string VoReason { get; set; } = string.Empty;

    /// <summary>关联举报单 ID</summary>
    public long? VoSourceReportId { get; set; }

    /// <summary>来源举报目标类型</summary>
    public string? VoSourceReportTargetType { get; set; }

    /// <summary>来源举报目标内容 ID</summary>
    public long? VoSourceReportTargetContentId { get; set; }

    /// <summary>来源举报论坛目标帖子 ID</summary>
    public long? VoSourceReportTargetPostId { get; set; }

    /// <summary>来源举报论坛目标评论 ID</summary>
    public long? VoSourceReportTargetCommentId { get; set; }

    /// <summary>来源举报目标频道 ID</summary>
    public long? VoSourceReportTargetChannelId { get; set; }

    /// <summary>来源举报目标消息 ID</summary>
    public long? VoSourceReportTargetMessageId { get; set; }

    /// <summary>来源举报目标导航状态（Ready/Fallback/Unavailable/Unsupported）</summary>
    public string VoSourceReportTargetNavigationStatus { get; set; } = "Unavailable";

    /// <summary>来源举报目标导航状态说明</summary>
    public string? VoSourceReportTargetNavigationMessage { get; set; }

    /// <summary>来源举报目标快照标题</summary>
    public string? VoSourceReportTargetSnapshotTitle { get; set; }

    /// <summary>来源举报目标快照摘要</summary>
    public string? VoSourceReportTargetSnapshotSummary { get; set; }

    /// <summary>动作持续时长（小时）</summary>
    public int? VoDurationHours { get; set; }

    /// <summary>动作生效时间</summary>
    public DateTime VoStartTime { get; set; }

    /// <summary>动作失效时间</summary>
    public DateTime? VoEndTime { get; set; }

    /// <summary>是否生效中</summary>
    public bool VoIsActive { get; set; }

    /// <summary>操作者 ID</summary>
    public long VoOperatorUserId { get; set; }

    /// <summary>操作者用户名</summary>
    public string VoOperatorUserName { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}

/// <summary>用户治理状态视图模型</summary>
public class UserModerationStatusVo
{
    /// <summary>用户 ID</summary>
    public long VoUserId { get; set; }

    /// <summary>是否禁言中</summary>
    public bool VoIsMuted { get; set; }

    /// <summary>禁言截止时间（null 表示永久或未禁言）</summary>
    public DateTime? VoMutedUntil { get; set; }

    /// <summary>是否封禁中</summary>
    public bool VoIsBanned { get; set; }

    /// <summary>封禁截止时间（null 表示永久或未封禁）</summary>
    public DateTime? VoBannedUntil { get; set; }
}

/// <summary>发布权限视图模型</summary>
public class ContentModerationPermissionVo
{
    /// <summary>用户 ID</summary>
    public long VoUserId { get; set; }

    /// <summary>是否允许发布</summary>
    public bool VoCanPublish { get; set; } = true;

    /// <summary>是否禁言中</summary>
    public bool VoIsMuted { get; set; }

    /// <summary>禁言截止时间</summary>
    public DateTime? VoMutedUntil { get; set; }

    /// <summary>是否封禁中</summary>
    public bool VoIsBanned { get; set; }

    /// <summary>封禁截止时间</summary>
    public DateTime? VoBannedUntil { get; set; }

    /// <summary>拒绝原因</summary>
    public string? VoDenyReason { get; set; }
}
