using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>切换回应 DTO</summary>
public class ToggleReactionDto
{
    [Required(ErrorMessage = "targetType 不能为空")]
    [StringLength(50, ErrorMessage = "targetType 长度不能超过50个字符")]
    public string TargetType { get; set; } = string.Empty;

    [Range(1, long.MaxValue, ErrorMessage = "targetId 必须大于0")]
    public long TargetId { get; set; }

    [Required(ErrorMessage = "emojiType 不能为空")]
    [StringLength(20, ErrorMessage = "emojiType 长度不能超过20个字符")]
    public string EmojiType { get; set; } = string.Empty;

    [Required(ErrorMessage = "emojiValue 不能为空")]
    [StringLength(200, ErrorMessage = "emojiValue 长度不能超过200个字符")]
    public string EmojiValue { get; set; } = string.Empty;
}

/// <summary>批量获取回应汇总 DTO</summary>
public class BatchGetReactionSummaryDto
{
    [Required(ErrorMessage = "targetType 不能为空")]
    [StringLength(50, ErrorMessage = "targetType 长度不能超过50个字符")]
    public string TargetType { get; set; } = string.Empty;

    [Required(ErrorMessage = "targetIds 不能为空")]
    [MinLength(1, ErrorMessage = "targetIds 至少包含一个目标")]
    public List<long> TargetIds { get; set; } = new();
}
