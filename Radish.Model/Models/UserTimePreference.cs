using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.Models;

/// <summary>
/// 用户时间偏好设置
/// </summary>
[SugarTable("UserTimePreference")]
[SugarIndex("idx_user_time_preference_user_id", nameof(UserId), OrderByType.Asc, IsUnique = true)]
public class UserTimePreference : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    [SugarColumn(ColumnDescription = "用户ID", IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>
    /// 用户自定义时区（IANA）
    /// </summary>
    [SugarColumn(ColumnDescription = "用户时区", Length = 100, IsNullable = false)]
    public string TimeZoneId { get; set; } = "Asia/Shanghai";

    /// <summary>
    /// 租户 ID
    /// </summary>
    [SugarColumn(IsNullable = false, ColumnDescription = "租户ID")]
    public long TenantId { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>
    /// 创建者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者")]
    public string CreateBy { get; set; } = "System";

    /// <summary>
    /// 修改者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者")]
    public string? ModifyBy { get; set; }
}
