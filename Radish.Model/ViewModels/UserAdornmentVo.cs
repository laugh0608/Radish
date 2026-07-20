namespace Radish.Model.ViewModels;

/// <summary>用户当前公开身份装饰。</summary>
/// <remarks>仅包含公开展示所需字段，不暴露权益、订单或商城流水标识。</remarks>
public sealed class UserAdornmentVo
{
    /// <summary>当前有效徽章。</summary>
    public UserAdornmentItemVo? VoBadge { get; set; }

    /// <summary>当前有效称号。</summary>
    public UserAdornmentItemVo? VoTitle { get; set; }
}

/// <summary>一项公开身份装饰。</summary>
public sealed class UserAdornmentItemVo
{
    /// <summary>公开资源标识。</summary>
    public string VoResourceKey { get; set; } = string.Empty;

    /// <summary>公开展示名称。</summary>
    public string VoName { get; set; } = string.Empty;

    /// <summary>安全公开附件地址；纯文本称号可以为空。</summary>
    public string? VoImageUrl { get; set; }
}
