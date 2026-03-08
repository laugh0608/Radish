using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(UserFollowController))]
public class UserFollowControllerTest
{
    [Fact]
    public async Task Follow_Should_Return_BadRequest_When_Target_Is_Self()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.Follow(new FollowUserDto { TargetUserId = 10001 });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Follow_Should_Return_Status_When_Success()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.FollowAsync(10001, 20002, 0, "Tester"))
            .ReturnsAsync(true);
        serviceMock
            .Setup(s => s.GetFollowStatusAsync(10001, 20002))
            .ReturnsAsync(new UserFollowStatusVo
            {
                VoTargetUserId = 20002,
                VoIsFollowing = true,
                VoIsFollower = false,
                VoFollowerCount = 10,
                VoFollowingCount = 3
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.Follow(new FollowUserDto { TargetUserId = 20002 });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<UserFollowStatusVo>(result.ResponseData);
        Assert.True(payload.VoIsFollowing);
        Assert.Equal(20002, payload.VoTargetUserId);
    }

    [Fact]
    public async Task GetMyFollowers_Should_Return_Paged_Result()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetMyFollowersAsync(10001, 1, 20))
            .ReturnsAsync(new VoPagedResult<UserFollowUserVo>
            {
                VoItems =
                [
                    new UserFollowUserVo
                    {
                        VoUserId = 20002,
                        VoUserName = "alice",
                        VoIsMutualFollow = true
                    }
                ],
                VoTotal = 1,
                VoPageIndex = 1,
                VoPageSize = 20
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetMyFollowers();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<VoPagedResult<UserFollowUserVo>>(result.ResponseData);
        Assert.Single(payload.VoItems);
    }

    [Fact]
    public async Task GetMyFollowingFeed_Should_Return_Posts()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetMyFollowingFeedAsync(10001, 1, 20))
            .ReturnsAsync(new VoPagedResult<PostVo>
            {
                VoItems =
                [
                    new PostVo
                    {
                        VoId = 9527,
                        VoTitle = "关注动态帖子",
                        VoAuthorId = 20002,
                        VoAuthorName = "alice"
                    }
                ],
                VoTotal = 1,
                VoPageIndex = 1,
                VoPageSize = 20
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetMyFollowingFeed();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<VoPagedResult<PostVo>>(result.ResponseData);
        Assert.Single(payload.VoItems);
        Assert.Equal(9527, payload.VoItems[0].VoId);
    }

    [Fact]
    public async Task GetMyDistributionFeed_Should_Return_Posts()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetMyDistributionFeedAsync(10001, "hot", 1, 20))
            .ReturnsAsync(new VoPagedResult<PostVo>
            {
                VoItems =
                [
                    new PostVo
                    {
                        VoId = 10086,
                        VoTitle = "热门分发帖子",
                        VoAuthorId = 20003,
                        VoAuthorName = "bob"
                    }
                ],
                VoTotal = 1,
                VoPageIndex = 1,
                VoPageSize = 20
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetMyDistributionFeed("hot");

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<VoPagedResult<PostVo>>(result.ResponseData);
        Assert.Single(payload.VoItems);
        Assert.Equal(10086, payload.VoItems[0].VoId);
    }

    [Fact]
    public async Task GetMyDistributionFeed_Should_Return_BadRequest_When_StreamType_Invalid()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetMyDistributionFeed("timeline");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    private static UserFollowController CreateController(IUserFollowService followService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new UserFollowController(followService, currentUserAccessorMock.Object);
    }

    private static Mock<IUserFollowService> CreateServiceMock()
    {
        return new Mock<IUserFollowService>(MockBehavior.Strict);
    }
}
