using System;
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

[TestSubject(typeof(ContentModerationController))]
public class ContentModerationControllerTest
{
    [Fact]
    public async Task Report_Should_Return_BadRequest_When_TargetId_Invalid()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.Report(new SubmitContentReportDto
        {
            TargetType = "Post",
            TargetContentId = 0,
            ReasonType = "Spam"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Report_Should_Return_Success_When_Valid()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.SubmitReportAsync(
                It.Is<SubmitContentReportDto>(dto =>
                    dto.TargetType == "Post" &&
                    dto.TargetContentId == 9527 &&
                    dto.ReasonType == "Spam"),
                10001,
                "Tester",
                0))
            .ReturnsAsync(777001);

        var controller = CreateController(serviceMock.Object);
        var result = await controller.Report(new SubmitContentReportDto
        {
            TargetType = "Post",
            TargetContentId = 9527,
            ReasonType = "Spam",
            ReasonDetail = "重复广告"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<long>(result.ResponseData);
        Assert.Equal(777001, payload);
    }

    [Fact]
    public async Task GetReviewQueue_Should_Return_Paged_Result()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetReportQueueAsync(0, 1, 20))
            .ReturnsAsync(new VoPagedResult<ContentReportQueueItemVo>
            {
                VoItems =
                [
                    new ContentReportQueueItemVo
                    {
                        VoReportId = 80001,
                        VoTargetType = "Post",
                        VoTargetContentId = 9527,
                        VoStatus = "Pending"
                    }
                ],
                VoTotal = 1,
                VoPageIndex = 1,
                VoPageSize = 20
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetReviewQueue();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<VoPagedResult<ContentReportQueueItemVo>>(result.ResponseData);
        Assert.Single(payload.VoItems);
        Assert.Equal(80001, payload.VoItems[0].VoReportId);
    }

    [Fact]
    public async Task Review_Should_Return_NotFound_When_Report_Not_Exists()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.ReviewReportAsync(
                It.Is<ReviewContentReportDto>(dto => dto.ReportId == 99999),
                10001,
                "Tester",
                0))
            .ThrowsAsync(new InvalidOperationException("举报单不存在"));

        var controller = CreateController(serviceMock.Object);
        var result = await controller.Review(new ReviewContentReportDto
        {
            ReportId = 99999,
            IsApproved = true,
            ActionType = 1,
            DurationHours = 24
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GetMyPublishPermission_Should_Return_Permission()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetPublishPermissionAsync(10001))
            .ReturnsAsync(new ContentModerationPermissionVo
            {
                VoUserId = 10001,
                VoCanPublish = false,
                VoIsMuted = true,
                VoMutedUntil = DateTime.UtcNow.AddHours(1),
                VoDenyReason = "账号已被禁言"
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetMyPublishPermission();

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<ContentModerationPermissionVo>(result.ResponseData);
        Assert.False(payload.VoCanPublish);
        Assert.True(payload.VoIsMuted);
    }

    private static ContentModerationController CreateController(IContentModerationService moderationService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new ContentModerationController(moderationService, currentUserAccessorMock.Object);
    }

    private static Mock<IContentModerationService> CreateServiceMock()
    {
        return new Mock<IContentModerationService>(MockBehavior.Strict);
    }
}
