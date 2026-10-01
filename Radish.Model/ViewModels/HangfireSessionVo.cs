namespace Radish.Model.ViewModels;

public sealed class HangfireSessionVo
{
    public string VoDashboardPath { get; set; } = string.Empty;
    public DateTime VoExpiresAtUtc { get; set; }
}
