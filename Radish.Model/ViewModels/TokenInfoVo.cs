namespace Radish.Model.ViewModels;

/// <summary>
/// JWT Token 视图模型
/// </summary>
public class TokenInfoVo
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool VoIsSuccess { get; set; } = false;

    /// <summary>
    /// Token 内容
    /// </summary>
    public string VoTokenInfo { get; set; } = string.Empty;

    /// <summary>
    /// 有效期时间
    /// </summary>
    /// <remarks>单位 s</remarks>
    public double VoExpiresIn { get; set; } = 0.0;

    /// <summary>
    /// Token 类型
    /// </summary>
    public string VoTokenType { get; set; } = string.Empty;
}