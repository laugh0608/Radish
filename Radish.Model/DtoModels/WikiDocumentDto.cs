using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Radish.Shared.CustomEnum;

namespace Radish.Model.DtoModels;

/// <summary>创建 Wiki 文档 DTO</summary>
public class CreateWikiDocumentDto
{
    [Required(ErrorMessage = "文档标题不能为空")]
    [StringLength(200, ErrorMessage = "文档标题不能超过200个字符")]
    public string Title { get; set; } = string.Empty;

    [StringLength(80, ErrorMessage = "Slug 不能超过80个字符")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "摘要不能超过1000个字符")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Markdown 内容不能为空")]
    public string MarkdownContent { get; set; } = string.Empty;

    public long? ParentId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;

    public long? CoverAttachmentId { get; set; }

    [Range((int)WikiDocumentVisibilityEnum.Public, (int)WikiDocumentVisibilityEnum.Restricted, ErrorMessage = "文档可见性无效")]
    public int Visibility { get; set; } = (int)WikiDocumentVisibilityEnum.Authenticated;

    public List<string>? AllowedRoles { get; set; }

    public List<string>? AllowedPermissions { get; set; }
}

/// <summary>更新 Wiki 文档 DTO</summary>
public class UpdateWikiDocumentDto
{
    [Required(ErrorMessage = "文档标题不能为空")]
    [StringLength(200, ErrorMessage = "文档标题不能超过200个字符")]
    public string Title { get; set; } = string.Empty;

    [StringLength(80, ErrorMessage = "Slug 不能超过80个字符")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "摘要不能超过1000个字符")]
    public string? Summary { get; set; }

    [Required(ErrorMessage = "Markdown 内容不能为空")]
    public string MarkdownContent { get; set; } = string.Empty;

    public long? ParentId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;

    public long? CoverAttachmentId { get; set; }

    [StringLength(300, ErrorMessage = "修改说明不能超过300个字符")]
    public string? ChangeSummary { get; set; }

    [Range((int)WikiDocumentVisibilityEnum.Public, (int)WikiDocumentVisibilityEnum.Restricted, ErrorMessage = "文档可见性无效")]
    public int Visibility { get; set; } = (int)WikiDocumentVisibilityEnum.Authenticated;

    public List<string>? AllowedRoles { get; set; }

    public List<string>? AllowedPermissions { get; set; }
}

/// <summary>导入 Markdown DTO</summary>
public class WikiMarkdownImportDto
{
    [Required(ErrorMessage = "Markdown 文件不能为空")]
    public IFormFile File { get; set; } = default!;

    [StringLength(80, ErrorMessage = "Slug 不能超过80个字符")]
    public string? Slug { get; set; }

    [StringLength(1000, ErrorMessage = "摘要不能超过1000个字符")]
    public string? Summary { get; set; }

    public long? ParentId { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "排序值不能为负数")]
    public int Sort { get; set; } = 0;

    public bool PublishAfterImport { get; set; } = false;

    [Range((int)WikiDocumentVisibilityEnum.Public, (int)WikiDocumentVisibilityEnum.Restricted, ErrorMessage = "文档可见性无效")]
    public int Visibility { get; set; } = (int)WikiDocumentVisibilityEnum.Authenticated;

    public List<string>? AllowedRoles { get; set; }

    public List<string>? AllowedPermissions { get; set; }
}
