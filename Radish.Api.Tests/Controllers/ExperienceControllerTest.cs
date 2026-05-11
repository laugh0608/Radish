using System.Threading.Tasks;
using JetBrains.Annotations;
using Moq;
using Radish.Api.Controllers.v1;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(ExperienceController))]
public class ExperienceControllerTest
{
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
        Assert.Equal("用户ID无效", result.MessageInfo);
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

    private static ExperienceController CreateController(IExperienceService experienceService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new ExperienceController(experienceService, currentUserAccessorMock.Object);
    }

    private static Mock<IExperienceService> CreateServiceMock()
    {
        return new Mock<IExperienceService>(MockBehavior.Strict);
    }
}
