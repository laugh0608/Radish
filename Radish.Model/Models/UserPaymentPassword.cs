using SqlSugar;
using Radish.Model.Models.Base;

namespace Radish.Model.Models;

/// <summary>
/// 用户支付密码实体
/// </summary>
[SugarTable("UserPaymentPassword")]
[MultiTenant(TenantTypeEnum.Id)]
public class UserPaymentPassword : RootEntityTKey<long>
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "用户ID", IsNullable = false)]
    [SugarIndex("idx_user_payment_password_user_id", nameof(UserId), OrderByType.Asc)]
    public long UserId { get; set; }

    /// <summary>
    /// 密码哈希值
    /// </summary>
    [SugarColumn(ColumnDescription = "密码哈希值", Length = 255, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 盐值
    /// </summary>
    [SugarColumn(ColumnDescription = "盐值", Length = 255, IsNullable = false)]
    public string Salt { get; set; } = string.Empty;

    /// <summary>
    /// 失败尝试次数
    /// </summary>
    [SugarColumn(ColumnDescription = "失败尝试次数", IsNullable = false)]
    public int FailedAttempts { get; set; } = 0;

    /// <summary>
    /// 锁定到期时间
    /// </summary>
    [SugarColumn(ColumnDescription = "锁定到期时间", IsNullable = true)]
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    [SugarColumn(ColumnDescription = "最后使用时间", IsNullable = true)]
    public DateTime? LastUsedTime { get; set; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    [SugarColumn(ColumnDescription = "最后修改时间", IsNullable = true)]
    public DateTime? LastModifiedTime { get; set; }

    /// <summary>
    /// 密码强度等级 (1-5)
    /// </summary>
    [SugarColumn(ColumnDescription = "密码强度等级", IsNullable = false)]
    public int StrengthLevel { get; set; } = 1;

    /// <summary>
    /// 是否启用
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启用", IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 备注信息
    /// </summary>
    [SugarColumn(ColumnDescription = "备注信息", Length = 500, IsNullable = true)]
    public string? Remark { get; set; }
}