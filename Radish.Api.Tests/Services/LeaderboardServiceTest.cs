using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class LeaderboardServiceTest
{
    [Fact]
    public async Task GetLeaderboardTypes_ShouldReturnOnlyPublicTypesInProductOrder()
    {
        var service = CreateService();

        var types = await service.GetLeaderboardTypesAsync();

        Assert.Equal(
            [
                LeaderboardType.Experience,
                LeaderboardType.PostCount,
                LeaderboardType.CommentCount,
                LeaderboardType.Popularity,
                LeaderboardType.HotProduct
            ],
            types.Select(type => type.VoType).ToArray());
        Assert.Equal([1, 2, 3, 4, 5], types.Select(type => type.VoSortOrder).ToArray());
    }

    [Theory]
    [InlineData(LeaderboardType.Balance)]
    [InlineData(LeaderboardType.TotalSpent)]
    [InlineData(LeaderboardType.PurchaseCount)]
    [InlineData((LeaderboardType)999)]
    public async Task GetLeaderboard_ShouldRejectNonPublicTypeBeforeRepositoryQuery(
        LeaderboardType type)
    {
        var repository = new Mock<ILeaderboardRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetLeaderboardAsync(type, 1, 20));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(LeaderboardType.Balance)]
    [InlineData(LeaderboardType.TotalSpent)]
    [InlineData(LeaderboardType.PurchaseCount)]
    [InlineData((LeaderboardType)999)]
    public async Task GetUserRank_ShouldRejectNonPublicType(LeaderboardType type)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetUserRankAsync(type, 1001));
    }

    [Fact]
    public async Task GetUserRank_ShouldRejectProductLeaderboard()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetUserRankAsync(LeaderboardType.HotProduct, 1001));
    }

    [Fact]
    public async Task GetUserRank_ShouldNotConvertRepositoryFailureIntoNotRanked()
    {
        var repository = new Mock<ILeaderboardRepository>(MockBehavior.Strict);
        repository
            .Setup(item => item.GetUserPostCountRankAsync(1001))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetUserRankAsync(LeaderboardType.PostCount, 1001));
    }

    private static LeaderboardService CreateService(
        Mock<ILeaderboardRepository>? repository = null)
    {
        return new LeaderboardService(
            Mock.Of<IBaseRepository<UserExperience>>(),
            Mock.Of<IBaseRepository<LevelConfig>>(),
            repository?.Object ?? Mock.Of<ILeaderboardRepository>(),
            Mock.Of<IAttachmentService>(),
            Mock.Of<IAttachmentUrlResolver>());
    }
}
