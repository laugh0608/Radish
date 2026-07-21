using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>提交内容举报请求</summary>
public class SubmitContentReportDto
{
    /// <summary>举报目标类型（Post/Comment）</summary>
    [Required(ErrorMessage = "targetType 不能为空")]
    [StringLength(20, ErrorMessage = "targetType 长度不能超过20个字符")]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>举报目标内容 ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "targetContentId 必须大于0")]
    public long TargetContentId { get; set; }

    /// <summary>举报原因类型</summary>
    [Required(ErrorMessage = "reasonType 不能为空")]
    [StringLength(30, ErrorMessage = "reasonType 长度不能超过30个字符")]
    public string ReasonType { get; set; } = "Other";

    /// <summary>举报补充说明</summary>
    [StringLength(500, ErrorMessage = "reasonDetail 长度不能超过500个字符")]
    public string? ReasonDetail { get; set; }
}

/// <summary>审核举报请求</summary>
public class ReviewContentReportDto
{
    /// <summary>举报单 ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "reportId 必须大于0")]
    public long ReportId { get; set; }

    /// <summary>是否通过举报</summary>
    public bool IsApproved { get; set; }

    /// <summary>审核动作（0=None,1=Mute,2=Ban）</summary>
    [Range(0, 2, ErrorMessage = "actionType 仅支持 0(None)/1(Mute)/2(Ban)")]
    public int ActionType { get; set; }

    /// <summary>动作时长（小时）</summary>
    [Range(1, 24 * 30, ErrorMessage = "durationHours 需在 1-720 之间")]
    public int? DurationHours { get; set; }

    /// <summary>审核备注</summary>
    [StringLength(500, ErrorMessage = "reviewRemark 长度不能超过500个字符")]
    public string? ReviewRemark { get; set; }
}

/// <summary>审核队列查询请求</summary>
public class ContentReportQueueQueryDto
{
    /// <summary>处理状态（0=Pending,1=Approved,2=Rejected；为空表示全部）</summary>
    public int? Status { get; set; }

    /// <summary>举报目标类型（Post/Comment/PostQuickReply/ChatMessage/Product）</summary>
    [StringLength(30, ErrorMessage = "targetType 长度不能超过30个字符")]
    public string? TargetType { get; set; }

    /// <summary>举报原因类型（Spam/Abuse/...）</summary>
    [StringLength(30, ErrorMessage = "reasonType 长度不能超过30个字符")]
    public string? ReasonType { get; set; }

    /// <summary>目标回看状态（Ready/Fallback/Unavailable/Unsupported）</summary>
    [StringLength(30, ErrorMessage = "navigationStatus 长度不能超过30个字符")]
    public string? NavigationStatus { get; set; }

    /// <summary>关键词检索</summary>
    [StringLength(100, ErrorMessage = "keyword 长度不能超过100个字符")]
    public string? Keyword { get; set; }

    /// <summary>页码（从 1 开始）</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>每页大小</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>治理动作日志查询请求</summary>
public class ContentModerationActionLogQueryDto
{
    /// <summary>目标用户 ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "targetUserId 必须大于0")]
    public long? TargetUserId { get; set; }

    /// <summary>关联举报单 ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "sourceReportId 必须大于0")]
    public long? SourceReportId { get; set; }

    /// <summary>治理动作类型（Mute/Ban/Unmute/Unban）</summary>
    [StringLength(20, ErrorMessage = "actionType 长度不能超过20个字符")]
    public string? ActionType { get; set; }

    /// <summary>是否仅查看生效中的动作</summary>
    public bool? IsActive { get; set; }

    /// <summary>关键词检索</summary>
    [StringLength(100, ErrorMessage = "keyword 长度不能超过100个字符")]
    public string? Keyword { get; set; }

    /// <summary>页码（从 1 开始）</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>每页大小</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>手动执行治理动作请求</summary>
public class ApplyUserModerationActionDto
{
    /// <summary>目标用户 ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "targetUserId 必须大于0")]
    public long TargetUserId { get; set; }

    /// <summary>动作类型（1=Mute,2=Ban,3=Unmute,4=Unban）</summary>
    [Range(1, 4, ErrorMessage = "actionType 仅支持 1(Mute)/2(Ban)/3(Unmute)/4(Unban)")]
    public int ActionType { get; set; }

    /// <summary>动作时长（小时）</summary>
    [Range(1, 24 * 30, ErrorMessage = "durationHours 需在 1-720 之间")]
    public int? DurationHours { get; set; }

    /// <summary>动作原因</summary>
    [StringLength(500, ErrorMessage = "reason 长度不能超过500个字符")]
    public string? Reason { get; set; }

    /// <summary>关联举报单 ID</summary>
    public long? SourceReportId { get; set; }
}

/// <summary>本人举报分页查询。</summary>
public sealed class MyContentReportQueryDto
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>治理案件队列查询。</summary>
public sealed class ContentModerationCaseQueueDto
{
    public int? Status { get; set; }

    [StringLength(30)]
    public string? TargetType { get; set; }

    [StringLength(100)]
    public string? Keyword { get; set; }

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>追加治理证据。</summary>
public sealed class CaptureContentModerationEvidenceDto
{
    [Required, StringLength(40)]
    public string CasePublicId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [Range(2, 3)]
    public int EvidenceType { get; set; }

    [StringLength(200)]
    public string? SnapshotTitle { get; set; }

    [StringLength(500)]
    public string? SnapshotSummary { get; set; }
}

/// <summary>登记案件决定与可选用户动作。</summary>
public sealed class ReviewContentModerationCaseDto
{
    [Required, StringLength(40)]
    public string CasePublicId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [Range(1, 3)]
    public int Decision { get; set; }

    [Range(1, 3)]
    public int TargetDisposition { get; set; }

    /// <summary>目标内容当前版本；Post/Comment 使用 EditCount，Product 使用 Version。</summary>
    [Range(0, int.MaxValue)]
    public int? ExpectedTargetVersion { get; set; }

    [Required, StringLength(50)]
    public string PublicResultCode { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? InternalRemark { get; set; }

    public ContentModerationCaseUserActionDto? UserAction { get; set; }

    [Required, StringLength(160, MinimumLength = 8)]
    public string OperationKey { get; set; } = string.Empty;
}

/// <summary>案件内用户治理动作。</summary>
public sealed class ContentModerationCaseUserActionDto
{
    [Range(1, 4)]
    public int ActionType { get; set; }

    [Range(0, int.MaxValue)]
    public int ExpectedStateVersion { get; set; }

    [Range(1, 24 * 30)]
    public int? DurationHours { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>对已结案件追加用户状态纠正动作。</summary>
public sealed class ApplyContentModerationCorrectiveActionDto
{
    [Required, StringLength(40)]
    public string CasePublicId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [Required]
    public ContentModerationCaseUserActionDto UserAction { get; set; } = new();

    [Required, StringLength(160, MinimumLength = 8)]
    public string OperationKey { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Remark { get; set; } = string.Empty;
}
