using System;

namespace Radish.Model.ViewModels;

/// <summary>OIDC 授权记录视图模型</summary>
public class OidcAuthVo
{
    /// <summary>授权 Id</summary>
    public long VoId { get; set; }

    /// <summary>关联的应用 Id</summary>
    public long VoApplicationId { get; set; }

    /// <summary>授权主体（用户 Id）</summary>
    public string VoSubject { get; set; } = string.Empty;

    /// <summary>授权类型</summary>
    public string VoType { get; set; } = string.Empty;

    /// <summary>授权状态</summary>
    public string VoStatus { get; set; } = string.Empty;

    /// <summary>授权的作用域列表</summary>
    public string VoScopes { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    public string VoProperties { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }
}
