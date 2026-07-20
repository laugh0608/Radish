using System;
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

public sealed class ReliableOutboxPostgresIntegrationTest
{
    private const string ConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task Outbox_ShouldPreserveTransactionAndClaimSemantics_OnPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {ConnectionStringEnvironmentVariable}，跳过 PostgreSQL 可靠任务集成测试");

        var suffix = Guid.NewGuid().ToString("N");
        var mainSchema = $"q1a_main_{suffix}";
        var chatSchema = $"q1a_chat_{suffix}";
        var messageSchema = $"q1a_message_{suffix}";
        using var adminDb = CreateSingleConnection(adminConnectionString!, "admin");

        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(mainSchema)}");
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(chatSchema)}");
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(messageSchema)}");
        try
        {
            var mainConnectionString = BuildSchemaConnectionString(adminConnectionString!, mainSchema);
            var chatConnectionString = BuildSchemaConnectionString(adminConnectionString!, chatSchema);
            var messageConnectionString = BuildSchemaConnectionString(adminConnectionString!, messageSchema);
            using var initializationScope = CreateScope(mainConnectionString, chatConnectionString);
            initializationScope.GetConnectionScope("main").CodeFirst.InitTables<ReliableOutboxMessage>();
            initializationScope.GetConnectionScope("chat").CodeFirst.InitTables<ChatReliableOutboxMessage>();
            await initializationScope.GetConnectionScope("main").Ado.ExecuteCommandAsync(
                "CREATE TABLE q1_business_fact (id bigint PRIMARY KEY)");

            await VerifySourceTransactionRollbackAsync(initializationScope);
            await VerifyConcurrentClaimAndLeaseRecoveryAsync(mainConnectionString, chatConnectionString);
            await VerifySourceDatabaseIsolationAsync(initializationScope);
            await VerifyNotificationTransactionAndBusinessKeyAsync(messageConnectionString);
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(mainSchema)} CASCADE");
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(chatSchema)} CASCADE");
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(messageSchema)} CASCADE");
        }
    }

    private static async Task VerifySourceTransactionRollbackAsync(SqlSugarScope dbScope)
    {
        var mainDb = dbScope.GetConnectionScope("main");
        var repository = new ReliableOutboxRepository(dbScope);
        var occurredAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);

        mainDb.Ado.BeginTran();
        try
        {
            await mainDb.Ado.ExecuteCommandAsync("INSERT INTO q1_business_fact (id) VALUES (1)");
            await repository.AddAsync(CreateDraft(1, occurredAtUtc));
            mainDb.Ado.RollbackTran();
        }
        catch
        {
            mainDb.Ado.RollbackTran();
            throw;
        }

        var businessFactCount = Convert.ToInt32(
            await mainDb.Ado.GetScalarAsync("SELECT COUNT(*) FROM q1_business_fact"));
        var outboxCount = await mainDb.Queryable<ReliableOutboxMessage>().CountAsync();

        Assert.Equal(0, businessFactCount);
        Assert.Equal(0, outboxCount);
    }

    private static async Task VerifyConcurrentClaimAndLeaseRecoveryAsync(
        string mainConnectionString,
        string chatConnectionString)
    {
        var occurredAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        using var writerScope = CreateScope(mainConnectionString, chatConnectionString);
        var writer = new ReliableOutboxRepository(writerScope);
        for (var index = 1; index <= 20; index++)
        {
            await writer.AddAsync(CreateDraft(index, occurredAtUtc));
        }

        await Assert.ThrowsAnyAsync<Exception>(() => writer.AddAsync(CreateDraft(1, occurredAtUtc)));

        using var workerScopeA = CreateScope(mainConnectionString, chatConnectionString);
        using var workerScopeB = CreateScope(mainConnectionString, chatConnectionString);
        var workerA = new ReliableOutboxRepository(workerScopeA);
        var workerB = new ReliableOutboxRepository(workerScopeB);
        var claims = await Task.WhenAll(
            workerA.ClaimDueAsync(
                ReliableOutboxSources.Main,
                20,
                "postgres-worker-a",
                occurredAtUtc,
                TimeSpan.FromMinutes(5)),
            workerB.ClaimDueAsync(
                ReliableOutboxSources.Main,
                20,
                "postgres-worker-b",
                occurredAtUtc,
                TimeSpan.FromMinutes(5)));

        var firstWorkerIds = claims[0].Select(message => message.Id).ToHashSet();
        var secondWorkerIds = claims[1].Select(message => message.Id).ToHashSet();
        Assert.Empty(firstWorkerIds.Intersect(secondWorkerIds));
        Assert.Equal(20, firstWorkerIds.Count + secondWorkerIds.Count);

        var claimedId = firstWorkerIds.Concat(secondWorkerIds).First();
        await writer.MarkFailedAsync(
            ReliableOutboxSources.Main,
            claimedId,
            "TimeoutException",
            "transient failure",
            occurredAtUtc.AddSeconds(10),
            occurredAtUtc.AddMinutes(1));

        var beforeRetry = await writer.ClaimDueAsync(
            ReliableOutboxSources.Main,
            20,
            "postgres-retry-worker",
            occurredAtUtc.AddSeconds(30),
            TimeSpan.FromMinutes(5));
        Assert.Empty(beforeRetry);

        var retryClaim = await writer.ClaimDueAsync(
            ReliableOutboxSources.Main,
            20,
            "postgres-retry-worker",
            occurredAtUtc.AddMinutes(1),
            TimeSpan.FromMinutes(5));
        Assert.Contains(retryClaim, message => message.Id == claimedId);

        var recoveredClaims = await writer.ClaimDueAsync(
            ReliableOutboxSources.Main,
            20,
            "postgres-recovery-worker",
            occurredAtUtc.AddMinutes(6).AddSeconds(1),
            TimeSpan.FromMinutes(5));
        Assert.Equal(20, recoveredClaims.Count);
    }

    private static async Task VerifySourceDatabaseIsolationAsync(SqlSugarScope dbScope)
    {
        var repository = new ReliableOutboxRepository(dbScope);
        var occurredAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var chatDraft = CreateDraft(100, occurredAtUtc) with
        {
            SourceDatabase = ReliableOutboxSources.Chat,
            IdempotencyKey = "task:chat-notification:100"
        };
        var chatOutboxId = await repository.AddAsync(chatDraft);

        var chatMessage = await repository.QueryByIdAsync(ReliableOutboxSources.Chat, chatOutboxId);
        var mainMessage = await repository.QueryByIdAsync(ReliableOutboxSources.Main, chatOutboxId);

        Assert.NotNull(chatMessage);
        Assert.Null(mainMessage);
    }

    private static async Task VerifyNotificationTransactionAndBusinessKeyAsync(string messageConnectionString)
    {
        using var messageScope = PostgreSqlIntegrationSqlSugarFactory.CreateScope(
            CreateConnectionConfig("message", messageConnectionString));
        var messageDb = messageScope.GetConnectionScope("message");
        messageDb.CodeFirst.InitTables<Notification>();
        messageDb.CodeFirst.InitTables<UserNotification>();
        messageDb.CodeFirst.InitTables<NotificationSetting>();
        messageDb.CodeFirst.InitTables<NotificationInboxGroup>();
        messageDb.CodeFirst.InitTables<NotificationInboxState>();
        var unitOfWork = new UnitOfWorkManage(
            messageScope,
            NullLogger<UnitOfWorkManage>.Instance);
        var repository = new NotificationRepository(unitOfWork);

        var nowUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        var created = await repository.PersistAsync(
            CreateNotification(7101, "notification:postgres:business-key", nowUtc),
            [new NotificationInboxRecipient(1001, true)],
            nowUtc);
        var duplicateBusinessKey = await repository.PersistAsync(
            CreateNotification(7102, "notification:postgres:business-key", nowUtc),
            [new NotificationInboxRecipient(1001, true)],
            nowUtc);
        var partialReplay = await repository.PersistAsync(
            CreateNotification(7101, "notification:postgres:business-key", nowUtc),
            [
                new NotificationInboxRecipient(1001, true),
                new NotificationInboxRecipient(1002, true)
            ],
            nowUtc);

        var notificationTableName = Convert.ToString(await messageDb.Ado.GetScalarAsync(
            "SELECT table_name FROM information_schema.tables " +
            "WHERE table_schema = current_schema() AND table_name ~ '^notification_[0-9]{8}$' " +
            "ORDER BY table_name DESC LIMIT 1"));
        Assert.False(string.IsNullOrWhiteSpace(notificationTableName));
        var notificationCount = Convert.ToInt32(await messageDb.Ado.GetScalarAsync(
            $"SELECT COUNT(*) FROM {QuoteIdentifier(notificationTableName!)}"));
        var userNotificationCount = await messageDb.Queryable<UserNotification>().CountAsync();

        Assert.True(created.EventCreated);
        Assert.Empty(duplicateBusinessKey.RecipientChanges);
        Assert.Single(partialReplay.RecipientChanges);
        Assert.Equal(1002, partialReplay.RecipientChanges[0].UserId);
        Assert.Equal(1, notificationCount);
        Assert.Equal(2, userNotificationCount);
    }

    private static Notification CreateNotification(long id, string businessKey, DateTime occurredAtUtc)
    {
        return new Notification(new NotificationInitializationOptions("SystemAnnouncement", "测试通知")
        {
            Category = NotificationCategory.System,
            TemplateKey = "notification.SystemAnnouncement",
            TargetKind = NotificationTargetKind.None,
            OccurredAtUtc = occurredAtUtc,
            TenantId = 0
        })
        {
            Id = id,
            BusinessKey = businessKey,
            CreateTime = occurredAtUtc
        };
    }

    private static ReliableOutboxDraft CreateDraft(int sequence, DateTime occurredAtUtc)
    {
        return new ReliableOutboxDraft(
            ReliableOutboxSources.Main,
            0,
            ReliableTaskTypes.PostPublished,
            1,
            $"task:postgres-post-published:{sequence}",
            "Post",
            sequence.ToString(),
            $"{{\"postId\":{sequence},\"authorId\":9}}",
            occurredAtUtc);
    }

    private static SqlSugarScope CreateScope(string mainConnectionString, string chatConnectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateScope(
            CreateConnectionConfig("main", mainConnectionString),
            CreateConnectionConfig("chat", chatConnectionString));
    }

    private static SqlSugarClient CreateSingleConnection(string connectionString, string configId)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateClient(
            CreateConnectionConfig(configId, connectionString));
    }

    private static ConnectionConfig CreateConnectionConfig(string configId, string connectionString)
    {
        return new ConnectionConfig
        {
            ConfigId = configId,
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        };
    }

    private static string BuildSchemaConnectionString(string connectionString, string schema)
    {
        return $"{connectionString.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
