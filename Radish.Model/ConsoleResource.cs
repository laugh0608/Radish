using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// Console 资源目录
/// </summary>
public class ConsoleResource : RootEntityTKey<long>, IDeleteFilter
{
    /// <summary>父资源 Id，0 表示根节点</summary>
    public long ParentId { get; set; }

    /// <summary>资源键，例如 console.roles.view</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string ResourceKey { get; set; } = string.Empty;

    /// <summary>资源名称</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>资源类型（Page/Button/Entry）</summary>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>模块键，例如 roles、users</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string ModuleKey { get; set; } = string.Empty;

    /// <summary>路由路径或入口路径</summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? RoutePath { get; set; }

    /// <summary>图标标识</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? Icon { get; set; }

    /// <summary>排序</summary>
    public int OrderSort { get; set; }

    /// <summary>是否显示在侧边栏</summary>
    public bool ShowInSidebar { get; set; }

    /// <summary>是否显示在全局搜索</summary>
    public bool ShowInSearch { get; set; }

    /// <summary>描述</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

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
