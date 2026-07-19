using System.Collections.Generic;
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

public sealed class ChannelMessageReactionControllerTest
{
    [Fact]
    public async Task Set_ShouldBroadcastFullStateOnlyWhenAuthoritativeStateChanged()
    {
        var request = new SetChatMessageReactionDto
        {
            ChannelId = 70001,
            MessageId = 90001,
            EmojiType = "unicode",
            EmojiValue = "👍",
            IsActive = true,
            ClientOperationId = "operation-90001"
        };
        var state = new ChatMessageReactionStateVo
        {
            VoMessageId = 90001,
            VoRevision = 8,
            VoItems =
            [
                new ReactionSummaryVo
                {
                    VoEmojiType = "unicode",
                    VoEmojiValue = "👍",
                    VoCount = 2,
                    VoIsReacted = true
                }
            ]
        };
        var service = new Mock<IChatMessageReactionService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.SetAsync(30000, 20001, "Tester", request))
            .ReturnsAsync(new ChatMessageReactionMutationVo
            {
                VoState = state,
                VoChanged = true
            });
        var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        clientProxy
            .Setup(proxy => proxy.SendCoreAsync(
                "MessageReactionsChanged",
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
        Assert.Same(state, Assert.IsType<ChatMessageReactionMutationVo>(result.ResponseData).VoState);
        clientProxy.VerifyAll();
    }

    [Fact]
    public async Task GetStates_ShouldUseCurrentTenantAndUserWithoutBroadcasting()
    {
        var request = new GetChatMessageReactionStatesDto
        {
            ChannelId = 70001,
            MessageIds = [90001]
        };
        var states = new List<ChatMessageReactionStateVo>
        {
            new() { VoMessageId = 90001, VoRevision = 0 }
        };
        var service = new Mock<IChatMessageReactionService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.GetStatesAsync(30000, 20001, request))
            .ReturnsAsync(states);
        var controller = CreateController(service.Object, Mock.Of<IHubContext<ChatHub>>());

        var result = await controller.GetStates(request);

        Assert.True(result.IsSuccess);
        Assert.Same(states, result.ResponseData);
    }

    private static ChannelMessageReactionController CreateController(
        IChatMessageReactionService service,
        IHubContext<ChatHub> hubContext)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            TenantId = 30000,
            UserId = 20001,
            UserName = "Tester"
        });
        return new ChannelMessageReactionController(service, hubContext, currentUserAccessor.Object);
    }
}
