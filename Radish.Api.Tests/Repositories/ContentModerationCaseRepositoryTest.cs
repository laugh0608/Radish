using System;
using System.IO;
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

public sealed class ContentModerationCaseRepositoryTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SubmitReportAsync_ShouldAggregateTargetAndReturnExistingReporterReceipt()
    {
        using var harness = new RepositoryHarness();

        var first = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));
        var second = await harness.Repository.SubmitReportAsync(CreateReportCommand(1002));
        var replay = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));

        Assert.False(first.IsDuplicate);
        Assert.False(second.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Case.Id, second.Case.Id);
        Assert.Equal(first.Report.Id, replay.Report.Id);
        Assert.Equal(1, harness.Db.Queryable<ContentModerationCase>().Count());
        Assert.Equal(2, harness.Db.Queryable<ContentReport>().Count());
        Assert.Equal([1, 2], harness.Db.Queryable<ContentModerationEvidence>()
            .OrderBy(item => item.EvidenceSequence)
            .Select(item => item.EvidenceSequence)
            .ToList());
        Assert.Equal(3, harness.Db.Queryable<ContentModerationCaseEvent>().Count());
    }

    [Fact]
    public async Task ReviewCaseAsync_ShouldAtomicallySetStateAndReplaySameOperation()
    {
        using var harness = new RepositoryHarness();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));
        var command = new ContentModerationCaseReviewWriteCommand(
            9,
            submitted.Case.PublicId,
            1,
            (int)ContentModerationDecision.Violation,
            (int)ContentModerationTargetDisposition.Keep,
            null,
            "MeasuresTaken",
            "verified",
            new ContentModerationUserActionWriteCommand(
                5001,
                "target",
                (int)ModerationActionTypeEnum.Mute,
                0,
                24,
                "spam"),
            "case-review-1001",
            9001,
            "reviewer",
            NowUtc.AddMinutes(1));

        var result = await harness.Repository.ReviewCaseAsync(command);
        var replay = await harness.Repository.ReviewCaseAsync(command);

        Assert.Equal((int)ContentModerationCaseStatus.Resolved, result.Case.Status);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(result.UserAction!.Id, replay.UserAction!.Id);
        var state = Assert.Single(harness.Db.Queryable<UserModerationState>().ToList());
        Assert.Equal((int)UserModerationStateValue.Active, state.State);
        Assert.Equal(1, state.Version);
        Assert.Equal(NowUtc.AddHours(24).AddMinutes(1), state.EffectiveUntil);
        Assert.Equal("Resolved", Assert.Single(harness.Db.Queryable<ContentReport>().ToList()).ReporterState);
        Assert.Equal(2, harness.Db.Queryable<ReliableOutboxMessage>().Count());
    }

    [Fact]
    public async Task ReviewCaseAsync_ShouldRollbackWhenPostVersionChanged()
    {
        using var harness = new RepositoryHarness();
        harness.Db.Insertable(new Post
        {
            Id = 7001,
            TenantId = 9,
            Title = "reported",
            Content = "content",
            AuthorId = 5001,
            AuthorName = "target",
            EditCount = 2,
            IsPublished = true,
            PublishTime = NowUtc,
            CreateTime = NowUtc,
            CreateBy = "target",
            CreateId = 5001
        }).ExecuteCommand();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));

        var exception = await Assert.ThrowsAsync<ContentModerationTargetActionException>(() =>
            harness.Repository.ReviewCaseAsync(new ContentModerationCaseReviewWriteCommand(
                9,
                submitted.Case.PublicId,
                1,
                (int)ContentModerationDecision.Violation,
                (int)ContentModerationTargetDisposition.Restricted,
                1,
                "MeasuresTaken",
                null,
                null,
                "case-review-version-conflict",
                9001,
                "reviewer",
                NowUtc.AddMinutes(1))));

        Assert.Equal("VersionConflict", exception.ResultCode);
        Assert.False(harness.Db.Queryable<Post>().InSingle(7001)!.IsDeleted);
        Assert.Equal((int)ContentModerationCaseStatus.Open, harness.Db.Queryable<ContentModerationCase>().Single().Status);
        Assert.Empty(harness.Db.Queryable<UserModerationAction>().ToList());
    }

    [Fact]
    public async Task ReviewCaseAsync_ShouldReplayDecisionWithoutUserActionAndRejectDifferentPayload()
    {
        using var harness = new RepositoryHarness();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));
        var command = new ContentModerationCaseReviewWriteCommand(
            9,
            submitted.Case.PublicId,
            1,
            (int)ContentModerationDecision.NoViolation,
            (int)ContentModerationTargetDisposition.Keep,
            null,
            "NoViolation",
            "reviewed",
            null,
            "case-decision-no-action",
            9001,
            "reviewer",
            NowUtc.AddMinutes(1));

        var first = await harness.Repository.ReviewCaseAsync(command);
        var replay = await harness.Repository.ReviewCaseAsync(command);

        Assert.False(first.IsIdempotentReplay);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Null(replay.UserAction);
        await Assert.ThrowsAsync<ContentModerationIdempotencyConflictException>(() =>
            harness.Repository.ReviewCaseAsync(command with { PublicResultCode = "Different" }));
    }

    [Fact]
    public async Task ReviewCaseAsync_ShouldQueueChatActionAndResolveOnlyAfterCompletion()
    {
        using var harness = new RepositoryHarness();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001) with
        {
            TargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
            TargetContentId = 8001,
            TargetPostId = null,
            TargetChannelId = 8101
        });
        var command = new ContentModerationCaseReviewWriteCommand(
            9,
            submitted.Case.PublicId,
            1,
            (int)ContentModerationDecision.Violation,
            (int)ContentModerationTargetDisposition.Restricted,
            null,
            "MeasuresTaken",
            null,
            null,
            "case-chat-recall",
            9001,
            "reviewer",
            NowUtc.AddMinutes(1));

        var pending = await harness.Repository.ReviewCaseAsync(command);
        var replay = await harness.Repository.ReviewCaseAsync(command);

        Assert.Equal((int)ContentModerationCaseStatus.Reviewing, pending.Case.Status);
        Assert.Equal((int)ContentModerationTargetDisposition.ActionPending, pending.Case.TargetDisposition);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Single(harness.Db.Queryable<ReliableOutboxMessage>().ToList());
        Assert.Equal("Submitted", Assert.Single(harness.Db.Queryable<ContentReport>().ToList()).ReporterState);

        var completed = await harness.Repository.CompleteChatTargetActionAsync(
            new ContentModerationChatActionCompletionCommand(
                9,
                submitted.Case.Id,
                command.OperationKey,
                true,
                "Restricted",
                9001,
                "reviewer",
                NowUtc.AddMinutes(2)));

        Assert.Equal((int)ContentModerationCaseStatus.Resolved, completed.Status);
        Assert.Equal((int)ContentModerationTargetDisposition.Restricted, completed.TargetDisposition);
        Assert.Equal("Resolved", Assert.Single(harness.Db.Queryable<ContentReport>().ToList()).ReporterState);
        Assert.Equal(2, harness.Db.Queryable<ReliableOutboxMessage>().Count());
    }

    [Fact]
    public async Task ApplyStandaloneUserActionAsync_ShouldUseAuthoritativeStateAndReplayOperation()
    {
        using var harness = new RepositoryHarness();
        var muteCommand = new ContentModerationStandaloneUserActionCommand(
            9,
            5001,
            "target",
            (int)ModerationActionTypeEnum.Mute,
            24,
            "legacy review",
            7101,
            "legacy-review:9:7101:1",
            9001,
            "reviewer",
            NowUtc);

        var mute = await harness.Repository.ApplyStandaloneUserActionAsync(muteCommand);
        var replay = await harness.Repository.ApplyStandaloneUserActionAsync(muteCommand);
        var ban = await harness.Repository.ApplyStandaloneUserActionAsync(
            new ContentModerationStandaloneUserActionCommand(
                9,
                5001,
                "target",
                (int)ModerationActionTypeEnum.Ban,
                null,
                "legacy manual action",
                null,
                "legacy-manual:ban-5001",
                9001,
                "reviewer",
                NowUtc.AddMinutes(1)));

        Assert.False(mute.IsIdempotentReplay);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(mute.Action.Id, replay.Action.Id);
        Assert.Equal((int)UserModerationPolicyType.Ban, ban.State.PolicyType);
        Assert.Equal((int)UserModerationStateValue.Active, ban.State.State);
        var states = harness.Db.Queryable<UserModerationState>().OrderBy(item => item.PolicyType).ToList();
        Assert.Equal(2, states.Count);
        Assert.Equal((int)UserModerationStateValue.Inactive, states[0].State);
        Assert.Equal((int)UserModerationStateValue.Active, states[1].State);
        Assert.Equal(3, harness.Db.Queryable<UserModerationAction>().Count());
        Assert.Equal(2, harness.Db.Queryable<ReliableOutboxMessage>().Count());
    }

    private static ContentModerationReportWriteCommand CreateReportCommand(long reporterUserId)
    {
        return new ContentModerationReportWriteCommand(
            9,
            (int)ContentReportTargetTypeEnum.Post,
            7001,
            5001,
            "target",
            7001,
            null,
            "reported",
            "snapshot",
            new string('a', 64),
            reporterUserId,
            $"reporter-{reporterUserId}",
            "Spam",
            "detail",
            NowUtc);
    }

    private sealed class RepositoryHarness : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"radish-moderation-case-{Guid.NewGuid():N}.db");

        public RepositoryHarness()
        {
            Db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "main",
                DbType = DbType.Sqlite,
                ConnectionString = $"Data Source={_path}",
                IsAutoCloseConnection = false,
                InitKeyType = InitKeyType.Attribute
            });
            Db.CodeFirst.InitTables<
                ContentModerationCase,
                ContentReport,
                ContentModerationEvidence,
                ContentModerationCaseEvent,
                UserModerationAction>();
            Db.CodeFirst.InitTables<UserModerationState, ReliableOutboxMessage, Post, Comment, PostQuickReply>();
            Db.CodeFirst.InitTables<Product>();
            Repository = new ContentModerationCaseRepository(
                new UnitOfWorkManage(Db, NullLogger<UnitOfWorkManage>.Instance));
        }

        public SqlSugarScope Db { get; }
        public ContentModerationCaseRepository Repository { get; }

        public void Dispose()
        {
            Db.Dispose();
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
    }
}
