using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
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
        Assert.Equal("Moderation.ValidationFailed", result.Code);
        Assert.Equal("error.moderation.validation_failed", result.MessageKey);
    }

    [Fact]
    public async Task Report_Should_Return_Success_When_Valid()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.SubmitCaseReportAsync(
                It.Is<SubmitContentReportDto>(dto =>
                    dto.TargetType == "Post" &&
                    dto.TargetContentId == 9527 &&
                    dto.ReasonType == "Spam"),
                10001,
                "Tester",
                0))
            .ReturnsAsync(new ContentReportReceiptVo
            {
                VoReportPublicId = "rpt_demo",
                VoTargetType = "Post",
                VoReporterState = "Submitted"
            });

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
        var payload = Assert.IsType<ContentReportReceiptVo>(result.ResponseData);
        Assert.Equal("rpt_demo", payload.VoReportPublicId);
    }

    [Fact]
    public async Task GetReviewQueue_Should_Return_Paged_Result()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetReportQueueAsync(It.Is<ContentReportQueueQueryDto>(query =>
                query.Status == (int)ContentReportStatusEnum.Pending &&
                query.TargetType == "Comment" &&
                query.ReasonType == "Spam" &&
                query.NavigationStatus == "Fallback" &&
                query.Keyword == "9527" &&
                query.PageIndex == 1 &&
                query.PageSize == 20)))
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
        var result = await controller.GetReviewQueue(new ContentReportQueueQueryDto
        {
            Status = (int)ContentReportStatusEnum.Pending,
            TargetType = "Comment",
            ReasonType = "Spam",
            NavigationStatus = "Fallback",
            Keyword = "9527",
            PageIndex = 1,
            PageSize = 20
        });

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
            .ThrowsAsync(new BusinessException(
                "举报单不存在",
                404,
                "Moderation.ReportNotFound",
                "error.moderation.report_not_found"));

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
        Assert.Equal("Moderation.ReportNotFound", result.Code);
        Assert.Equal("error.moderation.report_not_found", result.MessageKey);
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

    [Fact]
    public async Task ApplyUserAction_Should_Forward_Request_And_Return_Action_Result()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.ApplyUserActionAsync(
                It.Is<ApplyUserModerationActionDto>(dto =>
                    dto.TargetUserId == 20002 &&
                    dto.ActionType == 3 &&
                    dto.DurationHours == null &&
                    dto.SourceReportId == 70003 &&
                    dto.Reason == "人工复核后解除禁言"),
                10001,
                "Tester",
                0))
            .ReturnsAsync(new UserModerationActionVo
            {
                VoActionId = 82011,
                VoTargetUserId = 20002,
                VoActionType = "Unmute",
                VoSourceReportId = 70003,
                VoIsActive = false
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.ApplyUserAction(new ApplyUserModerationActionDto
        {
            TargetUserId = 20002,
            ActionType = 3,
            SourceReportId = 70003,
            Reason = "人工复核后解除禁言"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<UserModerationActionVo>(result.ResponseData);
        Assert.Equal(82011, payload.VoActionId);
        Assert.Equal("Unmute", payload.VoActionType);
        Assert.False(payload.VoIsActive);
    }

    [Fact]
    public async Task GetActionLogs_Should_Return_Paged_Result_With_Source_Report_Navigation()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetActionLogsAsync(It.Is<ContentModerationActionLogQueryDto>(query =>
                query.PageIndex == 1 &&
                query.PageSize == 20 &&
                query.TargetUserId == null &&
                query.SourceReportId == 70003 &&
                query.ActionType == "Mute" &&
                query.IsActive == true &&
                query.Keyword == "reviewer")))
            .ReturnsAsync(new VoPagedResult<UserModerationActionVo>
            {
                VoItems =
                [
                    new UserModerationActionVo
                    {
                        VoActionId = 81001,
                        VoSourceReportId = 70003,
                        VoSourceReportTargetType = "ChatMessage",
                        VoSourceReportTargetContentId = 90004,
                        VoSourceReportTargetChannelId = 108,
                        VoSourceReportTargetMessageId = 90004
                    }
                ],
                VoTotal = 1,
                VoPageIndex = 1,
                VoPageSize = 20
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetActionLogs(new ContentModerationActionLogQueryDto
        {
            PageIndex = 1,
            PageSize = 20,
            SourceReportId = 70003,
            ActionType = "Mute",
            IsActive = true,
            Keyword = "reviewer"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<VoPagedResult<UserModerationActionVo>>(result.ResponseData);
        Assert.Single(payload.VoItems);
        Assert.Equal(108, payload.VoItems[0].VoSourceReportTargetChannelId);
        Assert.Equal(90004, payload.VoItems[0].VoSourceReportTargetMessageId);
    }

    private static ContentModerationController CreateController(IContentModerationService moderationService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            IsAuthenticated = true,
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0,
            Roles = [UserRoles.System]
        });

        return new ContentModerationController(
            moderationService,
            currentUserAccessorMock.Object,
            CreateErrorsLocalizer());
    }

    private static Mock<IContentModerationService> CreateServiceMock()
    {
        return new Mock<IContentModerationService>(MockBehavior.Strict);
    }

    private static IStringLocalizer<Errors> CreateErrorsLocalizer()
    {
        var localizerMock = new Mock<IStringLocalizer<Errors>>();
        localizerMock
            .Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key, resourceNotFound: true));
        return localizerMock.Object;
    }
}
