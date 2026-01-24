using SqlSugar;
using Radish.Model.Root;
using System.ComponentModel.DataAnnotations;

namespace Radish.Model.Models;

/// <summary>
/// 用户支付密码实体
/// </summary>
[SugarTable("UserPaymentPassword")]
[SugarIndex("idx_user_payment_password_user_id", nameof(UserPaymentPassword.UserId), OrderByType.Asc)]
// [MultiTenant(TenantTypeEnum.Id)]
public class UserPaymentPassword : RootEntityTKey<long>, IDeleteFilter
{
    /// <summary>
    /// 用户ID
    /// </summary>
    [SugarColumn(ColumnDescription = "用户ID", IsNullable = false)]
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

    #region 审计信息

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true, ColumnDescription = "创建时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = false, ColumnDescription = "创建者名称")]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "创建者ID")]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    /// <remarks>可空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "修改者名称")]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "修改者ID")]
    public long? ModifyId { get; set; }

    /// <summary>是否删除</summary>
    /// <remarks>不可为空，默认为 false，软删除标记</remarks>
    [SugarColumn(IsNullable = false, ColumnDescription = "是否删除")]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    /// <remarks>可空，软删除时自动设置，恢复时清空</remarks>
    [SugarColumn(IsNullable = true, ColumnDescription = "删除时间")]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除操作者</summary>
    /// <remarks>可空，最大 50 字符，执行软删除操作的用户名或系统标识</remarks>
    [SugarColumn(Length = 50, IsNullable = true, ColumnDescription = "删除操作者")]
    public string? DeletedBy { get; set; }

    #endregion
}