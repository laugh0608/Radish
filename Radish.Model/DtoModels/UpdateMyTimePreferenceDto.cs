using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 更新我的时间偏好
/// </summary>
public class UpdateMyTimePreferenceDto
{
    /// <summary>
    /// 用户时区（IANA）
    /// </summary>
    [Required(ErrorMessage = "时区不能为空")]
    [StringLength(100, ErrorMessage = "时区长度不能超过100个字符")]
    public string TimeZoneId { get; set; } = string.Empty;
}
