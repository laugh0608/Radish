using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户展示名变更审计记录</summary>
[SugarTable("UserDisplayNameChangeRecord")]
[SugarIndex(
    "idx_user_display_name_change_user_time",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(UserId),
    OrderByType.Asc,
    nameof(ChangeTime),
    OrderByType.Desc)]
public class UserDisplayNameChangeRecord : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>修改前展示名</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string OldDisplayName { get; set; } = string.Empty;

    /// <summary>修改后展示名</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string NewDisplayName { get; set; } = string.Empty;

    /// <summary>操作人 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long OperatorUserId { get; set; }

    /// <summary>操作人名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string OperatorUserName { get; set; } = string.Empty;

    /// <summary>变更来源</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string ChangeSource { get; set; } = string.Empty;

    /// <summary>变更原因</summary>
    [SugarColumn(Length = 500, IsNullable = false)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>变更时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime ChangeTime { get; set; } = DateTime.UtcNow;

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
}
