using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Radish.Api.Filters;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>个人频道已读游标控制器。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("聊天室")]
[Authorize]
public sealed class ChannelReadStateController : ControllerBase
{
    private readonly IChatReadReceiptService _readReceiptService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelReadStateController(
        IChatReadReceiptService readReceiptService,
        IHubContext<ChatHub> chatHubContext,
        ICurrentUserAccessor currentUserAccessor)
    {
        _readReceiptService = readReceiptService;
        _chatHubContext = chatHubContext;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>把当前账号的已读游标单调推进到实际展示过的消息。</summary>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Advance([FromBody] AdvanceChannelReadStateDto request)
    {
        var result = await _readReceiptService.AdvanceAsync(
            Current.TenantId,
            Current.UserId,
            Current.UserName,
            request);

        if (result.State.VoChanged)
        {
            await _chatHubContext.Clients.Group($"user:{Current.UserId}")
                .SendAsync("ChannelUnreadChanged", new
                {
                    channelId = result.State.VoChannelId,
                    unreadCount = result.State.VoUnreadCount,
                    hasMention = result.State.VoHasMention
                });
        }

        if (result.ReceiptsChanged)
        {
            var channelGroup = ChatHub.BuildChannelGroup(Current.TenantId, result.State.VoChannelId);
            await _chatHubContext.Clients.Group(channelGroup)
                .SendAsync("ReadReceiptsChanged", new
                {
                    channelId = result.State.VoChannelId
                });
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "频道已读状态已更新",
            ResponseData = result.State
        };
    }
}
