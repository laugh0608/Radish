using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.Api.Hubs;
using Radish.Api.Services;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class UserInteractionRealtimeNotifierTest
{
    [Fact]
    public async Task NotifyRelationshipChangedAsync_ShouldSendStringVersionWithoutDirectionToBothUsers()
    {
        var sentGroups = new List<string>();
        var sentPayloads = new List<UserInteractionChangedVo>();
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        clientProxy
            .Setup(proxy => proxy.SendCoreAsync(
                "UserInteractionChanged",
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Callback((string _, object?[] arguments, CancellationToken _) =>
                sentPayloads.Add(Assert.IsType<UserInteractionChangedVo>(Assert.Single(arguments))))
            .Returns(Task.CompletedTask);
        var hubClients = new Mock<IHubClients>(MockBehavior.Strict);
        hubClients
            .Setup(clients => clients.Group(It.IsAny<string>()))
            .Callback((string groupName) => sentGroups.Add(groupName))
            .Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<ChatHub>>(MockBehavior.Strict);
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);
        var notifier = new UserInteractionRealtimeNotifier(
            hubContext.Object,
            Mock.Of<ILogger<UserInteractionRealtimeNotifier>>());

        await notifier.NotifyRelationshipChangedAsync(1001, 2002, 9007199254740993);

        Assert.Equal(["user:1001", "user:2002"], sentGroups);
        Assert.All(sentPayloads, payload =>
            Assert.Equal("9007199254740993", payload.VoRelationshipVersion));
    }
}
