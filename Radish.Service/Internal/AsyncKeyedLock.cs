using System.Collections.Concurrent;

namespace Radish.Service.Internal;

/// <summary>
/// 进程内按键异步互斥。引用计数保证最后一个等待者离开后即可回收锁对象。
/// </summary>
internal static class AsyncKeyedLock
{
    private static readonly object RegistryLock = new();
    private static readonly ConcurrentDictionary<string, LockEntry> Entries = new(StringComparer.Ordinal);

    public static async ValueTask<IDisposable> AcquireAsync(string key)
    {
        LockEntry entry;
        lock (RegistryLock)
        {
            entry = Entries.GetOrAdd(key, static _ => new LockEntry());
            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync();
            return new Releaser(key, entry);
        }
        catch
        {
            ReleaseReference(key, entry, releaseSemaphore: false);
            throw;
        }
    }

    private static void ReleaseReference(string key, LockEntry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (RegistryLock)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0 && Entries.TryRemove(new KeyValuePair<string, LockEntry>(key, entry)))
            {
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class Releaser(string key, LockEntry entry) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                ReleaseReference(key, entry, releaseSemaphore: true);
            }
        }
    }
}
