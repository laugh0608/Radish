namespace Radish.Model.ViewModels;

public sealed class WikiAuthorDocumentVo
{
    public long VoDocumentId { get; set; }
    public long? VoDraftId { get; set; }
    public string VoTitle { get; set; } = string.Empty;
    public string VoSlug { get; set; } = string.Empty;
    public string? VoSummary { get; set; }
    public int VoDocumentVersion { get; set; }
    public int? VoDraftVersion { get; set; }
    public int? VoReviewState { get; set; }
    public int VoStatus { get; set; }
    public string VoAuthorRole { get; set; } = string.Empty;
    public bool VoCanEdit { get; set; }
    public bool VoCanSubmit { get; set; }
    public bool VoCanManageCollaborators { get; set; }
    public DateTime VoCreateTime { get; set; }
    public DateTime? VoModifyTime { get; set; }
}

public sealed class WikiAuthorDraftDetailVo
{
    public long VoDocumentId { get; set; }
    public long VoDraftId { get; set; }
    public long? VoOwnerUserId { get; set; }
    public string VoOwnerUserPublicId { get; set; } = string.Empty;
    public string VoOwnerUserName { get; set; } = string.Empty;
    public string VoTitle { get; set; } = string.Empty;
    public string VoSlug { get; set; } = string.Empty;
    public string? VoSummary { get; set; }
    public string VoMarkdownContent { get; set; } = string.Empty;
    public long? VoCoverAttachmentId { get; set; }
    public long? VoProposedParentId { get; set; }
    public int VoDocumentVersion { get; set; }
    public int VoBaseDocumentVersion { get; set; }
    public int VoDraftVersion { get; set; }
    public int VoReviewState { get; set; }
    public int VoDocumentStatus { get; set; }
    public string VoAuthorRole { get; set; } = string.Empty;
    public bool VoCanEdit { get; set; }
    public bool VoCanSubmit { get; set; }
    public bool VoCanManageCollaborators { get; set; }
    public string? VoReadOnlyReason { get; set; }
    public string? VoChangeSummary { get; set; }
    public string? VoReviewComment { get; set; }
    public DateTime? VoSubmittedAt { get; set; }
    public IReadOnlyList<WikiDocumentCollaboratorVo> VoCollaborators { get; set; } = [];
    public IReadOnlyList<WikiDocumentReviewEventVo> VoReviewEvents { get; set; } = [];
}

public sealed class WikiDocumentCollaboratorVo
{
    public long VoId { get; set; }
    public long VoDocumentId { get; set; }
    public string VoUserPublicId { get; set; } = string.Empty;
    public string VoUserName { get; set; } = string.Empty;
    public int VoRole { get; set; }
    public int VoInviteState { get; set; }
    public DateTime VoInvitedAt { get; set; }
    public DateTime? VoRespondedAt { get; set; }
}

public sealed class WikiReviewQueueItemVo
{
    public long VoDocumentId { get; set; }
    public long VoDraftId { get; set; }
    public long? VoOwnerUserId { get; set; }
    public string VoOwnerUserPublicId { get; set; } = string.Empty;
    public string VoOwnerUserName { get; set; } = string.Empty;
    public string VoTitle { get; set; } = string.Empty;
    public string VoSlug { get; set; } = string.Empty;
    public int VoDocumentVersion { get; set; }
    public int VoBaseDocumentVersion { get; set; }
    public int VoDraftVersion { get; set; }
    public int VoReviewState { get; set; }
    public string? VoChangeSummary { get; set; }
    public DateTime? VoSubmittedAt { get; set; }
}

public sealed class WikiDocumentReviewEventVo
{
    public long VoId { get; set; }
    public string VoAction { get; set; } = string.Empty;
    public string VoActorName { get; set; } = string.Empty;
    public string? VoComment { get; set; }
    public int VoDocumentVersion { get; set; }
    public int VoDraftVersion { get; set; }
    public DateTime VoCreateTime { get; set; }
}
