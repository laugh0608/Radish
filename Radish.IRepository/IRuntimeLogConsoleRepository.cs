using Radish.Model.DtoModels;
using Radish.Model.LogModels;

namespace Radish.IRepository;

public interface IRuntimeLogConsoleRepository
{
    Task<(IReadOnlyList<RuntimeLogEventRecord> Rows, RuntimeLogPageBoundary Boundary)> QueryAsync(
        RuntimeLogPageFilter filter, RuntimeLogPageBoundary? boundary, CancellationToken token);
    Task<RuntimeLogEventRecord?> GetAsync(string deployment, string eventId, CancellationToken token);
    Task<IReadOnlyList<LegacyRuntimeLogRow>> GetLegacyAsync(DateTime from, DateTime to, bool audit, CancellationToken token);
    Task<(IReadOnlyList<RuntimeLogAlert> Rows, long Open)> GetAlertsAsync(string deployment, CancellationToken token);
    Task<bool> TransitionAsync(string deployment, string alertId, int version, string action, string actor, CancellationToken token);
    Task<RuntimeLogPipeline?> GetPipelineAsync(string deployment, CancellationToken token);
    Task UpdatePipelineAsync(string deployment, RuntimeLogPipelineSample sample, CancellationToken token);
}
