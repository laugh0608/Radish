namespace Radish.Model.ViewModels;

public sealed class ReliableOutboxVo
{
    public string VoSourceDatabase { get; set; } = string.Empty;
    public long VoId { get; set; }
    public long VoTenantId { get; set; }
    public string VoTaskType { get; set; } = string.Empty;
    public string VoAggregateType { get; set; } = string.Empty;
    public string VoAggregateId { get; set; } = string.Empty;
    public string VoStatus { get; set; } = string.Empty;
    public int VoAttemptCount { get; set; }
    public int VoMaxAttempts { get; set; }
    public DateTime VoAvailableAtUtc { get; set; }
    public string? VoLastErrorCode { get; set; }
    public string? VoLastErrorSummary { get; set; }
}
