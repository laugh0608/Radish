using System;
using System.Linq;
using System.Reflection;
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
    public async Task GetCaseQueue_Should_Return_Paged_Result()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(s => s.GetCaseQueueAsync(It.Is<ContentModerationCaseQueueDto>(query =>
                query.Status == 0 &&
                query.TargetType == "Comment" &&
                query.Keyword == "9527" &&
                query.PageIndex == 1 &&
                query.PageSize == 20),
                0))
            .ReturnsAsync(new VoPagedResult<ContentModerationCaseQueueItemVo>
            {
                VoItems =
                [
                    new ContentModerationCaseQueueItemVo
                    {
                        VoCasePublicId = "case_demo",
                        VoTargetType = "Comment",
                        VoTargetContentId = 9527,
                        VoStatus = "Open"
                    }
                ],
                VoTotal = 1,
                VoPageIndex = 1,
                VoPageSize = 20
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetCaseQueue(new ContentModerationCaseQueueDto
        {
            Status = 0,
            TargetType = "Comment",
            Keyword = "9527",
            PageIndex = 1,
            PageSize = 20
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<VoPagedResult<ContentModerationCaseQueueItemVo>>(result.ResponseData);
        Assert.Single(payload.VoItems);
        Assert.Equal("case_demo", payload.VoItems[0].VoCasePublicId);
    }

    [Fact]
    public void Controller_Should_Expose_Only_Case_Based_Console_Actions()
    {
        var actionNames = typeof(ContentModerationController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains(nameof(ContentModerationController.GetCaseQueue), actionNames);
        Assert.Contains(nameof(ContentModerationController.GetCase), actionNames);
        Assert.Contains(nameof(ContentModerationController.CaptureEvidence), actionNames);
        Assert.Contains(nameof(ContentModerationController.ReviewCase), actionNames);
        Assert.Contains(nameof(ContentModerationController.ApplyCorrectiveAction), actionNames);
        Assert.DoesNotContain("GetReviewQueue", actionNames);
        Assert.DoesNotContain("Review", actionNames);
        Assert.DoesNotContain("ApplyUserAction", actionNames);
        Assert.DoesNotContain("GetActionLogs", actionNames);
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
