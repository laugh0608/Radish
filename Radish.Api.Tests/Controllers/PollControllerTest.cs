using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(PollController))]
public class PollControllerTest
{
    [Fact]
    public async Task Close_Should_Return_UpdatedPoll_When_RequestIsValid()
    {
        var postPollServiceMock = new Mock<IPostPollService>(MockBehavior.Strict);

        postPollServiceMock
            .Setup(service => service.CloseAsync(9527, 10001, "Tester"))
            .ReturnsAsync(new PostPollVo
            {
                VoPollId = 2001,
                VoPostId = 9527,
                VoIsClosed = true,
                VoTotalVoteCount = 19
            });

        var controller = CreateController(postPollServiceMock.Object);

        var result = await controller.Close(new ClosePollDto
        {
            PostId = 9527
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);

        var poll = Assert.IsType<PostPollVo>(result.ResponseData);
        Assert.True(poll.VoIsClosed);
        Assert.Equal(19, poll.VoTotalVoteCount);
    }

    [Fact]
    public async Task Close_Should_Return_Forbidden_When_CurrentUserIsNotAuthor()
    {
        var postPollServiceMock = new Mock<IPostPollService>(MockBehavior.Strict);

        postPollServiceMock
            .Setup(service => service.CloseAsync(9527, 10001, "Tester"))
            .ThrowsAsync(new InvalidOperationException("只有发帖者可以结束投票"));

        var controller = CreateController(postPollServiceMock.Object);

        var result = await controller.Close(new ClosePollDto
        {
            PostId = 9527
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("只有发帖者可以结束投票", result.MessageInfo);
    }

    [Fact]
    public async Task Close_Should_Return_NotFound_When_PostDoesNotExist()
    {
        var postPollServiceMock = new Mock<IPostPollService>(MockBehavior.Strict);

        postPollServiceMock
            .Setup(service => service.CloseAsync(9527, 10001, "Tester"))
            .ThrowsAsync(new InvalidOperationException("帖子不存在"));

        var controller = CreateController(postPollServiceMock.Object);

        var result = await controller.Close(new ClosePollDto
        {
            PostId = 9527
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("帖子不存在", result.MessageInfo);
    }

    private static PollController CreateController(IPostPollService postPollService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new PollController(postPollService, currentUserAccessorMock.Object);
    }
}
