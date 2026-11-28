namespace Radish.Model.ViewModels;

public sealed class AccountDisableVo
{
    public required string VoWebSessionRevocation { get; init; }

    public static string FromTaskStatus(string status) => status switch
    {
        ReliableOutboxStatuses.Succeeded => "Completed",
        ReliableOutboxStatuses.DeadLetter => "NeedsAttention",
        ReliableOutboxStatuses.Pending or ReliableOutboxStatuses.Processing => "Pending",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
