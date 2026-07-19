using System.Linq;
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

public sealed class ChannelReadStateControllerTest
{
    [Fact]
    public async Task Advance_ShouldBroadcastPersonalUnreadAndDataFreeReceiptInvalidation()
    {
        var request = new AdvanceChannelReadStateDto
        {
            ChannelId = 70001,
            ReadThroughMessageId = 90001
        };
        var state = new ChannelReadStateVo
        {
            VoChannelId = 70001,
            VoLastReadMessageId = 90001,
            VoUnreadCount = 2,
            VoHasMention = true,
            VoChanged = true
        };
        var service = new Mock<IChatReadReceiptService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.AdvanceAsync(30000, 20001, "Tester", request))
            .ReturnsAsync(new ChannelReadStateAdvanceResult(state, true));
        var userProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        userProxy
            .Setup(proxy => proxy.SendCoreAsync(
                "ChannelUnreadChanged",
                It.Is<object?[]>(arguments => arguments.Length == 1),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var channelProxy = new Mock<IClientProxy>(MockBehavior.Strict);
        channelProxy
            .Setup(proxy => proxy.SendCoreAsync(
                "ReadReceiptsChanged",
                It.Is<object?[]>(arguments => HasOnlyChannelId(arguments, 70001)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubClients = new Mock<IHubClients>(MockBehavior.Strict);
        hubClients.Setup(clients => clients.Group("user:20001")).Returns(userProxy.Object);
        hubClients.Setup(clients => clients.Group("channel:30000:70001")).Returns(channelProxy.Object);
        var hubContext = new Mock<IHubContext<ChatHub>>(MockBehavior.Strict);
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);
        var controller = CreateController(service.Object, hubContext.Object);

        var result = await controller.Advance(request);

        Assert.True(result.IsSuccess);
        Assert.Same(state, result.ResponseData);
        userProxy.VerifyAll();
        channelProxy.VerifyAll();
    }

    [Fact]
    public async Task Advance_ShouldNotBroadcastWhenCursorDidNotAdvance()
    {
        var request = new AdvanceChannelReadStateDto
        {
            ChannelId = 70001,
            ReadThroughMessageId = 90001
        };
        var state = new ChannelReadStateVo
        {
            VoChannelId = 70001,
            VoLastReadMessageId = 90002,
            VoChanged = false
        };
        var service = new Mock<IChatReadReceiptService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.AdvanceAsync(30000, 20001, "Tester", request))
            .ReturnsAsync(new ChannelReadStateAdvanceResult(state, false));
        var hubContext = new Mock<IHubContext<ChatHub>>(MockBehavior.Strict);
        var controller = CreateController(service.Object, hubContext.Object);

        var result = await controller.Advance(request);

        Assert.True(result.IsSuccess);
        hubContext.VerifyGet(context => context.Clients, Times.Never);
    }

    private static bool HasOnlyChannelId(object?[] arguments, long expectedChannelId)
    {
        if (arguments.Length != 1 || arguments[0] == null)
        {
            return false;
        }

        var properties = arguments[0]!.GetType().GetProperties();
        return properties.Length == 1 &&
               properties.Single().Name == "channelId" &&
               Equals(properties.Single().GetValue(arguments[0]), expectedChannelId);
    }

    private static ChannelReadStateController CreateController(
        IChatReadReceiptService service,
        IHubContext<ChatHub> hubContext)
    {
        var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
        currentUserAccessor.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            TenantId = 30000,
            UserId = 20001,
            UserName = "Tester"
        });
        return new ChannelReadStateController(service, hubContext, currentUserAccessor.Object);
    }
}
