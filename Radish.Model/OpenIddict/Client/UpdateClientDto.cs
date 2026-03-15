using System.ComponentModel.DataAnnotations;

namespace Radish.Model.ViewModels.Client;

/// <summary>
/// 更新客户端 DTO
/// </summary>
public class UpdateClientDto
{
    /// <summary>
    /// 显示名称
    /// </summary>
    [StringLength(200, ErrorMessage = "显示名称长度不能超过 200")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// 应用描述
    /// </summary>
    [StringLength(1000, ErrorMessage = "应用描述长度不能超过 1000")]
    public string? Description { get; set; }

    /// <summary>
    /// 开发者名称
    /// </summary>
    [StringLength(100, ErrorMessage = "开发者名称长度不能超过 100")]
    public string? DeveloperName { get; set; }

    /// <summary>
    /// 开发者邮箱
    /// </summary>
    [EmailAddress(ErrorMessage = "开发者邮箱格式不正确")]
    [StringLength(200, ErrorMessage = "开发者邮箱长度不能超过 200")]
    public string? DeveloperEmail { get; set; }

    /// <summary>
    /// 授权类型
    /// </summary>
    public List<string>? GrantTypes { get; set; }

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
    public List<string>? Scopes { get; set; }

    /// <summary>
    /// 同意类型
    /// </summary>
    public string? ConsentType { get; set; }

    /// <summary>
    /// 是否需要 PKCE
    /// </summary>
    public bool? RequirePkce { get; set; }
}
