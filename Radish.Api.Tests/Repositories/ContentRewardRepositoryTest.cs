using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ContentRewardRepositoryTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 27, 8, 0, 0, DateTimeKind.Utc);
    private const string RequestHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

    [Fact]
    public async Task CreateAsync_ShouldAtomicallyTransferBalanceAndCompleteIdempotency()
    {
        using var harness = new MainRepositoryHarness();

        var result = await harness.Repository.CreateAsync(CreateCommand(7001));

        Assert.Equal(7001, result.Reward.TargetId);
        Assert.Equal(1001, result.Reward.SenderUserId);
        Assert.Equal(2002, result.Reward.RecipientUserId);
        Assert.Equal(2, result.SenderAvailableBalance);
        Assert.Equal(1, result.TotalCount);

        var balances = harness.Db.Queryable<UserBalance>()
            .OrderBy(item => item.UserId)
            .ToList();
        Assert.Collection(
            balances,
            sender =>
            {
                Assert.Equal(1001, sender.UserId);
                Assert.Equal(2, sender.Balance);
                Assert.Equal(1, sender.TotalSpent);
                Assert.Equal(1, sender.TotalTransferredOut);
                Assert.Equal(1, sender.Version);
            },
            recipient =>
            {
                Assert.Equal(2002, recipient.UserId);
                Assert.Equal(1, recipient.Balance);
                Assert.Equal(1, recipient.TotalEarned);
                Assert.Equal(1, recipient.TotalTransferredIn);
                Assert.Equal(1, recipient.Version);
            });

        var transaction = Assert.Single(harness.Db.Queryable<CoinTransaction>().ToList());
        Assert.Equal("TIP", transaction.TransactionType);
        Assert.Equal("SUCCESS", transaction.Status);
        Assert.Equal(BusinessType.ContentReward, transaction.BusinessType);
        Assert.Equal(result.Reward.Id, transaction.BusinessId);

        var idempotency = harness.Db.Queryable<OperationIdempotencyRecord>().InSingle(9001);
        Assert.NotNull(idempotency);
        Assert.Equal(OperationIdempotencyStatuses.Succeeded, idempotency!.Status);
        Assert.Equal(OperationIdempotencyResourceTypes.ContentReward, idempotency.ResourceType);
        var response = JsonSerializer.Deserialize<ContentRewardMutationVo>(
            idempotency.ResponsePayload!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(response);
        Assert.Equal(result.Reward.Id, response!.VoRewardId);
        Assert.Equal(2, response.VoSenderAvailableBalance);

        var outbox = harness.Db.Queryable<ReliableOutboxMessage>()
            .OrderBy(item => item.TaskType)
            .ToList();
        Assert.Equal(2, outbox.Count);
        Assert.Contains(outbox, item => item.TaskType == ReliableTaskTypes.ContentRewardAuditProjection);
        Assert.Contains(outbox, item => item.TaskType == ReliableTaskTypes.NotificationRequested);

        var target = new ContentRewardTargetKey(ContentRewardTargetTypes.Post, 7001);
        var count = Assert.Single(await harness.Repository.QueryTargetCountsAsync(9, [target]));
        Assert.Equal(1, count.TotalCount);
        Assert.Contains(
            target,
            await harness.Repository.QueryRewardedTargetsAsync(9, 1001, [target]));
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectSecondRewardForSameSenderAndTargetWithoutChangingBalance()
    {
        using var harness = new MainRepositoryHarness();
        await harness.Repository.CreateAsync(CreateCommand(7001));
        harness.SeedIdempotency(9002, "content-reward:second");

        await Assert.ThrowsAsync<ContentRewardAlreadyExistsException>(() =>
            harness.Repository.CreateAsync(CreateCommand(7001, 9002)));

        Assert.Single(harness.Db.Queryable<ContentReward>().ToList());
        Assert.Single(harness.Db.Queryable<CoinTransaction>().ToList());
        Assert.Equal(2, harness.Db.Queryable<UserBalance>().Single(item => item.UserId == 1001).Balance);
        Assert.Equal(1, harness.Db.Queryable<UserBalance>().Single(item => item.UserId == 2002).Balance);
        Assert.Equal(
            OperationIdempotencyStatuses.Processing,
            harness.Db.Queryable<OperationIdempotencyRecord>().InSingle(9002)!.Status);
    }

    [Fact]
    public async Task CreateAsync_ShouldRollbackWhenSenderBalanceIsInsufficient()
    {
        using var harness = new MainRepositoryHarness(senderBalance: 0);

        await Assert.ThrowsAsync<ContentRewardInsufficientBalanceException>(() =>
            harness.Repository.CreateAsync(CreateCommand(7001)));

        Assert.Empty(harness.Db.Queryable<ContentReward>().ToList());
        Assert.Empty(harness.Db.Queryable<CoinTransaction>().ToList());
        Assert.Empty(harness.Db.Queryable<ReliableOutboxMessage>().ToList());
        Assert.Equal(0, harness.Db.Queryable<UserBalance>().Single(item => item.UserId == 1001).Balance);
        Assert.Equal(0, harness.Db.Queryable<UserBalance>().Single(item => item.UserId == 2002).Balance);
        Assert.Equal(
            OperationIdempotencyStatuses.Processing,
            harness.Db.Queryable<OperationIdempotencyRecord>().InSingle(9001)!.Status);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectBlockedPairWithoutAssetWrite()
    {
        using var harness = new MainRepositoryHarness();
        harness.Db.Insertable(new UserBlock
        {
            Id = 6001,
            TenantId = 9,
            BlockerUserId = 2002,
            BlockedUserId = 1001,
            RelationshipVersion = 1,
            CreateTime = NowUtc,
            CreateBy = "bob",
            CreateId = 2002
        }).ExecuteCommand();

        await Assert.ThrowsAsync<ContentRewardInteractionUnavailableException>(() =>
            harness.Repository.CreateAsync(CreateCommand(7001)));

        Assert.Empty(harness.Db.Queryable<ContentReward>().ToList());
        Assert.Empty(harness.Db.Queryable<CoinTransaction>().ToList());
        Assert.Equal(3, harness.Db.Queryable<UserBalance>().Single(item => item.UserId == 1001).Balance);
        Assert.Equal(0, harness.Db.Queryable<UserBalance>().Single(item => item.UserId == 2002).Balance);
    }

    [Fact]
    public async Task CreateAsync_ShouldRepairExpiredProcessingFromCommittedMainFacts()
    {
        using var harness = new MainRepositoryHarness();
        var committed = await harness.Repository.CreateAsync(CreateCommand(7001));
        var idempotency = harness.Db.Queryable<OperationIdempotencyRecord>().InSingle(9001)!;
        idempotency.Status = OperationIdempotencyStatuses.Processing;
        idempotency.ResourceType = null;
        idempotency.ResourceId = null;
        idempotency.ResourceNo = null;
        idempotency.ResponsePayload = null;
        idempotency.CompleteTime = null;
        idempotency.ExpiresAt = NowUtc.AddMinutes(-1);
        harness.Db.Updateable(idempotency).ExecuteCommand();
        harness.Db.Updateable<Post>()
            .SetColumns(item => new Post { IsEnabled = false })
            .Where(item => item.Id == 7001)
            .ExecuteCommand();

        var recovered = await harness.Repository.CreateAsync(
            CreateCommand(7001, recoverExpiredProcessing: true));

        Assert.Equal(committed.Reward.Id, recovered.Reward.Id);
        Assert.Equal(committed.TransactionNo, recovered.TransactionNo);
        Assert.Equal(2, recovered.SenderAvailableBalance);
        Assert.Single(harness.Db.Queryable<ContentReward>().ToList());
        Assert.Single(harness.Db.Queryable<CoinTransaction>().ToList());
        Assert.Equal(2, harness.Db.Queryable<ReliableOutboxMessage>().Count());
        var repairedIdempotency =
            harness.Db.Queryable<OperationIdempotencyRecord>().InSingle(9001)!;
        Assert.Equal(OperationIdempotencyStatuses.Succeeded, repairedIdempotency.Status);
        Assert.Equal(NowUtc.AddHours(24), repairedIdempotency.ExpiresAt);
    }

    [Fact]
    public async Task AuditProjection_ShouldBeIdempotentAndRejectPayloadDrift()
    {
        using var harness = new LogRepositoryHarness();
        var command = CreateProjectionCommand();

        await harness.Repository.ProjectAsync(command);
        await harness.Repository.ProjectAsync(command);

        var entries = harness.Db.Queryable<BalanceChangeLog>()
            .SplitTable()
            .OrderBy(item => item.UserId)
            .ToList();
        Assert.Collection(
            entries,
            sender =>
            {
                Assert.Equal(1001, sender.UserId);
                Assert.Equal(-1, sender.ChangeAmount);
                Assert.Equal("TRANSFER_OUT", sender.ChangeType);
            },
            recipient =>
            {
                Assert.Equal(2002, recipient.UserId);
                Assert.Equal(1, recipient.ChangeAmount);
                Assert.Equal("TRANSFER_IN", recipient.ChangeType);
            });

        var drifted = command with
        {
            SenderEntry = command.SenderEntry with { BalanceBefore = 99, BalanceAfter = 98 }
        };
        await Assert.ThrowsAsync<ContentRewardAuditProjectionConflictException>(() =>
            harness.Repository.ProjectAsync(drifted));
        Assert.Equal(2, harness.Db.Queryable<BalanceChangeLog>().SplitTable().Count());
    }

    private static ContentRewardWriteCommand CreateCommand(
        long targetId,
        long idempotencyRecordId = 9001,
        bool recoverExpiredProcessing = false)
    {
        return new ContentRewardWriteCommand(
            9,
            1001,
            "alice",
            ContentRewardTargetTypes.Post,
            targetId,
            ContentRewardReasonCodes.Helpful,
            idempotencyRecordId,
            RequestHash,
            NowUtc.Date,
            NowUtc.Date.AddDays(1),
            20,
            5,
            recoverExpiredProcessing,
            NowUtc);
    }

    private static ContentRewardAuditProjectionCommand CreateProjectionCommand()
    {
        return new ContentRewardAuditProjectionCommand(
            9,
            8001,
            8101,
            NowUtc,
            "alice",
            1001,
            new ContentRewardAuditEntry(
                1001,
                -1,
                3,
                2,
                "TRANSFER_OUT",
                "content-reward:8101:1001:out"),
            new ContentRewardAuditEntry(
                2002,
                1,
                0,
                1,
                "TRANSFER_IN",
                "content-reward:8101:2002:in"));
    }

    private sealed class MainRepositoryHarness : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"radish-content-reward-{Guid.NewGuid():N}.db");

        public MainRepositoryHarness(long senderBalance = 3)
        {
            Db = CreateClient("main", _path);
            Db.CodeFirst.InitTables<
                ContentReward,
                CoinTransaction,
                UserBalance,
                OperationIdempotencyRecord>();
            Db.CodeFirst.InitTables<User, UserBlock, Post, ReliableOutboxMessage>();
            Repository = new ContentRewardRepository(
                new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance));

            Db.Insertable(new[]
            {
                CreateUser(1001, 10001, "alice"),
                CreateUser(2002, 10002, "bob")
            }).ExecuteCommand();
            Db.Insertable(new Post
            {
                Id = 7001,
                PublicId = "post_reward_test",
                TenantId = 9,
                Title = "值得赞赏的帖子",
                Content = "content",
                AuthorId = 2002,
                AuthorName = "bob",
                IsPublished = true,
                PublishTime = NowUtc,
                IsEnabled = true,
                CreateTime = NowUtc,
                CreateBy = "bob",
                CreateId = 2002
            }).ExecuteCommand();
            Db.Insertable(new[]
            {
                CreateBalance(1001, senderBalance),
                CreateBalance(2002, 0)
            }).ExecuteCommand();
            SeedIdempotency(9001, "content-reward:first");
        }

        public SqlSugarScope Db { get; }

        public ContentRewardRepository Repository { get; }

        public void SeedIdempotency(long id, string key)
        {
            Db.Insertable(new OperationIdempotencyRecord
            {
                Id = id,
                TenantId = 9,
                UserId = 1001,
                OperationType = OperationIdempotencyOperationTypes.ContentReward,
                IdempotencyKey = key,
                RequestHash = RequestHash,
                RequestSummary = "{}",
                Status = OperationIdempotencyStatuses.Processing,
                ExpiresAt = NowUtc.AddDays(1),
                CreateTime = NowUtc,
                CreateBy = "alice",
                CreateId = 1001
            }).ExecuteCommand();
        }

        public void Dispose()
        {
            Db.Dispose();
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }

        private static User CreateUser(long id, long publicIndex, string name)
        {
            return new User
            {
                Id = id,
                PublicId = $"usr_{id:D32}",
                PublicIndex = publicIndex,
                UserName = name,
                TenantId = 9,
                IsEnable = true,
                IsDeleted = false,
                StatusCode = (int)UserStatusCodeEnum.Normal
            };
        }

        private static UserBalance CreateBalance(long userId, long balance)
        {
            return new UserBalance
            {
                Id = userId + 10000,
                UserId = userId,
                Balance = balance,
                TenantId = 9,
                CreateTime = NowUtc,
                CreateBy = "Seed",
                CreateId = 0
            };
        }
    }

    private sealed class LogRepositoryHarness : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"radish-content-reward-log-{Guid.NewGuid():N}.db");

        public LogRepositoryHarness()
        {
            Db = CreateClient("log", _path);
            Db.CodeFirst.InitTables<BalanceChangeLog>();
            Repository = new ContentRewardAuditProjectionRepository(
                new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance));
        }

        public SqlSugarScope Db { get; }

        public ContentRewardAuditProjectionRepository Repository { get; }

        public void Dispose()
        {
            Db.Dispose();
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }

    private static SqlSugarScope CreateClient(string configId, string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = configId,
            DbType = DbType.Sqlite,
            ConnectionString = $"Data Source={path};Cache=Shared",
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }
}
