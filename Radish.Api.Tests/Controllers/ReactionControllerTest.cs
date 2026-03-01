using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(ReactionController))]
public class ReactionControllerTest
{
    [Fact]
    public async Task GetSummary_Should_Return_Success()
    {
        var serviceMock = CreateReactionServiceMock();
        serviceMock
            .Setup(s => s.GetSummaryAsync("Post", 100, 10001))
            .ReturnsAsync(new List<ReactionSummaryVo>
            {
                new()
                {
                    VoEmojiType = "unicode",
                    VoEmojiValue = "😀",
                    VoCount = 2,
                    VoIsReacted = true
                }
            });

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetSummary("Post", 100);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<List<ReactionSummaryVo>>(result.ResponseData);
        Assert.Single(payload);
        Assert.True(payload[0].VoIsReacted);
    }

    [Fact]
    public async Task GetSummary_Should_Return_BusinessError()
    {
        var serviceMock = CreateReactionServiceMock();
        serviceMock
            .Setup(s => s.GetSummaryAsync("Invalid", 100, 10001))
            .ThrowsAsync(new BusinessException("targetType 非法", 400, "InvalidArgument"));

        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetSummary("Invalid", 100);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("InvalidArgument", result.Code);
    }

    [Fact]
    public async Task BatchGetSummary_Should_Return_Success()
    {
        var serviceMock = CreateReactionServiceMock();
        serviceMock
            .Setup(s => s.BatchGetSummaryAsync("Comment", It.IsAny<List<long>>(), 10001))
            .ReturnsAsync(new Dictionary<string, List<ReactionSummaryVo>>
            {
                ["1"] =
                [
                    new ReactionSummaryVo
                    {
                        VoEmojiType = "unicode",
                        VoEmojiValue = "😂",
                        VoCount = 1,
                        VoIsReacted = true
                    }
                ],
                ["2"] = []
            });

        var controller = CreateController(serviceMock.Object);
        var request = new BatchGetReactionSummaryDto
        {
            TargetType = "Comment",
            TargetIds = [1, 2]
        };

        var result = await controller.BatchGetSummary(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<Dictionary<string, List<ReactionSummaryVo>>>(result.ResponseData);
        Assert.Equal(2, payload.Count);
        Assert.Single(payload["1"]);
    }

    [Fact]
    public async Task Toggle_Should_Return_Success()
    {
        var serviceMock = CreateReactionServiceMock();
        serviceMock
            .Setup(s => s.ToggleAsync(It.IsAny<ToggleReactionDto>(), 10001, "Admin", 0))
            .ReturnsAsync(new List<ReactionSummaryVo>
            {
                new()
                {
                    VoEmojiType = "sticker",
                    VoEmojiValue = "radish/happy",
                    VoCount = 3,
                    VoIsReacted = true,
                    VoThumbnailUrl = "https://example.com/thumb.webp"
                }
            });

        var controller = CreateController(serviceMock.Object);
        var request = new ToggleReactionDto
        {
            TargetType = "Post",
            TargetId = 9527,
            EmojiType = "sticker",
            EmojiValue = "radish/happy"
        };

        var result = await controller.Toggle(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<List<ReactionSummaryVo>>(result.ResponseData);
        Assert.Single(payload);
        Assert.Equal("sticker", payload[0].VoEmojiType);
    }

    [Fact]
    public async Task Toggle_Should_Return_BusinessError()
    {
        var serviceMock = CreateReactionServiceMock();
        serviceMock
            .Setup(s => s.ToggleAsync(It.IsAny<ToggleReactionDto>(), 10001, "Admin", 0))
            .ThrowsAsync(new BusinessException("达到上限", 400, "ReactionLimitExceeded"));

        var controller = CreateController(serviceMock.Object);
        var request = new ToggleReactionDto
        {
            TargetType = "Post",
            TargetId = 9527,
            EmojiType = "unicode",
            EmojiValue = "😀"
        };

        var result = await controller.Toggle(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("ReactionLimitExceeded", result.Code);
    }

    private static ReactionController CreateController(IReactionService reactionService)
    {
        var httpContextUserMock = new Mock<IHttpContextUser>();
        httpContextUserMock.SetupGet(x => x.UserId).Returns(10001);
        httpContextUserMock.SetupGet(x => x.UserName).Returns("Admin");
        httpContextUserMock.SetupGet(x => x.TenantId).Returns(0);

        return new ReactionController(reactionService, httpContextUserMock.Object);
    }

    private static Mock<IReactionService> CreateReactionServiceMock()
    {
        return new Mock<IReactionService>(MockBehavior.Strict);
    }
}
