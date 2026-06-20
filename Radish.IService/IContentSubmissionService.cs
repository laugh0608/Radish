namespace Radish.IService;

/// <summary>论坛内容提交意图记录服务。</summary>
public interface IContentSubmissionService
{
    string? NormalizeClientSubmissionId(string? clientSubmissionId);

    ContentSubmissionRequestSnapshot CreateRequestSnapshot(
        IReadOnlyDictionary<string, object?> requestValues,
        IReadOnlyDictionary<string, object?> fingerprintValues);

    Task<ContentSubmissionBeginResult> BeginAsync(ContentSubmissionBeginRequest request);

    Task CompleteSuccessAsync(ContentSubmissionCompletionRequest request);

    Task CompleteFailureAsync(long recordId, string? errorCode, string? errorMessage);
}

public sealed class ContentSubmissionRequestSnapshot
{
    public string RequestDigest { get; init; } = string.Empty;

    public string RequestSummary { get; init; } = string.Empty;

    public string ContentFingerprint { get; init; } = string.Empty;
}

public sealed class ContentSubmissionBeginRequest
{
    public long TenantId { get; init; }

    public long UserId { get; init; }

    public string OperationType { get; init; } = string.Empty;

    public string? ClientSubmissionId { get; init; }

    public string? TargetType { get; init; }

    public long? TargetId { get; init; }

    public string RequestDigest { get; init; } = string.Empty;

    public string RequestSummary { get; init; } = string.Empty;

    public string ContentFingerprint { get; init; } = string.Empty;

    public int DuplicateWindowSeconds { get; init; }
}

public enum ContentSubmissionBeginStatus
{
    Started = 1,
    Succeeded = 2,
    Processing = 3,
    Conflict = 4,
    InvalidKey = 5,
    DuplicateContent = 6
}

public sealed class ContentSubmissionBeginResult
{
    public ContentSubmissionBeginStatus Status { get; init; }

    public long? RecordId { get; init; }

    public string? ResultType { get; init; }

    public long? ResultId { get; init; }

    public string? ResultPublicId { get; init; }

    public string? Message { get; init; }

    public int? RetryAfterSeconds { get; init; }
}

public sealed class ContentSubmissionCompletionRequest
{
    public long RecordId { get; init; }

    public string ResultType { get; init; } = string.Empty;

    public long ResultId { get; init; }

    public string? ResultPublicId { get; init; }
}
