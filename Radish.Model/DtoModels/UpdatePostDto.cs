using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>
/// 编辑帖子请求DTO
/// </summary>
public class UpdatePostDto
{
    /// <summary>帖子 ID</summary>
    [Required(ErrorMessage = "帖子ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "帖子ID必须大于0")]
    public long PostId { get; set; }

    /// <summary>帖子标题</summary>
    [Required(ErrorMessage = "帖子标题不能为空")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "帖子标题长度必须在1-200个字符之间")]
    public string Title { get; set; } = string.Empty;

    /// <summary>帖子内容</summary>
    [Required(ErrorMessage = "帖子内容不能为空")]
    [StringLength(50000, MinimumLength = 1, ErrorMessage = "帖子内容长度必须在1-50000个字符之间")]
    public string Content { get; set; } = string.Empty;

    /// <summary>分类 ID（可选，不传则保持原分类）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "分类ID必须大于0")]
    public long? CategoryId { get; set; }
}