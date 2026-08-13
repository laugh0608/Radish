using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>按已确认预览重算等级配置。</summary>
public sealed class RecalculateLevelConfigsDto
{
    [Required(ErrorMessage = "预览指纹不能为空")]
    [StringLength(64, MinimumLength = 64, ErrorMessage = "预览指纹格式无效")]
    public string ExpectedFingerprint { get; set; } = string.Empty;

    [Required(ErrorMessage = "重算原因不能为空")]
    [MaxLength(500, ErrorMessage = "重算原因不能超过500个字符")]
    public string Reason { get; set; } = string.Empty;
}
