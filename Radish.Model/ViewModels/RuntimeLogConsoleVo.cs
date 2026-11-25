using System.Text.Json;

namespace Radish.Model.ViewModels;

public sealed record RuntimeLogSummaryVo(string VoEventId, DateTime VoOccurredAtUtc, string VoService, string VoLevel,
    string VoEventCode, string? VoTraceId, string? VoOperationId);

public sealed record RuntimeLogDetailVo(RuntimeLogSummaryVo VoEvent, string VoMessage, string? VoRequestId,
    DateTime VoObservedAtUtc, DateTime VoIngestedAtUtc, JsonElement VoProperties);

public sealed record RuntimeLogPageVo(IReadOnlyList<RuntimeLogSummaryVo> VoItems, string? VoNextCursor, DateTime VoSnapshotUtc);

public sealed record RuntimeLogAlertVo(string VoId, string VoCode, string VoService, string? VoEventId,
    string VoState, int VoVersion, string VoKnownFailures, DateTime VoOpenedUtc, DateTime VoUpdatedUtc);

public sealed record RuntimeLogAlertsVo(IReadOnlyList<RuntimeLogAlertVo> VoItems, string VoOpenCount);

public sealed record RuntimeLogPipelineVo(string VoState, DateTime? VoObservedAtUtc, int? VoActiveChunks,
    string? VoKnownCreateFailures, string? VoKnownWriteFailures, bool VoTailCountUnknown);

public sealed record RuntimeLogHistoryVo(string VoId, string VoLocalTime, string VoSource, string? VoCategory, bool? VoSucceeded);

public sealed record RuntimeLogCsvVo(string VoCsv, int VoCount);
