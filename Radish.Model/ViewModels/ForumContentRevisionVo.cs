namespace Radish.Model.ViewModels;

public sealed class ForumContentRevisionTagVo
{
    public long VoTagId { get; set; }

    public string VoTagName { get; set; } = string.Empty;

    public int VoSortOrder { get; set; }
}

public sealed class PostContentRevisionSummaryVo
{
    public long VoRevisionId { get; set; }

    public int VoRevisionNumber { get; set; }

    public string VoSourceType { get; set; } = string.Empty;

    public string VoIntegrityStatus { get; set; } = string.Empty;

    public long? VoRestoredFromRevisionId { get; set; }

    public int? VoRestoredFromRevisionNumber { get; set; }

    public long VoEditorId { get; set; }

    public string VoEditorName { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }

    public bool VoIsCurrent { get; set; }

    public bool VoCanViewSnapshot { get; set; }

    public bool VoCanRestore { get; set; }

    public string? VoUnavailableReasonCode { get; set; }
}

public sealed class CommentContentRevisionSummaryVo
{
    public long VoRevisionId { get; set; }

    public int VoRevisionNumber { get; set; }

    public string VoSourceType { get; set; } = string.Empty;

    public string VoIntegrityStatus { get; set; } = string.Empty;

    public long? VoRestoredFromRevisionId { get; set; }

    public int? VoRestoredFromRevisionNumber { get; set; }

    public long VoEditorId { get; set; }

    public string VoEditorName { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }

    public bool VoIsCurrent { get; set; }

    public bool VoCanViewSnapshot { get; set; }

    public bool VoCanRestore { get; set; }

    public string? VoUnavailableReasonCode { get; set; }
}

public sealed class PostContentRevisionListVo
{
    public bool VoIsEdited { get; set; }

    public int VoEditCount { get; set; }

    public int VoCurrentContentRevision { get; set; }

    public DateTime? VoLastEditedAt { get; set; }

    public bool VoCanViewDetails { get; set; }

    public List<PostContentRevisionSummaryVo> VoItems { get; set; } = [];

    public int VoTotal { get; set; }

    public int VoPageIndex { get; set; }

    public int VoPageSize { get; set; }
}

public sealed class CommentContentRevisionListVo
{
    public bool VoIsEdited { get; set; }

    public int VoEditCount { get; set; }

    public int VoCurrentContentRevision { get; set; }

    public DateTime? VoLastEditedAt { get; set; }

    public bool VoCanViewDetails { get; set; }

    public List<CommentContentRevisionSummaryVo> VoItems { get; set; } = [];

    public int VoTotal { get; set; }

    public int VoPageIndex { get; set; }

    public int VoPageSize { get; set; }
}

public sealed class PostContentRevisionDetailVo
{
    public PostContentRevisionSummaryVo VoSummary { get; set; } = new();

    public long VoPostId { get; set; }

    public string VoTitle { get; set; } = string.Empty;

    public string VoContent { get; set; } = string.Empty;

    public string VoContentType { get; set; } = "markdown";

    public long VoCategoryId { get; set; }

    public string VoCategoryName { get; set; } = string.Empty;

    public long? VoCoverAttachmentId { get; set; }

    public List<ForumContentRevisionTagVo> VoTags { get; set; } = [];

    public List<long> VoAttachmentIds { get; set; } = [];

    public int VoExpectedContentRevision { get; set; }
}

public sealed class CommentContentRevisionDetailVo
{
    public CommentContentRevisionSummaryVo VoSummary { get; set; } = new();

    public long VoCommentId { get; set; }

    public long VoPostId { get; set; }

    public string VoContent { get; set; } = string.Empty;

    public List<long> VoAttachmentIds { get; set; } = [];

    public int VoExpectedContentRevision { get; set; }
}

public sealed class ForumContentRevisionWriteResult
{
    public long VoTargetId { get; set; }

    public long VoRevisionId { get; set; }

    public int VoContentRevision { get; set; }
}
