using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Radish.Api.Controllers.v1;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public sealed class LeaderboardControllerTest
{
    [Theory]
    [InlineData(LeaderboardType.Balance)]
    [InlineData(LeaderboardType.TotalSpent)]
    [InlineData(LeaderboardType.PurchaseCount)]
    [InlineData((LeaderboardType)999)]
    public async Task GetLeaderboard_ShouldRejectNonPublicTypeWithoutCallingService(
        LeaderboardType type)
    {
        var service = new Mock<ILeaderboardService>(MockBehavior.Strict);
        var controller = CreateController(service.Object);

        var result = await controller.GetLeaderboard((int)type);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(LeaderboardErrorCodes.TypeUnavailable, result.Code);
        Assert.Equal(
            LeaderboardErrorCodes.ResolveMessageKey(LeaderboardErrorCodes.TypeUnavailable),
            result.MessageKey);
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(LeaderboardType.Balance)]
    [InlineData(LeaderboardType.TotalSpent)]
    [InlineData(LeaderboardType.PurchaseCount)]
    [InlineData((LeaderboardType)999)]
    public async Task GetMyRank_ShouldRejectNonPublicTypeWithoutCallingService(
        LeaderboardType type)
    {
        var service = new Mock<ILeaderboardService>(MockBehavior.Strict);
        var controller = CreateController(service.Object, currentUserId: 1001);

        var result = await controller.GetMyRank((int)type);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(LeaderboardErrorCodes.TypeUnavailable, result.Code);
        Assert.Equal(
            LeaderboardErrorCodes.ResolveMessageKey(LeaderboardErrorCodes.TypeUnavailable),
            result.MessageKey);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetMyRank_ShouldRejectProductLeaderboardWithoutCallingService()
    {
        var service = new Mock<ILeaderboardService>(MockBehavior.Strict);
        var controller = CreateController(service.Object, currentUserId: 1001);

        var result = await controller.GetMyRank((int)LeaderboardType.HotProduct);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(LeaderboardErrorCodes.UserRankUnavailable, result.Code);
        Assert.Equal(
            LeaderboardErrorCodes.ResolveMessageKey(LeaderboardErrorCodes.UserRankUnavailable),
            result.MessageKey);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTypes_ShouldReturnServicePublicTypeSnapshot()
    {
        var types = new List<LeaderboardTypeVo>
        {
            new()
            {
                VoType = LeaderboardType.Experience,
                VoCategory = LeaderboardCategory.User,
                VoName = "经验排行",
                VoSortOrder = 1
            }
        };
        var service = new Mock<ILeaderboardService>(MockBehavior.Strict);
        service
            .Setup(item => item.GetLeaderboardTypesAsync())
            .ReturnsAsync(types);
        var controller = CreateController(service.Object);

        var result = await controller.GetTypes();

        Assert.True(result.IsSuccess);
        Assert.Equal(types, result.ResponseData);
    }

    private static LeaderboardController CreateController(
        ILeaderboardService service,
        long currentUserId = 0)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor
            .SetupGet(accessor => accessor.Current)
            .Returns(currentUserId > 0
                ? new CurrentUser
                {
                    IsAuthenticated = true,
                    UserId = currentUserId,
                    UserName = $"User-{currentUserId}",
                    TenantId = 0
                }
                : CurrentUser.Anonymous);
        return new LeaderboardController(service, currentUserAccessor.Object);
    }
}
