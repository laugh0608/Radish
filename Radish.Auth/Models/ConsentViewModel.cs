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
}
