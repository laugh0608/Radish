using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IService;

namespace Radish.Service;

public sealed class RuntimeLogRetentionService(IRuntimeLogRetentionRepository repository,
    RuntimeLogIngestOptions options) : IRuntimeLogRetentionService
{
    public async Task<IReadOnlyDictionary<string, long>> PruneAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        if (!options.Enabled || !options.RetentionEnabled || string.IsNullOrWhiteSpace(options.DeploymentId))
            throw new InvalidOperationException("Runtime log retention requires an explicitly enabled deployment.");
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Runtime log retention requires UTC.", nameof(utcNow));

        var deleted = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var level in RuntimeLogRetention.Levels)
        {
            var cutoff = RuntimeLogRetention.Cutoff(level, utcNow);
            long total = 0;
            int batch;
            do
            {
                batch = await repository.DeleteExpiredBatchAsync(options.DeploymentId, level, cutoff, cancellationToken);
                total += batch;
            } while (batch == RuntimeLogIngestOptions.BatchSize);
            deleted.Add(level, total);
        }
        return deleted;
    }
}
