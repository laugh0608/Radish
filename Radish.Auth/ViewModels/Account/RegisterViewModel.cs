using System.ComponentModel.DataAnnotations;

namespace Radish.Auth.ViewModels.Account;

/// <summary>
/// 注册视图模型
/// </summary>
public sealed class RegisterViewModel
{
    /// <summary>
    /// 公开展示名
    /// </summary>
    [Required(ErrorMessage = "auth.register.error.displayNameRequired")]
    [RegularExpression(@"^[\u4e00-\u9fffa-zA-Z0-9]+$", ErrorMessage = "auth.register.error.displayNameCharacters")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 展示名最小长度
    /// </summary>
    public int DisplayNameMinLength { get; set; } = 2;

    /// <summary>
    /// 展示名最大长度
    /// </summary>
    public int DisplayNameMaxLength { get; set; } = 24;

    /// <summary>
    /// 密码
    /// </summary>
    [Required(ErrorMessage = "auth.register.error.passwordRequired")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "auth.register.error.passwordLength")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 确认密码
    /// </summary>
    [Required(ErrorMessage = "auth.register.error.confirmPasswordRequired")]
    [Compare("Password", ErrorMessage = "auth.register.error.passwordMismatch")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// 电子邮箱
    /// </summary>
    [Required(ErrorMessage = "auth.register.error.emailRequired")]
    [EmailAddress(ErrorMessage = "auth.register.error.emailInvalid")]
    [StringLength(254, ErrorMessage = "auth.register.error.emailLength")]
    public string? Email { get; set; }

    /// <summary>
    /// 返回 URL（注册成功后跳转）
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 成功消息
    /// </summary>
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// 客户端信息
    /// </summary>
    public ClientSummaryViewModel Client { get; set; } = ClientSummaryViewModel.Empty;
}
