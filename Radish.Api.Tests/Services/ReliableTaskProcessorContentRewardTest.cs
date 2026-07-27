using System;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ReliableTaskProcessorContentRewardTest
{
    private static readonly DateTime OccurredAtUtc =
        new(2026, 7, 27, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_ShouldProjectBothBalanceEntries()
    {
        var projectionRepository =
            new Mock<IContentRewardAuditProjectionRepository>(MockBehavior.Strict);
        projectionRepository
            .Setup(repository => repository.ProjectAsync(
                It.Is<ContentRewardAuditProjectionCommand>(command =>
                    command.TenantId == 9 &&
                    command.CoinTransactionId == 8101 &&
                    command.SenderEntry.UserId == 1001 &&
                    command.SenderEntry.ChangeAmount == -1 &&
                    command.RecipientEntry.UserId == 2002 &&
                    command.RecipientEntry.ChangeAmount == 1)))
            .Returns(Task.CompletedTask);
        var processor = CreateProcessor(projectionRepository.Object);

        await processor.ProcessAsync(CreateSnapshot(), TestContext.Current.CancellationToken);

        projectionRepository.VerifyAll();
    }

    [Fact]
    public async Task ProcessAsync_ShouldConvertProjectionPayloadDriftToPermanentFailure()
    {
        var projectionRepository =
            new Mock<IContentRewardAuditProjectionRepository>(MockBehavior.Strict);
        projectionRepository
            .Setup(repository => repository.ProjectAsync(
                It.IsAny<ContentRewardAuditProjectionCommand>()))
            .ThrowsAsync(new ContentRewardAuditProjectionConflictException());
        var processor = CreateProcessor(projectionRepository.Object);

        var exception = await Assert.ThrowsAsync<PermanentReliableTaskException>(() =>
            processor.ProcessAsync(CreateSnapshot(), TestContext.Current.CancellationToken));

        Assert.Contains("载荷不一致", exception.Message, StringComparison.Ordinal);
        projectionRepository.VerifyAll();
    }

    private static ReliableTaskProcessor CreateProcessor(
        IContentRewardAuditProjectionRepository projectionRepository)
    {
        return new ReliableTaskProcessor(
            Mock.Of<ICoinRewardService>(),
            Mock.Of<ICoinService>(),
            Mock.Of<IExperienceService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IChatAttachmentBindingService>(),
            Mock.Of<IBaseRepository<Post>>(),
            Mock.Of<IBaseRepository<Comment>>(),
            contentRewardAuditProjectionRepository: projectionRepository);
    }

    private static ReliableOutboxSnapshot CreateSnapshot()
    {
        var payload = new ContentRewardAuditProjectionTaskPayload(
            9,
            8001,
            9001,
            8101,
            OccurredAtUtc,
            "alice",
            1001,
            new ContentRewardAuditProjectionEntryPayload(
                1001,
                -1,
                3,
                2,
                "TRANSFER_OUT",
                "content-reward:8101:1001:out"),
            new ContentRewardAuditProjectionEntryPayload(
                2002,
                1,
                0,
                1,
                "TRANSFER_IN",
                "content-reward:8101:2002:in"));
        return new ReliableOutboxSnapshot(
            ReliableOutboxSources.Main,
            10,
            9,
            ReliableTaskTypes.ContentRewardAuditProjection,
            1,
            "task:content-reward:8001:audit",
            BusinessType.ContentReward,
            "8001",
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
