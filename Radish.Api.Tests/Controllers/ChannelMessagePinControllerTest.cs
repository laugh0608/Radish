using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Radish.Api.Controllers;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Xunit;

namespace Radish.Api.Tests.Controllers;

public sealed class ChannelMessagePinControllerTest
{
    [Fact]
    public async Task Set_ShouldBroadcastFullStateOnlyWhenAuthoritativeStateChanged()
    {
        var request = new SetChatMessagePinDto
        {
            ChannelId = 70001,
            MessageId = 90001,
            IsPinned = true
        };
        var state = new ChatMessagePinStateVo
        {
            VoChannelId = 70001,
            VoRevision = 8,
            VoItems =
            [
                new ChatMessagePinVo
                {
                    VoId = 91001,
                    VoMessageId = 90001,
                    VoMessage = new ChannelMessageVo { VoId = 90001 }
                }
            ]
        };
        var service = new Mock<IChatMessagePinService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.SetAsync(30000, 20001, "Tester", false, request))
            .ReturnsAsync(new ChatMessagePinMutationVo
            {
                VoState = state,
                VoChanged = true
            });
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        clientProxy
            .Setup(proxy => proxy.SendCoreAsync(
                "MessagePinsChanged",
                It.Is<object?[]>(arguments => arguments.Length == 1 && ReferenceEquals(arguments[0], state)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubClients = new Mock<IHubClients>(MockBehavior.Strict);
        hubClients
            .Setup(clients => clients.Group("channel:30000:70001"))
            .Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<ChatHub>>(MockBehavior.Strict);
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);
        var controller = CreateController(service.Object, hubContext.Object);

        var result = await controller.Set(request);

        Assert.True(result.IsSuccess);
        Assert.Same(state, Assert.IsType<ChatMessagePinMutationVo>(result.ResponseData).VoState);
        clientProxy.VerifyAll();
    }

    [Fact]
    public async Task GetState_ShouldUseCurrentTenantAndUserWithoutBroadcasting()
    {
        var state = new ChatMessagePinStateVo { VoChannelId = 70001, VoRevision = 3 };
        var service = new Mock<IChatMessagePinService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.GetStateAsync(30000, 20001, 70001))
            .ReturnsAsync(state);
        var controller = CreateController(service.Object, Mock.Of<IHubContext<ChatHub>>());

        var result = await controller.GetState(70001);

        Assert.True(result.IsSuccess);
        Assert.Same(state, result.ResponseData);
    }

    [Fact]
    public async Task Set_ShouldNotBroadcastWhenDesiredStateAlreadyMatches()
    {
        var request = new SetChatMessagePinDto
        {
            ChannelId = 70001,
            MessageId = 90001,
            IsPinned = true
        };
        var state = new ChatMessagePinStateVo { VoChannelId = 70001, VoRevision = 8 };
        var service = new Mock<IChatMessagePinService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.SetAsync(30000, 20001, "Tester", false, request))
            .ReturnsAsync(new ChatMessagePinMutationVo
            {
                VoState = state,
                VoChanged = false
            });
        var hubContext = new Mock<IHubContext<ChatHub>>(MockBehavior.Strict);
        var controller = CreateController(service.Object, hubContext.Object);

        var result = await controller.Set(request);

        Assert.True(result.IsSuccess);
        hubContext.VerifyGet(context => context.Clients, Times.Never);
    }

    private static ChannelMessagePinController CreateController(
        IChatMessagePinService service,
        IHubContext<ChatHub> hubContext)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            TenantId = 30000,
            UserId = 20001,
            UserName = "Tester"
        });
        return new ChannelMessagePinController(service, hubContext, currentUserAccessor.Object);
    }
}
