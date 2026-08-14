using System;
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
        Assert.Equal(3, harness.Db.Queryable<ReliableOutboxMessage>().Count());
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
    public async Task ReviewAndAppealRelief_ShouldRestrictAndRestoreProductReviewWithCasVersions()
    {
        using var harness = new RepositoryHarness();
        harness.Db.Insertable(new Product
        {
            Id = 7002,
            TenantId = 9,
            Name = "青玉头像框",
            CategoryId = "appearance",
            IsEnabled = true,
            IsOnSale = true,
            CreateTime = NowUtc,
            CreateBy = "shop",
            CreateId = 6001
        }).ExecuteCommand();
        harness.Db.Insertable(new ProductReview
        {
            Id = 8002,
            TenantId = 9,
            ProductId = 7002,
            UserId = 5001,
            EligibleOrderId = 7102,
            AuthorName = "target",
            Rating = 4,
            Comment = "待治理评价",
            Version = 1,
            CreateTime = NowUtc,
            CreateBy = "target",
            CreateId = 5001
        }).ExecuteCommand();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001) with
        {
            TargetType = (int)ContentReportTargetTypeEnum.ProductReview,
            TargetContentId = 8002,
            TargetPostId = null,
            TargetProductId = 7002,
            SnapshotTitle = "青玉头像框",
            SnapshotSummary = "待治理评价"
        });

        var result = await harness.Repository.ReviewCaseAsync(
            new ContentModerationCaseReviewWriteCommand(
                9,
                submitted.Case.PublicId,
                1,
                (int)ContentModerationDecision.Violation,
                (int)ContentModerationTargetDisposition.Restricted,
                1,
                "MeasuresTaken",
                "限制违规评价",
                null,
                "case-review-product-review",
                9001,
                "reviewer",
                NowUtc.AddMinutes(1)));

        var review = harness.Db.Queryable<ProductReview>().InSingle(8002)!;
        Assert.True(review.IsDeleted);
        Assert.Equal(2, review.Version);
        Assert.Equal(result.TargetAction!.Id, review.ModerationTargetActionId);
        Assert.Equal(2, result.TargetAction.ResultTargetVersion);
        Assert.Equal("Restricted", result.TargetAction.ResultCode);

        var submittedAppeal = await harness.Repository.SubmitAppealAsync(
            new ContentModerationAppealSubmitCommand(
                9,
                submitted.Case.PublicId,
                5001,
                "target",
                "请求恢复商品评价",
                "appeal-submit-product-review",
                NowUtc.AddMinutes(2)));
        var appealDecision = await harness.Repository.ReviewAppealAsync(
            new ContentModerationAppealReviewCommand(
                9,
                submittedAppeal.Appeal.PublicId,
                1,
                (int)ContentModerationAppealOutcome.Granted,
                (int)ContentModerationReliefScope.TargetContent,
                "Granted",
                "评价限制予以纠正",
                null,
                "appeal-decision-product-review",
                9002,
                "appeal-reviewer",
                NowUtc.AddMinutes(3)));
        var relief = await harness.Repository.ExecuteAppealReliefAsync(
            new ContentModerationAppealReliefCommand(
                9,
                submittedAppeal.Appeal.PublicId,
                appealDecision.Appeal.Version,
                "appeal-relief-product-review",
                9003,
                "action-executor",
                NowUtc.AddMinutes(4)));

        var restored = harness.Db.Queryable<ProductReview>().InSingle(8002)!;
        Assert.False(restored.IsDeleted);
        Assert.Equal(3, restored.Version);
        Assert.Null(restored.ModerationTargetActionId);
        var restoreAction = Assert.Single(relief.TargetActions);
        Assert.Equal(result.TargetAction.Id, restoreAction.SourceTargetActionId);
        Assert.Equal(3, restoreAction.ResultTargetVersion);
        Assert.Equal("Restored", restoreAction.ResultCode);
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

        var failed = await harness.Repository.CompleteChatTargetActionAsync(
            new ContentModerationChatActionCompletionCommand(
                9,
                submitted.Case.Id,
                pending.TargetAction!.Id,
                command.OperationKey,
                false,
                "RecallFailed",
                9001,
                "reviewer",
                NowUtc.AddMinutes(2)));

        Assert.Equal((int)ContentModerationCaseStatus.Reviewing, failed.Status);
        Assert.Equal((int)ContentModerationTargetDisposition.ActionFailed, failed.TargetDisposition);
        Assert.Equal("Submitted", Assert.Single(harness.Db.Queryable<ContentReport>().ToList()).ReporterState);

        var completed = await harness.Repository.CompleteChatTargetActionAsync(
            new ContentModerationChatActionCompletionCommand(
                9,
                submitted.Case.Id,
                pending.TargetAction!.Id,
                command.OperationKey,
                true,
                "Restricted",
                9001,
                "reviewer",
                NowUtc.AddMinutes(3)));

        Assert.Equal((int)ContentModerationCaseStatus.Resolved, completed.Status);
        Assert.Equal((int)ContentModerationTargetDisposition.Restricted, completed.TargetDisposition);
        Assert.Equal("Resolved", Assert.Single(harness.Db.Queryable<ContentReport>().ToList()).ReporterState);
        Assert.Equal(3, harness.Db.Queryable<ReliableOutboxMessage>().Count());
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

    [Fact]
    public async Task AppealRelief_ShouldRestoreOnlyTargetOwnedByOriginalRestriction()
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
            EditCount = 0,
            IsPublished = true,
            PublishTime = NowUtc,
            CreateTime = NowUtc,
            CreateBy = "target",
            CreateId = 5001
        }).ExecuteCommand();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));
        var reviewed = await harness.Repository.ReviewCaseAsync(
            new ContentModerationCaseReviewWriteCommand(
                9,
                submitted.Case.PublicId,
                1,
                (int)ContentModerationDecision.Violation,
                (int)ContentModerationTargetDisposition.Restricted,
                0,
                "MeasuresTaken",
                null,
                null,
                "case-review-for-appeal",
                9001,
                "reviewer",
                NowUtc.AddMinutes(1)));
        var submittedAppeal = await harness.Repository.SubmitAppealAsync(
            new ContentModerationAppealSubmitCommand(
                9,
                submitted.Case.PublicId,
                5001,
                "target",
                "请求复核治理决定及内容处置",
                "appeal-submit-5001",
                NowUtc.AddMinutes(2)));

        await Assert.ThrowsAsync<ContentModerationAppealAlreadyExistsException>(() =>
            harness.Repository.SubmitAppealAsync(
                new ContentModerationAppealSubmitCommand(
                    9,
                    submitted.Case.PublicId,
                    5001,
                    "target",
                    "再次提交同一案件申诉",
                    "appeal-submit-duplicate",
                    NowUtc.AddMinutes(3))));

        var decision = await harness.Repository.ReviewAppealAsync(
            new ContentModerationAppealReviewCommand(
                9,
                submittedAppeal.Appeal.PublicId,
                1,
                (int)ContentModerationAppealOutcome.Granted,
                (int)ContentModerationReliefScope.TargetContent,
                "Granted",
                "原内容处置予以纠正",
                null,
                "appeal-decision-5001",
                9001,
                "reviewer",
                NowUtc.AddMinutes(4)));
        var relief = await harness.Repository.ExecuteAppealReliefAsync(
            new ContentModerationAppealReliefCommand(
                9,
                decision.Appeal.PublicId,
                decision.Appeal.Version,
                "appeal-relief-5001",
                9001,
                "reviewer",
                NowUtc.AddMinutes(5)));

        var post = harness.Db.Queryable<Post>().InSingle(7001)!;
        Assert.False(post.IsDeleted);
        Assert.Null(post.ModerationTargetActionId);
        Assert.Equal((int)ContentModerationAppealStatus.Resolved, relief.Appeal.Status);
        Assert.Equal(2, harness.Db.Queryable<ContentModerationTargetAction>().Count());
        Assert.Equal(reviewed.TargetAction!.Id, relief.TargetActions.Single().SourceTargetActionId);
    }

    [Fact]
    public async Task AppealReviewPreparation_ShouldCompleteOnSqliteAndReplayOperations()
    {
        using var harness = new RepositoryHarness();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001));
        await harness.Repository.ReviewCaseAsync(
            new ContentModerationCaseReviewWriteCommand(
                9,
                submitted.Case.PublicId,
                1,
                (int)ContentModerationDecision.Violation,
                (int)ContentModerationTargetDisposition.Keep,
                null,
                "MeasuresTaken",
                null,
                new ContentModerationUserActionWriteCommand(
                    5001,
                    "target",
                    (int)ModerationActionTypeEnum.Mute,
                    0,
                    24,
                    "appeal preparation"),
                "case-review-for-preparation",
                9001,
                "reviewer",
                NowUtc.AddMinutes(1)));
        var submittedAppeal = await harness.Repository.SubmitAppealAsync(
            new ContentModerationAppealSubmitCommand(
                9,
                submitted.Case.PublicId,
                5001,
                "target",
                "请求复核原治理决定与用户限制动作",
                "appeal-submit-for-preparation",
                NowUtc.AddMinutes(2)));
        var startCommand = new ContentModerationAppealStartReviewCommand(
            9,
            submittedAppeal.Appeal.PublicId,
            1,
            "appeal-start-review",
            9002,
            "appeal-reviewer",
            NowUtc.AddMinutes(3));

        var started = await harness.Repository.StartAppealReviewAsync(startCommand);
        var startReplay = await harness.Repository.StartAppealReviewAsync(startCommand);
        var evidenceCommand = new ContentModerationAppealEvidenceCommand(
            9,
            submittedAppeal.Appeal.PublicId,
            started.Appeal.Version,
            (int)ContentModerationTargetState.Available,
            "current target",
            "current target summary",
            7001,
            null,
            null,
            null,
            new string('b', 64),
            "appeal-capture-evidence",
            9002,
            "appeal-reviewer",
            NowUtc.AddMinutes(4));
        var captured = await harness.Repository.AppendAppealEvidenceAsync(evidenceCommand);
        var evidenceReplay = await harness.Repository.AppendAppealEvidenceAsync(evidenceCommand);

        Assert.Equal((int)ContentModerationAppealStatus.Reviewing, started.Appeal.Status);
        Assert.True(startReplay.IsIdempotentReplay);
        Assert.Equal(2, started.Appeal.Version);
        Assert.Equal((int)ContentModerationAppealStatus.Reviewing, captured.Appeal.Status);
        Assert.True(evidenceReplay.IsIdempotentReplay);
        Assert.Equal(3, captured.Appeal.Version);
        Assert.Equal(
            ["Submitted", "ReviewStarted", "EvidenceCaptured"],
            harness.Db.Queryable<ContentModerationAppealEvent>()
                .OrderBy(item => item.EventSequence)
                .Select(item => item.EventType)
                .ToList());
    }

    [Fact]
    public async Task ChatAppealRelief_ShouldRecoverFromFailedOutboxAttempt()
    {
        using var harness = new RepositoryHarness();
        var submitted = await harness.Repository.SubmitReportAsync(CreateReportCommand(1001) with
        {
            TargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
            TargetContentId = 8001,
            TargetPostId = null,
            TargetChannelId = 8101
        });
        var reviewed = await harness.Repository.ReviewCaseAsync(
            new ContentModerationCaseReviewWriteCommand(
                9,
                submitted.Case.PublicId,
                1,
                (int)ContentModerationDecision.Violation,
                (int)ContentModerationTargetDisposition.Restricted,
                null,
                "MeasuresTaken",
                null,
                null,
                "case-review-chat-for-appeal",
                9001,
                "reviewer",
                NowUtc.AddMinutes(1)));
        await harness.Repository.CompleteChatTargetActionAsync(
            new ContentModerationChatActionCompletionCommand(
                9,
                submitted.Case.Id,
                reviewed.TargetAction!.Id,
                "case-review-chat-for-appeal",
                true,
                "Restricted",
                9001,
                "reviewer",
                NowUtc.AddMinutes(2)));
        var submittedAppeal = await harness.Repository.SubmitAppealAsync(
            new ContentModerationAppealSubmitCommand(
                9,
                submitted.Case.PublicId,
                5001,
                "target",
                "请求复核并恢复聊天消息及关联状态",
                "appeal-submit-chat",
                NowUtc.AddMinutes(3)));
        var decision = await harness.Repository.ReviewAppealAsync(
            new ContentModerationAppealReviewCommand(
                9,
                submittedAppeal.Appeal.PublicId,
                1,
                (int)ContentModerationAppealOutcome.Granted,
                (int)ContentModerationReliefScope.TargetContent,
                "Granted",
                "准予恢复聊天消息",
                null,
                "appeal-decision-chat",
                9002,
                "appeal-reviewer",
                NowUtc.AddMinutes(4)));
        var relief = await harness.Repository.ExecuteAppealReliefAsync(
            new ContentModerationAppealReliefCommand(
                9,
                submittedAppeal.Appeal.PublicId,
                decision.Appeal.Version,
                "appeal-relief-chat",
                9003,
                "action-executor",
                NowUtc.AddMinutes(5)));
        var restoreAction = Assert.Single(relief.TargetActions);
        var failureCommand = new ContentModerationChatReliefCompletionCommand(
            9,
            submittedAppeal.Appeal.Id,
            restoreAction.Id,
            "appeal-relief-chat",
            false,
            "SqliteException",
            9003,
            "action-executor",
            NowUtc.AddMinutes(6));

        var failed = await harness.Repository.CompleteChatReliefAsync(failureCommand);
        var failureReplay = await harness.Repository.CompleteChatReliefAsync(failureCommand);
        var completed = await harness.Repository.CompleteChatReliefAsync(
            failureCommand with
            {
                Succeeded = true,
                ResultCode = "Restored",
                NowUtc = NowUtc.AddMinutes(7)
            });
        var completionReplay = await harness.Repository.CompleteChatReliefAsync(
            failureCommand with
            {
                Succeeded = true,
                ResultCode = "Restored",
                NowUtc = NowUtc.AddMinutes(8)
            });

        Assert.Equal((int)ContentModerationAppealStatus.ReliefFailed, failed.Status);
        Assert.Equal(failed.Version, failureReplay.Version);
        Assert.Equal((int)ContentModerationAppealStatus.Resolved, completed.Status);
        Assert.Equal(completed.Version, completionReplay.Version);
        var persistedAction = harness.Db.Queryable<ContentModerationTargetAction>()
            .InSingle(restoreAction.Id)!;
        Assert.Equal((int)ContentModerationTargetActionStatus.Succeeded, persistedAction.Status);
        Assert.Equal(
            ["ReliefFailed", "ReliefCompleted"],
            harness.Db.Queryable<ContentModerationAppealEvent>()
                .Where(item =>
                    item.AppealId == submittedAppeal.Appeal.Id &&
                    (item.EventType == "ReliefFailed" || item.EventType == "ReliefCompleted"))
                .OrderBy(item => item.EventSequence)
                .Select(item => item.EventType)
                .ToList());
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
                ContentModerationAppeal>();
            Db.CodeFirst.InitTables<
                ContentModerationAppealEvent,
                ContentModerationTargetAction,
                UserModerationAction,
                UserModerationState,
                ReliableOutboxMessage>();
            Db.CodeFirst.InitTables<Post, Comment, PostQuickReply>();
            Db.CodeFirst.InitTables<Product, ProductReview>();
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
