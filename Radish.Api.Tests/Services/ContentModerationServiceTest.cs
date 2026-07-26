using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ContentModerationServiceTest
{
    [Fact]
    public async Task GetAppealQueueAsync_ShouldReturnRedactedItemsForViewPermission()
    {
        var repository = new Mock<IContentModerationCaseRepository>(MockBehavior.Strict);
        var appeal = new ContentModerationAppeal
        {
            Id = 73001,
            TenantId = 5,
            CaseId = 72001,
            PublicId = "appeal_public",
            Status = (int)ContentModerationAppealStatus.Reviewing,
            Outcome = (int)ContentModerationAppealOutcome.None,
            EligibleScopeSnapshot = (int)ContentModerationReliefScope.TargetContent,
            GrantedScope = (int)ContentModerationReliefScope.None,
            Version = 3,
            Statement = "仅复核人员可读取的完整申诉陈述",
            InternalRemark = "仅复核人员可读取的内部备注",
            SubmittedAt = new DateTime(2026, 7, 25, 1, 0, 0, DateTimeKind.Utc),
            EligibleUntilUtc = new DateTime(2026, 8, 1, 1, 0, 0, DateTimeKind.Utc)
        };
        var aggregate = new ContentModerationAppealAggregate(
            appeal,
            new ContentModerationCase { Id = 72001, TenantId = 5, PublicId = "case_public" },
            [],
            [
                new ContentModerationAppealEvent
                {
                    EventSequence = 1,
                    EventType = "Submitted",
                    Remark = "内部事件备注",
                    ActorUserId = 61001,
                    ActorName = "appellant"
                }
            ],
            [],
            [],
            []);
        repository
            .Setup(item => item.QueryAppealQueueAsync(It.Is<ContentModerationAppealQueueCommand>(
                command => command.TenantId == 5 && command.PageIndex == 1 && command.PageSize == 20)))
            .ReturnsAsync((new List<ContentModerationAppeal> { appeal }, 1));
        repository
            .Setup(item => item.QueryAppealAggregateAsync(5, "appeal_public", null))
            .ReturnsAsync(aggregate);
        var service = CreateService(moderationCaseRepository: repository);

        var result = await service.GetAppealQueueAsync(new ContentModerationAppealQueryDto(), 5);

        var item = result.VoItems.ShouldHaveSingleItem();
        item.VoAppealPublicId.ShouldBe("appeal_public");
        item.VoCasePublicId.ShouldBe("case_public");
        item.VoStatement.ShouldBeEmpty();
        item.VoInternalRemark.ShouldBeNull();
        item.VoEvents.ShouldBeEmpty();
        item.VoTargetActions.ShouldBeEmpty();
        item.VoUserActions.ShouldBeEmpty();
        item.VoUserActionSummaries.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMyAppealableDecisionsAsync_ShouldReturnServerEligibilityAndSafeSnapshot()
    {
        var repository = new Mock<IContentModerationCaseRepository>(MockBehavior.Strict);
        var eligibleCase = new ContentModerationCase
        {
            Id = 72011,
            TenantId = 5,
            PublicId = "case_eligible",
            TargetType = (int)ContentReportTargetTypeEnum.Post,
            TargetContentId = 91001,
            TargetUserId = 61001,
            Status = (int)ContentModerationCaseStatus.Resolved,
            Decision = (int)ContentModerationDecision.Violation,
            ResolvedAt = new DateTime(2099, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var expiredCase = new ContentModerationCase
        {
            Id = 72012,
            TenantId = 5,
            PublicId = "case_expired",
            TargetType = (int)ContentReportTargetTypeEnum.Comment,
            TargetContentId = 91002,
            TargetUserId = 61001,
            Status = (int)ContentModerationCaseStatus.Resolved,
            Decision = (int)ContentModerationDecision.Violation,
            ResolvedAt = new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        repository
            .Setup(item => item.QueryMyAppealableDecisionsAsync(
                5,
                61001,
                1,
                20))
            .ReturnsAsync((
                new List<ContentModerationDecisionCandidate>
                {
                    new(
                        eligibleCase,
                        null,
                        [
                            new ContentModerationEvidence
                            {
                                EvidenceSequence = 2,
                                EvidenceType = (int)ContentModerationEvidenceType.CurrentTargetSnapshot,
                                SnapshotTitle = "安全目标标题",
                                SnapshotSummary = "安全目标摘要"
                            }
                        ],
                        [
                            new ContentModerationTargetAction
                            {
                                ActionType = (int)ContentModerationTargetActionType.Restrict,
                                ChangedTargetState = true
                            }
                        ],
                        []),
                    new(
                        expiredCase,
                        null,
                        [],
                        [
                            new ContentModerationTargetAction
                            {
                                ActionType = (int)ContentModerationTargetActionType.Restrict,
                                ChangedTargetState = true
                            }
                        ],
                        [])
                },
                2));
        var service = CreateService(moderationCaseRepository: repository);

        var result = await service.GetMyAppealableDecisionsAsync(
            new ContentModerationAppealQueryDto(),
            61001,
            5);

        result.VoItems.Count.ShouldBe(2);
        result.VoItems[0].VoCanAppeal.ShouldBeTrue();
        result.VoItems[0].VoIneligibleReason.ShouldBeNull();
        result.VoItems[0].VoTargetSnapshotTitle.ShouldBe("安全目标标题");
        result.VoItems[0].VoTargetSnapshotSummary.ShouldBe("安全目标摘要");
        result.VoItems[1].VoCanAppeal.ShouldBeFalse();
        result.VoItems[1].VoIneligibleReason.ShouldBe("Expired");
    }

    [Fact]
    public async Task GetAppealAsync_ForAppellant_ShouldReturnSafeUserActionSummariesOnly()
    {
        var repository = new Mock<IContentModerationCaseRepository>(MockBehavior.Strict);
        var aggregate = new ContentModerationAppealAggregate(
            new ContentModerationAppeal
            {
                Id = 73003,
                TenantId = 5,
                CaseId = 72013,
                AppellantUserId = 61001,
                PublicId = "appeal_personal",
                Statement = "本人申诉陈述"
            },
            new ContentModerationCase { Id = 72013, TenantId = 5, PublicId = "case_personal" },
            [],
            [],
            [],
            [
                new UserModerationAction
                {
                    Id = 74001,
                    ActionType = (int)ModerationActionTypeEnum.Unmute,
                    ResultCode = "StateDeactivated",
                    Reason = "内部纠正原因",
                    TargetUserId = 61001,
                    TargetUserName = "appellant",
                    CreateId = 62001,
                    CreateBy = "operator",
                    StartTime = new DateTime(2026, 7, 25, 2, 0, 0, DateTimeKind.Utc),
                    CreateTime = new DateTime(2026, 7, 25, 2, 0, 0, DateTimeKind.Utc)
                }
            ],
            []);
        repository
            .Setup(item => item.QueryAppealAggregateAsync(5, "appeal_personal", 61001))
            .ReturnsAsync(aggregate);
        var service = CreateService(moderationCaseRepository: repository);

        var result = await service.GetAppealAsync("appeal_personal", 5, 61001);

        result.VoUserActions.ShouldBeEmpty();
        var summary = result.VoUserActionSummaries.ShouldHaveSingleItem();
        summary.VoActionType.ShouldBe(nameof(ModerationActionTypeEnum.Unmute));
        summary.VoResultCode.ShouldBe("StateDeactivated");
    }

    [Fact]
    public async Task ExecuteAppealReliefAsync_ShouldReturnActionResultWithoutAppealDetail()
    {
        var repository = new Mock<IContentModerationCaseRepository>(MockBehavior.Strict);
        var appeal = new ContentModerationAppeal
        {
            Id = 73002,
            TenantId = 5,
            PublicId = "appeal_action_result",
            Status = (int)ContentModerationAppealStatus.Resolved,
            Outcome = (int)ContentModerationAppealOutcome.Granted,
            GrantedScope = (int)ContentModerationReliefScope.TargetContent,
            Version = 6,
            Statement = "动作执行权限不应通过写入响应读取此申诉陈述",
            InternalRemark = "动作执行权限不应通过写入响应读取此内部备注"
        };
        repository
            .Setup(item => item.ExecuteAppealReliefAsync(It.Is<ContentModerationAppealReliefCommand>(
                command =>
                    command.TenantId == 5
                    && command.AppealPublicId == "appeal_action_result"
                    && command.ExpectedAppealVersion == 5
                    && command.OperationKey == "appeal-action-operation"
                    && command.OperatorUserId == 62001
                    && command.OperatorName == "action-operator")))
            .ReturnsAsync(new ContentModerationAppealReliefWriteResult(appeal, [], [], true));
        var service = CreateService(moderationCaseRepository: repository);

        var result = await service.ExecuteAppealReliefAsync(
            new ContentModerationAppealVersionedOperationDto
            {
                AppealPublicId = " appeal_action_result ",
                ExpectedVersion = 5,
                OperationKey = " appeal-action-operation "
            },
            62001,
            "action-operator",
            5);

        result.VoAppealPublicId.ShouldBe("appeal_action_result");
        result.VoStatus.ShouldBe(nameof(ContentModerationAppealStatus.Resolved));
        result.VoOutcome.ShouldBe(nameof(ContentModerationAppealOutcome.Granted));
        result.VoGrantedScope.ShouldBe((int)ContentModerationReliefScope.TargetContent);
        result.VoVersion.ShouldBe(6);
        result.VoIsIdempotentReplay.ShouldBeTrue();
    }

    [Theory]
    [InlineData((int)ContentModerationDecision.NoViolation, (int)ContentModerationTargetDisposition.Keep, "MeasuresTaken")]
    [InlineData((int)ContentModerationDecision.Violation, (int)ContentModerationTargetDisposition.Restricted, "NoViolation")]
    [InlineData((int)ContentModerationDecision.InsufficientEvidence, (int)ContentModerationTargetDisposition.Unavailable, "MeasuresTaken")]
    public async Task ReviewCaseAsync_ShouldRejectPublicResultCodeThatDoesNotMatchDecision(
        int decision,
        int targetDisposition,
        string publicResultCode)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewCaseAsync(
                new ReviewContentModerationCaseDto
                {
                    CasePublicId = "mod_test_public_result",
                    ExpectedVersion = 1,
                    Decision = decision,
                    TargetDisposition = targetDisposition,
                    PublicResultCode = publicResultCode,
                    OperationKey = "moderation-public-result-test"
                },
                9001,
                "reviewer",
                0));

        exception.Message.ShouldBe("公开结果分类与案件决定不一致");
    }

    [Fact]
    public async Task SubmitReportAsync_Should_Persist_Comment_Target_Snapshot_On_Create()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var service = CreateService(
            contentReportRepository: contentReportRepository,
            postRepository: postRepository,
            commentRepository: commentRepository);

        commentRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Comment, bool>>?>()))
            .ReturnsAsync(new Comment
            {
                Id = 61001,
                PostId = 51001,
                AuthorId = 31001,
                AuthorName = "comment-author",
                Content = "创建时评论内容"
            });
        postRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new Post
            {
                Id = 51001,
                Title = "创建时帖子标题"
            });
        contentReportRepository
            .Setup(repository => repository.QueryExistsAsync(It.IsAny<Expression<Func<ContentReport, bool>>>()))
            .ReturnsAsync(false);

        ContentReport? createdReport = null;
        contentReportRepository
            .Setup(repository => repository.AddAsync(It.IsAny<ContentReport>()))
            .Callback<ContentReport>(report => createdReport = report)
            .ReturnsAsync(71001);

        var result = await service.SubmitReportAsync(
            new SubmitContentReportDto
            {
                TargetType = "Comment",
                TargetContentId = 61001,
                ReasonType = "Abuse",
                ReasonDetail = "需要快照固化"
            },
            41001,
            "reporter",
            1);

        result.ShouldBe(71001);
        createdReport.ShouldNotBeNull();
        createdReport!.TargetUserId.ShouldBe(31001);
        createdReport.TargetUserName.ShouldBe("comment-author");
        createdReport.TargetSnapshotPostId.ShouldBe(51001);
        createdReport.TargetSnapshotChannelId.ShouldBeNull();
        createdReport.TargetSnapshotTitle.ShouldBe("创建时帖子标题");
        createdReport.TargetSnapshotSummary.ShouldBe("创建时评论内容");
    }

    [Fact]
    public async Task GetReportQueueAsync_Should_Populate_Chat_Target_Navigation_For_Recalled_Message()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        contentReportRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ContentReport, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<ContentReport, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<ContentReport>
                {
                    new()
                    {
                        Id = 70001,
                        ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                        TargetContentId = 90002,
                        TargetUserId = 10002,
                        TargetUserName = "target",
                        ReporterUserId = 10001,
                        ReporterUserName = "reporter",
                        ReasonType = "Spam",
                        Status = (int)ContentReportStatusEnum.Pending,
                        ReviewActionType = (int)ModerationActionTypeEnum.None,
                        CreateTime = new DateTime(2026, 5, 10, 12, 0, 0)
                    }
                },
                1));
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90002)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90002,
                    ChannelId = 88,
                    UserId = 10002,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = null,
                    IsDeleted = true,
                    CreateTime = new DateTime(2026, 5, 10, 11, 50, 0)
                }
            });

        var result = await service.GetReportQueueAsync(new ContentReportQueueQueryDto
        {
            PageIndex = 1,
            PageSize = 20
        });

        result.VoItems.Count.ShouldBe(1);
        var reportItem = result.VoItems[0];
        reportItem.VoTargetType.ShouldBe("ChatMessage");
        reportItem.VoTargetChannelId.ShouldBe(88);
        reportItem.VoTargetMessageId.ShouldBe(90002);
        reportItem.VoTargetSnapshotIsPersisted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportQueueAsync_Should_Load_Post_Navigation_Without_Unused_User_Joins()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var service = CreateService(
            contentReportRepository: contentReportRepository,
            postRepository: postRepository);

        contentReportRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ContentReport, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<ContentReport, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<ContentReport>
                {
                    new()
                    {
                        Id = 70002,
                        ReportTargetType = (int)ContentReportTargetTypeEnum.Post,
                        TargetContentId = 50002,
                        TargetUserId = 20002,
                        TargetUserName = "post-author",
                        ReporterUserId = 20003,
                        ReporterUserName = "reporter",
                        ReasonType = "Other",
                        Status = (int)ContentReportStatusEnum.Pending,
                        ReviewActionType = (int)ModerationActionTypeEnum.None,
                        CreateTime = new DateTime(2026, 7, 11, 14, 0, 0)
                    }
                },
                1));
        postRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new List<Post>
            {
                new()
                {
                    Id = 50002,
                    AuthorId = 20002,
                    Title = "运行态验收帖子",
                    Summary = "用于验证举报队列导航",
                    IsDeleted = false
                }
            });

        var result = await service.GetReportQueueAsync(new ContentReportQueueQueryDto
        {
            PageIndex = 1,
            PageSize = 20
        });

        result.VoItems.Count.ShouldBe(1);
        var reportItem = result.VoItems[0];
        reportItem.VoReportId.ShouldBe(70002);
        reportItem.VoTargetType.ShouldBe("Post");
        reportItem.VoTargetPostId.ShouldBe(50002);
        reportItem.VoTargetNavigationStatus.ShouldBe("Ready");
        reportItem.VoTargetSnapshotTitle.ShouldBe("运行态验收帖子");
        reportItem.VoTargetSnapshotSummary.ShouldBe("用于验证举报队列导航");
    }

    [Fact]
    public async Task GetReportQueueAsync_Should_Prefer_Persisted_Chat_Snapshot_When_Current_Content_Changes()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        contentReportRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<ContentReport, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<ContentReport, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<ContentReport>
                {
                    new()
                    {
                        Id = 70004,
                        ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                        TargetContentId = 90005,
                        TargetSnapshotChannelId = 118,
                        TargetSnapshotSummary = "创建时聊天快照",
                        TargetUserId = 10005,
                        TargetUserName = "target",
                        ReporterUserId = 10001,
                        ReporterUserName = "reporter",
                        ReasonType = "Spam",
                        Status = (int)ContentReportStatusEnum.Pending,
                        ReviewActionType = (int)ModerationActionTypeEnum.None,
                        CreateTime = new DateTime(2026, 5, 10, 12, 20, 0)
                    }
                },
                1));
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90005)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90005,
                    ChannelId = 118,
                    UserId = 10005,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = "当前消息内容已经变化",
                    IsDeleted = false,
                    CreateTime = new DateTime(2026, 5, 10, 12, 19, 0)
                }
            });

        var result = await service.GetReportQueueAsync(new ContentReportQueueQueryDto
        {
            PageIndex = 1,
            PageSize = 20
        });

        result.VoItems.Count.ShouldBe(1);
        var reportItem = result.VoItems[0];
        reportItem.VoTargetChannelId.ShouldBe(118);
        reportItem.VoTargetSnapshotSummary.ShouldBe("创建时聊天快照");
        reportItem.VoTargetSnapshotIsPersisted.ShouldBeTrue();
    }

    [Fact]
    public async Task ReviewReportAsync_Should_Return_Chat_Target_Navigation_When_Report_Target_Is_ChatMessage()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        var report = new ContentReport
        {
            Id = 70002,
            ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
            TargetContentId = 90003,
            TargetUserId = 10003,
            TargetUserName = "target",
            ReporterUserId = 10001,
            ReporterUserName = "reporter",
            ReasonType = "Abuse",
            Status = (int)ContentReportStatusEnum.Pending,
            ReviewActionType = (int)ModerationActionTypeEnum.None,
            CreateTime = new DateTime(2026, 5, 10, 12, 5, 0)
        };

        contentReportRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ContentReport, bool>>?>()))
            .ReturnsAsync(report);
        contentReportRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<ContentReport, ContentReport>>>(),
                It.IsAny<Expression<Func<ContentReport, bool>>>()))
            .ReturnsAsync(1);
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90003)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90003,
                    ChannelId = 99,
                    UserId = 10003,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = "hello",
                    IsDeleted = false,
                    CreateTime = new DateTime(2026, 5, 10, 11, 55, 0)
                }
            });

        var result = await service.ReviewReportAsync(
            new ReviewContentReportDto
            {
                ReportId = 70002,
                IsApproved = false,
                ActionType = (int)ModerationActionTypeEnum.None,
                DurationHours = null,
                ReviewRemark = "not enough evidence"
            },
            20001,
            "reviewer",
            0);

        result.VoReportId.ShouldBe(70002);
        result.VoTargetChannelId.ShouldBe(99);
        result.VoTargetMessageId.ShouldBe(90003);
        result.VoStatus.ShouldBe("Rejected");
    }

    [Fact]
    public async Task ReviewReportAsync_Should_Reject_When_Report_Already_Handled_During_Update()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        contentReportRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ContentReport, bool>>?>()))
            .ReturnsAsync(new ContentReport
            {
                Id = 70004,
                TenantId = 1,
                ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                TargetContentId = 90007,
                TargetUserId = 10007,
                TargetUserName = "target",
                ReporterUserId = 10001,
                ReporterUserName = "reporter",
                ReasonType = "Spam",
                Status = (int)ContentReportStatusEnum.Pending,
                CreateTime = new DateTime(2026, 5, 10, 12, 5, 0)
            });
        contentReportRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<ContentReport, ContentReport>>>(),
                It.IsAny<Expression<Func<ContentReport, bool>>>()))
            .ReturnsAsync(0);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.ReviewReportAsync(
            new ReviewContentReportDto
            {
                ReportId = 70004,
                IsApproved = false,
                ActionType = (int)ModerationActionTypeEnum.None
            },
            20001,
            "reviewer",
            1));

        exception.Message.ShouldBe("举报单已被处理，请刷新审核队列");
        channelMessageRepository.Verify(
            repository => repository.QueryByIdsIncludingDeletedAsync(It.IsAny<List<long>>()),
            Times.Never);
    }

    [Fact]
    public async Task GetActionLogsAsync_Should_Populate_Chat_Source_Report_Navigation_For_Action_Log()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var moderationActionRepository = new Mock<IBaseRepository<UserModerationAction>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(
            contentReportRepository: contentReportRepository,
            moderationActionRepository: moderationActionRepository,
            channelMessageRepository: channelMessageRepository);

        moderationActionRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<UserModerationAction, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<UserModerationAction, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<UserModerationAction>
                {
                    new()
                    {
                        Id = 81001,
                        TargetUserId = 10002,
                        TargetUserName = "target",
                        ActionType = (int)ModerationActionTypeEnum.Mute,
                        Reason = "举报审核通过：Abuse",
                        SourceReportId = 70003,
                        DurationHours = 24,
                        StartTime = new DateTime(2026, 5, 10, 12, 10, 0),
                        EndTime = new DateTime(2026, 5, 11, 12, 10, 0),
                        IsActive = true,
                        CreateId = 20001,
                        CreateBy = "reviewer",
                        CreateTime = new DateTime(2026, 5, 10, 12, 10, 0)
                    }
                },
                1));
        contentReportRepository
            .Setup(repository => repository.QueryByIdsAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 70003)))
            .ReturnsAsync(new List<ContentReport>
            {
                new()
                {
                    Id = 70003,
                    ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                    TargetContentId = 90004,
                    TargetUserId = 10002,
                    TargetUserName = "target",
                    ReporterUserId = 10001,
                    ReporterUserName = "reporter",
                    ReasonType = "Abuse",
                    Status = (int)ContentReportStatusEnum.Approved,
                    ReviewActionType = (int)ModerationActionTypeEnum.Mute,
                    CreateTime = new DateTime(2026, 5, 10, 12, 0, 0)
                }
            });
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90004)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 90004,
                    ChannelId = 108,
                    UserId = 10002,
                    UserName = "target",
                    Type = MessageType.Text,
                    Content = "flagged",
                    IsDeleted = true,
                    CreateTime = new DateTime(2026, 5, 10, 11, 59, 0)
                }
            });

        var result = await service.GetActionLogsAsync(new ContentModerationActionLogQueryDto
        {
            PageIndex = 1,
            PageSize = 20
        });

        result.VoItems.Count.ShouldBe(1);
        var action = result.VoItems[0];
        action.VoActionId.ShouldBe(81001);
        action.VoSourceReportId.ShouldBe(70003);
        action.VoSourceReportTargetType.ShouldBe("ChatMessage");
        action.VoSourceReportTargetContentId.ShouldBe(90004);
        action.VoSourceReportTargetChannelId.ShouldBe(108);
        action.VoSourceReportTargetMessageId.ShouldBe(90004);
        action.VoSourceReportTargetSnapshotIsPersisted.ShouldBeFalse();
    }

    [Fact]
    public async Task GetReportQueueAsync_Should_Filter_By_NavigationStatus_After_Building_Target_State()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(contentReportRepository: contentReportRepository, channelMessageRepository: channelMessageRepository);

        contentReportRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<ContentReport, bool>>?>()))
            .ReturnsAsync(new List<ContentReport>
            {
                new()
                {
                    Id = 72001,
                    ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                    TargetContentId = 91001,
                    TargetUserId = 10011,
                    TargetUserName = "ready-target",
                    ReporterUserId = 10001,
                    ReporterUserName = "reporter",
                    ReasonType = "Spam",
                    Status = (int)ContentReportStatusEnum.Pending,
                    ReviewActionType = (int)ModerationActionTypeEnum.None,
                    CreateTime = new DateTime(2026, 5, 11, 8, 0, 0)
                },
                new()
                {
                    Id = 72002,
                    ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                    TargetContentId = 91002,
                    TargetUserId = 10012,
                    TargetUserName = "missing-target",
                    ReporterUserId = 10001,
                    ReporterUserName = "reporter",
                    ReasonType = "Spam",
                    Status = (int)ContentReportStatusEnum.Pending,
                    ReviewActionType = (int)ModerationActionTypeEnum.None,
                    CreateTime = new DateTime(2026, 5, 11, 9, 0, 0)
                }
            });
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids =>
                ids.Count == 2 &&
                ids.Contains(91001) &&
                ids.Contains(91002))))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 91001,
                    ChannelId = 208,
                    UserId = 10011,
                    UserName = "ready-target",
                    Type = MessageType.Text,
                    Content = "仍可定位",
                    IsDeleted = false,
                    CreateTime = new DateTime(2026, 5, 11, 7, 59, 0)
                }
            });

        var result = await service.GetReportQueueAsync(new ContentReportQueueQueryDto
        {
            NavigationStatus = "Unavailable",
            PageIndex = 1,
            PageSize = 20
        });

        result.VoTotal.ShouldBe(1);
        result.VoItems.Count.ShouldBe(1);
        result.VoItems[0].VoReportId.ShouldBe(72002);
        result.VoItems[0].VoTargetNavigationStatus.ShouldBe("Unavailable");
    }

    [Fact]
    public async Task GetActionLogsAsync_Should_Filter_By_SourceReport_ActionType_IsActive_And_Keyword()
    {
        var contentReportRepository = new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict);
        var moderationActionRepository = new Mock<IBaseRepository<UserModerationAction>>(MockBehavior.Strict);
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(
            contentReportRepository: contentReportRepository,
            moderationActionRepository: moderationActionRepository,
            channelMessageRepository: channelMessageRepository);

        var actions = new List<UserModerationAction>
        {
            new()
            {
                Id = 82001,
                TargetUserId = 10021,
                TargetUserName = "alpha-user",
                ActionType = (int)ModerationActionTypeEnum.Mute,
                Reason = "举报审核通过：Abuse",
                SourceReportId = 73001,
                DurationHours = 24,
                StartTime = new DateTime(2026, 5, 11, 10, 0, 0),
                EndTime = new DateTime(2026, 5, 12, 10, 0, 0),
                IsActive = true,
                CreateId = 20001,
                CreateBy = "reviewer-alpha",
                CreateTime = new DateTime(2026, 5, 11, 10, 0, 0)
            },
            new()
            {
                Id = 82002,
                TargetUserId = 10022,
                TargetUserName = "beta-user",
                ActionType = (int)ModerationActionTypeEnum.Ban,
                Reason = "举报审核通过：Spam",
                SourceReportId = 73002,
                DurationHours = null,
                StartTime = new DateTime(2026, 5, 11, 9, 0, 0),
                EndTime = null,
                IsActive = true,
                CreateId = 20002,
                CreateBy = "reviewer-beta",
                CreateTime = new DateTime(2026, 5, 11, 9, 0, 0)
            },
            new()
            {
                Id = 82003,
                TargetUserId = 10021,
                TargetUserName = "alpha-user",
                ActionType = (int)ModerationActionTypeEnum.Unmute,
                Reason = "管理员解除禁言",
                SourceReportId = 73001,
                DurationHours = null,
                StartTime = new DateTime(2026, 5, 11, 11, 0, 0),
                EndTime = new DateTime(2026, 5, 11, 11, 0, 0),
                IsActive = false,
                CreateId = 20003,
                CreateBy = "reviewer-gamma",
                CreateTime = new DateTime(2026, 5, 11, 11, 0, 0)
            }
        };

        moderationActionRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<UserModerationAction, bool>>?>(),
                1,
                20,
                It.IsAny<Expression<Func<UserModerationAction, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((Expression<Func<UserModerationAction, bool>>? expression, int pageIndex, int pageSize, Expression<Func<UserModerationAction, object>>? orderByExpression, OrderByType orderByType) =>
            {
                var predicate = expression?.Compile() ?? (_ => true);
                var filtered = actions
                    .AsEnumerable()
                    .Where(predicate)
                    .OrderByDescending(item => item.CreateTime)
                    .ToList();
                return (filtered, filtered.Count);
            });
        contentReportRepository
            .Setup(repository => repository.QueryByIdsAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 73001)))
            .ReturnsAsync(new List<ContentReport>
            {
                new()
                {
                    Id = 73001,
                    ReportTargetType = (int)ContentReportTargetTypeEnum.ChatMessage,
                    TargetContentId = 93001,
                    TargetSnapshotChannelId = 208,
                    TargetSnapshotSummary = "来源举报聊天快照",
                    TargetUserId = 10021,
                    TargetUserName = "alpha-user",
                    ReporterUserId = 10001,
                    ReporterUserName = "reporter",
                    ReasonType = "Abuse",
                    Status = (int)ContentReportStatusEnum.Approved,
                    ReviewActionType = (int)ModerationActionTypeEnum.Mute,
                    CreateTime = new DateTime(2026, 5, 11, 8, 0, 0)
                }
            });
        channelMessageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 93001)))
            .ReturnsAsync(new List<ChannelMessage>
            {
                new()
                {
                    Id = 93001,
                    ChannelId = 208,
                    UserId = 10021,
                    UserName = "alpha-user",
                    Type = MessageType.Text,
                    Content = "当前聊天内容",
                    IsDeleted = false,
                    CreateTime = new DateTime(2026, 5, 11, 7, 59, 0)
                }
            });

        var result = await service.GetActionLogsAsync(new ContentModerationActionLogQueryDto
        {
            PageIndex = 1,
            PageSize = 20,
            TargetUserId = 10021,
            SourceReportId = 73001,
            ActionType = "Mute",
            IsActive = true,
            Keyword = "alpha"
        });

        result.VoTotal.ShouldBe(1);
        result.VoItems.Count.ShouldBe(1);
        result.VoItems[0].VoActionId.ShouldBe(82001);
        result.VoItems[0].VoActionType.ShouldBe("Mute");
        result.VoItems[0].VoTargetUserId.ShouldBe(10021);
        result.VoItems[0].VoSourceReportId.ShouldBe(73001);
        result.VoItems[0].VoIsActive.ShouldBeTrue();
    }

    private static ContentModerationService CreateService(
        Mock<IBaseRepository<ContentReport>>? contentReportRepository = null,
        Mock<IBaseRepository<UserModerationAction>>? moderationActionRepository = null,
        Mock<IBaseRepository<Post>>? postRepository = null,
        Mock<IBaseRepository<Comment>>? commentRepository = null,
        Mock<IChannelMessageRepository>? channelMessageRepository = null,
        Mock<IBaseRepository<PostQuickReply>>? postQuickReplyRepository = null,
        Mock<IContentModerationCaseRepository>? moderationCaseRepository = null)
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var basePostRepository = postRepository ?? new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var baseCommentRepository = commentRepository ?? new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        var productRepository = new Mock<IBaseRepository<Product>>(MockBehavior.Strict);
        var basePostQuickReplyRepository = postQuickReplyRepository ?? new Mock<IBaseRepository<PostQuickReply>>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);

        return new ContentModerationService(
            mapper.Object,
            (contentReportRepository ?? new Mock<IBaseRepository<ContentReport>>(MockBehavior.Strict)).Object,
            (moderationActionRepository ?? new Mock<IBaseRepository<UserModerationAction>>(MockBehavior.Strict)).Object,
            basePostRepository.Object,
            baseCommentRepository.Object,
            (channelMessageRepository ?? new Mock<IChannelMessageRepository>(MockBehavior.Strict)).Object,
            productRepository.Object,
            basePostQuickReplyRepository.Object,
            userRepository.Object,
            moderationCaseRepository: moderationCaseRepository?.Object);
    }
}
