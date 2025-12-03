using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Model;

/// <summary>角色实体</summary>
/// <remarks>主键为 Id，类型为 long</remarks>
public class Role : RootEntityTKey<long>
{
    /// <summary>初始化默认角色实例</summary>
    public Role()
    {
        InitializeDefaults();
    }

    /// <summary>通过角色名初始化角色</summary>
    /// <param name="roleName">角色名</param>
    public Role(string roleName)
        : this(new RoleInitializationOptions(roleName))
    {
    }

    /// <summary>通过初始化选项批量构造角色</summary>
    /// <param name="options">初始化选项</param>
    public Role(RoleInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyPermissionInformation(options);
        ApplyStatusInformation(options);
        ApplyCreatorInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        IsDeleted = true;
        RoleName = string.Empty;
        RoleDescription = string.Empty;
        OrderSort = 0;
        DepartmentIds = string.Empty;
        AuthorityScope = (int)AuthorityScopeKindEnum.None;
        IsEnabled = false;
        CreateId = 0;
        CreateBy = "System";
        CreateTime = DateTime.Now;
        ModifyId = 0;
        ModifyBy = "System";
        ModifyTime = DateTime.Now;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(RoleInitializationOptions options)
    {
        RoleName = NormalizeRequired(options.RoleName, nameof(options.RoleName));

        if (!string.IsNullOrWhiteSpace(options.RoleDescription))
        {
            RoleDescription = options.RoleDescription.Trim();
        }

        if (options.OrderSort.HasValue)
        {
            OrderSort = options.OrderSort.Value;
        }
    }

    /// <summary>处理权限信息</summary>
    private void ApplyPermissionInformation(RoleInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.DepartmentIds))
        {
            DepartmentIds = options.DepartmentIds.Trim();
        }

        if (options.AuthorityScope.HasValue)
        {
            AuthorityScope = options.AuthorityScope.Value;
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(RoleInitializationOptions options)
    {
        if (options.IsEnabled.HasValue)
        {
            IsEnabled = options.IsEnabled.Value;
        }

        if (options.IsDeleted.HasValue)
        {
            IsDeleted = options.IsDeleted.Value;
        }
    }

    /// <summary>处理创建者信息</summary>
    private void ApplyCreatorInformation(RoleInitializationOptions options)
    {
        if (options.CreateId.HasValue)
        {
            CreateId = options.CreateId.Value;
        }

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


    /// <summary>获取或设置是否禁用，逻辑上的删除，非物理删除</summary>
    /// <remarks>不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsDeleted { get; set; } = true;

    /// <summary>角色名</summary>
    /// <remarks>不可为空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色描述</summary>
    /// <remarks>不可为空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string RoleDescription { get; set; } = string.Empty;

    /// <summary>排序</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public int OrderSort { get; set; } = 0;

    /// <summary>自定义权限的部门 Ids</summary>
    /// <remarks>不可为空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string DepartmentIds { get; set; } = string.Empty;

    /// <summary>权限范围</summary>
    /// <remarks><para>不可为空</para>
    /// <para>默认为 -1 无任何权限</para>
    /// <para>-1 无任何权限；1 自定义权限；2 本部门；3 本部门及以下；4 仅自己；9 全部；</para></remarks>
    [SugarColumn(IsNullable = true)]
    public int AuthorityScope { get; set; } = (int)AuthorityScopeKindEnum.None;

    /// <summary>是否激活</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = true)]
    public bool IsEnabled { get; set; } = false;

    /// <summary>创建者的 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = true)]
    public long CreateId { get; set; } = 0;

    /// <summary>创建者的名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = true, IsOnlyIgnoreUpdate=true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>信息修改者的 Id</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }

    /// <summary>修改者的名称</summary>
    /// <remarks>最大 50 字符</remarks>
    [SugarColumn(Length = 50)]
    public string? ModifyBy { get; set; }

    /// <summary>修改时间</summary>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }
}

/// <summary>角色初始化选项</summary>
public sealed class RoleInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="roleName">角色名</param>
    public RoleInitializationOptions(string roleName)
    {
        RoleName = roleName ?? throw new ArgumentNullException(nameof(roleName));
    }

    /// <summary>角色名</summary>
    public string RoleName { get; }

    /// <summary>角色描述</summary>
    public string? RoleDescription { get; set; }

    /// <summary>排序</summary>
    public int? OrderSort { get; set; }

    /// <summary>自定义权限的部门 Ids</summary>
    public string? DepartmentIds { get; set; }

    /// <summary>权限范围</summary>
    public int? AuthorityScope { get; set; }

    /// <summary>是否激活</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>创建者的 Id</summary>
    public long? CreateId { get; set; }

    /// <summary>创建者的名称</summary>
    public string? CreateBy { get; set; }
}