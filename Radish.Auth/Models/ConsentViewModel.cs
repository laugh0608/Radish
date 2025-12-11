namespace Radish.Auth.Models;

/// <summary>
/// OIDC 授权同意页视图模型。
/// </summary>
public class ConsentViewModel
{
    /// <summary>
    /// 客户端 ID（client_id）。
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端显示名称。
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// 请求的 scope 原始字符串，空格分隔，便于调试查看。
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// 解析后的 scope 列表。
    /// </summary>
    public IReadOnlyList<string> Scopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 授权响应类型（response_type），例如：code。
    /// </summary>
    public string ResponseType { get; set; } = string.Empty;

    /// <summary>
    /// 授权完成后的回调地址（redirect_uri）。
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 授权请求状态（state），用于防 CSRF。
    /// </summary>
    public string State { get; set; } = string.Empty;
}
