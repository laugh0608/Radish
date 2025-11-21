namespace Radish.Model.ViewModels;

/// <summary>
/// JWT Token 视图模型
/// </summary>
public class TokenInfoVo
{
    public bool IsSuccess { get; set; } = false;
    public string TokenInfo { get; set; } = string.Empty;
    public double ExpiresIn { get; set; } = 0.0;
    public string TokenType { get; set; } = string.Empty;
}