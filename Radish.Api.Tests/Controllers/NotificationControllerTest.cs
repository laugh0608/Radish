using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Radish.Api.Controllers;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public sealed class NotificationControllerTest
{
    [Fact]
    public async Task GetInbox_ShouldUseCurrentTenantAndUser()
    {
        var service = new Mock<INotificationService>(MockBehavior.Strict);
        var query = new NotificationInboxQueryDto { PageSize = 30 };
        service.Setup(item => item.GetInboxAsync(9, 1001, query))
            .ReturnsAsync(new NotificationInboxPageVo
            {
                VoSummary = new NotificationInboxSummaryVo { VoRevision = 3 }
            });

        var result = await CreateController(service.Object).GetInbox(query);

        Assert.True(result.IsSuccess);
        var payload = Assert.IsType<NotificationInboxPageVo>(result.ResponseData);
        Assert.Equal(3, payload.VoSummary.VoRevision);
        service.VerifyAll();
    }

    [Fact]
    public async Task MarkInboxGroupsAsRead_ShouldRejectEmptyRequestBeforeService()
    {
        var service = new Mock<INotificationService>(MockBehavior.Strict);

        var result = await CreateController(service.Object).MarkInboxGroupsAsRead(
            new MarkInboxGroupsAsReadDto());

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdatePreferences_ShouldUseCurrentOperatorIdentity()
    {
        var service = new Mock<INotificationService>(MockBehavior.Strict);
        var request = new UpdateNotificationPreferencesDto
        {
            Preferences =
            [
                new UpdateNotificationPreferenceDto
                {
                    Category = "Reaction",
                    InAppEnabled = false,
                    RealtimePreviewEnabled = false
                }
            ]
        };
        service.Setup(item => item.UpdatePreferencesAsync(9, 1001, request, 1001, "Tester"))
            .ReturnsAsync(new List<NotificationPreferenceVo>());

        var result = await CreateController(service.Object).UpdatePreferences(request);

        Assert.True(result.IsSuccess);
        service.VerifyAll();
    }

    [Fact]
    public void Controller_ShouldRequireClientPolicy()
    {
        var authorize = typeof(NotificationController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(AuthorizationPolicies.Client, authorize.Policy);
    }

    private static NotificationController CreateController(INotificationService service)
    {
        var current = new Mock<ICurrentUserAccessor>();
        current.SetupGet(item => item.Current).Returns(new CurrentUser
        {
            IsAuthenticated = true,
            TenantId = 9,
            UserId = 1001,
            UserName = "Tester"
        });
        return new NotificationController(service, current.Object);
    }
}
