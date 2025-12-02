using System;
using Radish.Model.OpenIddict;

namespace Radish.Model.ViewModels;

/// <summary>OIDC 客户端应用视图模型</summary>
public class VoOidcApp
{
    /// <summary>应用 Id</summary>
    public long Id { get; set; }

    /// <summary>客户端 Id</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>客户端密钥</summary>
    /// <remarks>仅在创建时返回一次，后续不再返回</remarks>
    public string? ClientSecret { get; set; }

    /// <summary>同意类型</summary>
    public string ConsentType { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>客户端类型</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>重定向 URI 列表</summary>
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>登出后重定向 URI 列表</summary>
    public string PostLogoutRedirectUris { get; set; } = string.Empty;

    /// <summary>权限列表</summary>
    public string Permissions { get; set; } = string.Empty;

    /// <summary>要求列表</summary>
    public string Requirements { get; set; } = string.Empty;

    /// <summary>自定义属性</summary>
    public string Properties { get; set; } = string.Empty;

    /// <summary>应用图标 URL</summary>
    public string? Logo { get; set; }

    /// <summary>应用描述</summary>
    public string? Description { get; set; }

    /// <summary>开发者名称</summary>
    public string? DeveloperName { get; set; }

    /// <summary>开发者邮箱</summary>
    public string? DeveloperEmail { get; set; }

    /// <summary>应用状态</summary>
    public ApplicationStatus Status { get; set; }

    /// <summary>应用状态描述</summary>
    public string StatusText => Status switch
    {
        ApplicationStatus.Active => "激活",
        ApplicationStatus.Disabled => "禁用",
        ApplicationStatus.PendingReview => "待审核",
        _ => "未知"
    };

    /// <summary>应用类型</summary>
    public ApplicationType AppType { get; set; }

    /// <summary>应用类型描述</summary>
    public string AppTypeText => AppType switch
    {
        ApplicationType.Internal => "内部应用",
        ApplicationType.ThirdParty => "第三方应用",
        _ => "未知"
    };

    /// <summary>创建时间</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>创建者名称</summary>
    public string CreateBy { get; set; } = string.Empty;

    /// <summary>更新时间</summary>
    public DateTime? ModifyTime { get; set; }

    /// <summary>更新者名称</summary>
    public string? ModifyBy { get; set; }
}
