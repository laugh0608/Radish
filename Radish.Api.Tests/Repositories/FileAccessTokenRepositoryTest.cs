using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common.Security;
using Radish.DbMigrate;
using Radish.Model.Models;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public class FileAccessTokenRepositoryTest
{
    private static readonly DateTime Now = new(2026, 7, 11, 12, 0, 0);

    [Fact]
    public async Task TryConsumeAsync_ShouldNotExceedAccessLimitUnderConcurrency()
    {
        using var harness = CreateHarness();
        var repository = harness.Repository;
        var rawToken = FileAccessTokenHashing.GenerateRawToken();
        var tokenHash = FileAccessTokenHashing.HashToken(rawToken);
        await repository.AddAsync(CreateToken(1001, tokenHash, maxAccessCount: 3));

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => repository.TryConsumeAsync(tokenHash, null, "127.0.0.1", Now)));

        Assert.Equal(3, results.Count(result => result != null));
        var stored = await repository.GetByHashAsync(tokenHash);
        Assert.NotNull(stored);
        Assert.Equal(3, stored.AccessCount);
    }

    [Fact]
    public async Task TryConsumeAsync_ShouldAllowUnlimitedTokenAndEnforceUserIpAndExpiry()
    {
        using var harness = CreateHarness();
        var repository = harness.Repository;
        var unlimitedHash = FileAccessTokenHashing.HashToken("unlimited-token");
        await repository.AddAsync(CreateToken(1001, unlimitedHash, 0));

        var unlimitedResults = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => repository.TryConsumeAsync(unlimitedHash, null, "127.0.0.1", Now)));
        Assert.All(unlimitedResults, Assert.NotNull);

        var restrictedHash = FileAccessTokenHashing.HashToken("restricted-token");
        var restricted = CreateToken(1002, restrictedHash, 2);
        restricted.AuthorizedUserId = 77;
        restricted.AuthorizedIp = "127.0.0.1";
        await repository.AddAsync(restricted);

        Assert.Null(await repository.TryConsumeAsync(restrictedHash, 78, "127.0.0.1", Now));
        Assert.Null(await repository.TryConsumeAsync(restrictedHash, 77, "127.0.0.2", Now));
        Assert.NotNull(await repository.TryConsumeAsync(restrictedHash, 77, "127.0.0.1", Now));

        var expiredHash = FileAccessTokenHashing.HashToken("expired-token");
        var expired = CreateToken(1003, expiredHash, 1);
        expired.ExpiresAt = Now;
        await repository.AddAsync(expired);
        Assert.Null(await repository.TryConsumeAsync(expiredHash, null, null, Now));
    }

    [Fact]
    public async Task RevokeAndConsume_ShouldFollowAtomicCommitOrder()
    {
        using var harness = CreateHarness();
        var repository = harness.Repository;
        var tokenHash = FileAccessTokenHashing.HashToken("revoke-race-token");
        var tokenId = await repository.AddAsync(CreateToken(1001, tokenHash, 10));

        var consumeTask = repository.TryConsumeAsync(tokenHash, null, "127.0.0.1", Now);
        var revokeTask = repository.TryRevokeByIdAsync(tokenId, Now);
        await Task.WhenAll(consumeTask, revokeTask);
        var revoked = await revokeTask;

        Assert.True(revoked);
        Assert.Null(await repository.TryConsumeAsync(tokenHash, null, "127.0.0.1", Now.AddSeconds(1)));
        var stored = await repository.GetByHashAsync(tokenHash);
        Assert.NotNull(stored);
        Assert.True(stored.IsRevoked);
        Assert.InRange(stored.AccessCount, 0, 1);
    }

    [Fact]
    public async Task Migration_ShouldHashLegacyTokenOnceAndKeepOldRawTokenUsable()
    {
        using var harness = CreateHarness();
        const string legacyRawToken = "abcdef0123456789abcdef0123456789";
        var token = CreateToken(1001, legacyRawToken, 1);
        harness.Db.Insertable(token).ExecuteCommand();

        var firstUpdated = FileAccessTokenSecurityMigration.Apply(harness.Db);
        var secondUpdated = FileAccessTokenSecurityMigration.Apply(harness.Db);
        var stored = harness.Db.Queryable<FileAccessToken>().Single();

        Assert.Equal(1, firstUpdated);
        Assert.Equal(0, secondUpdated);
        Assert.Equal(FileAccessTokenHashing.HashToken(legacyRawToken), stored.TokenHash);
        Assert.Empty(FileAccessTokenSecurityMigration.Verify(harness.Db));
        Assert.NotNull(await harness.Repository.TryConsumeAsync(
            FileAccessTokenHashing.HashToken(legacyRawToken),
            null,
            "127.0.0.1",
            Now));
    }

    [Fact]
    public void Migration_ShouldRejectUnknownTokenFormatWithoutChangingRows()
    {
        using var harness = CreateHarness();
        const string invalidToken = "not-a-supported-token-format";
        harness.Db.Insertable(CreateToken(1001, invalidToken, 1)).ExecuteCommand();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FileAccessTokenSecurityMigration.Apply(harness.Db));

        Assert.Contains("1001", exception.Message, StringComparison.Ordinal);
        Assert.Equal(invalidToken, harness.Db.Queryable<FileAccessToken>().Single().TokenHash);
        Assert.Single(FileAccessTokenSecurityMigration.Verify(harness.Db));
    }

    private static FileAccessToken CreateToken(long id, string tokenHash, int maxAccessCount)
    {
        return new FileAccessToken
        {
            Id = id,
            TokenHash = tokenHash,
            AttachmentId = 5001,
            MaxAccessCount = maxAccessCount,
            AccessCount = 0,
            ExpiresAt = Now.AddHours(1),
            CreatedBy = 42,
            IsRevoked = false,
            CreateTime = Now,
            ModifyTime = Now
        };
    }

    private static RepositoryHarness CreateHarness()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-file-token-{Guid.NewGuid():N}.db");
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        db.CodeFirst.InitTables<FileAccessToken>();
        var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
        return new RepositoryHarness(db, new FileAccessTokenRepository(unitOfWork), path);
    }

    private sealed class RepositoryHarness(
        SqlSugarScope db,
        FileAccessTokenRepository repository,
        string path) : IDisposable
    {
        public SqlSugarScope Db { get; } = db;

        public FileAccessTokenRepository Repository { get; } = repository;

        public void Dispose()
        {
            Db.Dispose();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
