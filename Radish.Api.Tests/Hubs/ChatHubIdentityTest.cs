using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Xunit;

namespace Radish.Api.Tests.Hubs;

public class ChatHubIdentityTest
{
    [Fact]
    public async Task OnConnectedAsync_ShouldJoinUserGroupFromStandardSubjectClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(UserClaimTypes.Sub, "10001")
        ], "TestAuth"));
        var callerContext = new Mock<HubCallerContext>();
        callerContext.SetupGet(item => item.ConnectionId).Returns("connection-1");
        callerContext.SetupGet(item => item.User).Returns(principal);
        callerContext.SetupGet(item => item.Features).Returns(new FeatureCollection());
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        groups
            .Setup(item => item.AddToGroupAsync("connection-1", "user:10001", default))
            .Returns(Task.CompletedTask);

        var hub = new ChatHub(
            Mock.Of<IChatService>(),
            Mock.Of<IChatPresenceService>(),
            Mock.Of<IChatChannelAccessService>(),
            new ClaimsPrincipalNormalizer(),
            NullLogger<ChatHub>.Instance)
        {
            Context = callerContext.Object,
            Groups = groups.Object
        };

        await hub.OnConnectedAsync();

        groups.VerifyAll();
    }
}
