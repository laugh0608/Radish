using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class NotificationUserBlockPolicyTest
{
    private static readonly DateTime NowUtc =
        new(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateNotificationAsync_ShouldSuppressRelationshipTypeBeforeMessageWrite()
    {
        var fixture = CreateFixture();
        fixture.Policy
            .Setup(item => item.ExcludeInteractionBarriersAsync(9, 2002, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync([]);
        var notificationId = 9007199254740993;

        var result = await fixture.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = notificationId,
            TenantId = 9,
            Type = NotificationType.Followed,
            Title = "有人关注了你",
            TriggerId = 2002,
            ReceiverUserIds = [1001],
            TargetKind = NotificationTargetKind.UserProfile,
            Target = new NotificationTargetData { UserId = 2002 },
            TemplateArguments = new Dictionary<string, string?> { ["actorName"] = "bob" },
            OccurredAtUtc = NowUtc
        });

        Assert.Equal(notificationId, result);
        fixture.Repository.Verify(
            item => item.PersistAsync(
                It.IsAny<Notification>(),
                It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(),
                It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateNotificationAsync_ShouldNotSuppressGovernanceType()
    {
        var fixture = CreateFixture();
        fixture.Users
            .Setup(item => item.GetActiveUserIdsAsync(9, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync([1001]);
        fixture.Repository
            .Setup(item => item.GetPreferencesAsync(9, 1001))
            .ReturnsAsync(new Dictionary<string, NotificationSetting>());
        fixture.Repository
            .Setup(item => item.PersistAsync(
                It.IsAny<Notification>(),
                It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((Notification notification, IReadOnlyList<NotificationInboxRecipient> _, DateTime _) =>
                new NotificationInboxPersistResult(notification.Id, false, []));

        var result = await fixture.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = 7001,
            TenantId = 9,
            Type = NotificationType.ContentModerationDecisionAvailable,
            Title = "治理决定",
            TriggerId = 2002,
            ReceiverUserIds = [1001],
            TargetKind = NotificationTargetKind.GovernanceDecision,
            Target = new NotificationTargetData { GovernanceCasePublicId = "case_public" },
            TemplateArguments = new Dictionary<string, string?> { ["resultCode"] = "Restricted" },
            OccurredAtUtc = NowUtc
        });

        Assert.Equal(7001, result);
        fixture.Policy.Verify(
            item => item.ExcludeInteractionBarriersAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<IReadOnlyCollection<long>>()),
            Times.Never);
        fixture.Repository.Verify(
            item => item.PersistAsync(
                It.Is<Notification>(notification =>
                    notification.Type == NotificationType.ContentModerationDecisionAvailable),
                It.IsAny<IReadOnlyList<NotificationInboxRecipient>>(),
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public void Registry_ShouldExplicitlyClassifyRelationshipAndProtectedNotifications()
    {
        Assert.True(NotificationDefinitionRegistry
            .GetRequired(NotificationType.PostLiked)
            .SuppressWhenInteractionBlocked);
        Assert.True(NotificationDefinitionRegistry
            .GetRequired(NotificationType.DirectMessageRequested)
            .SuppressWhenInteractionBlocked);
        Assert.False(NotificationDefinitionRegistry
            .GetRequired(NotificationType.AccountSecurity)
            .SuppressWhenInteractionBlocked);
        Assert.False(NotificationDefinitionRegistry
            .GetRequired(NotificationType.ContentModerationAppealUpdated)
            .SuppressWhenInteractionBlocked);
        Assert.False(NotificationDefinitionRegistry
            .GetRequired(NotificationType.PurchaseSucceeded)
            .SuppressWhenInteractionBlocked);
    }

    private static Fixture CreateFixture()
    {
        var repository = new Mock<INotificationInboxRepository>(MockBehavior.Strict);
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        var policy = new Mock<IUserInteractionPolicyService>(MockBehavior.Strict);
        var service = new NotificationService(
            repository.Object,
            users.Object,
            Mock.Of<INotificationTargetResolver>(),
            Mock.Of<INotificationPushService>(),
            policy.Object,
            new FixedTimeProvider(new DateTimeOffset(NowUtc)),
            NullLogger<NotificationService>.Instance);
        return new Fixture(service, repository, users, policy);
    }

    private sealed record Fixture(
        NotificationService Service,
        Mock<INotificationInboxRepository> Repository,
        Mock<IUserRepository> Users,
        Mock<IUserInteractionPolicyService> Policy);

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
