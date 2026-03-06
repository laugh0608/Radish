using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户治理动作记录</summary>
[SugarTable("UserModerationAction")]
[SugarIndex("idx_moderation_target_active", nameof(TenantId), OrderByType.Asc, nameof(TargetUserId), OrderByType.Asc, nameof(ActionType), OrderByType.Asc, nameof(IsActive), OrderByType.Asc)]
[SugarIndex("idx_moderation_starttime", nameof(TenantId), OrderByType.Asc, nameof(CreateTime), OrderByType.Desc)]
public class UserModerationAction : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
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

    /// <summary>动作类型（Mute/Ban/Unmute/Unban）</summary>
    [SugarColumn(IsNullable = false)]
    public int ActionType { get; set; } = (int)ModerationActionTypeEnum.None;

    /// <summary>操作原因</summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>关联举报单 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? SourceReportId { get; set; }

    /// <summary>动作持续时长（小时）</summary>
    [SugarColumn(IsNullable = true)]
    public int? DurationHours { get; set; }

    /// <summary>动作生效时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    /// <summary>动作失效时间（null 表示永久）</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? EndTime { get; set; }

    /// <summary>是否为当前生效状态</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsActive { get; set; } = true;

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
