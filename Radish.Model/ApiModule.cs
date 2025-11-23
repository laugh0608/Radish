using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 接口 API 地址信息表
/// </summary>
public class ApiModule : RootEntityTKey<long>
{
    /// <summary>
    /// 父节点 Id
    /// </summary>
    public long ParentId { get; set; } = 0;

    /// <summary>
    /// 是否被删除，逻辑上的删除，非物理删除
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = true;

    /// <summary>
    /// 接口名称
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string ApiModuleName { get; set; } = string.Empty;

    /// <summary>
    /// API 地址
    /// </summary>
    /// <remarks>
    /// <para>如果使用了路径参数，那么在 ApiModule 表中存 URL 的时候必须加上正则匹配：</para>
    /// <para>例如：/api/GetById/\d+</para>
    /// <para>api 前面的根符号别忘了</para>
    /// </remarks>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string LinkUrl { get; set; } = "/api/HealthCheck";

    /// <summary>
    /// 区域名称
    /// </summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string AreaName { get; set; } = string.Empty;

    /// <summary>
    /// 控制器名称
    /// </summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string ControllerName { get; set; } = string.Empty;

    /// <summary>
    /// Action 名称
    /// </summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// 图标 Url/icon
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string IconUrl { get; set; } = string.Empty;

    /// <summary>
    /// 菜单编号
    /// </summary>
    [SugarColumn(Length = 10, IsNullable = true)]
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>
    /// 排序
    /// </summary>
    public int OrderSort { get; set; } = 0;

    /// <summary>
    /// 描述
    /// </summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string ModuleDescription { get; set; } = string.Empty;

    /// <summary>
    /// 是否是左/右菜单
    /// </summary>
    public bool IsMenu { get; set; } = false;

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 创建者 Id
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public long CreateId { get; set; } = 0;

    /// <summary>
    /// 创建者
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } =  "System";

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
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    // public virtual Module ParentModule { get; set; }
    // public virtual ICollection<Module> ChildModule { get; set; }
    // public virtual ICollection<ModulePermission> ModulePermission { get; set; }
    // public virtual ICollection<RoleModulePermission> RoleModulePermission { get; set; }
}