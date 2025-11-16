using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>角色实体</summary>
/// <remarks>主键为 Id，类型为 long</remarks>
public class Role : RootEntityTKey<long>
{
    /// <summary>获取或设置是否禁用，逻辑上的删除，非物理删除</summary>
    /// <remarks>数据库级不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = true;

    /// <summary>角色名</summary>
    /// <remarks>数据库级不可为空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色描述</summary>
    /// <remarks>数据库级不可为空，最大 100 字符</remarks>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string RoleDescription { get; set; } = string.Empty;

    /// <summary>排序</summary>
    public int OrderSort { get; set; }

    /// <summary>自定义权限的部门 Ids</summary>
    /// <remarks>数据库级不可为空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string DepartmentIds { get; set; } = string.Empty;

    /// <summary>权限范围</summary>
    /// <remarks><para>数据库级不可为空</para>
    /// <para>默认为 -1 无任何权限</para>
    /// <para>-1 无任何权限；1 自定义权限；2 本部门；3 本部门及以下；4 仅自己；9 全部；</para></remarks>
    [SugarColumn(IsNullable = true)]
    public int AuthorityScope { get; set; } = -1;

    /// <summary>是否激活</summary>
    /// <remarks>数据库级不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = true)]
    public bool Enabled { get; set; } = false;

    /// <summary>部门创建者的 Id</summary>
    /// <remarks>数据库级不可为空，默认为 1000001</remarks>
    [SugarColumn(IsNullable = true)]
    public long CreateId { get; set; } = 1000001;

    /// <summary>部门创建者的名称</summary>
    /// <remarks>数据库级不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建时间</summary>
    /// <remarks>数据库级不可为空，默认为当前时间</remarks>
    [SugarColumn(IsNullable = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>信息修改者的 Id</summary>
    /// <remarks>数据库级不可为空，默认为 1000001</remarks>
    [SugarColumn(IsNullable = true)]
    public long ModifyId { get; set; } = 1000001;

    /// <summary>修改者的名称</summary>
    /// <remarks>数据库级不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(IsNullable = true)]
    public string ModifyBy { get; set; } = "System";

    /// <summary>修改时间</summary>
    /// <remarks>数据库级不可为空，默认为当前时间</remarks>
    [SugarColumn(IsNullable = true)]
    public DateTime ModifyTime { get; set; } = DateTime.Now;
}