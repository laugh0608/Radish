using System;
using System.Linq.Expressions;
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

public sealed class ReliableTaskProcessorContentModerationTest
{
    private static readonly DateTime OccurredAtUtc =
        new(2026, 7, 25, 2, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessAsync_ShouldPreserveChatRecallExceptionForOutboxLifecycle()
    {
        var expectedException = new InvalidOperationException("Chat database unavailable");
        var channelMessageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        channelMessageRepository
            .Setup(repository => repository.QueryFirstIncludingDeletedAsync(
                It.IsAny<Expression<Func<ChannelMessage, bool>>>()))
            .ThrowsAsync(expectedException);
        var moderationCaseRepository = new Mock<IContentModerationCaseRepository>(MockBehavior.Strict);
        var processor = CreateProcessor(
            channelMessageRepository.Object,
            moderationCaseRepository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(
                CreateSnapshot(),
                TestContext.Current.CancellationToken));

        Assert.Same(expectedException, exception);
        channelMessageRepository.VerifyAll();
        moderationCaseRepository.VerifyNoOtherCalls();
    }

    private static ReliableTaskProcessor CreateProcessor(
        IChannelMessageRepository channelMessageRepository,
        IContentModerationCaseRepository contentModerationCaseRepository)
    {
        return new ReliableTaskProcessor(
            Mock.Of<ICoinRewardService>(),
            Mock.Of<ICoinService>(),
            Mock.Of<IExperienceService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IChatAttachmentBindingService>(),
            Mock.Of<IBaseRepository<Post>>(),
            Mock.Of<IBaseRepository<Comment>>(),
            channelMessageRepository,
            contentModerationCaseRepository);
    }

    private static ReliableOutboxSnapshot CreateSnapshot()
    {
        var payload = new ContentModerationChatRecallTaskPayload(
            9,
            7001,
            7501,
            8001,
            "moderation-chat-recall:test:7001",
            9001,
            "reviewer");
        return new ReliableOutboxSnapshot(
            ReliableOutboxSources.Main,
            10,
            9,
            ReliableTaskTypes.ContentModerationChatRecall,
            1,
            "moderation-chat-recall:test:7001",
            "ContentModerationCase",
            "7001",
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
