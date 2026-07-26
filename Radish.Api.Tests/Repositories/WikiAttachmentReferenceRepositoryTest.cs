using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class WikiAttachmentReferenceRepositoryTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable =
        "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public async Task SyncSourceAsync_ShouldConvergeTargetSetAndRestoreSoftDeletedRows()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-reference-{Guid.NewGuid():N}.db");
        using var db = CreateScope(path);
        try
        {
            db.CodeFirst.InitTables<WikiAttachmentReference>();
            var repository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var now = DateTime.UtcNow;

            await repository.SyncSourceAsync(Command([101, 102], now));
            await repository.SyncSourceAsync(Command([102, 103], now.AddMinutes(1)));
            await repository.SyncSourceAsync(Command([101], now.AddMinutes(2)));

            var rows = db.Queryable<WikiAttachmentReference>()
                .OrderBy(reference => reference.AttachmentId)
                .ToList();
            Assert.Equal(3, rows.Count);
            Assert.False(rows.Single(reference => reference.AttachmentId == 101).IsDeleted);
            Assert.True(rows.Single(reference => reference.AttachmentId == 102).IsDeleted);
            Assert.True(rows.Single(reference => reference.AttachmentId == 103).IsDeleted);

            var active = await repository.QueryActiveBySourceAsync(
                0,
                (int)WikiAttachmentReferenceKind.DraftContent,
                30001);
            Assert.Equal(101, Assert.Single(active).AttachmentId);
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
    public async Task SyncSourceAsync_ShouldRemainIdempotentForSameTargetSet()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-reference-idempotent-{Guid.NewGuid():N}.db");
        using var db = CreateScope(path);
        try
        {
            db.CodeFirst.InitTables<WikiAttachmentReference>();
            var repository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var command = Command([101, 102], DateTime.UtcNow);

            await repository.SyncSourceAsync(command);
            await repository.SyncSourceAsync(command);

            Assert.Equal(2, db.Queryable<WikiAttachmentReference>().Count());
            Assert.All(
                db.Queryable<WikiAttachmentReference>().ToList(),
                reference => Assert.False(reference.IsDeleted));
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
    public async Task Queries_ShouldKeepTenantBoundaryAndReturnReferencedTargetSet()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-reference-tenant-{Guid.NewGuid():N}.db");
        using var db = CreateScope(path);
        try
        {
            db.CodeFirst.InitTables<WikiAttachmentReference>();
            var repository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var now = DateTime.UtcNow;

            await repository.SyncSourceAsync(Command([101], now));
            await repository.SyncSourceAsync(Command([101, 102], now) with
            {
                TenantId = 9,
                DocumentId = 29001,
                ReferenceSourceId = 39001
            });

            Assert.Single(await repository.QueryActiveByAttachmentAsync(0, 101));
            Assert.Single(await repository.QueryActiveByAttachmentAsync(9, 101));
            Assert.Empty(await repository.QueryActiveByAttachmentAsync(8, 101));
            Assert.Equal(
                [101L, 102L],
                (await repository.GetReferencedAttachmentIdsAsync([100, 101, 102]))
                .Order());
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
    public async Task AppendRevisionAsync_ShouldReplaySameSetAndRejectMutation()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-reference-revision-{Guid.NewGuid():N}.db");
        using var db = CreateScope(path);
        try
        {
            db.CodeFirst.InitTables<WikiAttachmentReference>();
            var repository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var command = Command([101, 102], DateTime.UtcNow) with
            {
                ReferenceKind = (int)WikiAttachmentReferenceKind.RevisionContent,
                ReferenceSourceId = 40001
            };

            await repository.AppendRevisionAsync(command);
            await repository.AppendRevisionAsync(command);

            Assert.Equal(2, db.Queryable<WikiAttachmentReference>().Count());
            await Assert.ThrowsAsync<WikiAttachmentReferenceConflictException>(() =>
                repository.AppendRevisionAsync(command with { AttachmentIds = [101, 103] }));
            Assert.Equal(
                [101L, 102L],
                db.Queryable<WikiAttachmentReference>()
                    .Select(reference => reference.AttachmentId)
                    .ToList()
                    .Order());
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
    public async Task SyncSourceAsync_ShouldConvergeConcurrentSqliteWritersToOneCompleteTargetSet()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"radish-wiki-attachment-reference-concurrent-{Guid.NewGuid():N}.db");
        using var db = CreateScope(path);
        try
        {
            db.CodeFirst.InitTables<WikiAttachmentReference>();
            var repository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var now = DateTime.UtcNow;

            await Task.WhenAll(
                repository.SyncSourceAsync(Command([101, 102], now)),
                repository.SyncSourceAsync(Command([103], now.AddSeconds(1))));

            var activeIds = db.Queryable<WikiAttachmentReference>()
                .Where(reference => !reference.IsDeleted)
                .Select(reference => reference.AttachmentId)
                .ToList()
                .Order()
                .ToArray();
            Assert.True(
                activeIds.SequenceEqual([101L, 102L]) ||
                activeIds.SequenceEqual([103L]));
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
    [Trait("Database", "PostgreSQL")]
    public async Task Repository_ShouldSupportPostgreSqlTargetSetAndSoftDeleteRecovery()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(
            PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过 Wiki 附件 PostgreSQL Repository 测试");

        var schema = $"wiki_attachment_reference_{Guid.NewGuid():N}";
        using var adminDb = PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "admin",
            ConnectionString = adminConnectionString!,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA \"{schema}\"");
        try
        {
            var connectionString =
                $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<WikiAttachmentReference>();
            var repository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
            var now = DateTime.UtcNow;

            await repository.SyncSourceAsync(Command([101, 102], now));
            await repository.SyncSourceAsync(Command([102], now.AddMinutes(1)));
            await repository.SyncSourceAsync(Command([101], now.AddMinutes(2)));

            var active = await repository.QueryActiveBySourceAsync(
                0,
                (int)WikiAttachmentReferenceKind.DraftContent,
                30001);
            Assert.Equal(101, Assert.Single(active).AttachmentId);
            Assert.Equal(2, db.Queryable<WikiAttachmentReference>().Count());

            using var secondDb = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                ConnectionString = connectionString,
                DbType = DbType.PostgreSQL,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            var secondRepository = new WikiAttachmentReferenceRepository(
                new UnitOfWorkManage(secondDb, NullLogger<UnitOfWorkManage>.Instance));
            await Task.WhenAll(
                repository.SyncSourceAsync(Command([101, 102], now.AddMinutes(3))),
                secondRepository.SyncSourceAsync(Command([103], now.AddMinutes(4))));

            var concurrentActiveIds = db.Queryable<WikiAttachmentReference>()
                .Where(reference => !reference.IsDeleted)
                .Select(reference => reference.AttachmentId)
                .ToList()
                .Order()
                .ToArray();
            Assert.True(
                concurrentActiveIds.SequenceEqual([101L, 102L]) ||
                concurrentActiveIds.SequenceEqual([103L]));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE");
        }
    }

    private static WikiAttachmentReferenceSyncCommand Command(
        IReadOnlyCollection<long> attachmentIds,
        DateTime nowUtc) =>
        new(
            0,
            20001,
            (int)WikiAttachmentReferenceKind.DraftContent,
            30001,
            attachmentIds,
            10001,
            "Author",
            nowUtc);

    private static SqlSugarScope CreateScope(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
}
