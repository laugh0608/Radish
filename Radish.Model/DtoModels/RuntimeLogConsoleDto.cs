namespace Radish.Model.DtoModels;

public sealed record RuntimeLogQueryDto(
    DateTimeOffset? From, DateTimeOffset? To, string[]? Services, string? Level,
    string? EventCode, string? TraceId, string? OperationId, string? Keyword, int? Limit, string? Cursor);

public sealed record RuntimeLogPageFilter(
    string Deployment, DateTime From, DateTime To, string[] Services, string? Level,
    string? EventCode, string? TraceId, string? OperationId, string? Keyword, int Limit);

public sealed record RuntimeLogPageBoundary(
    string Snapshot, DateTime AsOfUtc, DateTime? LastTime = null, string? LastId = null);

public sealed record RuntimeLogAlertActionDto(int Version, string Action);

public sealed record RuntimeLogPipelineSample(long Created, long Written, long FileDelivered, long LogDelivered,
    int Chunks, DateTime AtUtc, long InputAccepted = 0);

public sealed record LegacyRuntimeLogRow(long Id, DateTime RecordedAt, string? Category, bool? Succeeded);
