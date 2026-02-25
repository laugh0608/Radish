using System.ComponentModel.DataAnnotations;
using Radish.Model;

namespace Radish.Model.DtoModels;

/// <summary>创建/更新表情包分组 DTO</summary>
public class CreateStickerGroupDto
{
    [Required(ErrorMessage = "分组名称不能为空")]
    [StringLength(100, ErrorMessage = "分组名称不能超过100个字符")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "分组标识符不能为空")]
    [StringLength(100, ErrorMessage = "分组标识符不能超过100个字符")]
    [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "分组标识符仅允许小写字母、数字和下划线")]
    public string Code { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "封面图地址不能超过500个字符")]
    public string? CoverImageUrl { get; set; }

    public StickerGroupType GroupType { get; set; } = StickerGroupType.Official;

    public bool IsEnabled { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;
}

/// <summary>更新表情包分组 DTO（Code 不可修改）</summary>
public class UpdateStickerGroupDto
{
    [Required(ErrorMessage = "分组名称不能为空")]
    [StringLength(100, ErrorMessage = "分组名称不能超过100个字符")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "描述不能超过500个字符")]
    public string? Description { get; set; }

    [StringLength(500, ErrorMessage = "封面图地址不能超过500个字符")]
    public string? CoverImageUrl { get; set; }

    public StickerGroupType GroupType { get; set; } = StickerGroupType.Official;

    public bool IsEnabled { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;
}

/// <summary>创建表情 DTO</summary>
public class CreateStickerDto
{
    [Required(ErrorMessage = "分组ID不能为空")]
    public long GroupId { get; set; }

    [Required(ErrorMessage = "表情标识符不能为空")]
    [StringLength(100, ErrorMessage = "表情标识符不能超过100个字符")]
    [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "表情标识符仅允许小写字母、数字和下划线")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "表情名称不能为空")]
    [StringLength(200, ErrorMessage = "表情名称不能超过200个字符")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "图片地址不能超过500个字符")]
    public string? ImageUrl { get; set; }

    [StringLength(500, ErrorMessage = "缩略图地址不能超过500个字符")]
    public string? ThumbnailUrl { get; set; }

    public bool IsAnimated { get; set; }

    public bool AllowInline { get; set; } = true;

    public long? AttachmentId { get; set; }

    public bool IsEnabled { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;
}

/// <summary>更新表情 DTO（Code 不可修改）</summary>
public class UpdateStickerDto
{
    [Required(ErrorMessage = "表情名称不能为空")]
    [StringLength(200, ErrorMessage = "表情名称不能超过200个字符")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "图片地址不能超过500个字符")]
    public string? ImageUrl { get; set; }

    [StringLength(500, ErrorMessage = "缩略图地址不能超过500个字符")]
    public string? ThumbnailUrl { get; set; }

    public bool IsAnimated { get; set; }

    public bool AllowInline { get; set; } = true;

    public long? AttachmentId { get; set; }

    public bool IsEnabled { get; set; } = true;

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;
}

/// <summary>记录表情使用 DTO</summary>
public class RecordStickerUseDto
{
    [Required(ErrorMessage = "emojiType 不能为空")]
    [StringLength(20, ErrorMessage = "emojiType 长度不能超过20个字符")]
    public string EmojiType { get; set; } = string.Empty;

    [Required(ErrorMessage = "emojiValue 不能为空")]
    [StringLength(200, ErrorMessage = "emojiValue 长度不能超过200个字符")]
    public string EmojiValue { get; set; } = string.Empty;
}

/// <summary>批量新增表情 DTO</summary>
public class BatchAddStickersDto
{
    [Required(ErrorMessage = "分组ID不能为空")]
    public long GroupId { get; set; }

    [Required(ErrorMessage = "表情列表不能为空")]
    public List<BatchAddStickerItemDto> Stickers { get; set; } = new();
}

/// <summary>批量新增表情项 DTO</summary>
public class BatchAddStickerItemDto
{
    [Required(ErrorMessage = "附件ID不能为空")]
    public long AttachmentId { get; set; }

    [Required(ErrorMessage = "表情标识符不能为空")]
    [StringLength(100, ErrorMessage = "表情标识符不能超过100个字符")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "表情名称不能为空")]
    [StringLength(200, ErrorMessage = "表情名称不能超过200个字符")]
    public string Name { get; set; } = string.Empty;

    public bool AllowInline { get; set; } = true;
}

/// <summary>批量更新排序 DTO</summary>
public class BatchUpdateStickerSortDto
{
    [Required(ErrorMessage = "排序列表不能为空")]
    public List<StickerSortItemDto> Items { get; set; } = new();
}

/// <summary>排序项 DTO</summary>
public class StickerSortItemDto
{
    [Required(ErrorMessage = "表情ID不能为空")]
    public long Id { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; }
}
