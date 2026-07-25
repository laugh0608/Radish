namespace Radish.Model.ViewModels;

public sealed class UserInteractionCapabilityVo
{
    public bool VoCanFollow { get; set; }

    public bool VoCanDirectMessage { get; set; }

    public bool VoCanInteract { get; set; }

    public bool VoInteractionUnavailable { get; set; }

    public bool VoIsBlockedByCurrentUser { get; set; }
}

public sealed class UserBlockMutationVo
{
    public string VoTargetUserPublicId { get; set; } = string.Empty;

    public string VoRelationshipVersion { get; set; } = "0";

    public bool VoChanged { get; set; }

    public UserInteractionCapabilityVo VoCapabilities { get; set; } = new();
}

public sealed class UserInteractionChangedVo
{
    public string VoRelationshipVersion { get; set; } = "0";
}

public sealed class UserBlockListItemVo
{
    public string VoTargetUserPublicId { get; set; } = string.Empty;

    public string VoTargetDisplayName { get; set; } = string.Empty;

    public string? VoTargetAvatarUrl { get; set; }

    public DateTime VoBlockedAtUtc { get; set; }

    public bool VoCanUnblock { get; set; }
}

public sealed class UserBlockPageVo
{
    public List<UserBlockListItemVo> VoItems { get; set; } = [];

    public int VoTotal { get; set; }

    public int VoPageIndex { get; set; }

    public int VoPageSize { get; set; }
}
