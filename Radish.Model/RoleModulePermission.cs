using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 角色、按钮和权限关联表
/// </summary>
public class RoleModulePermission : RootEntityTKey<long>
{
    /// <summary>
    /// 角色 Id
    /// </summary>
    public long RoleId { get; set; }

    /// <summary>
    /// API 接口信息 Id
    /// </summary>
    public long ApiModuleId { get; set; }

    /// <summary>
    /// 前端按钮 Id
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long PermissionId { get; set; }

    /// <summary>
    /// 是否被删除，逻辑上的删除，非物理删除
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = true;

    /// <summary>
    /// 创建者 Id
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long CreateId { get; set; } = 1000001;

    /// <summary>
    /// 创建者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改者 Id
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }

    /// <summary>
    /// 修改者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>
    /// 角色
    /// </summary>
    /// <remarks>不写入数据库，已著名 IsIgnore 特性</remarks>
    [SugarColumn(IsIgnore = true)]
    public Role Role { get; set; } = new Role();

    /// <summary>
    /// 接口 API 地址信息
    /// </summary>
    /// <remarks>不写入数据库，已著名 IsIgnore 特性</remarks>
    [SugarColumn(IsIgnore = true)]
    public ApiModule ApiModule { get; set; } = new ApiModule();
}