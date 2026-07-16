using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Model.Models;
using Radish.Model.Root;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class UploadSessionRepositoryTest
{
    [Fact(DisplayName = "后台仓储可跨租户查询并原子标记过期会话")]
    public async Task CleanupOperations_ShouldBypassRequestTenantFilter()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"radish-upload-session-repository-{Guid.NewGuid():N}.db");
        using var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={databasePath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

        try
        {
            db.CodeFirst.InitTables<UploadSession>();
            var now = new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc);
            db.Insertable(new[]
            {
                CreateSession(1, "00000000000000000000000000000001", 0, now.AddMinutes(-1)),
                CreateSession(2, "00000000000000000000000000000002", 42, now.AddMinutes(-1)),
                CreateSession(3, "00000000000000000000000000000003", 77, now.AddMinutes(1))
            }).ExecuteCommand();
            db.QueryFilter.AddTableFilter<ITenantEntity>(entity => entity.TenantId == 0);

            db.Queryable<UploadSession>().ToList().Select(session => session.Id).ShouldBe([1]);

            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var repository = new UploadSessionRepository(unitOfWork);
            var expired = await repository.QueryExpiredAcrossTenantsAsync(now);

            expired.Select(session => session.Id).OrderBy(id => id).ShouldBe([1, 2]);
            var marked = await repository.TryMarkExpiredAcrossTenantsAsync(
                "00000000000000000000000000000002",
                42,
                7,
                now,
                now);

            marked.ShouldBeTrue();
            var tenantSession = db.Queryable<UploadSession>()
                .ClearFilter()
                .InSingle(2);
            tenantSession.Status.ShouldBe("Expired");
            tenantSession.ErrorMessage.ShouldBe("上传会话已过期");

            var terminalSessions = await repository.QueryTerminalForSettlementAcrossTenantsAsync(
                now.AddDays(-1),
                10);
            terminalSessions.Select(session => session.Id).ShouldBe([2]);
        }
        finally
        {
            db.Dispose();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static UploadSession CreateSession(long id, string sessionId, long tenantId, DateTime expiresAt)
    {
        return new UploadSession
        {
            Id = id,
            SessionId = sessionId,
            TenantId = tenantId,
            UserId = 7,
            UserName = "tester",
            FileName = "safe.txt",
            TotalSize = 2,
            ChunkSize = 2,
            TotalChunks = 1,
            UploadedChunkIndexes = "[]",
            BusinessType = "General",
            Status = "Uploading",
            ExpiresAt = expiresAt,
            CreateTime = expiresAt.AddHours(-1)
        };
    }
}
