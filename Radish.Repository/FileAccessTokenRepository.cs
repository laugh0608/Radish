using Radish.IRepository;
using Radish.Model.Models;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;

namespace Radish.Repository;

public sealed class FileAccessTokenRepository : BaseRepository<FileAccessToken>, IFileAccessTokenRepository
{
    private static readonly SemaphoreSlim SqliteWriteLock = new(1, 1);

    public FileAccessTokenRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<FileAccessToken?> TryConsumeAsync(
        string tokenHash,
        long? userId,
        string? normalizedIp,
        DateTime now)
    {
        var affectedRows = await ExecuteAtomicWriteAsync(async () => await DbProtectedClient
            .Updateable<FileAccessToken>()
            .SetColumns(token => token.AccessCount == token.AccessCount + 1)
            .SetColumns(token => token.LastAccessedAt == now)
            .SetColumns(token => token.ModifyTime == now)
            .Where(token =>
                token.TokenHash == tokenHash &&
                !token.IsRevoked &&
                token.ExpiresAt > now &&
                (token.AuthorizedUserId == null || token.AuthorizedUserId == userId) &&
                (token.AuthorizedIp == null || token.AuthorizedIp == string.Empty || token.AuthorizedIp == normalizedIp) &&
                (token.MaxAccessCount == 0 || token.AccessCount < token.MaxAccessCount))
            .ExecuteCommandAsync());

        return affectedRows == 1
            ? await GetByHashAsync(tokenHash)
            : null;
    }

    public async Task<bool> TryRevokeByIdAsync(long tokenId, DateTime now)
    {
        var affectedRows = await ExecuteAtomicWriteAsync(async () => await DbProtectedClient
            .Updateable<FileAccessToken>()
            .SetColumns(token => token.IsRevoked == true)
            .SetColumns(token => token.RevokedAt == now)
            .SetColumns(token => token.ModifyTime == now)
            .Where(token => token.Id == tokenId && !token.IsRevoked)
            .ExecuteCommandAsync());
        return affectedRows == 1;
    }

    public async Task<bool> TryRevokeByHashAsync(string tokenHash, DateTime now)
    {
        var affectedRows = await ExecuteAtomicWriteAsync(async () => await DbProtectedClient
            .Updateable<FileAccessToken>()
            .SetColumns(token => token.IsRevoked == true)
            .SetColumns(token => token.RevokedAt == now)
            .SetColumns(token => token.ModifyTime == now)
            .Where(token => token.TokenHash == tokenHash && !token.IsRevoked)
            .ExecuteCommandAsync());
        return affectedRows == 1;
    }

    public async Task<FileAccessToken?> GetByHashAsync(string tokenHash)
    {
        return await QueryFirstAsync(token => token.TokenHash == tokenHash);
    }

    private async Task<int> ExecuteAtomicWriteAsync(Func<Task<int>> operation)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != SqlSugar.DbType.Sqlite)
        {
            return await operation();
        }

        await SqliteWriteLock.WaitAsync();
        try
        {
            return await operation();
        }
        finally
        {
            SqliteWriteLock.Release();
        }
    }
}
