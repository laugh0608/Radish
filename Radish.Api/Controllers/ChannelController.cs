using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>聊天室频道控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("聊天室")]
[Authorize]
public class ChannelController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelController(IChatService chatService, ICurrentUserAccessor currentUserAccessor)
    {
        _chatService = chatService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>获取可见频道列表</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetList()
    {
        var channels = await _chatService.GetChannelListAsync(Current.TenantId, Current.UserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = channels
        };
    }

    /// <summary>获取频道详情</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetDetail(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "频道 Id 无效"
            };
        }

        var channel = await _chatService.GetChannelDetailAsync(Current.TenantId, Current.UserId, id);
        if (channel == null)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "频道不存在"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = channel
        };
    }

    /// <summary>获取频道在线成员</summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetOnlineMembers(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "频道 Id 无效"
            };
        }

        var members = await _chatService.GetOnlineMembersAsync(Current.TenantId, id);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = members
        };
    }
}
