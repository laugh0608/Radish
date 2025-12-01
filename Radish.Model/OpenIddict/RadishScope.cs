using System;
using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model.OpenIddict;

/// <summary>OIDC 作用域表</summary>
/// <remarks>用于定义可用的 OAuth 2.0 / OIDC 作用域</remarks>
public class RadishScope : RootEntityTKey<long>
{
    /// <summary>初始化默认作用域实例</summary>
    public RadishScope()
    {
        InitializeDefaults();
    }

    /// <summary>通过作用域名称初始化作用域</summary>
    /// <param name="name">作用域名称</param>
    public RadishScope(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("作用域名称不能为空。", nameof(name));
        }

        InitializeDefaults();
        Name = name.Trim();
    }

    /// <summary>统一设置默认值</summary>
    private void InitializeDefaults()
    {
        Name = string.Empty;
        DisplayName = string.Empty;
        Description = null;
        Resources = string.Empty;
        Properties = string.Empty;
        CreateTime = DateTime.Now;
    }


    /// <summary>作用域名称</summary>
    /// <remarks>
    /// <para>不可为空，最大 200 字符，唯一标识作用域</para>
    /// <para>如：openid, profile, email, radish-api 等</para>
    /// </remarks>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    /// <remarks>可空，最大 200 字符，向用户展示的作用域名称</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>作用域描述</summary>
    /// <remarks>可空，最大 1000 字符，描述该作用域的用途</remarks>
    [SugarColumn(Length = 1000, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>关联的资源列表</summary>
    /// <remarks>可空，最大长度 4000，JSON 格式存储，如 API 资源名称</remarks>
    [SugarColumn(Length = 4000, IsNullable = true, ColumnDataType = "text")]
    public string Resources { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    /// <remarks>可空，最大长度 8000，JSON 格式存储</remarks>
    [SugarColumn(Length = 8000, IsNullable = true, ColumnDataType = "text")]
    public string Properties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    /// <remarks>不可为空，默认为当前时间</remarks>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;
}
