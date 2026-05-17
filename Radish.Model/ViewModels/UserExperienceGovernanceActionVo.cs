namespace Radish.Model.ViewModels;

/// <summary>用户经验治理动作记录视图模型</summary>
public class UserExperienceGovernanceActionVo
{
    /// <summary>动作记录 ID</summary>
    public long VoActionId { get; set; }

    /// <summary>目标用户 ID</summary>
    public long VoTargetUserId { get; set; }

    /// <summary>目标用户名</summary>
    public string? VoTargetUserName { get; set; }

    /// <summary>动作类型代码</summary>
    public string VoActionType { get; set; } = "Unknown";

    /// <summary>动作类型名称</summary>
    public string VoActionTypeDisplay { get; set; } = string.Empty;

    /// <summary>复核结论代码</summary>
    public string? VoReviewResult { get; set; }

    /// <summary>复核结论名称</summary>
    public string? VoReviewResultDisplay { get; set; }

    /// <summary>治理备注</summary>
    public string VoRemark { get; set; } = string.Empty;

    /// <summary>证据快照摘要</summary>
    public string? VoEvidenceSummary { get; set; }

    /// <summary>复核窗口天数</summary>
    public int? VoWindowDays { get; set; }

    /// <summary>关联命中日期</summary>
    public DateTime? VoStatDate { get; set; }

    /// <summary>规则代码列表</summary>
    public List<string> VoRuleCodes { get; set; } = [];

    /// <summary>规则标签列表</summary>
    public List<string> VoRuleLabels { get; set; } = [];

    /// <summary>治理建议等级快照</summary>
    public string? VoRecommendationLevel { get; set; }

    /// <summary>治理建议标题</summary>
    public string? VoRecommendationTitle { get; set; }

    /// <summary>治理建议原因快照</summary>
    public string? VoRecommendationReason { get; set; }

    /// <summary>冻结到期时间快照</summary>
    public DateTime? VoFrozenUntil { get; set; }

    /// <summary>操作人 ID</summary>
    public long VoOperatorId { get; set; }

    /// <summary>操作人名称</summary>
    public string? VoOperatorName { get; set; }

    /// <summary>记录时间</summary>
    public DateTime VoCreateTime { get; set; }
}
