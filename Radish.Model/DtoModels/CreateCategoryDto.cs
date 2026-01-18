using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>创建分类 DTO</summary>
/// <remarks>用于接收创建分类的请求参数，避免直接暴露实体类</remarks>
public class CreateCategoryDto
{
    /// <summary>分类名称</summary>
    /// <remarks>必填，最大 100 字符</remarks>
    [Required(ErrorMessage = "分类名称不能为空")]
    [StringLength(100, ErrorMessage = "分类名称不能超过100个字符")]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL 友好的标识符</summary>
    /// <remarks>可选，最大 100 字符，如果不提供则自动生成</remarks>
    [StringLength(100, ErrorMessage = "标识符不能超过100个字符")]
    public string? Slug { get; set; }

    /// <summary>分类描述</summary>
    /// <remarks>可选，最大 1000 字符</remarks>
    [StringLength(1000, ErrorMessage = "分类描述不能超过1000个字符")]
    public string? Description { get; set; }

    /// <summary>分类图标</summary>
    /// <remarks>可选，最大 200 字符，可存储图标类名或图标 URL</remarks>
    [StringLength(200, ErrorMessage = "分类图标不能超过200个字符")]
    public string? Icon { get; set; }

    /// <summary>分类封面图</summary>
    /// <remarks>可选，最大 500 字符</remarks>
    [StringLength(500, ErrorMessage = "分类封面图不能超过500个字符")]
    public string? CoverImage { get; set; }

    /// <summary>父分类 Id</summary>
    /// <remarks>可选，顶级分类为 null</remarks>
    public long? ParentId { get; set; }

    /// <summary>排序权重</summary>
    /// <remarks>可选，数值越大越靠前，默认为 0</remarks>
    [Range(0, int.MaxValue, ErrorMessage = "排序权重不能为负数")]
    public int OrderSort { get; set; } = 0;

    /// <summary>是否启用</summary>
    /// <remarks>可选，默认为 true</remarks>
    public bool IsEnabled { get; set; } = true;
}