using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SqlSugar;

namespace Radish.Repository;

/// <summary>协调置顶写入、撤回联动与权威快照读取的频道级锁。</summary>
internal static class ChatMessagePinTransactionLock
{
    private static readonly SemaphoreSlim SqliteLock = new(1, 1);

    public static async Task<bool> EnterSqliteAsync(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.Sqlite)
        {
            return false;
        }

        await SqliteLock.WaitAsync();
        return true;
    }

    public static void ExitSqlite(bool lockHeld)
    {
        if (lockHeld)
        {
            SqliteLock.Release();
        }
    }

    public static async Task AcquirePostgreSqlAsync(
        ISqlSugarClient db,
        long tenantId,
        long channelId)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var source = $"radish-chat-message-pin:{tenantId}:{channelId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await db.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }
}
