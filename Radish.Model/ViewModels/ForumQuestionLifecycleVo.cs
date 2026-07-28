namespace Radish.Model.ViewModels;

public sealed class PostAnswerPageVo
{
    public string VoPostPublicId { get; set; } = string.Empty;
    public bool VoIsSolved { get; set; }
    public string? VoAcceptedAnswerPublicId { get; set; }
    public PostAnswerVo? VoAcceptedAnswer { get; set; }
    public int VoAcceptanceRevision { get; set; }
    public int VoTotal { get; set; }
    public int VoOtherTotal { get; set; }
    public int VoPageIndex { get; set; }
    public int VoPageSize { get; set; }
    public List<PostAnswerVo> VoItems { get; set; } = [];
}

public sealed class PostAnswerRevisionSummaryVo
{
    public int VoRevisionNumber { get; set; }
    public string VoSourceType { get; set; } = string.Empty;
    public string VoIntegrityStatus { get; set; } = string.Empty;
    public bool VoIsCurrent { get; set; }
    public bool VoCanRestore { get; set; }
    public int? VoRestoredFromRevisionNumber { get; set; }
    public DateTime VoCreateTime { get; set; }
    public string VoEditorName { get; set; } = string.Empty;
}

public sealed class PostAnswerRevisionListVo
{
    public string VoAnswerPublicId { get; set; } = string.Empty;
    public int VoCurrentContentRevision { get; set; }
    public List<PostAnswerRevisionSummaryVo> VoItems { get; set; } = [];
}

public sealed class PostAnswerRevisionDetailVo
{
    public string VoAnswerPublicId { get; set; } = string.Empty;
    public int VoRevisionNumber { get; set; }
    public string VoSourceType { get; set; } = string.Empty;
    public string VoIntegrityStatus { get; set; } = string.Empty;
    public string VoContent { get; set; } = string.Empty;
    public int VoExpectedContentRevision { get; set; }
    public DateTime VoCreateTime { get; set; }
    public string VoEditorName { get; set; } = string.Empty;
}

public sealed class PostAnswerMutationVo
{
    public string VoPostPublicId { get; set; } = string.Empty;
    public PostAnswerVo VoAnswer { get; set; } = new();
    public int VoAnswerCount { get; set; }
}

public sealed class PostAnswerAcceptanceMutationVo
{
    public string VoPostPublicId { get; set; } = string.Empty;
    public string? VoAcceptedAnswerPublicId { get; set; }
    public int VoAcceptanceRevision { get; set; }
    public bool VoIsSolved { get; set; }
}
