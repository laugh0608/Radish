using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

/// <summary>创建、编辑或恢复本人商品评价。</summary>
public sealed class UpsertProductReviewDto
{
    [Range(1, 5, ErrorMessage = "rating 必须在 1-5 之间")]
    public int Rating { get; set; }

    [StringLength(500, ErrorMessage = "comment 长度不能超过500个字符")]
    public string? Comment { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "expectedVersion 不能小于0")]
    public int ExpectedVersion { get; set; }
}
