#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
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

[TestSubject(typeof(ChannelMessageController))]
public class ChannelMessageControllerTest
{
    [Fact]
    public async Task GetHistory_Should_Return_BadRequest_When_ChannelId_Invalid()
    {
        var serviceMock = CreateChatServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetHistory(0, null, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetHistory_Should_Return_BadRequest_When_Before_And_After_Are_Both_Provided()
    {
        var serviceMock = CreateChatServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetHistory(1, 10, 20, 50);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetHistory_Should_Return_Messages_When_Request_Valid()
    {
        var serviceMock = CreateChatServiceMock();
        serviceMock
            .Setup(s => s.GetHistoryAsync(0, 10001, 1, null, null, 50))
            .ReturnsAsync(new List<ChannelMessageVo>
            {
                new()
                {
                    VoId = 90001,
                    VoChannelId = 1,
                    VoUserId = 10001,
                    VoUserName = "Tester",
                    VoContent = "hello",
                    VoIsRecalled = false
                }
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetHistory(1, null, null, 50);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<List<ChannelMessageVo>>(result.ResponseData);
        Assert.Single(payload);
        Assert.Equal(90001, payload[0].VoId);
    }

    [Fact]
    public async Task GetMessageWindow_Should_Return_BadRequest_When_MessageId_Invalid()
    {
        var serviceMock = CreateChatServiceMock();
        var controller = CreateController(serviceMock.Object);

        var result = await controller.GetMessageWindow(1, 0);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetMessageWindow_Should_Return_Window_When_Request_Valid()
    {
        var serviceMock = CreateChatServiceMock();
        serviceMock
            .Setup(s => s.GetMessageWindowAsync(0, 10001, 1, 90001, 25, 25))
            .ReturnsAsync(new ChannelMessageWindowVo
            {
                VoChannelId = 1,
                VoAnchorMessageId = 90001,
                VoHasMoreBefore = true,
                VoHasMoreAfter = false,
                VoMessages =
                {
                    new ChannelMessageVo
                    {
                        VoId = 90001,
                        VoChannelId = 1,
                        VoUserId = 10001,
                        VoUserName = "Tester",
                        VoContent = "hello",
                        VoIsRecalled = false
                    }
                }
            });

        var controller = CreateController(serviceMock.Object);
        var result = await controller.GetMessageWindow(1, 90001, 25, 25);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.StatusCode);
        var payload = Assert.IsType<ChannelMessageWindowVo>(result.ResponseData);
        Assert.Equal(90001, payload.VoAnchorMessageId);
        Assert.Single(payload.VoMessages);
    }

    private static ChannelMessageController CreateController(IChatService chatService)
    {
        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(x => x.Current).Returns(new CurrentUser
        {
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        return new ChannelMessageController(
            chatService,
            CreateHubContextMock().Object,
            currentUserAccessorMock.Object);
    }

    private static Mock<IChatService> CreateChatServiceMock()
    {
        return new Mock<IChatService>(MockBehavior.Strict);
    }

    private static Mock<IHubContext<ChatHub>> CreateHubContextMock()
    {
        var hubContextMock = new Mock<IHubContext<ChatHub>>();
        var hubClientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        clientProxyMock
            .Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        hubClientsMock
            .Setup(clients => clients.Group(It.IsAny<string>()))
            .Returns(clientProxyMock.Object);

        hubContextMock
            .SetupGet(context => context.Clients)
            .Returns(hubClientsMock.Object);

        return hubContextMock;
    }
}
