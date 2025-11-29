using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CuatomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>部门表</summary>
public class Department : RootEntityTKey<long>
{
    /// <summary>初始化默认部门实例</summary>
    public Department()
    {
        InitializeDefaults();
    }

    /// <summary>通过部门名初始化部门</summary>
    /// <param name="departmentName">部门名称</param>
    public Department(string departmentName)
        : this(new DepartmentInitializationOptions(departmentName))
    {
    }

    /// <summary>通过初始化选项批量构造部门</summary>
    /// <param name="options">初始化选项</param>
    public Department(DepartmentInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyStructureInformation(options);
        ApplyStatusInformation(options);
        ApplyCreatorInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Pid = 0;
        CodeRelationship = string.Empty;
        DepartmentName = "Radish";
        LeaderName = "System";
        OrderSort = 0;
        StatusCode = (int)DepartmentStatusCodeEnum.Normal;
        IsDeleted = false;
        CreateBy = "System";
        CreateTime = DateTime.Now;
        ModifyBy = null;
        ModifyTime = null;
        HasChildren = true;
        PidArr = new List<long>();
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(DepartmentInitializationOptions options)
    {
        DepartmentName = NormalizeRequired(options.DepartmentName, nameof(options.DepartmentName));

        if (!string.IsNullOrWhiteSpace(options.LeaderName))
        {
            LeaderName = options.LeaderName.Trim();
        }

        if (options.OrderSort.HasValue)
        {
            OrderSort = options.OrderSort.Value;
        }
    }

    /// <summary>处理结构信息</summary>
    private void ApplyStructureInformation(DepartmentInitializationOptions options)
    {
        if (options.Pid.HasValue)
        {
            Pid = options.Pid.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.CodeRelationship))
        {
            CodeRelationship = options.CodeRelationship.Trim();
        }

        if (options.PidArr != null)
        {
            PidArr = NormalizeIds(options.PidArr);
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(DepartmentInitializationOptions options)
    {
        if (options.StatusCode.HasValue)
        {
            StatusCode = options.StatusCode.Value;
        }

        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
        }

        if (options.HasChildren.HasValue)
        {
            HasChildren = options.HasChildren.Value;
        }
    }

    /// <summary>处理创建者信息</summary>
    private void ApplyCreatorInformation(DepartmentInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CreateBy))
        {
            CreateBy = options.CreateBy.Trim();
        }
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} 不能为空。", paramName);
        }

        return value.Trim();
    }

    private static List<long> NormalizeIds(IEnumerable<long> ids)
    {
        return ids.Where(id => id > 0).Distinct().ToList();
    }


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
    public int StatusCode { get; set; } = (int)DepartmentStatusCodeEnum.Normal;

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

/// <summary>部门初始化选项</summary>
public sealed class DepartmentInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="departmentName">部门名称</param>
    public DepartmentInitializationOptions(string departmentName)
    {
        DepartmentName = departmentName ?? throw new ArgumentNullException(nameof(departmentName));
    }

    /// <summary>部门名称</summary>
    public string DepartmentName { get; }

    /// <summary>上级部门 Id</summary>
    public long? Pid { get; set; }

    /// <summary>部门关系编码</summary>
    public string? CodeRelationship { get; set; }

    /// <summary>负责人名称</summary>
    public string? LeaderName { get; set; }

    /// <summary>排序</summary>
    public int? OrderSort { get; set; }

    /// <summary>部门状态</summary>
    public int? StatusCode { get; set; }

    /// <summary>是否已被删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>创建者的名称</summary>
    public string? CreateBy { get; set; }

    /// <summary>是否有子部门</summary>
    public bool? HasChildren { get; set; }

    /// <summary>自定义部门 Id 列表</summary>
    public IEnumerable<long>? PidArr { get; set; }
}