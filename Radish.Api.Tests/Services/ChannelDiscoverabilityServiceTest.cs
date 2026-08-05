using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChannelDiscoverabilityServiceTest
{
    private static readonly DateTime NowUtc = new(2026, 8, 5, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateVisibilityAsync_ShouldNormalizeCommandAndReturnAuthoritativeState()
    {
        var repository = new Mock<IChannelDiscoverabilityRepository>(MockBehavior.Strict);
        repository
            .Setup(candidate => candidate.SetVisibilityAsync(It.Is<ChannelDiscoverVisibilityChangeCommand>(command =>
                command.TenantId == 30000 &&
                command.ChannelId == 70001 &&
                command.DiscoverVisibility == ChannelDiscoverVisibility.Summary &&
                command.ExpectedVersion == 0 &&
                command.Reason == "经复核开放摘要" &&
                command.ActorUserId == 20001 &&
                command.ActorName == "Moderator" &&
                command.NowUtc == NowUtc)))
            .ReturnsAsync(new ChannelDiscoverVisibilityWriteResult(
                CreateChannel(ChannelDiscoverVisibility.Summary, version: 1),
                true));
        var service = CreateService(repository);

        var result = await service.UpdateVisibilityAsync(
            30000,
            70001,
            20001,
            " Moderator ",
            new UpdateChannelDiscoverVisibilityDto
            {
                DiscoverVisibility = ChannelDiscoverVisibility.Summary,
                ExpectedVersion = 0,
                Reason = " 经复核开放摘要 "
            });

        Assert.True(result.VoChanged);
        Assert.Equal("70001", result.VoChannel.VoChannelId);
        Assert.Equal(ChannelDiscoverVisibility.Summary, result.VoChannel.VoDiscoverVisibility);
        Assert.Equal(1, result.VoChannel.VoDiscoverVisibilityVersion);
        Assert.True(result.VoChannel.VoCanEnableSummary);
        repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateVisibilityAsync_ShouldMapVersionConflictToStableBusinessError()
    {
        var repository = new Mock<IChannelDiscoverabilityRepository>(MockBehavior.Strict);
        repository
            .Setup(candidate => candidate.SetVisibilityAsync(It.IsAny<ChannelDiscoverVisibilityChangeCommand>()))
            .ThrowsAsync(new ChannelDiscoverabilityStateConflictException());
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateVisibilityAsync(
            30000,
            70001,
            20001,
            "Moderator",
            new UpdateChannelDiscoverVisibilityDto
            {
                DiscoverVisibility = ChannelDiscoverVisibility.Summary,
                ExpectedVersion = 0,
                Reason = "经复核开放摘要"
            }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("ChannelDiscoverability.VersionConflict", exception.ErrorCode);
        Assert.Equal("error.channel_discoverability.version_conflict", exception.MessageKey);
        repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateVisibilityAsync_ShouldRejectOversizedReasonBeforeRepositoryCall()
    {
        var repository = new Mock<IChannelDiscoverabilityRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateVisibilityAsync(
            30000,
            70001,
            20001,
            "Moderator",
            new UpdateChannelDiscoverVisibilityDto
            {
                DiscoverVisibility = ChannelDiscoverVisibility.Summary,
                ExpectedVersion = 0,
                Reason = new string('x', 501)
            }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("ChannelDiscoverability.InvalidArgument", exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateVisibilityAsync_ShouldRejectUnknownVisibilityWithStableBusinessError()
    {
        var repository = new Mock<IChannelDiscoverabilityRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateVisibilityAsync(
            30000,
            70001,
            20001,
            "Moderator",
            new UpdateChannelDiscoverVisibilityDto
            {
                DiscoverVisibility = (ChannelDiscoverVisibility)99,
                ExpectedVersion = 0,
                Reason = "非法枚举不应进入仓储"
            }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("ChannelDiscoverability.InvalidArgument", exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateVisibilityAsync_ShouldRejectMissingReasonWithStableBusinessError()
    {
        var repository = new Mock<IChannelDiscoverabilityRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateVisibilityAsync(
            30000,
            70001,
            20001,
            "Moderator",
            new UpdateChannelDiscoverVisibilityDto
            {
                DiscoverVisibility = ChannelDiscoverVisibility.Summary,
                ExpectedVersion = 0,
                Reason = null
            }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("ChannelDiscoverability.ReasonRequired", exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPageAsync_ShouldClampPagingAndExposeEligibilityIssues()
    {
        var repository = new Mock<IChannelDiscoverabilityRepository>(MockBehavior.Strict);
        var channel = CreateChannel(ChannelDiscoverVisibility.Hidden, version: 0);
        channel.IsEnabled = false;
        repository
            .Setup(candidate => candidate.QueryPageAsync(It.Is<ChannelDiscoverabilityPageQuery>(query =>
                query.TenantId == 30000 &&
                query.PageIndex == 1 &&
                query.PageSize == 100 &&
                query.Keyword == "general")))
            .ReturnsAsync(((IReadOnlyList<Channel>)[channel], 1));
        var service = CreateService(repository);

        var result = await service.GetPageAsync(
            30000,
            pageIndex: 0,
            pageSize: 500,
            keyword: " general ",
            discoverVisibility: null,
            isEnabled: null,
            includeDeleted: false);

        Assert.Equal(1, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Single(result.Data);
        Assert.False(result.Data[0].VoCanEnableSummary);
        Assert.Contains(ChannelDiscoverabilityPolicy.ChannelDisabled, result.Data[0].VoEligibilityIssues);
        repository.VerifyAll();
    }

    private static ChannelDiscoverabilityService CreateService(
        Mock<IChannelDiscoverabilityRepository> repository) => new(
        repository.Object,
        new FixedTimeProvider(NowUtc));

    private static Channel CreateChannel(ChannelDiscoverVisibility visibility, int version) => new()
    {
        Id = 70001,
        TenantId = 30000,
        Name = "General",
        Slug = "general",
        Type = ChannelType.Public,
        DiscoverVisibility = visibility,
        DiscoverVisibilityVersion = version,
        IsEnabled = true,
        CreateTime = NowUtc,
        CreateBy = "System"
    };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
