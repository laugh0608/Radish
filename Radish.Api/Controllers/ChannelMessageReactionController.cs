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

/// <summary>聊天消息回应控制器。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("聊天室")]
[Authorize]
public sealed class ChannelMessageReactionController : ControllerBase
{
    private readonly IChatMessageReactionService _reactionService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelMessageReactionController(
        IChatMessageReactionService reactionService,
        IHubContext<ChatHub> chatHubContext,
        ICurrentUserAccessor currentUserAccessor)
    {
        _reactionService = reactionService;
        _chatHubContext = chatHubContext;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>批量读取当前可见消息的回应状态。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetStates([FromBody] GetChatMessageReactionStatesDto request)
    {
        var states = await _reactionService.GetStatesAsync(
            Current.TenantId,
            Current.UserId,
            request);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = states
        };
    }

    /// <summary>把当前用户的指定回应设置为目标状态。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    public async Task<MessageModel> Set([FromBody] SetChatMessageReactionDto request)
    {
        var mutation = await _reactionService.SetAsync(
            Current.TenantId,
            Current.UserId,
            Current.UserName,
            request);

        if (mutation.VoChanged)
        {
            var channelGroup = ChatHub.BuildChannelGroup(Current.TenantId, request.ChannelId);
            await _chatHubContext.Clients.Group(channelGroup)
                .SendAsync("MessageReactionsChanged", mutation.VoState);
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "回应状态已更新",
            ResponseData = mutation
        };
    }
}
