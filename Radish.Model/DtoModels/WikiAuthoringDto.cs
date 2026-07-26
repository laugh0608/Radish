using System.ComponentModel.DataAnnotations;

namespace Radish.Model.DtoModels;

public class CreateWikiAuthorDraftDto
{
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;
    [StringLength(80)]
    public string? Slug { get; set; }
    [StringLength(1000)]
    public string? Summary { get; set; }
    [Required]
    public string MarkdownContent { get; set; } = string.Empty;
    public long? CoverAttachmentId { get; set; }
    public long? ProposedParentId { get; set; }
    [StringLength(300)]
    public string? ChangeSummary { get; set; }
}

public sealed class SaveWikiAuthorDraftDto : CreateWikiAuthorDraftDto
{
    [Range(1, int.MaxValue)]
    public int ExpectedDraftVersion { get; set; }
}

public sealed class InviteWikiCollaboratorDto
{
    [Required, StringLength(80)]
    public string UserPublicId { get; set; } = string.Empty;
}

public sealed class RespondWikiCollaboratorInvitationDto
{
    public bool Accept { get; set; }
}

public sealed class SubmitWikiDraftDto
{
    [Range(1, int.MaxValue)]
    public int ExpectedDraftVersion { get; set; }
    [StringLength(300)]
    public string? ChangeSummary { get; set; }
}

public sealed class ReviewWikiDraftDto
{
    [Required, StringLength(30)]
    public string Action { get; set; } = string.Empty;
    [Range(1, int.MaxValue)]
    public int ExpectedDraftVersion { get; set; }
    [Range(0, int.MaxValue)]
    public int ExpectedDocumentVersion { get; set; }
    [StringLength(1000)]
    public string? Comment { get; set; }
    public long? FinalParentId { get; set; }
}
