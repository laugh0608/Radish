using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Services;
using Radish.IService;
using Radish.Model;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ReliableOutboxJobTest
{
    [Theory]
    [InlineData(ReliableOutboxStatuses.Pending)]
    [InlineData(ReliableOutboxStatuses.Succeeded)]
    [InlineData(ReliableOutboxStatuses.DeadLetter)]
    public async Task ExecuteAsync_ShouldIgnoreMessageOutsideProcessingState(string status)
    {
        var outboxService = new Mock<IReliableOutboxService>(MockBehavior.Strict);
        outboxService
            .Setup(item => item.QueryByIdAsync(ReliableOutboxSources.Main, 10))
            .ReturnsAsync(CreateSnapshot(status));
        var processor = new Mock<IReliableTaskProcessor>(MockBehavior.Strict);
        var job = new ReliableOutboxExecutionJob(
            outboxService.Object,
            processor.Object,
            Mock.Of<ILogger<ReliableOutboxExecutionJob>>());

        await job.ExecuteAsync(ReliableOutboxSources.Main, 10, CancellationToken.None);

        outboxService.VerifyAll();
        processor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessClaimedMessageAndMarkSucceeded()
    {
        var completedAtUtc = DateTime.UtcNow;
        var message = CreateSnapshot(ReliableOutboxStatuses.Processing);
        var outboxService = new Mock<IReliableOutboxService>(MockBehavior.Strict);
        outboxService
            .Setup(item => item.QueryByIdAsync(ReliableOutboxSources.Main, 10))
            .ReturnsAsync(message);
        outboxService
            .Setup(item => item.MarkSucceededAsync(
                ReliableOutboxSources.Main,
                10,
                It.Is<DateTime>(value => value >= completedAtUtc)))
            .Returns(Task.CompletedTask);
        var processor = new Mock<IReliableTaskProcessor>(MockBehavior.Strict);
        processor
            .Setup(item => item.ProcessAsync(message, CancellationToken.None))
            .Returns(Task.CompletedTask);
        var job = new ReliableOutboxExecutionJob(
            outboxService.Object,
            processor.Object,
            Mock.Of<ILogger<ReliableOutboxExecutionJob>>());

        await job.ExecuteAsync(ReliableOutboxSources.Main, 10, CancellationToken.None);

        outboxService.VerifyAll();
        processor.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMarkFailedWhenProcessorThrows()
    {
        var message = CreateSnapshot(ReliableOutboxStatuses.Processing);
        var failure = new InvalidOperationException("通知持久化失败");
        var outboxService = new Mock<IReliableOutboxService>(MockBehavior.Strict);
        outboxService
            .Setup(item => item.QueryByIdAsync(ReliableOutboxSources.Main, 10))
            .ReturnsAsync(message);
        outboxService
            .Setup(item => item.MarkFailedAsync(
                ReliableOutboxSources.Main,
                10,
                failure,
                It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        var processor = new Mock<IReliableTaskProcessor>(MockBehavior.Strict);
        processor
            .Setup(item => item.ProcessAsync(message, CancellationToken.None))
            .ThrowsAsync(failure);
        var job = new ReliableOutboxExecutionJob(
            outboxService.Object,
            processor.Object,
            Mock.Of<ILogger<ReliableOutboxExecutionJob>>());

        await job.ExecuteAsync(ReliableOutboxSources.Main, 10, CancellationToken.None);

        outboxService.VerifyAll();
        processor.VerifyAll();
    }

    private static ReliableOutboxSnapshot CreateSnapshot(string status)
    {
        return new ReliableOutboxSnapshot(
            ReliableOutboxSources.Main,
            10,
            0,
            ReliableTaskTypes.PostPublished,
            1,
            "task:post-published:42",
            "Post",
            "42",
            "{}",
            status,
            0,
            6,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null);
    }
}
