namespace Radish.IService;

public interface IRuntimeLogRetentionService
{
    Task<IReadOnlyDictionary<string, long>> PruneAsync(DateTime utcNow, CancellationToken cancellationToken);
}
