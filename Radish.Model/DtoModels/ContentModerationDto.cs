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
