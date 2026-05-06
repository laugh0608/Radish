using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public class ChangeMyLoginPasswordDto
{
    [Required(ErrorMessage = "当前密码不能为空")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "新密码不能为空")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "确认密码不能为空")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
