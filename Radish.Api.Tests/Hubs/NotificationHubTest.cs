using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Hubs;

public sealed class NotificationHubTest
{
    [Fact]
    public async Task OnConnectedAsync_ShouldJoinUserGroupAndSendAuthoritativeRevision()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(UserClaimTypes.Sub, "1001"),
            new Claim(UserClaimTypes.TenantId, "9")
        ], "TestAuth"));
        var context = new Mock<HubCallerContext>();
        context.SetupGet(item => item.ConnectionId).Returns("connection-1");
        context.SetupGet(item => item.User).Returns(principal);
        context.SetupGet(item => item.Features).Returns(new FeatureCollection());
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        groups.Setup(item => item.AddToGroupAsync("connection-1", "user:1001", default))
            .Returns(Task.CompletedTask);
        var caller = new Mock<ISingleClientProxy>(MockBehavior.Strict);
        caller.Setup(item => item.SendCoreAsync(
                "NotificationInboxChanged",
                It.Is<object?[]>(arguments => IsConnectedChange(arguments)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        caller.Setup(item => item.SendCoreAsync(
                "UnreadCountChanged",
                It.Is<object?[]>(arguments => arguments.Length == 1),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubCallerClients>(MockBehavior.Strict);
        clients.SetupGet(item => item.Caller).Returns(caller.Object);
        var push = new Mock<INotificationPushService>(MockBehavior.Strict);
        push.Setup(item => item.GetInboxSummaryAsync(9, 1001))
            .ReturnsAsync(new NotificationInboxSummaryVo
            {
                VoRevision = 7,
                VoUnreadGroupCount = 2,
                VoUnreadOccurrenceCount = 3
            });
        var hub = new NotificationHub(
            push.Object,
            new ClaimsPrincipalNormalizer(),
            NullLogger<NotificationHub>.Instance)
        {
            Context = context.Object,
            Groups = groups.Object,
            Clients = clients.Object
        };

        await hub.OnConnectedAsync();

        groups.VerifyAll();
        caller.VerifyAll();
        push.VerifyAll();
    }

    [Fact]
    public void Hub_ShouldNotExposeBroadcastOnlyReadCommands()
    {
        var publicMethods = typeof(NotificationHub).GetMethods()
            .Where(method => method.DeclaringType == typeof(NotificationHub))
            .Select(method => method.Name)
            .ToList();

        Assert.DoesNotContain("MarkAsRead", publicMethods);
        Assert.DoesNotContain("MarkAllAsRead", publicMethods);
    }

    private static bool IsConnectedChange(object?[] arguments)
    {
        var change = arguments.SingleOrDefault() as NotificationInboxChangedVo;
        return change?.VoRevision == 7 && change.VoReason == "Connected";
    }
}
