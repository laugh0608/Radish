using System.ComponentModel.DataAnnotations;

namespace Radish.Auth.ViewModels.Account;

/// <summary>
/// 注册视图模型
/// </summary>
public sealed class RegisterViewModel
{
    /// <summary>
    /// 用户名
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(32, MinimumLength = 3, ErrorMessage = "登录名长度必须在 3-32 个字符之间")]
    [RegularExpression(@"^(?=.*[a-zA-Z])[a-zA-Z0-9]+$", ErrorMessage = "登录名只能包含字母和数字，且至少包含一个字母")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在 6-100 个字符之间")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 确认密码
    /// </summary>
    [Required(ErrorMessage = "确认密码不能为空")]
    [Compare("Password", ErrorMessage = "两次输入的密码不一致")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>
    /// 电子邮箱
    /// </summary>
    [Required(ErrorMessage = "电子邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(254, ErrorMessage = "邮箱长度不能超过 254 个字符")]
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
