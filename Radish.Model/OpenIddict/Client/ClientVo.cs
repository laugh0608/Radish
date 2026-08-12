namespace Radish.Model.ViewModels.Client;

/// <summary>
/// 客户端视图模型
/// </summary>
public class ClientVo
{
    /// <summary>
    /// 客户端唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 客户端 ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 应用描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 开发者名称
    /// </summary>
    public string? DeveloperName { get; set; }

    /// <summary>
    /// 开发者邮箱
    /// </summary>
    public string? DeveloperEmail { get; set; }

    /// <summary>
    /// 客户端类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 应用状态
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// OpenIddict 客户端类型（public/confidential）
    /// </summary>
    public string ClientType { get; set; } = "public";

    /// <summary>
    /// 授权类型
    /// </summary>
    public List<string> GrantTypes { get; set; } = new();

    /// <summary>
    /// 回调地址
    /// </summary>
    public List<string> RedirectUris { get; set; } = new();

    /// <summary>
    /// 登出回调地址
    /// </summary>
    public List<string> PostLogoutRedirectUris { get; set; } = new();

    /// <summary>
    /// 授权范围
    /// </summary>
    public List<string> Scopes { get; set; } = new();

    /// <summary>
    /// 同意类型（Explicit/Implicit/External/Systematic）
    /// </summary>
    public string? ConsentType { get; set; }

    /// <summary>
    /// 是否需要 PKCE
    /// </summary>
    public bool RequirePkce { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime? CreatedAt { get; set; }
}
