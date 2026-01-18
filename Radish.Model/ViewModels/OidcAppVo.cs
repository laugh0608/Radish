using System;
using Radish.Model.OpenIddict;

namespace Radish.Model.ViewModels;

/// <summary>OIDC 客户端应用视图模型</summary>
public class OidcAppVo
{
    /// <summary>应用 Id</summary>
    public long VoId { get; set; }

    /// <summary>客户端 Id</summary>
    public string VoClientId { get; set; } = string.Empty;

    /// <summary>客户端密钥</summary>
    /// <remarks>仅在创建时返回一次，后续不再返回</remarks>
    public string? VoClientSecret { get; set; }

    /// <summary>同意类型</summary>
    public string VoConsentType { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string VoDisplayName { get; set; } = string.Empty;

    /// <summary>客户端类型</summary>
    public string VoType { get; set; } = string.Empty;

    /// <summary>重定向 URI 列表</summary>
    public string VoRedirectUris { get; set; } = string.Empty;

    /// <summary>登出后重定向 URI 列表</summary>
    public string VoPostLogoutRedirectUris { get; set; } = string.Empty;

    /// <summary>权限列表</summary>
    public string VoPermissions { get; set; } = string.Empty;

    /// <summary>要求列表</summary>
    public string VoRequirements { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    public string VoProperties { get; set; } = string.Empty;

    /// <summary>应用图标 URL</summary>
    public string? VoLogo { get; set; }

    /// <summary>应用描述</summary>
    public string? VoDescription { get; set; }

    /// <summary>开发者名称</summary>
    public string? VoDeveloperName { get; set; }

    /// <summary>开发者邮箱</summary>
    public string? VoDeveloperEmail { get; set; }

    /// <summary>应用状态</summary>
    public ApplicationStatus VoStatus { get; set; }

    /// <summary>应用状态描述</summary>
    public string VoStatusText => VoStatus switch
    {
        ApplicationStatus.Active => "激活",
        ApplicationStatus.Disabled => "禁用",
        ApplicationStatus.PendingReview => "待审核",
        _ => "未知"
    };

    /// <summary>应用类型</summary>
    public ApplicationType VoAppType { get; set; }

    /// <summary>应用类型描述</summary>
    public string VoAppTypeText => VoAppType switch
    {
        ApplicationType.Internal => "内部应用",
        ApplicationType.ThirdParty => "第三方应用",
        _ => "未知"
    };

    /// <summary>创建时间</summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>创建者名称</summary>
    public string VoCreateBy { get; set; } = string.Empty;

    /// <summary>更新时间</summary>
    public DateTime? VoModifyTime { get; set; }

    /// <summary>更新者名称</summary>
    public string? VoModifyBy { get; set; }
}
