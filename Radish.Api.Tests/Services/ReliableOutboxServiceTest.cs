using System;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ReliableOutboxServiceTest
{
    [Fact]
    public async Task AddAsync_ShouldPersistVersionedPayloadAndStableMetadata()
    {
        ReliableOutboxDraft? captured = null;
        var repository = new Mock<IReliableOutboxRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.AddAsync(It.IsAny<ReliableOutboxDraft>()))
            .Callback<ReliableOutboxDraft>(draft => captured = draft)
            .ReturnsAsync(123L);
        var service = new ReliableOutboxService(repository.Object);
        var occurredAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);

        var outboxId = await service.AddAsync(
            ReliableOutboxSources.Main,
            7,
            ReliableTaskTypes.PostPublished,
            "task:post-published:42",
            "Post",
            "42",
            new PostPublishedTaskPayload(42, 9),
            occurredAtUtc);

        outboxId.ShouldBe(123L);
        captured.ShouldNotBeNull();
        captured.SourceDatabase.ShouldBe(ReliableOutboxSources.Main);
        captured.TenantId.ShouldBe(7);
        captured.SchemaVersion.ShouldBe(1);
        captured.OccurredAtUtc.ShouldBe(occurredAtUtc);
        JsonSerializer.Deserialize<PostPublishedTaskPayload>(captured.PayloadJson)
            .ShouldBe(new PostPublishedTaskPayload(42, 9));
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldScheduleFirstTransientRetryAfterOneMinute()
    {
        var repository = new Mock<IReliableOutboxRepository>(MockBehavior.Strict);
        var failedAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        repository
            .Setup(item => item.QueryByIdAsync(ReliableOutboxSources.Main, 10))
            .ReturnsAsync(CreateSnapshot(attemptCount: 0));
        repository
            .Setup(item => item.MarkFailedAsync(
                ReliableOutboxSources.Main,
                10,
                nameof(TimeoutException),
                "任务执行失败，完整异常请查看服务端日志",
                failedAtUtc,
                failedAtUtc.AddMinutes(1).AddSeconds(10)))
            .Returns(Task.CompletedTask);
        var service = new ReliableOutboxService(repository.Object);

        await service.MarkFailedAsync(
            ReliableOutboxSources.Main,
            10,
            new TimeoutException("temporary"),
            failedAtUtc);

        repository.VerifyAll();
    }

    [Fact]
    public async Task MarkFailedAsync_ShouldDeadLetterPermanentFailureWithoutRetryTime()
    {
        var repository = new Mock<IReliableOutboxRepository>(MockBehavior.Strict);
        var failedAtUtc = new DateTime(2026, 7, 11, 8, 0, 0, DateTimeKind.Utc);
        repository
            .Setup(item => item.QueryByIdAsync(ReliableOutboxSources.Chat, 10))
            .ReturnsAsync(CreateSnapshot(attemptCount: 0, sourceDatabase: ReliableOutboxSources.Chat));
        repository
            .Setup(item => item.MarkFailedAsync(
                ReliableOutboxSources.Chat,
                10,
                "PermanentFailure",
                "invalid payload",
                failedAtUtc,
                null))
            .Returns(Task.CompletedTask);
        var service = new ReliableOutboxService(repository.Object);

        await service.MarkFailedAsync(
            ReliableOutboxSources.Chat,
            10,
            new PermanentReliableTaskException("invalid payload"),
            failedAtUtc);

        repository.VerifyAll();
    }

    private static ReliableOutboxSnapshot CreateSnapshot(
        int attemptCount,
        string sourceDatabase = ReliableOutboxSources.Main)
    {
        return new ReliableOutboxSnapshot(
            sourceDatabase,
            10,
            0,
            ReliableTaskTypes.PostPublished,
            1,
            "task:post-published:42",
            "Post",
            "42",
            "{}",
            ReliableOutboxStatuses.Processing,
            attemptCount,
            6,
            DateTime.UtcNow,
            null,
            null);
    }
}
