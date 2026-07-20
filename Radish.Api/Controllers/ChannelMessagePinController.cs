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

/// <summary>聊天消息共享置顶控制器。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("聊天室")]
[Authorize]
public sealed class ChannelMessagePinController : ControllerBase
{
    private readonly IChatMessagePinService _pinService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelMessagePinController(
        IChatMessagePinService pinService,
        IHubContext<ChatHub> chatHubContext,
        ICurrentUserAccessor currentUserAccessor)
    {
        _pinService = pinService;
        _chatHubContext = chatHubContext;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>读取当前可见频道的共享消息置顶快照。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetState([FromQuery] long channelId)
    {
        var state = await _pinService.GetStateAsync(Current.TenantId, Current.UserId, channelId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = state
        };
    }

    /// <summary>把指定消息的共享置顶设置为目标状态。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    public async Task<MessageModel> Set([FromBody] SetChatMessagePinDto request)
    {
        var mutation = await _pinService.SetAsync(
            Current.TenantId,
            Current.UserId,
            Current.UserName,
            Current.IsSystemOrAdmin(),
            request);

        if (mutation.VoChanged)
        {
            var channelGroup = ChatHub.BuildChannelGroup(Current.TenantId, request.ChannelId);
            await _chatHubContext.Clients.Group(channelGroup)
                .SendAsync("MessagePinsChanged", mutation.VoState);
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "置顶状态已更新",
            ResponseData = mutation
        };
    }
}
