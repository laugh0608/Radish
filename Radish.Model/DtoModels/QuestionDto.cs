using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 创建回答 DTO
/// </summary>
public class CreateAnswerDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }

    /// <summary>回答内容</summary>
    [Required(ErrorMessage = "回答内容不能为空")]
    [StringLength(20000, MinimumLength = 1, ErrorMessage = "回答内容长度必须在1-20000个字符之间")]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// 采纳回答 DTO
/// </summary>
public class AcceptAnswerDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }

    /// <summary>回答 ID</summary>
    [Required(ErrorMessage = "回答ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "回答ID必须大于0")]
    public long AnswerId { get; set; }
}
