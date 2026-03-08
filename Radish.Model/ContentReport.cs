using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>内容举报实体</summary>
[SugarTable("ContentReport")]
[SugarIndex("idx_contentreport_status", nameof(TenantId), OrderByType.Asc, nameof(Status), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
[SugarIndex("idx_contentreport_target", nameof(TenantId), OrderByType.Asc, nameof(ReportTargetType), OrderByType.Asc, nameof(TargetContentId), OrderByType.Asc)]
[SugarIndex("idx_contentreport_reporter", nameof(TenantId), OrderByType.Asc, nameof(ReporterUserId), OrderByType.Asc, nameof(Status), OrderByType.Asc)]
public class ContentReport : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>举报目标类型（Post/Comment）</summary>
    [SugarColumn(IsNullable = false)]
    public int ReportTargetType { get; set; } = (int)ContentReportTargetTypeEnum.Unknown;

    /// <summary>举报目标内容 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TargetContentId { get; set; }

    /// <summary>被举报用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TargetUserId { get; set; }

    /// <summary>被举报用户名</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? TargetUserName { get; set; }

    /// <summary>举报人用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long ReporterUserId { get; set; }

    /// <summary>举报人用户名</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string ReporterUserName { get; set; } = string.Empty;

    /// <summary>举报原因类型</summary>
    [SugarColumn(Length = 30, IsNullable = false)]
    public string ReasonType { get; set; } = "Other";

    /// <summary>举报补充说明</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ReasonDetail { get; set; }

    /// <summary>处理状态</summary>
    [SugarColumn(IsNullable = false)]
    public int Status { get; set; } = (int)ContentReportStatusEnum.Pending;

    /// <summary>审核动作（None/Mute/Ban）</summary>
    [SugarColumn(IsNullable = false)]
    public int ReviewActionType { get; set; } = (int)ModerationActionTypeEnum.None;

    /// <summary>审核动作时长（小时）</summary>
    [SugarColumn(IsNullable = true)]
    public int? ReviewDurationHours { get; set; }

    /// <summary>审核备注</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? ReviewRemark { get; set; }

    /// <summary>审核人 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? ReviewedById { get; set; }

    /// <summary>审核人用户名</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ReviewedByName { get; set; }

    /// <summary>审核时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ReviewedAt { get; set; }

    /// <summary>软删除标记</summary>
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
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

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
