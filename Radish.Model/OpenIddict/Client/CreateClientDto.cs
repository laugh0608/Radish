using System.ComponentModel.DataAnnotations;

namespace Radish.Model.ViewModels.Client;

/// <summary>
/// 创建客户端 DTO
/// </summary>
public class CreateClientDto
{
    /// <summary>
    /// 客户端 ID（必填，唯一）
    /// </summary>
    [Required(ErrorMessage = "客户端 ID 不能为空")]
    [StringLength(100, ErrorMessage = "客户端 ID 长度不能超过 100")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [StringLength(200, ErrorMessage = "显示名称长度不能超过 200")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 客户端类型（confidential/public）
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 授权类型（authorization_code, client_credentials, refresh_token 等）
    /// </summary>
    [Required(ErrorMessage = "授权类型不能为空")]
    public List<string> GrantTypes { get; set; } = new();

    /// <summary>
    /// 回调地址
    /// </summary>
    public List<string>? RedirectUris { get; set; }

    /// <summary>
    /// 登出回调地址
    /// </summary>
    public List<string>? PostLogoutRedirectUris { get; set; }

    /// <summary>
    /// 授权范围
    /// </summary>
    [Required(ErrorMessage = "授权范围不能为空")]
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// 同意类型（explicit/implicit）
    /// </summary>
    public string? ConsentType { get; set; }

    /// <summary>
    /// 是否需要 PKCE
    /// </summary>
    public bool RequirePkce { get; set; }

    /// <summary>
    /// 是否需要 Client Secret（机密客户端）
    /// </summary>
    public bool RequireClientSecret { get; set; }
}
