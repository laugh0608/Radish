using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using SqlSugar;

namespace Radish.Repository;

internal static class UserInteractionTransactionLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SqliteLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task<string?> EnterSqliteAsync(
        ISqlSugarClient db,
        long tenantId,
        long firstUserId,
        long secondUserId)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.Sqlite)
        {
            return null;
        }

        var key =
            $"{db.CurrentConnectionConfig.ConnectionString}:{tenantId}:{Math.Min(firstUserId, secondUserId)}:{Math.Max(firstUserId, secondUserId)}";
        await SqliteLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1)).WaitAsync();
        return key;
    }

    public static void ExitSqlite(string? key)
    {
        if (key != null && SqliteLocks.TryGetValue(key, out var gate))
        {
            gate.Release();
        }
    }

    public static async Task<string?> EnterSqliteOperationAsync(
        ISqlSugarClient db,
        long tenantId,
        long actorUserId,
        string operationKey)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.Sqlite)
        {
            return null;
        }

        var key =
            $"{db.CurrentConnectionConfig.ConnectionString}:operation:{tenantId}:{actorUserId}:{operationKey}";
        await SqliteLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1)).WaitAsync();
        return key;
    }

    public static async Task AcquirePostgreSqlAsync(
        ISqlSugarClient db,
        long tenantId,
        long firstUserId,
        long secondUserId)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var identity =
            $"user-interaction-pair:{tenantId}:{Math.Min(firstUserId, secondUserId)}:{Math.Max(firstUserId, secondUserId)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await db.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }
}
