using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 接口 API 地址信息表
/// </summary>
public class ApiModule : RootEntityTKey<long>
{
    /// <summary>初始化默认 API 模块实例</summary>
    public ApiModule()
    {
        InitializeDefaults();
    }

    /// <summary>通过 API 模块名和链接地址初始化 API 模块</summary>
    /// <param name="apiModuleName">接口名称</param>
    /// <param name="linkUrl">API 地址</param>
    public ApiModule(string apiModuleName, string linkUrl)
        : this(new ApiModuleInitializationOptions(apiModuleName, linkUrl))
    {
    }

    /// <summary>通过初始化选项批量构造 API 模块</summary>
    /// <param name="options">初始化选项</param>
    public ApiModule(ApiModuleInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyStructureInformation(options);
        ApplyDisplayInformation(options);
        ApplyStatusInformation(options);
        ApplyCreatorInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        ParentId = 0;
        IsDeleted = true;
        ApiModuleName = string.Empty;
        LinkUrl = "/api/HealthCheck";
        AreaName = string.Empty;
        ControllerName = string.Empty;
        ActionName = string.Empty;
        IconUrl = string.Empty;
        ModuleCode = string.Empty;
        OrderSort = 0;
        ModuleDescription = string.Empty;
        IsMenu = false;
        IsEnabled = false;
        CreateId = 0;
        CreateBy = "System";
        CreateTime = DateTime.Now;
        ModifyId = null;
        ModifyBy = null;
        ModifyTime = null;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(ApiModuleInitializationOptions options)
    {
        ApiModuleName = NormalizeRequired(options.ApiModuleName, nameof(options.ApiModuleName));
        LinkUrl = NormalizeRequired(options.LinkUrl, nameof(options.LinkUrl));

        if (options.ParentId.HasValue)
        {
            ParentId = options.ParentId.Value;
        }
    }

    /// <summary>处理结构信息</summary>
    private void ApplyStructureInformation(ApiModuleInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AreaName))
        {
            AreaName = options.AreaName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.ControllerName))
        {
            ControllerName = options.ControllerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.ActionName))
        {
            ActionName = options.ActionName.Trim();
        }
    }

    /// <summary>处理展示信息</summary>
    private void ApplyDisplayInformation(ApiModuleInitializationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.IconUrl))
        {
            IconUrl = options.IconUrl.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.ModuleCode))
        {
            ModuleCode = options.ModuleCode.Trim();
        }

        if (options.OrderSort.HasValue)
        {
            OrderSort = options.OrderSort.Value;
        }

        if (!string.IsNullOrWhiteSpace(options.ModuleDescription))
        {
            ModuleDescription = options.ModuleDescription.Trim();
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(ApiModuleInitializationOptions options)
    {
        if (options.IsMenu.HasValue)
        {
            IsMenu = options.IsMenu.Value;
        }

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
    private void ApplyCreatorInformation(ApiModuleInitializationOptions options)
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

/// <summary>API 模块初始化选项</summary>
public sealed class ApiModuleInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="apiModuleName">接口名称</param>
    /// <param name="linkUrl">API 地址</param>
    public ApiModuleInitializationOptions(string apiModuleName, string linkUrl)
    {
        ApiModuleName = apiModuleName ?? throw new ArgumentNullException(nameof(apiModuleName));
        LinkUrl = linkUrl ?? throw new ArgumentNullException(nameof(linkUrl));
    }

    /// <summary>接口名称</summary>
    public string ApiModuleName { get; }

    /// <summary>API 地址</summary>
    public string LinkUrl { get; }

    /// <summary>父节点 Id</summary>
    public long? ParentId { get; set; }

    /// <summary>区域名称</summary>
    public string? AreaName { get; set; }

    /// <summary>控制器名称</summary>
    public string? ControllerName { get; set; }

    /// <summary>Action 名称</summary>
    public string? ActionName { get; set; }

    /// <summary>图标 Url/icon</summary>
    public string? IconUrl { get; set; }

    /// <summary>菜单编号</summary>
    public string? ModuleCode { get; set; }

    /// <summary>排序</summary>
    public int? OrderSort { get; set; }

    /// <summary>描述</summary>
    public string? ModuleDescription { get; set; }

    /// <summary>是否是左/右菜单</summary>
    public bool? IsMenu { get; set; }

    /// <summary>是否激活</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>创建者 Id</summary>
    public long? CreateId { get; set; }

    /// <summary>创建者</summary>
    public string? CreateBy { get; set; }
}