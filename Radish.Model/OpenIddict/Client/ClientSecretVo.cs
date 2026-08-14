namespace Radish.Model.ViewModels.Client;

/// <summary>
/// 客户端密钥视图模型（仅在创建/重置时返回一次）
/// </summary>
public class ClientSecretVo
{
    /// <summary>
    /// 客户端 ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端密钥（明文，仅机密客户端创建或轮换成功后返回一次）
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// 提示信息
    /// </summary>
    public string Message { get; set; } = "请妥善保管此密钥，关闭后将无法再次查看";
}
