using System;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ReliableTaskProcessorNotificationTest
{
    private static readonly DateTime OccurredAtUtc =
        new(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_ShouldUseOutboxTenantAndOccurrenceAsSourceOfTruth()
    {
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        notificationService
            .Setup(service => service.CreateNotificationAsync(It.Is<CreateNotificationDto>(dto =>
                dto.TenantId == 9 &&
                dto.OccurredAtUtc == OccurredAtUtc &&
                dto.Type == NotificationType.SystemAnnouncement)))
            .ReturnsAsync(7001);
        var processor = CreateProcessor(notificationService.Object);

        await processor.ProcessAsync(
            CreateSnapshot(CreateDto()),
            TestContext.Current.CancellationToken);

        notificationService.VerifyAll();
    }

    [Fact]
    public async Task ProcessAsync_ShouldConvertInvalidNotificationContractToPermanentFailure()
    {
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        notificationService
            .Setup(service => service.CreateNotificationAsync(It.IsAny<CreateNotificationDto>()))
            .ThrowsAsync(new ArgumentException("未登记的通知类型"));
        var processor = CreateProcessor(notificationService.Object);

        var exception = await Assert.ThrowsAsync<PermanentReliableTaskException>(() =>
            processor.ProcessAsync(
                CreateSnapshot(CreateDto()),
                TestContext.Current.CancellationToken));

        Assert.Contains("通知可靠任务契约无效", exception.Message, StringComparison.Ordinal);
        notificationService.VerifyAll();
    }

    private static ReliableTaskProcessor CreateProcessor(INotificationService notificationService)
    {
        return new ReliableTaskProcessor(
            Mock.Of<ICoinRewardService>(),
            Mock.Of<ICoinService>(),
            Mock.Of<IExperienceService>(),
            notificationService,
            Mock.Of<IChatAttachmentBindingService>(),
            Mock.Of<IBaseRepository<Post>>(),
            Mock.Of<IBaseRepository<Comment>>());
    }

    private static ReliableOutboxSnapshot CreateSnapshot(CreateNotificationDto notification)
    {
        return new ReliableOutboxSnapshot(
            ReliableOutboxSources.Main,
            10,
            9,
            ReliableTaskTypes.NotificationRequested,
            1,
            "task:notification:test:7001",
            "Notification",
            "7001",
            JsonSerializer.Serialize(new NotificationRequestedTaskPayload(notification)),
            ReliableOutboxStatuses.Processing,
            1,
            6,
            OccurredAtUtc,
            OccurredAtUtc,
            null,
            null);
    }

    private static CreateNotificationDto CreateDto()
    {
        return new CreateNotificationDto
        {
            NotificationId = 7001,
            Type = NotificationType.SystemAnnouncement,
            Title = "测试通知",
            ReceiverUserIds = [1001],
            TenantId = 99,
            OccurredAtUtc = OccurredAtUtc.AddDays(-1)
        };
    }
}
