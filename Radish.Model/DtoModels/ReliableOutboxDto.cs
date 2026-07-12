namespace Radish.Model.DtoModels;

public sealed class ReliableOutboxReplayDto
{
    public string SourceDatabase { get; set; } = ReliableOutboxSources.Main;
    public long OutboxId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
