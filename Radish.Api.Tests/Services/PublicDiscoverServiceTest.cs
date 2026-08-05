using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class PublicDiscoverServiceTest
{
    private static readonly DateTime NowUtc = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetFeedAsync_ShouldMergeByStableKeyAndContinueWithOpaqueCursor()
    {
        var repository = new Mock<IPublicDiscoverRepository>(MockBehavior.Strict);
        var channelRepository = new Mock<IPublicDiscoverChannelRepository>(MockBehavior.Strict);
        PublicDiscoverSourceWindow? capturedWindow = null;
        var channel = CreateChannel(10, NowUtc.AddHours(-1));
        channel.Title = "<strong>公共频道</strong>";
        var member = CreateMemberActivity(20, NowUtc.AddHours(-1));
        var highlight = CreateHighlight(30, NowUtc.AddHours(-2));
        channelRepository
            .Setup(candidate => candidate.QueryChannelSummariesAsync(
                It.IsAny<PublicDiscoverSourceWindow>(),
                It.IsAny<DateTime>()))
            .Callback<PublicDiscoverSourceWindow, DateTime>((window, _) => capturedWindow = window)
            .ReturnsAsync([channel]);
        repository
            .Setup(candidate => candidate.QueryMemberActivitiesAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([member]);
        repository
            .Setup(candidate => candidate.QueryHighlightedCommentsAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([highlight]);
        repository
            .Setup(candidate => candidate.QueryPostsAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([]);
        repository
            .Setup(candidate => candidate.QueryQuestionsAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([]);
        channelRepository
            .Setup(candidate => candidate.QueryPulseAsync(0, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new PublicDiscoverChannelPulseCounts(4, 2));
        repository
            .Setup(candidate => candidate.QueryPulseAsync(0, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new PublicDiscoverMainPulseCounts(7, 3));
        var service = CreateService(repository, channelRepository);

        var first = await service.GetFeedAsync(null, 2);

        Assert.Equal(2, first.VoItems.Count);
        Assert.Equal(PublicDiscoverItemKind.ChannelSummary, first.VoItems[0].VoKind);
        Assert.Equal("公共频道", first.VoItems[0].VoTitle);
        Assert.Equal(PublicDiscoverItemKind.MemberActivity, first.VoItems[1].VoKind);
        Assert.True(first.VoHasMore);
        Assert.False(string.IsNullOrWhiteSpace(first.VoNextCursor));
        Assert.Equal(4, first.VoPulse.VoDiscoverableChannelCount);
        Assert.Equal(9, first.VoPulse.VoEligibleItemCount);
        Assert.Equal(3, first.VoPulse.VoKnowledgeContributionCount);

        var second = await service.GetFeedAsync(first.VoNextCursor, 2);

        Assert.Single(second.VoItems);
        Assert.Equal(PublicDiscoverItemKind.HighlightedComment, second.VoItems[0].VoKind);
        Assert.False(second.VoHasMore);
        Assert.Null(second.VoNextCursor);
        Assert.NotNull(capturedWindow);
        Assert.Equal(NowUtc, capturedWindow!.SnapshotCutoffUtc);
        Assert.Equal(NowUtc.AddHours(-1), capturedWindow.LastOccurredAtUtc);
        Assert.Equal(PublicDiscoverKindOrder.MemberActivity, capturedWindow.LastKindOrder);
        Assert.Equal(20, capturedWindow.LastSourceId);
        repository.VerifyAll();
        channelRepository.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetFeedAsync_ShouldRejectOutOfRangePageSize(int pageSize)
    {
        var repository = new Mock<IPublicDiscoverRepository>(MockBehavior.Strict);
        var channelRepository = new Mock<IPublicDiscoverChannelRepository>(MockBehavior.Strict);
        var service = CreateService(repository, channelRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetFeedAsync(null, pageSize));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("PublicDiscover.PageSizeInvalid", exception.ErrorCode);
        repository.VerifyNoOtherCalls();
        channelRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetFeedAsync_ShouldRejectMalformedCursorBeforeQueryingSources()
    {
        var repository = new Mock<IPublicDiscoverRepository>(MockBehavior.Strict);
        var channelRepository = new Mock<IPublicDiscoverChannelRepository>(MockBehavior.Strict);
        var service = CreateService(repository, channelRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetFeedAsync("not-a-cursor", 20));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("PublicDiscover.CursorInvalid", exception.ErrorCode);
        repository.VerifyNoOtherCalls();
        channelRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetFeedAsync_ShouldFailWholePageWhenAnySourceIsUnavailable()
    {
        var repository = new Mock<IPublicDiscoverRepository>(MockBehavior.Strict);
        var channelRepository = new Mock<IPublicDiscoverChannelRepository>(MockBehavior.Strict);
        channelRepository
            .Setup(candidate => candidate.QueryChannelSummariesAsync(
                It.IsAny<PublicDiscoverSourceWindow>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        repository
            .Setup(candidate => candidate.QueryMemberActivitiesAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        repository
            .Setup(candidate => candidate.QueryHighlightedCommentsAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([]);
        repository
            .Setup(candidate => candidate.QueryPostsAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([]);
        repository
            .Setup(candidate => candidate.QueryQuestionsAsync(It.IsAny<PublicDiscoverSourceWindow>()))
            .ReturnsAsync([]);
        channelRepository
            .Setup(candidate => candidate.QueryPulseAsync(0, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new PublicDiscoverChannelPulseCounts(0, 0));
        repository
            .Setup(candidate => candidate.QueryPulseAsync(0, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new PublicDiscoverMainPulseCounts(0, 0));
        var service = CreateService(repository, channelRepository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            service.GetFeedAsync(null, 20));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal("PublicDiscover.SourceUnavailable", exception.ErrorCode);
        repository.VerifyAll();
        channelRepository.VerifyAll();
    }

    private static PublicDiscoverService CreateService(
        Mock<IPublicDiscoverRepository> repository,
        Mock<IPublicDiscoverChannelRepository> channelRepository) => new(
        repository.Object,
        channelRepository.Object,
        new FixedTimeProvider(NowUtc));

    private static PublicDiscoverSourceProjection CreateChannel(long id, DateTime occurredAtUtc) => new()
    {
        SourceId = id,
        Kind = PublicDiscoverItemKind.ChannelSummary,
        OccurredAtUtc = occurredAtUtc,
        Title = "公共频道",
        Summary = "频道摘要",
        TargetKind = PublicDiscoverTargetKind.Messages,
        ChannelId = 7001,
        RequiresAuthentication = true,
        MetricKind = PublicDiscoverMetricKind.RecentReplies,
        MetricValue = 12
    };

    private static PublicDiscoverSourceProjection CreateMemberActivity(long id, DateTime occurredAtUtc) => new()
    {
        SourceId = id,
        Kind = PublicDiscoverItemKind.MemberActivity,
        OccurredAtUtc = occurredAtUtc,
        Title = "公开知识页",
        Summary = "文档摘要",
        ActorPublicId = "usr_00000000000000000000000000000001",
        ActorDisplayName = "青禾",
        TargetKind = PublicDiscoverTargetKind.Docs,
        DocumentSlug = "public-knowledge",
        RequiresAuthentication = false
    };

    private static PublicDiscoverSourceProjection CreateHighlight(long id, DateTime occurredAtUtc) => new()
    {
        SourceId = id,
        Kind = PublicDiscoverItemKind.HighlightedComment,
        OccurredAtUtc = occurredAtUtc,
        Title = "公开讨论",
        Summary = "**有价值的评论**",
        ActorPublicId = "usr_00000000000000000000000000000002",
        ActorDisplayName = "远山",
        TargetKind = PublicDiscoverTargetKind.ForumPost,
        PostPublicId = "pst_00000000000000000000000000000001",
        CommentId = 8001,
        MetricKind = PublicDiscoverMetricKind.Likes,
        MetricValue = 8
    };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
