namespace Radish.IService;

/// <summary>资产写操作幂等记录服务。</summary>
public interface IOperationIdempotencyService
{
    string? NormalizeKey(string? idempotencyKey);

    OperationIdempotencyRequestSnapshot CreateRequestSnapshot(IReadOnlyDictionary<string, object?> values);

    Task<OperationIdempotencyBeginResult> BeginAsync(OperationIdempotencyBeginRequest request);

    Task CompleteSuccessAsync(OperationIdempotencyCompletionRequest request);

    Task CompleteFailureAsync(long recordId, string? errorCode, string? errorMessage);

    string SerializeResponse<TResponse>(TResponse response);

    TResponse? DeserializeResponse<TResponse>(string? payload);
}

public sealed class OperationIdempotencyRequestSnapshot
{
    public string RequestHash { get; init; } = string.Empty;

    public string RequestSummary { get; init; } = string.Empty;
}

public sealed class OperationIdempotencyBeginRequest
{
    public long TenantId { get; init; }

    public long UserId { get; init; }

    public string OperationType { get; init; } = string.Empty;

    public string? IdempotencyKey { get; init; }

    public string RequestHash { get; init; } = string.Empty;

    public string RequestSummary { get; init; } = string.Empty;
}

public enum OperationIdempotencyBeginStatus
{
    Started = 1,
    Succeeded = 2,
    Processing = 3,
    Conflict = 4,
    InvalidKey = 5
}

public sealed class OperationIdempotencyBeginResult
{
    public OperationIdempotencyBeginStatus Status { get; init; }

    public long? RecordId { get; init; }

    public string? ResponsePayload { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string? Message { get; init; }
}

public sealed class OperationIdempotencyCompletionRequest
{
    public long RecordId { get; init; }

    public string? ResourceType { get; init; }

    public long? ResourceId { get; init; }

    public string? ResourceNo { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public string ResponsePayload { get; init; } = string.Empty;
}
