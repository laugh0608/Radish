namespace Radish.Model.ViewModels;

public sealed class ContentRewardMutationVo
{
    public long VoRewardId { get; set; }
    public string VoTargetType { get; set; } = string.Empty;
    public long VoTargetId { get; set; }
    public string VoReasonCode { get; set; } = string.Empty;
    public long VoTotalCount { get; set; }
    public bool VoViewerRewarded { get; set; }
    public long VoSenderAvailableBalance { get; set; }
    public string VoTransactionNo { get; set; } = string.Empty;
}

public sealed class ContentRewardTargetStateVo
{
    public string VoTargetType { get; set; } = string.Empty;
    public long VoTargetId { get; set; }
    public long VoTotalCount { get; set; }
    public bool VoViewerRewarded { get; set; }
    public bool VoCreateEnabled { get; set; }
}

public sealed class ContentRewardRecordVo
{
    public long VoRewardId { get; set; }
    public string? VoSenderPublicId { get; set; }
    public string VoSenderDisplayName { get; set; } = string.Empty;
    public string? VoSenderAvatarUrl { get; set; }
    public string VoReasonCode { get; set; } = string.Empty;
    public DateTime VoCreateTime { get; set; }
}

public sealed class ContentRewardTargetPageVo
{
    public string VoTargetType { get; set; } = string.Empty;
    public long VoTargetId { get; set; }
    public long VoTotalCount { get; set; }
    public bool VoViewerRewarded { get; set; }
    public bool VoCreateEnabled { get; set; }
    public List<ContentRewardRecordVo> VoItems { get; set; } = [];
    public int VoPageIndex { get; set; }
    public int VoPageSize { get; set; }
}
