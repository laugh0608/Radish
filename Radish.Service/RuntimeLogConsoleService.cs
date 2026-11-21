using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;

namespace Radish.Service;

public sealed class RuntimeLogConsoleService(IRuntimeLogConsoleRepository repository, RuntimeLogIngestOptions ingest,
    RuntimeLogConsoleOptions options, IDataProtectionProvider protection, ICurrentUserAccessor user,
    TimeProvider clock, IAuditLogService audit) : IRuntimeLogConsoleService
{
    private const string ProtectionPurpose = "Radish.RuntimeLogConsole.Query.v1";
    private static readonly Regex SafeKey = new(@"\A[A-Za-z0-9._-]{1,128}\z", RegexOptions.CultureInvariant);

    public bool Enabled => options.Enabled;

    public async Task<RuntimeLogPageVo> QueryAsync(RuntimeLogQueryDto query, CancellationToken token)
    {
        var (filter, boundary) = ReadQuery(query);
        var (rows, snapshot) = await repository.QueryAsync(filter, boundary, token);
        var items = rows.Take(filter.Limit).Select(Summarize).ToArray();
        string? next = null;
        if (rows.Count > filter.Limit)
        {
            var last = items[^1];
            next = protection.CreateProtector(ProtectionPurpose).Protect(JsonSerializer.Serialize(new QueryCursor(
                user.Current.UserId, filter, snapshot with { LastTime = last.VoOccurredAtUtc, LastId = last.VoEventId })));
        }
        return new RuntimeLogPageVo(items, next, snapshot.AsOfUtc);
    }

    public async Task<RuntimeLogDetailVo?> GetAsync(string eventId, CancellationToken token)
    {
        Require(Guid.TryParseExact(eventId, "D", out _));
        var row = await repository.GetAsync(ingest.DeploymentId, eventId, token);
        if (row == null) return null;
        using var data = JsonDocument.Parse(row.CanonicalJson);
        var root = data.RootElement;
        return new RuntimeLogDetailVo(Summarize(row), root.GetProperty("message").GetString()!,
            root.TryGetProperty("requestId", out var request) ? request.GetString() : null,
            row.ObservedAtUtc, row.IngestedAtUtc, root.GetProperty("properties").Clone());
    }

    public async Task<RuntimeLogAlertsVo> GetAlertsAsync(CancellationToken token)
    {
        var (rows, open) = await repository.GetAlertsAsync(ingest.DeploymentId, token);
        return new RuntimeLogAlertsVo(rows.Select(row => new RuntimeLogAlertVo(row.Id, row.Code, row.Service, row.EventId,
            row.State, row.Version, Invariant(row.KnownFailures), row.OpenedUtc, row.UpdatedUtc)).ToArray(), Invariant(open));
    }

    public Task<bool> TransitionAsync(string id, RuntimeLogAlertActionDto action, CancellationToken token)
    {
        Require(id.Length == 64 && id.All(Uri.IsHexDigit) && action.Version > 0 &&
            action.Action is "acknowledge" or "resolve");
        return repository.TransitionAsync(ingest.DeploymentId, id, action.Version, action.Action, Invariant(user.Current.UserId), token);
    }

    public async Task<RuntimeLogPipelineVo> GetPipelineAsync(CancellationToken token)
    {
        var pipeline = await repository.GetPipelineAsync(ingest.DeploymentId, token);
        var sample = pipeline?.Sample == null ? null : JsonSerializer.Deserialize<RuntimeLogPipelineSample>(pipeline.Sample);
        var fresh = sample != null && clock.GetUtcNow().UtcDateTime - sample.AtUtc <= TimeSpan.FromSeconds(20);
        return new RuntimeLogPipelineVo(fresh ? pipeline!.State : "Unknown", sample?.AtUtc, fresh ? sample!.Chunks : null,
            fresh ? Invariant(sample!.Created) : null, fresh ? Invariant(sample!.Written) : null, pipeline?.AlertId != null);
    }

    public async Task<IReadOnlyList<RuntimeLogHistoryVo>> GetHistoryAsync(DateTimeOffset? from, DateTimeOffset? to, bool audits,
        CancellationToken token)
    {
        var (start, end) = Range(from, to);
        var rows = await repository.GetLegacyAsync(start, end, audits, token);
        return rows.Select(row => new RuntimeLogHistoryVo(Invariant(row.Id), LocalTime(row.RecordedAt),
            audits ? "audit" : "legacy", row.Category, row.Succeeded)).ToArray();
    }

    public async Task<RuntimeLogCsvVo> ExportAsync(RuntimeLogQueryDto query, CancellationToken token)
    {
        Require(query.Cursor == null);
        var (filter, _) = ReadQuery(query with { Limit = 100 });
        var (rows, _) = await repository.QueryAsync(filter, null, token);
        var values = rows.Take(100).Select(row => string.Join(',', new[] { row.EventId, row.OccurredAtUtc.ToString("O"),
            row.Service, row.Level, row.EventCode, row.TraceId, row.OperationId }.Select(CsvCell))).ToArray();
        var actor = user.Current;
        var ids = await audit.AddSplitAsync(new AuditLog { DateTime = clock.GetUtcNow().UtcDateTime, UserId = actor.UserId,
            TenantId = actor.TenantId, OperationType = "Export", Module = "RuntimeLog",
            Description = "Exported bounded runtime log summaries.", IsSuccess = true });
        if (ids.Count != 1) throw new InvalidOperationException("The audit store did not commit the export action.");
        return new RuntimeLogCsvVo("eventId,occurredAtUtc,service,level,eventCode,traceId,operationId\r\n" +
            string.Join("\r\n", values) + "\r\n", values.Length);
    }

    private (RuntimeLogPageFilter Filter, RuntimeLogPageBoundary? Boundary) ReadQuery(RuntimeLogQueryDto query)
    {
        if (query.Cursor != null)
        {
            Require(query.Cursor.Length is > 0 and <= 4096);
            QueryCursor? cursor;
            try
            {
                cursor = JsonSerializer.Deserialize<QueryCursor>(protection.CreateProtector(ProtectionPurpose).Unprotect(query.Cursor));
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
            {
                throw new ArgumentException("Invalid log cursor.");
            }
            Require(cursor != null && cursor.User == user.Current.UserId && cursor.Filter.Deployment == ingest.DeploymentId &&
                clock.GetUtcNow().UtcDateTime - cursor.Boundary.AsOfUtc <= TimeSpan.FromMinutes(10));
            return (cursor!.Filter, cursor.Boundary);
        }
        var (from, to) = Range(query.From, query.To);
        var services = query.Services ?? [];
        var limit = query.Limit ?? 30;
        Require(limit is >= 1 and <= 100 && services.Length <= 16 && services.All(service => SafeKey.IsMatch(service)) &&
            query.Level is null or "Info" or "Warning" or "Error" &&
            (query.EventCode == null || SafeKey.IsMatch(query.EventCode)) &&
            (query.Keyword == null || query.Keyword.Length <= 64 && SafeKey.IsMatch(query.Keyword)) &&
            (query.TraceId == null || query.TraceId.Length == 32 && query.TraceId.All(Uri.IsHexDigit)) &&
            (query.OperationId == null || Guid.TryParseExact(query.OperationId, "D", out _)));
        return (new RuntimeLogPageFilter(ingest.DeploymentId, from, to, services.Distinct().ToArray(),
            query.Level, query.EventCode, query.TraceId, query.OperationId, query.Keyword, limit), null);
    }

    private (DateTime From, DateTime To) Range(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to?.UtcDateTime ?? clock.GetUtcNow().UtcDateTime;
        var start = from?.UtcDateTime ?? end.AddHours(-1);
        Require(start < end && end - start <= TimeSpan.FromDays(31));
        return (start, end);
    }

    private static RuntimeLogSummaryVo Summarize(RuntimeLogEventRecord row) => new(row.EventId, row.OccurredAtUtc,
        row.Service, row.Level, row.EventCode, row.TraceId, row.OperationId);

    private static void Require(bool valid)
    {
        if (!valid) throw new ArgumentException("Invalid log query or action.");
    }

    private static string LocalTime(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    private static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string CsvCell(string? value)
    {
        value ??= "";
        if (value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private sealed record QueryCursor(long User, RuntimeLogPageFilter Filter, RuntimeLogPageBoundary Boundary);
}
