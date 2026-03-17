namespace Radish.Auth.Models;

/// <summary>
/// OIDC 授权确认页显示策略。
/// </summary>
public sealed class AuthorizationConsentOptions
{
    /// <summary>
    /// 官方内置应用是否默认显示授权确认页。
    /// </summary>
    public bool RequireConsentForInternalClients { get; set; }

    /// <summary>
    /// 强制显示授权确认页的客户端 Id 列表。
    /// </summary>
    public List<string> ConsentRequiredClientIds { get; set; } = new();

    /// <summary>
    /// 永远跳过授权确认页的客户端 Id 列表。
    /// </summary>
    public List<string> ConsentBypassClientIds { get; set; } = new();
}
