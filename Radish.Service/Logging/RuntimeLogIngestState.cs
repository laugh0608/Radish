namespace Radish.Service.Logging;

/// <summary>进程内管道状态；重启清零，非持久或恰好一次统计。</summary>
public sealed class RuntimeLogIngestState(int concurrency)
{
    internal SemaphoreSlim Slots { get; } = new(concurrency, concurrency);
    private readonly object _gate = new();
    private long _accepted, _duplicate, _rejected, _failed;
    private DateTime? _lastCommitted;
    public (long Accepted, long Duplicate, long Rejected, long Failed, DateTime? LastCommittedUtc) Snapshot()
    {
        lock (_gate) return (_accepted, _duplicate, _rejected, _failed, _lastCommitted);
    }
    internal void Committed(int accepted, int duplicate, int rejected, DateTime utc)
    {
        lock (_gate)
        {
            _accepted = Add(_accepted, accepted); _duplicate = Add(_duplicate, duplicate); _rejected = Add(_rejected, rejected);
            _lastCommitted = utc;
        }
    }
    internal void Failed() { lock (_gate) _failed = Add(_failed, 1); }
    private static long Add(long current, int count) => Math.Min(9_007_199_254_740_991L, current + count);
}
