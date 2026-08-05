using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ChannelDiscoverabilityRepositoryTest
{
    private static readonly DateTime NowUtc = new(2026, 8, 5, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task QueryPageAsync_ShouldUseExactTenantWithoutPublicTenantFallback()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-channel-discover-page-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
            chatDb.Insertable(new[]
            {
                CreateChannel(70001, 30000, "tenant-channel", ChannelType.Public),
                CreateChannel(70002, 0, "public-tenant-channel", ChannelType.Public),
                CreateChannel(70003, 30000, "private-channel", ChannelType.Private),
                CreateChannel(
                    70004,
                    30000,
                    "retractable-channel",
                    ChannelType.Announcement,
                    ChannelDiscoverVisibility.Summary)
            }).ExecuteCommand();
            var repository = CreateRepository(db);

            var (items, total) = await repository.QueryPageAsync(new ChannelDiscoverabilityPageQuery(
                30000,
                1,
                20,
                string.Empty,
                null,
                null,
                false));

            Assert.Equal(2, total);
            Assert.Equal(new long[] { 70001, 70004 }, items.Select(channel => channel.Id));
            Assert.DoesNotContain(items, channel => channel.TenantId == 0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SetVisibilityAsync_ShouldPersistStateAndAppendAuditEventAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-channel-discover-write-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
            chatDb.Insertable(CreateChannel(70001, 30000, "general", ChannelType.Public)).ExecuteCommand();
            var repository = CreateRepository(db);

            var result = await repository.SetVisibilityAsync(CreateCommand(
                ChannelDiscoverVisibility.Summary,
                expectedVersion: 0));

            Assert.True(result.Changed);
            Assert.Equal(ChannelDiscoverVisibility.Summary, result.Channel.DiscoverVisibility);
            Assert.Equal(1, result.Channel.DiscoverVisibilityVersion);
            var storedChannel = chatDb.Queryable<Channel>().InSingle(70001);
            Assert.Equal(ChannelDiscoverVisibility.Summary, storedChannel.DiscoverVisibility);
            Assert.Equal(1, storedChannel.DiscoverVisibilityVersion);
            var auditEvent = chatDb.Queryable<ChannelDiscoverVisibilityEvent>().Single();
            Assert.Equal(ChannelDiscoverVisibility.Hidden, auditEvent.FromVisibility);
            Assert.Equal(ChannelDiscoverVisibility.Summary, auditEvent.ToVisibility);
            Assert.Equal(0, auditEvent.ExpectedVersion);
            Assert.Equal(1, auditEvent.ResultVersion);
            Assert.Equal("经内容与隐私边界复核后开放摘要", auditEvent.Reason);
            Assert.Equal(20001, auditEvent.ActorUserId);
            Assert.Equal(NowUtc, auditEvent.CreateTime);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SetVisibilityAsync_ShouldBeIdempotentWithoutAppendingDuplicateEvent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-channel-discover-idempotent-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
            chatDb.Insertable(CreateChannel(70001, 30000, "general", ChannelType.Public)).ExecuteCommand();
            var repository = CreateRepository(db);

            var first = await repository.SetVisibilityAsync(CreateCommand(
                ChannelDiscoverVisibility.Summary,
                expectedVersion: 0));
            var repeated = await repository.SetVisibilityAsync(CreateCommand(
                ChannelDiscoverVisibility.Summary,
                expectedVersion: 0));

            Assert.True(first.Changed);
            Assert.False(repeated.Changed);
            Assert.Equal(1, repeated.Channel.DiscoverVisibilityVersion);
            Assert.Equal(1, chatDb.Queryable<ChannelDiscoverVisibilityEvent>().Count());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SetVisibilityAsync_ShouldRejectStaleVersionWithoutPartialWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-channel-discover-conflict-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
            var channel = CreateChannel(70001, 30000, "general", ChannelType.Public);
            channel.DiscoverVisibility = ChannelDiscoverVisibility.Summary;
            channel.DiscoverVisibilityVersion = 2;
            chatDb.Insertable(channel).ExecuteCommand();
            var repository = CreateRepository(db);

            await Assert.ThrowsAsync<ChannelDiscoverabilityStateConflictException>(() =>
                repository.SetVisibilityAsync(CreateCommand(
                    ChannelDiscoverVisibility.Hidden,
                    expectedVersion: 1)));

            var storedChannel = chatDb.Queryable<Channel>().InSingle(70001);
            Assert.Equal(ChannelDiscoverVisibility.Summary, storedChannel.DiscoverVisibility);
            Assert.Equal(2, storedChannel.DiscoverVisibilityVersion);
            Assert.Equal(0, chatDb.Queryable<ChannelDiscoverVisibilityEvent>().Count());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SetVisibilityAsync_ShouldRejectIneligibleChannelWithoutPartialWrite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-channel-discover-ineligible-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
            var channel = CreateChannel(70001, 30000, "general", ChannelType.Public);
            channel.IsEnabled = false;
            chatDb.Insertable(channel).ExecuteCommand();
            var repository = CreateRepository(db);

            var exception = await Assert.ThrowsAsync<ChannelDiscoverabilityIneligibleException>(() =>
                repository.SetVisibilityAsync(CreateCommand(
                    ChannelDiscoverVisibility.Summary,
                    expectedVersion: 0)));

            Assert.Contains(ChannelDiscoverabilityPolicy.ChannelDisabled, exception.Issues);
            var storedChannel = chatDb.Queryable<Channel>().InSingle(70001);
            Assert.Equal(ChannelDiscoverVisibility.Hidden, storedChannel.DiscoverVisibility);
            Assert.Equal(0, storedChannel.DiscoverVisibilityVersion);
            Assert.Equal(0, chatDb.Queryable<ChannelDiscoverVisibilityEvent>().Count());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task SetVisibilityAsync_ShouldNotWritePublicTenantFallback()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-channel-discover-tenant-{Guid.NewGuid():N}.db");
        using var db = CreateSqliteScope(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
            chatDb.Insertable(CreateChannel(70001, 0, "public-tenant-channel", ChannelType.Public))
                .ExecuteCommand();
            var repository = CreateRepository(db);

            await Assert.ThrowsAsync<ChannelDiscoverabilityTargetUnavailableException>(() =>
                repository.SetVisibilityAsync(CreateCommand(
                    ChannelDiscoverVisibility.Summary,
                    expectedVersion: 0)));

            var storedChannel = chatDb.Queryable<Channel>().InSingle(70001);
            Assert.Equal(0, storedChannel.TenantId);
            Assert.Equal(ChannelDiscoverVisibility.Hidden, storedChannel.DiscoverVisibility);
            Assert.Equal(0, chatDb.Queryable<ChannelDiscoverVisibilityEvent>().Count());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static ChannelDiscoverabilityRepository CreateRepository(SqlSugarScope db) => new(
        new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static SqlSugarScope CreateSqliteScope(string path) => new(new ConnectionConfig
    {
        ConfigId = "chat",
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });

    private static Channel CreateChannel(
        long id,
        long tenantId,
        string slug,
        ChannelType type,
        ChannelDiscoverVisibility visibility = ChannelDiscoverVisibility.Hidden) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = slug,
        Slug = slug,
        Type = type,
        DiscoverVisibility = visibility,
        DiscoverVisibilityVersion = visibility == ChannelDiscoverVisibility.Hidden ? 0 : 1,
        IsEnabled = true,
        CreateTime = NowUtc,
        CreateBy = "System"
    };

    private static ChannelDiscoverVisibilityChangeCommand CreateCommand(
        ChannelDiscoverVisibility visibility,
        int expectedVersion) => new(
        30000,
        70001,
        visibility,
        expectedVersion,
        "经内容与隐私边界复核后开放摘要",
        20001,
        "Moderator",
        NowUtc);
}
