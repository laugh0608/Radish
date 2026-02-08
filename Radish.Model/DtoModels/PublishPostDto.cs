using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Radish.Model.DtoModels;

/// <summary>
/// 发布帖子请求DTO
/// </summary>
public class PublishPostDto
{
    /// <summary>帖子标题</summary>
    [Required(ErrorMessage = "帖子标题不能为空")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "帖子标题长度必须在1-200个字符之间")]
    public string Title { get; set; } = string.Empty;

    /// <summary>帖子内容</summary>
    [Required(ErrorMessage = "帖子内容不能为空")]
    [StringLength(50000, MinimumLength = 1, ErrorMessage = "帖子内容长度必须在1-50000个字符之间")]
    public string Content { get; set; } = string.Empty;

    /// <summary>内容类型（markdown、html、text）</summary>
    [StringLength(20, ErrorMessage = "内容类型长度不能超过20个字符")]
    public string? ContentType { get; set; }

    /// <summary>分类 ID</summary>
    [Required(ErrorMessage = "分类ID不能为空")]
    [Range(1, long.MaxValue, ErrorMessage = "分类ID必须大于0")]
    public long CategoryId { get; set; }

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
