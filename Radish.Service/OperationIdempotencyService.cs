using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Radish.Common.AttributeTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;

namespace Radish.Service;

/// <summary>资产写操作幂等记录服务。</summary>
public class OperationIdempotencyService : IOperationIdempotencyService
{
    private const int RetentionHours = 24;
    private const int MaxKeyLength = 80;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IBaseRepository<OperationIdempotencyRecord> _recordRepository;

    public OperationIdempotencyService(IBaseRepository<OperationIdempotencyRecord> recordRepository)
    {
        _recordRepository = recordRepository;
    }

    public string? NormalizeKey(string? idempotencyKey)
    {
        var trimmed = idempotencyKey?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    public OperationIdempotencyRequestSnapshot CreateRequestSnapshot(IReadOnlyDictionary<string, object?> values)
    {
        var orderedValues = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            orderedValues[key] = NormalizeSummaryValue(value);
        }

        var summary = JsonSerializer.Serialize(orderedValues, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(summary));

        return new OperationIdempotencyRequestSnapshot
        {
            RequestSummary = summary,
            RequestHash = Convert.ToHexString(hashBytes).ToLowerInvariant()
        };
    }

    public async Task<OperationIdempotencyBeginResult> BeginAsync(OperationIdempotencyBeginRequest request)
    {
        var key = NormalizeKey(request.IdempotencyKey);
        if (key == null)
        {
            return InvalidKey("幂等键不能为空");
        }

        if (!IsValidKey(key))
        {
            return InvalidKey("幂等键格式无效");
        }

        if (string.IsNullOrWhiteSpace(request.OperationType))
        {
            throw new ArgumentException("操作类型不能为空", nameof(request));
        }

        var now = DateTime.Now;
        var existing = await QueryRecordAsync(request.TenantId, request.UserId, request.OperationType, key);
        if (existing != null)
        {
            return await ResolveExistingRecordAsync(existing, request, now);
        }

        var record = new OperationIdempotencyRecord
        {
            TenantId = NormalizeTenantId(request.TenantId),
            UserId = request.UserId,
            OperationType = request.OperationType,
            IdempotencyKey = key,
            RequestHash = request.RequestHash,
            RequestSummary = request.RequestSummary,
            Status = OperationIdempotencyStatuses.Processing,
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
                "幂等记录并发创建冲突，转为读取既有记录：用户={UserId}, 操作={OperationType}, Key={IdempotencyKey}",
                request.UserId,
                request.OperationType,
                key);

            existing = await QueryRecordAsync(request.TenantId, request.UserId, request.OperationType, key);
            if (existing != null)
            {
                return await ResolveExistingRecordAsync(existing, request, now);
            }

            throw;
        }
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task CompleteSuccessAsync(OperationIdempotencyCompletionRequest request)
    {
        var record = await _recordRepository.QueryByIdAsync(request.RecordId);
        if (record == null)
        {
            Log.Warning("幂等记录完成成功时未找到记录：RecordId={RecordId}", request.RecordId);
            return;
        }

        var now = DateTime.Now;
        record.Status = OperationIdempotencyStatuses.Succeeded;
        record.ResourceType = request.ResourceType;
        record.ResourceId = request.ResourceId;
        record.ResourceNo = request.ResourceNo;
        record.ResponsePayload = request.ResponsePayload;
        record.ErrorCode = request.ErrorCode;
        record.ErrorMessage = Truncate(request.ErrorMessage, 500);
        record.CompleteTime = now;
        record.ModifyTime = now;
        record.ModifyBy = "System";
        record.ModifyId = 0;

        await _recordRepository.UpdateAsync(record);
    }

    [UseTran(Propagation = Propagation.Required)]
    public async Task CompleteFailureAsync(long recordId, string? errorCode, string? errorMessage)
    {
        var record = await _recordRepository.QueryByIdAsync(recordId);
        if (record == null)
        {
            Log.Warning("幂等记录完成失败时未找到记录：RecordId={RecordId}", recordId);
            return;
        }

        var now = DateTime.Now;
        record.Status = OperationIdempotencyStatuses.Failed;
        record.ResourceType = null;
        record.ResourceId = null;
        record.ResourceNo = null;
        record.ResponsePayload = null;
        record.ErrorCode = errorCode;
        record.ErrorMessage = Truncate(errorMessage, 500);
        record.CompleteTime = now;
        record.ModifyTime = now;
        record.ModifyBy = "System";
        record.ModifyId = 0;

        await _recordRepository.UpdateAsync(record);
    }

    public string SerializeResponse<TResponse>(TResponse response)
    {
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    public TResponse? DeserializeResponse<TResponse>(string? payload)
    {
        return string.IsNullOrWhiteSpace(payload)
            ? default
            : JsonSerializer.Deserialize<TResponse>(payload, JsonOptions);
    }

    private async Task<OperationIdempotencyBeginResult> ResolveExistingRecordAsync(
        OperationIdempotencyRecord record,
        OperationIdempotencyBeginRequest request,
        DateTime now)
    {
        if (record.ExpiresAt <= now)
        {
            await ResetToProcessingAsync(record, request, now);
            return Started(record.Id);
        }

        if (!string.Equals(record.RequestHash, request.RequestHash, StringComparison.Ordinal))
        {
            return Conflict("幂等键已被不同请求使用");
        }

        return record.Status switch
        {
            OperationIdempotencyStatuses.Succeeded => new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Succeeded,
                RecordId = record.Id,
                ResponsePayload = record.ResponsePayload,
                ErrorCode = record.ErrorCode,
                ErrorMessage = record.ErrorMessage
            },
            OperationIdempotencyStatuses.Processing => new OperationIdempotencyBeginResult
            {
                Status = OperationIdempotencyBeginStatus.Processing,
                RecordId = record.Id,
                Message = "请求处理中，请稍后查询结果或重试"
            },
            OperationIdempotencyStatuses.Failed => await ResetFailedRecordAsync(record, request, now),
            _ => Conflict("幂等记录状态无效")
        };
    }

    private async Task<OperationIdempotencyBeginResult> ResetFailedRecordAsync(
        OperationIdempotencyRecord record,
        OperationIdempotencyBeginRequest request,
        DateTime now)
    {
        await ResetToProcessingAsync(record, request, now);
        return Started(record.Id);
    }

    private async Task ResetToProcessingAsync(
        OperationIdempotencyRecord record,
        OperationIdempotencyBeginRequest request,
        DateTime now)
    {
        record.RequestHash = request.RequestHash;
        record.RequestSummary = request.RequestSummary;
        record.Status = OperationIdempotencyStatuses.Processing;
        record.ResourceType = null;
        record.ResourceId = null;
        record.ResourceNo = null;
        record.ResponsePayload = null;
        record.ErrorCode = null;
        record.ErrorMessage = null;
        record.ExpiresAt = now.AddHours(RetentionHours);
        record.CompleteTime = null;
        record.ModifyTime = now;
        record.ModifyBy = "System";
        record.ModifyId = 0;

        await _recordRepository.UpdateAsync(record);
    }

    private async Task<OperationIdempotencyRecord?> QueryRecordAsync(
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
            record.IdempotencyKey == key);
    }

    private static object? NormalizeSummaryValue(object? value)
    {
        return value is string text && string.IsNullOrWhiteSpace(text) ? string.Empty : value;
    }

    private static OperationIdempotencyBeginResult Started(long recordId)
    {
        return new OperationIdempotencyBeginResult
        {
            Status = OperationIdempotencyBeginStatus.Started,
            RecordId = recordId
        };
    }

    private static OperationIdempotencyBeginResult InvalidKey(string message)
    {
        return new OperationIdempotencyBeginResult
        {
            Status = OperationIdempotencyBeginStatus.InvalidKey,
            Message = message
        };
    }

    private static OperationIdempotencyBeginResult Conflict(string message)
    {
        return new OperationIdempotencyBeginResult
        {
            Status = OperationIdempotencyBeginStatus.Conflict,
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
