using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>标签实体</summary>
/// <remarks>用于帖子标签功能，主键为 Id，类型为 long</remarks>
public class Tag : RootEntityTKey<long>
{
    /// <summary>初始化默认标签实例</summary>
    public Tag()
    {
        InitializeDefaults();
    }

    /// <summary>通过标签名初始化标签</summary>
    /// <param name="name">标签名称</param>
    public Tag(string name)
        : this(new TagInitializationOptions(name))
    {
    }

    /// <summary>通过初始化选项批量构造标签</summary>
    /// <param name="options">初始化选项</param>
    public Tag(TagInitializationOptions options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeDefaults();
        ApplyBasicInformation(options);
        ApplyStatusInformation(options);
        ApplyAuditInformation(options);
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Name = string.Empty;
        Slug = string.Empty;
        Description = string.Empty;
        Color = string.Empty;
        PostCount = 0;
        IsEnabled = true;
        IsDeleted = false;
        CreateTime = DateTime.Now;
        CreateBy = "System";
        CreateId = 0;
    }

    /// <summary>处理基础信息</summary>
    private void ApplyBasicInformation(TagInitializationOptions options)
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

        if (!string.IsNullOrWhiteSpace(options.Color))
        {
            Color = options.Color.Trim();
        }
    }

    /// <summary>处理状态信息</summary>
    private void ApplyStatusInformation(TagInitializationOptions options)
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
    private void ApplyAuditInformation(TagInitializationOptions options)
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

    /// <summary>标签名称</summary>
    /// <remarks>不可为空，最大 50 字符，唯一索引</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL 友好的标识符</summary>
    /// <remarks>不可为空，最大 50 字符，唯一索引</remarks>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>标签描述</summary>
    /// <remarks>可空，最大 500 字符</remarks>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string Description { get; set; } = string.Empty;

    /// <summary>标签颜色</summary>
    /// <remarks>可空，最大 20 字符，存储颜色代码如 #FF5733</remarks>
    [SugarColumn(Length = 20, IsNullable = true)]
    public string Color { get; set; } = string.Empty;

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

/// <summary>标签初始化选项</summary>
public sealed class TagInitializationOptions
{
    /// <summary>必填项构造</summary>
    /// <param name="name">标签名称</param>
    public TagInitializationOptions(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>标签名称</summary>
    public string Name { get; }

    /// <summary>URL 友好标识符</summary>
    public string? Slug { get; set; }

    /// <summary>标签描述</summary>
    public string? Description { get; set; }

    /// <summary>标签颜色</summary>
    public string? Color { get; set; }

    /// <summary>是否启用</summary>
    public bool? IsEnabled { get; set; }

    /// <summary>是否删除</summary>
    public bool? IsDeleted { get; set; }

    /// <summary>创建者 Id</summary>
    public long? CreateId { get; set; }

    /// <summary>创建者名称</summary>
    public string? CreateBy { get; set; }
}
