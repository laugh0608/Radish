using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public sealed class GetPostAnswerPageDto
{
    [Required]
    [StringLength(80)]
    public string PostIdentifier { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(20)]
    public string Sort { get; set; } = "default";
}

public sealed class UpdatePostAnswerDto
{
    [Required]
    [StringLength(36)]
    public string AnswerPublicId { get; set; } = string.Empty;

    [Required]
    [StringLength(20000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedContentRevision { get; set; }

    [Required]
    [StringLength(80)]
    public string ClientSubmissionId { get; set; } = string.Empty;
}

public sealed class DeletePostAnswerDto
{
    [Required]
    [StringLength(36)]
    public string AnswerPublicId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedContentRevision { get; set; }

    [Required]
    [StringLength(80)]
    public string ClientSubmissionId { get; set; } = string.Empty;
}

public sealed class RestorePostAnswerRevisionDto
{
    [Required]
    [StringLength(36)]
    public string AnswerPublicId { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int RevisionNumber { get; set; }

    [Range(1, int.MaxValue)]
    public int ExpectedContentRevision { get; set; }

    [Required]
    [StringLength(80)]
    public string ClientSubmissionId { get; set; } = string.Empty;
}

public sealed class ChangePostAnswerAcceptanceDto
{
    [Required]
    [StringLength(80)]
    public string PostIdentifier { get; set; } = string.Empty;

    [Required]
    [StringLength(36)]
    public string AnswerPublicId { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int ExpectedAcceptanceRevision { get; set; }

    [Required]
    [StringLength(80)]
    public string ClientSubmissionId { get; set; } = string.Empty;
}

public sealed class RevokePostAnswerAcceptanceDto
{
    [Required]
    [StringLength(80)]
    public string PostIdentifier { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ExpectedAcceptanceRevision { get; set; }

    [Required]
    [StringLength(80)]
    public string ClientSubmissionId { get; set; } = string.Empty;
}
