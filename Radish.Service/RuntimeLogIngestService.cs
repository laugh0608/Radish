using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Radish.Service.Logging;

namespace Radish.Service;

public sealed class RuntimeLogIngestService(IRuntimeLogIngestRepository repository, RuntimeLogIngestOptions options,
    RuntimeLogIngestState state, RuntimeLogIngestDiagnostics diagnostics, TimeProvider clock) : IRuntimeLogIngestService
{
    public async Task<RuntimeLogIngestResult> IngestAsync(Stream body, CancellationToken cancellationToken)
    {
        var summary = new RuntimeLogIngestVo();
        if (!options.Enabled) return new(404, summary);
        if (!state.Slots.Wait(0)) return new(429, summary);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
        using var scope = new RuntimeLogIngestScope();
        try
        {
            var events = new List<RuntimeLogEventRecord>();
            var rejected = new List<RuntimeLogRejectionBucket>();
            var batchCount = 0;
            await foreach (var document in RuntimeLogJsonLines.ReadAsync(body, timeout.Token))
            {
                using (document)
                {
                    var value = RuntimeLogPolicy.ReadTransport(document.RootElement, options.DeploymentId, options.Services, out var reason, options.Mode);
                    string? canonical = null;
                    if (value != null)
                    {
                        try { canonical = value.ToJsonLine(); }
                        catch (InvalidOperationException) { value = null; reason = "oversize"; }
                    }
                    if (value == null)
                    {
                        // 载荷来源未通过验证前只归到固定接收器桶，不反射任意 service/eventId。
                        rejected.Add(RuntimeLogRejectionBucket.Create(options.DeploymentId, "log-collector", reason, clock.GetUtcNow().UtcDateTime));
                    }
                    else
                    {
                        RuntimeLogPolicy.TryReadUtc(value.OccurredAtUtc, out var occurred);
                        RuntimeLogPolicy.TryReadUtc(value.ObservedAtUtc, out var observed);
                        events.Add(new RuntimeLogEventRecord
                        {
                            Id = Guid.NewGuid().ToString("D"), DeploymentId = value.DeploymentId, EventId = value.EventId,
                            EventCode = value.EventCode, Service = value.Service, Level = value.Level, OccurredAtUtc = occurred,
                            ObservedAtUtc = observed, IngestedAtUtc = clock.GetUtcNow().UtcDateTime,
                            TraceId = value.TraceId, OperationId = value.OperationId, CanonicalJson = canonical!,
                            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical!)))
                        });
                    }
                }
                if (++batchCount == RuntimeLogIngestOptions.BatchSize)
                {
                    await CommitAsync(events, rejected, summary, timeout.Token);
                    events.Clear(); rejected.Clear(); batchCount = 0;
                }
            }
            if (batchCount > 0) await CommitAsync(events, rejected, summary, timeout.Token);
            if (summary.VoAccepted + summary.VoDuplicate + summary.VoRejected == 0) throw new IngestInputException(400);
            summary.VoState = "committed";
            return new(200, summary);
        }
        catch (IngestInputException exception)
        {
            state.Failed(); diagnostics.Report("invalid-input");
            summary.VoState = "invalid-envelope";
            return new(exception.StatusCode, summary);
        }
        catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception) when (exception.StatusCode is 400 or 413)
        {
            state.Failed(); diagnostics.Report("invalid-input");
            summary.VoState = "invalid-envelope";
            return new(exception.StatusCode, summary);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            state.Failed();
            summary.VoState = "cancelled";
            return new(cancellationToken.IsCancellationRequested ? 499 : 408, summary);
        }
        catch (Exception exception)
        {
            state.Failed(); diagnostics.Report("ingest", exception);
            summary.VoState = "unavailable";
            return new(503, summary);
        }
        finally { state.Slots.Release(); }
    }

    private async Task CommitAsync(List<RuntimeLogEventRecord> events, List<RuntimeLogRejectionBucket> rejected,
        RuntimeLogIngestVo summary, CancellationToken cancellationToken)
    {
        var persisted = await repository.CommitAsync(events, rejected, cancellationToken);
        if (persisted.Count != events.Count) throw new InvalidOperationException("Invalid ingestion repository result.");
        var acceptedCount = 0;
        var duplicateCount = 0;
        var rejectionCount = rejected.Count;
        for (var i = 0; i < persisted.Count; i++)
        {
            if (persisted[i] == null) rejectionCount++;
            else if (persisted[i]!.Id == events[i].Id) acceptedCount++;
            else duplicateCount++;
        }
        summary.VoAccepted += acceptedCount; summary.VoDuplicate += duplicateCount; summary.VoRejected += rejectionCount;
        state.Committed(acceptedCount, duplicateCount, rejectionCount, clock.GetUtcNow().UtcDateTime);
    }

}
