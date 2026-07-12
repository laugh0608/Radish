using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户经验治理动作记录</summary>
[SugarTable("UserExperienceGovernanceAction")]
[SugarIndex(
    "idx_exp_governance_target_createtime",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(TargetUserId),
    OrderByType.Asc,
    nameof(CreateTime),
    OrderByType.Desc)]
public class UserExperienceGovernanceAction : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>目标用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TargetUserId { get; set; }

    /// <summary>目标用户名</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? TargetUserName { get; set; }

    /// <summary>治理动作类型</summary>
    [SugarColumn(IsNullable = false)]
    public int ActionType { get; set; } = (int)ExperienceGovernanceActionTypeEnum.Unknown;

    /// <summary>人工复核结论</summary>
    [SugarColumn(IsNullable = true)]
    public int? ReviewResult { get; set; }

    /// <summary>治理备注</summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string Remark { get; set; } = string.Empty;

    /// <summary>复核窗口天数</summary>
    [SugarColumn(IsNullable = true)]
    public int? WindowDays { get; set; }

    /// <summary>关联命中日期</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "date")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime? StatDate { get; set; }

    /// <summary>规则代码快照（JSON 数组）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? RuleCodes { get; set; }

    /// <summary>规则标签快照（JSON 数组）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "longtext,text,clob")]
    public string? RuleLabels { get; set; }

    /// <summary>治理建议等级快照</summary>
    [SugarColumn(Length = 32, IsNullable = true)]
    public string? RecommendationLevel { get; set; }

    /// <summary>治理建议原因快照</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? RecommendationReason { get; set; }

    /// <summary>冻结到期时间快照</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? FrozenUntil { get; set; }

    /// <summary>是否删除</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建人</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建人 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改人 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
