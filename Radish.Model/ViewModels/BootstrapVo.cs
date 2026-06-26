namespace Radish.Model.ViewModels;

public sealed class BootstrapStatusVo
{
    public bool VoRequiresAdminInitialization { get; set; }

    public bool VoAdministratorExists { get; set; }
}

public sealed class BootstrapAdminCreatedVo
{
    public long VoUserId { get; set; }

    public string VoDisplayName { get; set; } = string.Empty;

    public string VoEmail { get; set; } = string.Empty;
}
