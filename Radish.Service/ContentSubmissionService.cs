using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>论坛内容提交意图记录服务。</summary>
public class ContentSubmissionService : IContentSubmissionService
{
    private const int RetentionHours = 24;
    private const int MaxKeyLength = 80;
    private const int SummaryStringMaxLength = 160;

    private static readonly Regex MultiWhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IBaseRepository<ContentSubmissionRecord> _recordRepository;

    public ContentSubmissionService(IBaseRepository<ContentSubmissionRecord> recordRepository)
    {
        _recordRepository = recordRepository;
    }

    public string? NormalizeClientSubmissionId(string? clientSubmissionId)
    {
        var trimmed = clientSubmissionId?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public ContentSubmissionRequestSnapshot CreateRequestSnapshot(
        IReadOnlyDictionary<string, object?> requestValues,
        IReadOnlyDictionary<string, object?> fingerprintValues)
    {
        var digestValues = NormalizeValues(requestValues, fullText: true);
        var summaryValues = NormalizeValues(requestValues, fullText: false);
        var normalizedFingerprintValues = NormalizeValues(fingerprintValues, fullText: true);

        var digestJson = JsonSerializer.Serialize(digestValues, JsonOptions);
        var summaryJson = JsonSerializer.Serialize(summaryValues, JsonOptions);
        var fingerprintJson = JsonSerializer.Serialize(normalizedFingerprintValues, JsonOptions);

        return new ContentSubmissionRequestSnapshot
        {
            RequestDigest = ComputeHash(digestJson),
            RequestSummary = Truncate(summaryJson, 1000) ?? string.Empty,
            ContentFingerprint = ComputeHash(fingerprintJson)
        };
    }

    public async Task<ContentSubmissionBeginResult> BeginAsync(ContentSubmissionBeginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OperationType))
        {
            throw new ArgumentException("操作类型不能为空", nameof(request));
        }

        var key = NormalizeClientSubmissionId(request.ClientSubmissionId);
        var hasClientKey = key != null;
        if (hasClientKey && !IsValidKey(key!))
        {
            return InvalidKey("提交意图 ID 格式无效");
        }

        var now = DateTime.Now;
        if (hasClientKey)
        {
            var existing = await QueryRecordAsync(request.TenantId, request.UserId, request.OperationType, key!);
            if (existing != null)
            {
                return await ResolveExistingRecordAsync(existing, request, now);
            }
        }
        else
        {
            var duplicate = await QueryRecentFingerprintRecordAsync(request, now);
            if (duplicate != null)
            {
                return ResolveFingerprintRecord(duplicate, now, request.DuplicateWindowSeconds);
            }
        }

        var frequencyRecord = await QueryRecentFrequencyRecordAsync(request, now);
        if (frequencyRecord != null)
        {
            return ResolveFrequencyRecord(frequencyRecord, now, request.FrequencyWindowSeconds);
        }

        key ??= $"auto:{Guid.NewGuid():N}";
        var record = new ContentSubmissionRecord
        {
            TenantId = NormalizeTenantId(request.TenantId),
            UserId = request.UserId,
            OperationType = request.OperationType,
            ClientSubmissionId = key,
            TargetType = NormalizeOptional(request.TargetType),
            TargetId = request.TargetId,
            RequestDigest = request.RequestDigest,
            RequestSummary = request.RequestSummary,
            ContentFingerprint = request.ContentFingerprint,
            Status = ContentSubmissionStatuses.Pending,
            ExpiresAt = now.AddHours(RetentionHours),
            CreateTime = now,
            CreateBy = $"User_{request.UserId}",
            CreateId = request.UserId
        };

        try
        {
            record.Id = await _recordRepository.AddAsync(record);
            return Started(record.Id);
        }
        catch (Exception ex) when (IsUniqueConstraintConflict(ex))
        {
            Log.Warning(
                ex,
                "内容提交记录并发创建冲突，转为读取既有记录：UserId={UserId}, OperationType={OperationType}, Key={ClientSubmissionId}",
                request.UserId,
                request.OperationType,
                key);

            var existing = hasClientKey
                ? await QueryRecordAsync(request.TenantId, request.UserId, request.OperationType, key)
                : await QueryRecentFingerprintRecordAsync(request, now);
            if (existing != null)
            {
                return hasClientKey
                    ? await ResolveExistingRecordAsync(existing, request, now)
                    : ResolveFingerprintRecord(existing, now, request.DuplicateWindowSeconds);
            }

            throw;
        }
    }

    public async Task CompleteSuccessAsync(ContentSubmissionCompletionRequest request)
    {
        var record = await _recordRepository.QueryByIdAsync(request.RecordId);
        if (record == null)
        {
            Log.Warning("内容提交记录完成成功时未找到记录：RecordId={RecordId}", request.RecordId);
            return;
        }

        var now = DateTime.Now;
        record.Status = ContentSubmissionStatuses.Succeeded;
        record.ResultType = request.ResultType;
        record.ResultId = request.ResultId;
        record.ResultPublicId = NormalizeOptional(request.ResultPublicId);
        record.ErrorCode = null;
        record.ErrorMessage = null;
        record.CompleteTime = now;
        record.ModifyTime = now;
        record.ModifyBy = "System";
        record.ModifyId = 0;

        await _recordRepository.UpdateAsync(record);
    }

    public async Task CompleteFailureAsync(long recordId, string? errorCode, string? errorMessage)
    {
        var record = await _recordRepository.QueryByIdAsync(recordId);
        if (record == null)
        {
            Log.Warning("内容提交记录完成失败时未找到记录：RecordId={RecordId}", recordId);
            return;
        }

        var now = DateTime.Now;
        record.Status = ContentSubmissionStatuses.Failed;
        record.ResultType = null;
        record.ResultId = null;
        record.ResultPublicId = null;
        record.ErrorCode = NormalizeOptional(errorCode);
        record.ErrorMessage = Truncate(errorMessage, 500);
        record.CompleteTime = now;
        record.ModifyTime = now;
        record.ModifyBy = "System";
        record.ModifyId = 0;

        await _recordRepository.UpdateAsync(record);
    }

    private async Task<ContentSubmissionBeginResult> ResolveExistingRecordAsync(
        ContentSubmissionRecord record,
        ContentSubmissionBeginRequest request,
        DateTime now)
    {
        if (record.ExpiresAt <= now || string.Equals(record.Status, ContentSubmissionStatuses.Failed, StringComparison.Ordinal))
        {
            await ResetToPendingAsync(record, request, now);
            return Started(record.Id);
        }

        if (!string.Equals(record.RequestDigest, request.RequestDigest, StringComparison.Ordinal))
        {
            return Conflict("提交意图 ID 已被不同内容使用，请刷新后重新提交");
        }

        return record.Status switch
        {
            ContentSubmissionStatuses.Succeeded => new ContentSubmissionBeginResult
            {
                Status = ContentSubmissionBeginStatus.Succeeded,
                RecordId = record.Id,
                ResultType = record.ResultType,
                ResultId = record.ResultId,
                ResultPublicId = record.ResultPublicId,
                Message = "刚刚已提交，已返回已有内容"
            },
            ContentSubmissionStatuses.Pending => new ContentSubmissionBeginResult
            {
                Status = ContentSubmissionBeginStatus.Processing,
                RecordId = record.Id,
                Message = "内容正在提交，请稍后确认",
                RetryAfterSeconds = 3
            },
            _ => Conflict("内容提交记录状态无效")
        };
    }

    private ContentSubmissionBeginResult ResolveFingerprintRecord(
        ContentSubmissionRecord record,
        DateTime now,
        int duplicateWindowSeconds)
    {
        if (string.Equals(record.Status, ContentSubmissionStatuses.Pending, StringComparison.Ordinal))
        {
            return new ContentSubmissionBeginResult
            {
                Status = ContentSubmissionBeginStatus.Processing,
                RecordId = record.Id,
                Message = "内容正在提交，请稍后确认",
                RetryAfterSeconds = 3
            };
        }

        if (string.Equals(record.Status, ContentSubmissionStatuses.Succeeded, StringComparison.Ordinal))
        {
            var retryAfterSeconds = Math.Max(
                0,
                (int)Math.Ceiling((record.CreateTime.AddSeconds(duplicateWindowSeconds) - now).TotalSeconds));
            return new ContentSubmissionBeginResult
            {
                Status = ContentSubmissionBeginStatus.DuplicateContent,
                RecordId = record.Id,
                ResultType = record.ResultType,
                ResultId = record.ResultId,
                ResultPublicId = record.ResultPublicId,
                Message = "刚刚已提交相同内容，请勿重复发布",
                RetryAfterSeconds = retryAfterSeconds
            };
        }

        return Conflict("内容提交记录状态无效");
    }

    private static ContentSubmissionBeginResult ResolveFrequencyRecord(
        ContentSubmissionRecord record,
        DateTime now,
        int frequencyWindowSeconds)
    {
        var retryAfterSeconds = Math.Max(
            1,
            (int)Math.Ceiling((record.CreateTime.AddSeconds(frequencyWindowSeconds) - now).TotalSeconds));
        return new ContentSubmissionBeginResult
        {
            Status = ContentSubmissionBeginStatus.FrequencyLimited,
            RecordId = record.Id,
            Message = $"操作过于频繁，请{retryAfterSeconds}秒后再试",
            RetryAfterSeconds = retryAfterSeconds
        };
    }

    private async Task ResetToPendingAsync(
        ContentSubmissionRecord record,
        ContentSubmissionBeginRequest request,
        DateTime now)
    {
        record.RequestDigest = request.RequestDigest;
        record.RequestSummary = request.RequestSummary;
        record.ContentFingerprint = request.ContentFingerprint;
        record.TargetType = NormalizeOptional(request.TargetType);
        record.TargetId = request.TargetId;
        record.Status = ContentSubmissionStatuses.Pending;
        record.ResultType = null;
        record.ResultId = null;
        record.ResultPublicId = null;
        record.ErrorCode = null;
        record.ErrorMessage = null;
        record.ExpiresAt = now.AddHours(RetentionHours);
        record.CompleteTime = null;
        record.ModifyTime = now;
        record.ModifyBy = "System";
        record.ModifyId = 0;

        await _recordRepository.UpdateAsync(record);
    }

    private async Task<ContentSubmissionRecord?> QueryRecordAsync(
        long tenantId,
        long userId,
        string operationType,
        string key)
    {
        var normalizedTenantId = NormalizeTenantId(tenantId);
        return await _recordRepository.QueryFirstAsync(record =>
            record.TenantId == normalizedTenantId &&
            record.UserId == userId &&
            record.OperationType == operationType &&
            record.ClientSubmissionId == key);
    }

    private async Task<ContentSubmissionRecord?> QueryRecentFingerprintRecordAsync(
        ContentSubmissionBeginRequest request,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(request.ContentFingerprint) || request.DuplicateWindowSeconds <= 0)
        {
            return null;
        }

        var normalizedTenantId = NormalizeTenantId(request.TenantId);
        var targetType = NormalizeOptional(request.TargetType);
        var cutoff = now.AddSeconds(-request.DuplicateWindowSeconds);

        return await _recordRepository.QueryFirstAsync(record =>
            record.TenantId == normalizedTenantId &&
            record.UserId == request.UserId &&
            record.OperationType == request.OperationType &&
            record.TargetType == targetType &&
            record.TargetId == request.TargetId &&
            record.ContentFingerprint == request.ContentFingerprint &&
            record.CreateTime >= cutoff &&
            (record.Status == ContentSubmissionStatuses.Pending || record.Status == ContentSubmissionStatuses.Succeeded));
    }

    private async Task<ContentSubmissionRecord?> QueryRecentFrequencyRecordAsync(
        ContentSubmissionBeginRequest request,
        DateTime now)
    {
        if (request.FrequencyWindowSeconds <= 0)
        {
            return null;
        }

        var normalizedTenantId = NormalizeTenantId(request.TenantId);
        var targetType = NormalizeOptional(request.FrequencyTargetType);
        var targetId = request.FrequencyTargetId;
        var cutoff = now.AddSeconds(-request.FrequencyWindowSeconds);

        if (targetType != null && targetId.HasValue)
        {
            return await QueryLatestFrequencyRecordAsync(record =>
                record.TenantId == normalizedTenantId &&
                record.UserId == request.UserId &&
                record.OperationType == request.OperationType &&
                record.TargetType == targetType &&
                record.TargetId == targetId &&
                record.CreateTime >= cutoff &&
                record.Status == ContentSubmissionStatuses.Succeeded);
        }

        if (targetType != null)
        {
            return await QueryLatestFrequencyRecordAsync(record =>
                record.TenantId == normalizedTenantId &&
                record.UserId == request.UserId &&
                record.OperationType == request.OperationType &&
                record.TargetType == targetType &&
                record.CreateTime >= cutoff &&
                record.Status == ContentSubmissionStatuses.Succeeded);
        }

        if (targetId.HasValue)
        {
            return await QueryLatestFrequencyRecordAsync(record =>
                record.TenantId == normalizedTenantId &&
                record.UserId == request.UserId &&
                record.OperationType == request.OperationType &&
                record.TargetId == targetId &&
                record.CreateTime >= cutoff &&
                record.Status == ContentSubmissionStatuses.Succeeded);
        }

        return await QueryLatestFrequencyRecordAsync(record =>
            record.TenantId == normalizedTenantId &&
            record.UserId == request.UserId &&
            record.OperationType == request.OperationType &&
            record.CreateTime >= cutoff &&
            record.Status == ContentSubmissionStatuses.Succeeded);
    }

    private async Task<ContentSubmissionRecord?> QueryLatestFrequencyRecordAsync(
        System.Linq.Expressions.Expression<Func<ContentSubmissionRecord, bool>> whereExpression)
    {
        var (records, _) = await _recordRepository.QueryPageAsync(
            whereExpression,
            1,
            1,
            record => record.CreateTime,
            OrderByType.Desc);
        return records.FirstOrDefault();
    }

    private static SortedDictionary<string, object?> NormalizeValues(
        IReadOnlyDictionary<string, object?> values,
        bool fullText)
    {
        var orderedValues = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            orderedValues[key] = NormalizeValue(value, fullText);
        }

        return orderedValues;
    }

    private static object? NormalizeValue(object? value, bool fullText)
    {
        if (value is null)
        {
            return null;
        }

        if (value is string text)
        {
            var normalized = NormalizeText(text);
            return fullText ? normalized : Truncate(normalized, SummaryStringMaxLength);
        }

        return value;
    }

    private static string NormalizeText(string value)
    {
        var normalizedNewLines = value.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return MultiWhitespacePattern.Replace(normalizedNewLines, " ");
    }

    private static string ComputeHash(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static ContentSubmissionBeginResult Started(long recordId)
    {
        return new ContentSubmissionBeginResult
        {
            Status = ContentSubmissionBeginStatus.Started,
            RecordId = recordId
        };
    }

    private static ContentSubmissionBeginResult InvalidKey(string message)
    {
        return new ContentSubmissionBeginResult
        {
            Status = ContentSubmissionBeginStatus.InvalidKey,
            Message = message
        };
    }

    private static ContentSubmissionBeginResult Conflict(string message)
    {
        return new ContentSubmissionBeginResult
        {
            Status = ContentSubmissionBeginStatus.Conflict,
            Message = message
        };
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length > MaxKeyLength)
        {
            return false;
        }

        foreach (var character in key)
        {
            if (!IsAllowedKeyCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedKeyCharacter(char character)
    {
        return character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '-'
            or '_'
            or ':';
    }

    private static long NormalizeTenantId(long tenantId)
    {
        return tenantId > 0 ? tenantId : 0;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static bool IsUniqueConstraintConflict(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                (current.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("23505", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            current = current.InnerException!;
        }

        return false;
    }
}
