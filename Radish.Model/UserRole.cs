using System.ComponentModel.DataAnnotations;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 用户跟角色关联表
/// </summary>
public class UserRole
{
    /// <summary>
    /// 用户 Id
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 角色 Id
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// 获取或设置是否禁用，逻辑上的删除，非物理删除
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = true;

    /// <summary>
    /// 创建 Id
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long CreateId { get; set; } = 0;

    /// <summary>
    /// 创建者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>
    /// 创建时间
    /// </summary>
    /// <remarks>更新时忽略改列</remarks>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改 Id
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public int? ModifyId { get; set; }

    /// <summary>
    /// 修改者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }
}