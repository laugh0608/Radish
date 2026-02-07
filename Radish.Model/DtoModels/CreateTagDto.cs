using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>创建标签 DTO</summary>
public class CreateTagDto
{
    /// <summary>标签名称</summary>
    [Required(ErrorMessage = "标签名称不能为空")]
    [StringLength(50, ErrorMessage = "标签名称不能超过50个字符")]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL 友好标识</summary>
    [StringLength(50, ErrorMessage = "Slug 不能超过50个字符")]
    public string? Slug { get; set; }

    /// <summary>标签描述</summary>
    [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
    public string? Description { get; set; }

    /// <summary>标签颜色</summary>
    [StringLength(20, ErrorMessage = "颜色值不能超过20个字符")]
    public string? Color { get; set; }

    /// <summary>排序值</summary>
    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int SortOrder { get; set; } = 0;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否固定标签</summary>
    public bool IsFixed { get; set; } = false;
}
