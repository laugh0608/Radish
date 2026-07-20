using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Localization;
using Moq;
using Radish.Api.Controllers.v1;
using Radish.Api.Resources;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(ExperienceController))]
public class ExperienceControllerTest
{
    [Fact]
    public async Task GetMyExperience_Should_Return_Unauthorized_When_Current_User_Is_Invalid()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object, currentUserId: 0);

        var result = await controller.GetMyExperience();

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Auth.Unauthorized", result.Code);
        Assert.Equal("error.auth.unauthorized", result.MessageKey);
    }

    [Fact]
    public async Task GetMyExperience_Should_Return_Forbidden_When_Requesting_Another_User()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetMyExperience(20002);

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Experience.OtherUserForbidden", result.Code);
        Assert.Equal("error.experience.other_user_forbidden", result.MessageKey);
    }

    [Fact]
    public async Task GetMyExperience_Should_Return_NotFound_When_Experience_Is_Missing()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetUserExperienceAsync(10001))
            .ReturnsAsync((UserExperienceVo?)null);
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetMyExperience();

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("Experience.NotFound", result.Code);
        Assert.Equal("error.experience.not_found", result.MessageKey);
    }

    [Fact]
    public async Task GetTransactions_Should_Return_Unauthorized_When_Current_User_Is_Invalid()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object, currentUserId: 0);

        var result = await controller.GetTransactions();

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Auth.Unauthorized", result.Code);
        Assert.Equal("error.auth.unauthorized", result.MessageKey);
    }

    [Fact]
    public async Task GetUserTransactions_Should_Return_Paged_Result()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetTransactionsAsync(9527, 2, 15, "ADMIN_ADJUST", null, null))
            .ReturnsAsync(new PageModel<ExpTransactionVo>
            {
                Page = 2,
                PageSize = 15,
                DataCount = 1,
                PageCount = 1,
                Data =
                [
                    new ExpTransactionVo
                    {
                        VoId = 70001,
                        VoUserId = 9527,
                        VoExpType = "ADMIN_ADJUST",
                        VoExpTypeDisplay = "管理员调整",
                        VoExpAmount = 30,
                        VoOperatorId = 9001,
                        VoOperatorName = "Auditor"
                    }
                ]
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetUserTransactions(9527, 2, 15, "ADMIN_ADJUST");

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<PageModel<ExpTransactionVo>>(result.ResponseData);
        Assert.Single(payload.Data);
        Assert.Equal("Auditor", payload.Data[0].VoOperatorName);
    }

    [Fact]
    public async Task GetUserTransactions_Should_Reject_Invalid_UserId()
    {
        var serviceMock = CreateServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetUserTransactions(0);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationFailed, result.Code);
        Assert.Equal("error.common.validation_failed", result.MessageKey);
    }

    [Fact]
    public async Task GetUserDailyStats_Should_Return_Governance_Recommendation()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetDailyStatsAsync(9527, 7))
            .ReturnsAsync(new UserExpDailyStatsWindowVo
            {
                VoWindowDays = 7,
                VoRecommendation = new UserExpGovernanceRecommendationVo
                {
                    VoLevel = "review",
                    VoTitle = "建议人工复核",
                    VoReason = "规则重复命中",
                    VoSuggestedAction = "先复核后决定是否冻结。"
                }
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetUserDailyStats(9527, 7);

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<UserExpDailyStatsWindowVo>(result.ResponseData);
        Assert.Equal("review", payload.VoRecommendation?.VoLevel);
        Assert.Equal("建议人工复核", payload.VoRecommendation?.VoTitle);
    }

    [Fact]
    public async Task GetUserTransactions_Should_Forward_Date_Range_Filter()
    {
        var startDate = new DateTime(2026, 5, 1, 0, 0, 0);
        var endDate = new DateTime(2026, 5, 1, 23, 59, 59);

        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetTransactionsAsync(
                9527,
                1,
                20,
                "RECEIVE_LIKE,GIVE_LIKE",
                startDate,
                endDate))
            .ReturnsAsync(new PageModel<ExpTransactionVo>
            {
                Page = 1,
                PageSize = 20,
                DataCount = 0,
                PageCount = 0,
                Data = []
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetUserTransactions(9527, 1, 20, "RECEIVE_LIKE,GIVE_LIKE", startDate, endDate);

        Assert.True(result.IsSuccess);
        _ = Assert.IsType<PageModel<ExpTransactionVo>>(result.ResponseData);
    }

    [Fact]
    public async Task GetUserGovernanceActions_Should_Return_Action_Logs()
    {
        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.GetGovernanceActionsAsync(9527, 20))
            .ReturnsAsync([
                new UserExperienceGovernanceActionVo
                {
                    VoActionId = 70021,
                    VoTargetUserId = 9527,
                    VoActionType = "Review",
                    VoActionTypeDisplay = "人工复核",
                    VoReviewResult = "Observe",
                    VoReviewResultDisplay = "已复核，继续观察",
                    VoRemark = "已回看经验流水，继续观察。"
                }
            ]);

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetUserGovernanceActions(9527, 20);

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<List<UserExperienceGovernanceActionVo>>(result.ResponseData);
        Assert.Single(payload);
        Assert.Equal("人工复核", payload[0].VoActionTypeDisplay);
    }

    [Fact]
    public async Task AdminRecordGovernanceReview_Should_Forward_Request()
    {
        var request = new AdminRecordExperienceGovernanceReviewDto
        {
            UserId = 9527,
            ReviewResult = "Observe",
            Remark = "已结合经验流水人工复核，继续观察。",
            WindowDays = 7,
            StatDate = new DateOnly(2026, 5, 10),
            RuleCodes = ["LIKE_SHARE_HEAVY"],
            RuleLabels = ["点赞占比偏高"],
            RecommendationLevel = "review",
            RecommendationReason = "最近 7 天重复命中"
        };

        var serviceMock = CreateServiceMock();
        serviceMock
            .Setup(service => service.RecordGovernanceReviewAsync(
                It.Is<AdminRecordExperienceGovernanceReviewDto>(dto =>
                    dto.UserId == 9527
                    && dto.ReviewResult == "Observe"
                    && dto.WindowDays == 7),
                10001,
                "Tester"))
            .ReturnsAsync(true);

        var controller = CreateController(serviceMock.Object);
        var result = await controller.AdminRecordGovernanceReview(request);

        Assert.True(result.IsSuccess);
        Assert.True(Assert.IsType<bool>(result.ResponseData));
        serviceMock.Verify(service => service.RecordGovernanceReviewAsync(
            It.IsAny<AdminRecordExperienceGovernanceReviewDto>(),
            10001,
            "Tester"), Times.Once);
    }

    private static ExperienceController CreateController(
        IExperienceService experienceService,
        long currentUserId = 10001)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = currentUserId,
            UserName = "Tester",
            TenantId = 0
        });

        return new ExperienceController(
            experienceService,
            currentUserAccessorMock.Object,
            CreateErrorsLocalizer());
    }

    private static Mock<IExperienceService> CreateServiceMock()
    {
        return new Mock<IExperienceService>(MockBehavior.Strict);
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
