using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class NotificationServiceTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact(DisplayName = "通知写入应使用注册定义并只推 revision 变化")]
    public async Task CreateNotificationAsync_ShouldPersistDefinitionAndPushRevision()
    {
        var harness = CreateHarness();
        harness.Repository
            .Setup(repository => repository.GetPreferencesAsync(9, 1001))
            .ReturnsAsync(new Dictionary<string, NotificationSetting>());
        harness.Repository
            .Setup(repository => repository.PersistAsync(
                It.Is<Notification>(notification =>
                    notification.Id == 7001 &&
                    notification.Type == NotificationType.SystemAnnouncement &&
                    notification.Category == NotificationCategory.System &&
                    notification.TemplateKey == "notification.SystemAnnouncement" &&
                    notification.TargetKind == NotificationTargetKind.None &&
                    notification.OccurredAtUtc == NowUtc),
                It.Is<IReadOnlyList<NotificationInboxRecipient>>(recipients =>
                    recipients.Count == 1 && recipients[0].UserId == 1001),
                NowUtc))
            .ReturnsAsync(new NotificationInboxPersistResult(
                7001,
                true,
                [new NotificationInboxRecipientChange(1001, 8001, true, Summary(3, 2, 4))]));
        harness.PushService
            .Setup(service => service.PushInboxChangedAsync(
                1001,
                It.Is<NotificationInboxChangedVo>(change =>
                    change.VoRevision == 3 &&
                    change.VoUnreadGroupCount == 2 &&
                    change.VoLatestGroupId == 8001 &&
                    change.VoReason == "Created")))
            .Returns(Task.CompletedTask);

        var notificationId = await harness.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = 7001,
            BusinessKey = "notification:test:7001",
            Type = NotificationType.SystemAnnouncement,
            Title = "测试通知",
            ReceiverUserIds = [1001],
            TenantId = 9,
            OccurredAtUtc = NowUtc
        });

        notificationId.ShouldBe(7001);
        harness.Repository.VerifyAll();
        harness.UserRepository.VerifyAll();
        harness.PushService.VerifyAll();
    }

    [Fact(DisplayName = "关闭普通分类后不应创建空通知事件")]
    public async Task CreateNotificationAsync_ShouldSuppressDisabledCategory()
    {
        var harness = CreateHarness();
        harness.Repository
            .Setup(repository => repository.GetPreferencesAsync(9, 1001))
            .ReturnsAsync(new Dictionary<string, NotificationSetting>
            {
                [NotificationCategory.Reaction] = new()
                {
                    Category = NotificationCategory.Reaction,
                    InAppEnabled = false,
                    RealtimePreviewEnabled = false
                }
            });

        var notificationId = await harness.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = 7002,
            BusinessKey = "notification:like:7002",
            Type = NotificationType.PostLiked,
            Title = "帖子被点赞",
            ReceiverUserIds = [1001],
            TenantId = 9,
            OccurredAtUtc = NowUtc,
            TemplateArguments = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["targetTitle"] = "测试帖子"
            },
            TargetKind = NotificationTargetKind.ForumPost,
            Target = new NotificationTargetData { PostId = 5001 }
        });

        notificationId.ShouldBe(7002);
        harness.Repository.VerifyAll();
        harness.UserRepository.VerifyAll();
        harness.PushService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "注册模板缺少必填参数应拒绝")]
    public async Task CreateNotificationAsync_ShouldRejectMissingTemplateArgument()
    {
        var harness = CreateHarness();

        await Should.ThrowAsync<ArgumentException>(() => harness.Service.CreateNotificationAsync(
            new CreateNotificationDto
            {
                NotificationId = 7010,
                Type = NotificationType.PostLiked,
                Title = "帖子被点赞",
                ReceiverUserIds = [1001],
                TenantId = 9,
                OccurredAtUtc = NowUtc,
                TargetKind = NotificationTargetKind.ForumPost,
                Target = new NotificationTargetData { PostId = 5001 }
            }));

        harness.Repository.VerifyNoOtherCalls();
        harness.UserRepository.VerifyNoOtherCalls();
        harness.PushService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "账号安全通知必须绕过关闭入箱偏好")]
    public async Task CreateNotificationAsync_ShouldKeepMandatoryAccountSecurityInbox()
    {
        var harness = CreateHarness();
        harness.Repository
            .Setup(repository => repository.GetPreferencesAsync(9, 1001))
            .ReturnsAsync(new Dictionary<string, NotificationSetting>
            {
                [NotificationCategory.System] = new()
                {
                    Category = NotificationCategory.System,
                    InAppEnabled = false,
                    RealtimePreviewEnabled = false
                }
            });
        harness.Repository
            .Setup(repository => repository.PersistAsync(
                It.IsAny<Notification>(),
                It.Is<IReadOnlyList<NotificationInboxRecipient>>(recipients => recipients.Count == 1),
                NowUtc))
            .ReturnsAsync(new NotificationInboxPersistResult(7003, true, []));

        await harness.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = 7003,
            BusinessKey = "notification:security:7003",
            Type = NotificationType.AccountSecurity,
            Title = "账号安全",
            ReceiverUserIds = [1001],
            TenantId = 9,
            OccurredAtUtc = NowUtc
        });

        harness.Repository.VerifyAll();
        harness.UserRepository.VerifyAll();
        harness.PushService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "可靠通知缺少固定发生时间应拒绝")]
    public async Task CreateNotificationAsync_ShouldRejectReliableEventWithoutOccurredAt()
    {
        var harness = CreateHarness();

        await Should.ThrowAsync<ArgumentException>(() => harness.Service.CreateNotificationAsync(
            new CreateNotificationDto
            {
                NotificationId = 7004,
                Type = NotificationType.SystemAnnouncement,
                Title = "测试通知",
                ReceiverUserIds = [1001]
            }));

        harness.Repository.VerifyNoOtherCalls();
        harness.UserRepository.VerifyNoOtherCalls();
        harness.PushService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "跨租户或不可用接收者应拒绝入箱")]
    public async Task CreateNotificationAsync_ShouldRejectInvalidRecipientScope()
    {
        var harness = CreateHarness(recipientValid: false);

        await Should.ThrowAsync<ArgumentException>(() => harness.Service.CreateNotificationAsync(
            new CreateNotificationDto
            {
                NotificationId = 7011,
                Type = NotificationType.SystemAnnouncement,
                Title = "测试通知",
                ReceiverUserIds = [1001],
                TenantId = 9,
                OccurredAtUtc = NowUtc
            }));

        harness.UserRepository.VerifyAll();
        harness.Repository.VerifyNoOtherCalls();
        harness.PushService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "列表 revision 变化后旧 cursor 应稳定过期")]
    public async Task GetInboxAsync_ShouldRejectCursorAfterRevisionChanged()
    {
        var harness = CreateHarness();
        var notification = CreateNotification(7005, NotificationType.PostLiked, NotificationCategory.Reaction);
        var group = new NotificationInboxGroup
        {
            Id = 8005,
            TenantId = 9,
            UserId = 1001,
            Category = NotificationCategory.Reaction,
            Kind = NotificationType.PostLiked,
            LatestNotificationId = 7005,
            OccurrenceCount = 2,
            UnreadOccurrenceCount = 2,
            FirstOccurredAtUtc = NowUtc.AddMinutes(-2),
            LastOccurredAtUtc = NowUtc
        };
        harness.Repository
            .SetupSequence(repository => repository.GetSummaryAsync(9, 1001))
            .ReturnsAsync(Summary(1, 1, 2))
            .ReturnsAsync(Summary(2, 2, 3));
        harness.Repository
            .Setup(repository => repository.QueryAsync(9, 1001, null, false, null, null, 0, 20))
            .ReturnsAsync(new NotificationInboxQueryResult(
                [new NotificationInboxGroupSnapshot(group, notification)],
                true,
                1,
                Summary(1, 1, 2)));

        var firstPage = await harness.Service.GetInboxAsync(
            9,
            1001,
            new NotificationInboxQueryDto());
        firstPage.VoNextCursor.ShouldNotBeNullOrWhiteSpace();

        var exception = await Should.ThrowAsync<BusinessException>(() => harness.Service.GetInboxAsync(
            9,
            1001,
            new NotificationInboxQueryDto { Cursor = firstPage.VoNextCursor }));
        exception.ErrorCode.ShouldBe("Notification.CursorExpired");
        exception.StatusCode.ShouldBe(409);
        harness.Repository.VerifyAll();
    }

    [Fact(DisplayName = "全部已读应返回并推送同一权威摘要")]
    public async Task MarkAllInboxAsReadAsync_ShouldPushAuthoritativeSummary()
    {
        var harness = CreateHarness();
        harness.Repository
            .Setup(repository => repository.MarkAllAsReadAsync(9, 1001, null, NowUtc))
            .ReturnsAsync(new NotificationInboxMutationResult(3, Summary(5, 0, 0)));
        harness.PushService
            .Setup(service => service.PushInboxChangedAsync(
                1001,
                It.Is<NotificationInboxChangedVo>(change =>
                    change.VoRevision == 5 &&
                    change.VoUnreadGroupCount == 0 &&
                    change.VoReason == "ReadAll")))
            .Returns(Task.CompletedTask);

        var result = await harness.Service.MarkAllInboxAsReadAsync(9, 1001);

        result.VoAffectedRows.ShouldBe(3);
        result.VoSummary.VoRevision.ShouldBe(5);
        harness.Repository.VerifyAll();
        harness.PushService.VerifyAll();
    }

    private static NotificationServiceHarness CreateHarness(bool recipientValid = true)
    {
        var repository = new Mock<INotificationInboxRepository>(MockBehavior.Strict);
        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        var pushService = new Mock<INotificationPushService>(MockBehavior.Strict);
        userRepository
            .Setup(item => item.GetActiveUserIdsAsync(
                It.IsAny<long>(),
                It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync((long _, IReadOnlyCollection<long> userIds) =>
                recipientValid ? userIds.ToList() : []);
        var service = new NotificationService(
            repository.Object,
            userRepository.Object,
            pushService.Object,
            new FixedTimeProvider(new DateTimeOffset(NowUtc)),
            Mock.Of<ILogger<NotificationService>>());
        return new NotificationServiceHarness(service, repository, userRepository, pushService);
    }

    private static NotificationInboxSummarySnapshot Summary(
        long revision,
        long unreadGroups,
        long unreadOccurrences)
    {
        return new NotificationInboxSummarySnapshot(
            revision,
            unreadGroups,
            unreadOccurrences,
            new Dictionary<string, long>(),
            NowUtc);
    }

    private static Notification CreateNotification(long id, string kind, string category)
    {
        return new Notification(new NotificationInitializationOptions(kind, "测试通知")
        {
            Category = category,
            TemplateKey = $"notification.{kind}",
            TargetKind = NotificationTargetKind.ForumPost,
            TargetDataJson = new NotificationTargetData { PostId = 5001 }.ToJson(),
            OccurredAtUtc = NowUtc,
            TenantId = 9
        })
        {
            Id = id,
            BusinessKey = $"notification:test:{id}",
            CreateTime = NowUtc
        };
    }

    private sealed record NotificationServiceHarness(
        NotificationService Service,
        Mock<INotificationInboxRepository> Repository,
        Mock<IUserRepository> UserRepository,
        Mock<INotificationPushService> PushService);

    private sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => nowUtc;
    }
}
