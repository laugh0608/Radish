using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    /// <summary>客户端提交意图 ID，用于网络重试和重复提交保护</summary>
    [StringLength(80, ErrorMessage = "clientSubmissionId 长度不能超过 80 个字符")]
    public string? ClientSubmissionId { get; set; }

    /// <summary>分类 ID（可选，不传则保持原分类）</summary>
    [Range(1, long.MaxValue, ErrorMessage = "分类ID必须大于0")]
    public long? CategoryId { get; set; }

    /// <summary>标签名称列表</summary>
    public List<string>? TagNames { get; set; }

    /// <summary>
    /// 向后兼容旧字段 tags
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags
    {
        get => TagNames;
        set => TagNames = value;
    }
}
