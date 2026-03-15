using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 角色与 Console 资源授权关系
/// </summary>
public class RoleConsoleResource : RootEntityTKey<long>, IDeleteFilter
{
    /// <summary>角色 Id</summary>
    public long RoleId { get; set; }

    /// <summary>Console 资源 Id</summary>
    public long ConsoleResourceId { get; set; }

    /// <summary>是否软删除</summary>
    public bool IsDeleted { get; set; }

    /// <summary>软删除时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>软删除操作者</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建者 Id</summary>
    public long CreateId { get; set; }

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>修改者 Id</summary>
    public long? ModifyId { get; set; }

    /// <summary>修改者</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }
}
