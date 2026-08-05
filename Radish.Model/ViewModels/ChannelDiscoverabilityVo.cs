namespace Radish.Model.ViewModels;

/// <summary>Console 频道匿名公开摘要治理项。</summary>
public sealed class ChannelDiscoverabilityVo
{
    public string VoChannelId { get; set; } = string.Empty;

    public string VoName { get; set; } = string.Empty;

    public string VoSlug { get; set; } = string.Empty;

    public string? VoDescription { get; set; }

    public string? VoIconEmoji { get; set; }

    public ChannelType VoType { get; set; }

    public bool VoIsEnabled { get; set; }

    public bool VoIsDeleted { get; set; }

    public ChannelDiscoverVisibility VoDiscoverVisibility { get; set; }

    public int VoDiscoverVisibilityVersion { get; set; }

    public bool VoCanEnableSummary { get; set; }

    public IReadOnlyList<string> VoEligibilityIssues { get; set; } = [];

    public DateTime? VoLastMessageTime { get; set; }

    public DateTime? VoModifyTime { get; set; }

    public string? VoModifyBy { get; set; }
}

/// <summary>频道匿名公开摘要资格的领域变更历史。</summary>
public sealed class ChannelDiscoverVisibilityEventVo
{
    public string VoId { get; set; } = string.Empty;

    public string VoChannelId { get; set; } = string.Empty;

    public ChannelDiscoverVisibility VoFromVisibility { get; set; }

    public ChannelDiscoverVisibility VoToVisibility { get; set; }

    public int VoExpectedVersion { get; set; }

    public int VoResultVersion { get; set; }

    public string VoReason { get; set; } = string.Empty;

    public string VoActorUserId { get; set; } = string.Empty;

    public string VoActorName { get; set; } = string.Empty;

    public DateTime VoCreateTime { get; set; }
}

/// <summary>频道匿名公开摘要资格修改结果。</summary>
public sealed class ChannelDiscoverVisibilityMutationVo
{
    public ChannelDiscoverabilityVo VoChannel { get; set; } = new();

    public bool VoChanged { get; set; }
}
