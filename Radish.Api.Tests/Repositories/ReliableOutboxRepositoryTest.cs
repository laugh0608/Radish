using System;
using System.IO;
using System.Threading.Tasks;
using Radish.Model;
using Radish.Repository;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public class ReliableOutboxRepositoryTest
{
    [Fact]
    public async Task ClaimDueAsync_ShouldRecoverProcessingMessageAfterLeaseExpires()
    {
        using var harness = CreateHarness();
        var repository = new ReliableOutboxRepository(harness.Db);
        var occurredAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var outboxId = await repository.AddAsync(CreateDraft(occurredAtUtc));

        var firstClaim = await repository.ClaimDueAsync(
            ReliableOutboxSources.Main,
            10,
            "worker-1",
            occurredAtUtc,
            TimeSpan.FromMinutes(5));
        var concurrentClaim = await repository.ClaimDueAsync(
            ReliableOutboxSources.Main,
            10,
            "worker-2",
            occurredAtUtc.AddMinutes(1),
            TimeSpan.FromMinutes(5));
        var recoveredClaim = await repository.ClaimDueAsync(
            ReliableOutboxSources.Main,
            10,
            "worker-2",
            occurredAtUtc.AddMinutes(6),
            TimeSpan.FromMinutes(5));

        firstClaim.Count.ShouldBe(1);
        firstClaim[0].Id.ShouldBe(outboxId);
        concurrentClaim.ShouldBeEmpty();
        recoveredClaim.Count.ShouldBe(1);
        recoveredClaim[0].Id.ShouldBe(outboxId);
    }

    [Fact]
    public async Task ReplayAsync_ShouldOnlyResetDeadLetterAndPreserveSameMessage()
    {
        using var harness = CreateHarness();
        var repository = new ReliableOutboxRepository(harness.Db);
        var occurredAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var outboxId = await repository.AddAsync(CreateDraft(occurredAtUtc));
        await repository.ClaimDueAsync(
            ReliableOutboxSources.Main,
            10,
            "worker-1",
            occurredAtUtc,
            TimeSpan.FromMinutes(5));
        await repository.MarkFailedAsync(
            ReliableOutboxSources.Main,
            outboxId,
            "PermanentFailure",
            "invalid payload",
            occurredAtUtc.AddMinutes(1),
            null);

        var replayed = await repository.ReplayAsync(
            ReliableOutboxSources.Main,
            outboxId,
            "Admin#2",
            "修复载荷处理器后重放",
            occurredAtUtc.AddMinutes(2));
        var message = await repository.QueryByIdAsync(ReliableOutboxSources.Main, outboxId);

        replayed.ShouldBeTrue();
        message.ShouldNotBeNull();
        message.Id.ShouldBe(outboxId);
        message.Status.ShouldBe(ReliableOutboxStatuses.Pending);
        message.AttemptCount.ShouldBe(0);
    }

    private static ReliableOutboxDraft CreateDraft(DateTime occurredAtUtc)
    {
        return new ReliableOutboxDraft(
            ReliableOutboxSources.Main,
            0,
            ReliableTaskTypes.PostPublished,
            1,
            "task:post-published:42",
            "Post",
            "42",
            "{\"postId\":42,\"authorId\":9}",
            occurredAtUtc);
    }

    private static OutboxRepositoryHarness CreateHarness()
    {
        var mainPath = Path.Combine(Path.GetTempPath(), $"radish-outbox-main-{Guid.NewGuid():N}.db");
        var chatPath = Path.Combine(Path.GetTempPath(), $"radish-outbox-chat-{Guid.NewGuid():N}.db");
        var db = new SqlSugarScope(
        [
            CreateConnection("main", mainPath),
            CreateConnection("chat", chatPath)
        ]);
        db.GetConnectionScope("main").CodeFirst.InitTables<ReliableOutboxMessage>();
        db.GetConnectionScope("chat").CodeFirst.InitTables<ChatReliableOutboxMessage>();
        return new OutboxRepositoryHarness(db, mainPath, chatPath);
    }

    private static ConnectionConfig CreateConnection(string configId, string path)
    {
        return new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        };
    }

    private sealed class OutboxRepositoryHarness : IDisposable
    {
        private readonly string[] _paths;

        public OutboxRepositoryHarness(SqlSugarScope db, params string[] paths)
        {
            Db = db;
            _paths = paths;
        }

        public SqlSugarScope Db { get; }

        public void Dispose()
        {
            Db.Dispose();
            foreach (var path in _paths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
