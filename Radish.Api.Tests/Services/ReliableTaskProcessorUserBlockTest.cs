using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ReliableTaskProcessorUserBlockTest
{
    private static readonly DateTime OccurredAtUtc =
        new(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_ShouldSuppressBothDirectionsAndPushMinimalInvalidation()
    {
        var inboxRepository = new Mock<INotificationInboxRepository>(MockBehavior.Strict);
        var pushService = new Mock<INotificationPushService>(MockBehavior.Strict);
        var realtimeNotifier = new Mock<IUserInteractionRealtimeNotifier>(MockBehavior.Strict);
        var summary = new NotificationInboxSummarySnapshot(
            8,
            1,
            1,
            new Dictionary<string, long> { [NotificationCategory.System] = 1 },
            OccurredAtUtc);
        inboxRepository
            .Setup(repository => repository.SuppressBlockedActorsAsync(
                9,
                1001,
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new[] { 2002L })),
                OccurredAtUtc))
            .ReturnsAsync(new NotificationInboxSuppressionResult(
                1,
                [new NotificationInboxRecipientChange(1001, 0, false, summary)]));
        inboxRepository
            .Setup(repository => repository.SuppressBlockedActorsAsync(
                9,
                2002,
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new[] { 1001L })),
                OccurredAtUtc))
            .ReturnsAsync(new NotificationInboxSuppressionResult(0, []));
        pushService
            .Setup(service => service.PushInboxChangedAsync(
                1001,
                It.Is<NotificationInboxChangedVo>(change =>
                    change.VoRevision == 8 &&
                    change.VoReason == "UserBlockSuppressed" &&
                    !change.VoRealtimePreviewAllowed)))
            .Returns(Task.CompletedTask);
        realtimeNotifier
            .Setup(service => service.NotifyRelationshipChangedAsync(1001, 2002, 4))
            .Returns(Task.CompletedTask);
        var processor = CreateProcessor(
            inboxRepository.Object,
            pushService.Object,
            realtimeNotifier.Object);

        await processor.ProcessAsync(
            CreateSnapshot(UserBlockRelationshipEventTypes.Blocked),
            TestContext.Current.CancellationToken);

        inboxRepository.VerifyAll();
        pushService.VerifyAll();
        realtimeNotifier.VerifyAll();
    }

    [Fact]
    public async Task ProcessAsync_ShouldNotRestoreNotificationsWhenUnblocked()
    {
        var inboxRepository = new Mock<INotificationInboxRepository>(MockBehavior.Strict);
        var pushService = new Mock<INotificationPushService>(MockBehavior.Strict);
        var realtimeNotifier = new Mock<IUserInteractionRealtimeNotifier>(MockBehavior.Strict);
        realtimeNotifier
            .Setup(service => service.NotifyRelationshipChangedAsync(1001, 2002, 4))
            .Returns(Task.CompletedTask);
        var processor = CreateProcessor(
            inboxRepository.Object,
            pushService.Object,
            realtimeNotifier.Object);

        await processor.ProcessAsync(
            CreateSnapshot(UserBlockRelationshipEventTypes.Unblocked),
            TestContext.Current.CancellationToken);

        inboxRepository.VerifyNoOtherCalls();
        pushService.VerifyNoOtherCalls();
        realtimeNotifier.VerifyAll();
    }

    [Fact]
    public async Task ProcessAsync_ShouldPropagateSuppressionFailureForOutboxRetry()
    {
        var expected = new InvalidOperationException("Message database unavailable");
        var inboxRepository = new Mock<INotificationInboxRepository>(MockBehavior.Strict);
        inboxRepository
            .Setup(repository => repository.SuppressBlockedActorsAsync(
                9,
                1001,
                It.IsAny<IReadOnlyCollection<long>>(),
                OccurredAtUtc))
            .ThrowsAsync(expected);
        var pushService = new Mock<INotificationPushService>(MockBehavior.Strict);
        var realtimeNotifier = new Mock<IUserInteractionRealtimeNotifier>(MockBehavior.Strict);
        var processor = CreateProcessor(
            inboxRepository.Object,
            pushService.Object,
            realtimeNotifier.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(
                CreateSnapshot(UserBlockRelationshipEventTypes.Blocked),
                TestContext.Current.CancellationToken));

        Assert.Same(expected, exception);
        pushService.VerifyNoOtherCalls();
        realtimeNotifier.VerifyNoOtherCalls();
    }

    private static ReliableTaskProcessor CreateProcessor(
        INotificationInboxRepository inboxRepository,
        INotificationPushService pushService,
        IUserInteractionRealtimeNotifier realtimeNotifier)
    {
        return new ReliableTaskProcessor(
            Mock.Of<ICoinRewardService>(),
            Mock.Of<ICoinService>(),
            Mock.Of<IExperienceService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IChatAttachmentBindingService>(),
            Mock.Of<IBaseRepository<Post>>(),
            Mock.Of<IBaseRepository<Comment>>(),
            notificationInboxRepository: inboxRepository,
            notificationPushService: pushService,
            userInteractionRealtimeNotifier: realtimeNotifier);
    }

    private static ReliableOutboxSnapshot CreateSnapshot(string eventType)
    {
        var payload = new UserBlockRelationshipChangedTaskPayload(
            9,
            3003,
            eventType,
            1001,
            2002,
            4,
            OccurredAtUtc);
        return new ReliableOutboxSnapshot(
            ReliableOutboxSources.Main,
            10,
            9,
            ReliableTaskTypes.UserBlockRelationshipChanged,
            1,
            "task:user-block:3003:version:4",
            nameof(UserBlock),
            "3003",
            JsonSerializer.Serialize(payload),
            ReliableOutboxStatuses.Processing,
            1,
            6,
            OccurredAtUtc,
            OccurredAtUtc,
            null,
            null);
    }
}
