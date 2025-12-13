using System;

namespace Radish.Model.ViewModels;

/// <summary>OIDC 作用域视图模型</summary>
public class OidcScopeVo
{
    /// <summary>作用域 Id</summary>
    public long Id { get; set; }

    /// <summary>作用域名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>作用域描述</summary>
    public string? Description { get; set; }

    /// <summary>关联的资源列表</summary>
    public string Resources { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    public string Properties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }
}
