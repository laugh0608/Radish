namespace Radish.IRepository;

public interface IRuntimeLogRetentionRepository
{
    Task<int> DeleteExpiredBatchAsync(string deploymentId, string level, DateTime cutoffUtc,
        CancellationToken cancellationToken);
}
