using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>管理员记录经验治理人工复核结论请求 DTO</summary>
public class AdminRecordExperienceGovernanceReviewDto
{
    /// <summary>用户 ID</summary>
    [Required(ErrorMessage = "用户ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "用户ID必须大于0")]
    public long UserId { get; set; }

    /// <summary>复核结论（NoIssue / Observe / FreezeSuggest）</summary>
    [Required(ErrorMessage = "复核结论不能为空")]
    [RegularExpression("^(NoIssue|Observe|FreezeSuggest)$", ErrorMessage = "复核结论不支持")]
    public string ReviewResult { get; set; } = string.Empty;

    /// <summary>复核备注</summary>
    [Required(ErrorMessage = "复核备注不能为空")]
    [MaxLength(500, ErrorMessage = "复核备注不能超过500个字符")]
    public string Remark { get; set; } = string.Empty;

    /// <summary>关联窗口天数</summary>
    [Range(1, 30, ErrorMessage = "窗口天数必须在1到30之间")]
    public int? WindowDays { get; set; }

    /// <summary>关联命中日期</summary>
    public DateOnly? StatDate { get; set; }

    /// <summary>关联规则代码</summary>
    public List<string>? RuleCodes { get; set; }

    /// <summary>关联规则标签</summary>
    public List<string>? RuleLabels { get; set; }

    /// <summary>治理建议等级快照</summary>
    [MaxLength(32, ErrorMessage = "治理建议等级不能超过32个字符")]
    public string? RecommendationLevel { get; set; }

    /// <summary>治理建议原因快照</summary>
    [MaxLength(500, ErrorMessage = "治理建议原因不能超过500个字符")]
    public string? RecommendationReason { get; set; }
}
