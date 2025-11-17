using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>部门表</summary>
public class Department : RootEntityTKey<long>
{
    /// <summary>上级部门 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public long Pid { get; set; } = 0;

    /// <summary>部门关系编码</summary>
    public string CodeRelationship { get; set; } = string.Empty;

    /// <summary>部门名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 Radish</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string DepartmentName { get; set; } = "Radish";

    /// <summary>负责人名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string LeaderName { get; set; } = "System";

    /// <summary>排序</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public int OrderSort { get; set; } = 0;

    /// <summary>部门状态</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public int StatusCode { get; set; } = 0;

    /// <summary>是否已被删除</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>创建者的名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate=true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } =  DateTime.Now;

    /// <summary>更新者名称</summary>
    public string? ModifyBy { get; set; }

    /// <summary>更新时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }
    
    [SugarColumn(IsIgnore = true)]
    public bool HasChildren { get; set; } = true;

    [SugarColumn(IsIgnore = true)]
    public List<long> PidArr { get; set; } =  new List<long>();
}