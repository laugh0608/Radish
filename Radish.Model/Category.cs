using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>论坛分类实体</summary>
/// <remarks>支持多级分类，主键为 Id，类型为 long</remarks>
public class Category : RootEntityTKey<long>
{
    /// <summary>初始化默认分类实例</summary>
    public Category()
    {
        InitializeDefaults();
    }

    /// <summary>通过分类名初始化分类</summary>
    /// <param name="name">分类名称</param>
    public Category(string name)
        : this(new CategoryInitializationOptions(name))
    {
    }

    /// <summary>通过初始化选项批量构造分类</summary>
    /// <param name="options">初始化选项</param>
    public Category(CategoryInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyHierarchyInformation(options);
        ApplyStatusInformation(options);
        ApplyAuditInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Name = string.Empty;
        Slug = string.Empty;
        Description = string.Empty;
        Icon = string.Empty;
        CoverImage = string.Empty;
        ParentId = null;
        Level = 0;
        OrderSort = 0;
        PostCount = 0;
        IsEnabled = true;
        IsDeleted = false;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(CategoryInitializationOptions options)
    {
        Name = NormalizeRequired(options.Name, nameof(options.Name));

        if (!string.IsNullOrWhiteSpace(options.Slug))
        {
            Slug = options.Slug.Trim().ToLowerInvariant();
        }
        else
        {
            // 默认使用名称生成 slug
            Slug = Name.ToLowerInvariant().Replace(" ", "-");
        }

        if (!string.IsNullOrWhiteSpace(options.Description))
        {
            Description = options.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Icon))
        {
            Icon = options.Icon.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.CoverImage))
        {
            CoverImage = options.CoverImage.Trim();
        }

        if (options.OrderSort.HasValue)
        {
            OrderSort = options.OrderSort.Value;
        }
    }

    /// <summary>处理层级信息</summary>
    private void ApplyHierarchyInformation(CategoryInitializationOptions options)
    {
        if (options.ParentId.HasValue)
        {
            ParentId = options.ParentId.Value;
        }

        if (options.Level.HasValue)
        {
            Level = Math.Max(0, options.Level.Value);
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(CategoryInitializationOptions options)
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

    /// <summary>处理审计信息</summary>
    private void ApplyAuditInformation(CategoryInitializationOptions options)
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

    #region 基础信息

    /// <summary>分类名称</summary>
    /// <remarks>不可为空，最大 100 字符</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL 友好的标识符</summary>
    /// <remarks>不可为空，最大 100 字符，唯一索引</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>分类描述</summary>
    /// <remarks>可空，最大 1000 字符</remarks>
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string Description { get; set; } = string.Empty;

    /// <summary>分类图标</summary>
    /// <remarks>可空，最大 200 字符，可存储图标类名或图标 URL</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string Icon { get; set; } = string.Empty;

    /// <summary>分类封面图</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string CoverImage { get; set; } = string.Empty;

    #endregion

    #region 层级信息

    /// <summary>父分类 Id</summary>
    /// <remarks>可空，顶级分类为 null</remarks>
    [SugarColumn(IsNullable = true)]
    public long? ParentId { get; set; }

    /// <summary>层级深度</summary>
    /// <remarks>不可为空，顶级为 0，子级递增，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int Level { get; set; } = 0;

    /// <summary>排序权重</summary>
    /// <remarks>不可为空，数值越大越靠前，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int OrderSort { get; set; } = 0;

    #endregion

    #region 统计信息

    /// <summary>帖子数量</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public int PostCount { get; set; } = 0;

    #endregion

    #region 状态信息

    /// <summary>是否启用</summary>
    /// <remarks>不可为空，默认为 true</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否删除</summary>
    /// <remarks>不可为空，默认为 false</remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    #endregion

    #region 审计信息

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间，更新时忽略该列</remarks>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者名称</summary>
    /// <remarks>不可为空，最大 50 字符，默认为 System</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    /// <remarks>不可为空，默认为 0</remarks>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; } = 0;

    /// <summary>修改时间</summary>
    /// <remarks>可空</remarks>
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改者名称</summary>
    /// <remarks>可空，最大 50 字符</remarks>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改者 Id</summary>
    /// <remarks>可空</remarks>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }

    #endregion
}

/// <summary>分类初始化选项</summary>
public sealed class CategoryInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="name">分类名称</param>
    public CategoryInitializationOptions(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>分类名称</summary>
    public string Name { get; }

    /// <summary>URL 友好标识符</summary>
    public string? Slug { get; set; }

    /// <summary>分类描述</summary>
    public string? Description { get; set; }

    /// <summary>分类图标</summary>
    public string? Icon { get; set; }

    /// <summary>分类封面图</summary>
    public string? CoverImage { get; set; }

    /// <summary>父分类 Id</summary>
    public long? ParentId { get; set; }

    /// <summary>层级深度</summary>
    public int? Level { get; set; }

    /// <summary>排序权重</summary>
    public int? OrderSort { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>创建者 Id</summary>
    public long? CreateId { get; set; }

    /// <summary>创建者名称</summary>
    public string? CreateBy { get; set; }
}
