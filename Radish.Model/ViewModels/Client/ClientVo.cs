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
    /// 客户端类型
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 授权类型（逗号分隔）
    /// </summary>
    public string? GrantTypes { get; set; }

    /// <summary>
    /// 回调地址（逗号分隔）
    /// </summary>
    public string? RedirectUris { get; set; }

    /// <summary>
    /// 登出回调地址（逗号分隔）
    /// </summary>
    public string? PostLogoutRedirectUris { get; set; }

    /// <summary>
    /// 授权范围（逗号分隔）
    /// </summary>
    public string? Scopes { get; set; }

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
