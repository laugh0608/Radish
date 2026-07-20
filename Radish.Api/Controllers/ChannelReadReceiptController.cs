using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>发送者受限的聊天消息阅读回执控制器。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("聊天室")]
[Authorize]
public sealed class ChannelReadReceiptController : ControllerBase
{
    private readonly IChatReadReceiptService _readReceiptService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelReadReceiptController(
        IChatReadReceiptService readReceiptService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _readReceiptService = readReceiptService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>批量读取当前账号自己所发消息的回执摘要。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetSummaries([FromBody] GetChatReadReceiptSummariesDto request)
    {
        var summaries = await _readReceiptService.GetSummariesAsync(
            Current.TenantId,
            Current.UserId,
            request);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = summaries
        };
    }

    /// <summary>分页读取一条普通 Private 消息的当前有效读者。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    public async Task<MessageModel> GetReaders(
        [FromQuery] long channelId,
        [FromQuery] long messageId,
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50)
    {
        var readers = await _readReceiptService.GetReadersAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            messageId,
            cursor,
            pageSize);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = readers
        };
    }
}
