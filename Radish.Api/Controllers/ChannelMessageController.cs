using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Radish.Api.Hubs;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>聊天室消息控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("聊天室")]
[Authorize]
public class ChannelMessageController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ChannelMessageController(
        IChatService chatService,
        IHubContext<ChatHub> chatHubContext,
        ICurrentUserAccessor currentUserAccessor)
    {
        _chatService = chatService;
        _chatHubContext = chatHubContext;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>获取频道历史消息</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetHistory([FromQuery] long channelId, [FromQuery] long? beforeMessageId, [FromQuery] int pageSize = 50)
    {
        if (channelId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "频道 Id 无效"
            };
        }

        var messages = await _chatService.GetHistoryAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            beforeMessageId,
            pageSize);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = messages
        };
    }

    /// <summary>发送频道消息</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Send([FromBody] SendChannelMessageDto request)
    {
        if (!ModelState.IsValid)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "请求参数验证失败"
            };
        }

        try
        {
            var messageVo = await _chatService.SendMessageAsync(
                Current.TenantId,
                Current.UserId,
                Current.UserName,
                request);
            messageVo.VoClientRequestId = string.IsNullOrWhiteSpace(request.ClientRequestId)
                ? null
                : request.ClientRequestId.Trim();

            var channelGroup = ChatHub.BuildChannelGroup(Current.TenantId, request.ChannelId);
            await _chatHubContext.Clients.Group(channelGroup).SendAsync("MessageReceived", messageVo);

            var audienceUserIds = (await _chatService.GetChannelAudienceUserIdsAsync(Current.TenantId, request.ChannelId))
                .Where(userId => userId != Current.UserId)
                .Distinct()
                .ToList();

            foreach (var targetUserId in audienceUserIds)
            {
                var unreadState = await _chatService.GetChannelUnreadStateAsync(Current.TenantId, targetUserId, request.ChannelId);
                await _chatHubContext.Clients.Group($"user:{targetUserId}")
                    .SendAsync("ChannelUnreadChanged", new
                    {
                        channelId = unreadState.VoChannelId,
                        unreadCount = unreadState.VoUnreadCount,
                        hasMention = unreadState.VoHasMention
                    });
            }

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "发送成功",
                ResponseData = messageVo
            };
        }
        catch (ArgumentException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
        catch (InvalidOperationException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = ex.Message
            };
        }
    }

    /// <summary>撤回消息</summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Recall(long id)
    {
        if (id <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "消息 Id 无效"
            };
        }

        var canRecallOthers = Current.IsSystemOrAdmin();
        var channelId = await _chatService.RecallMessageAsync(
            Current.TenantId,
            Current.UserId,
            Current.UserName,
            id,
            canRecallOthers);

        if (!channelId.HasValue)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "消息不存在或无权撤回"
            };
        }

        var channelGroup = ChatHub.BuildChannelGroup(Current.TenantId, channelId.Value);
        await _chatHubContext.Clients.Group(channelGroup)
            .SendAsync("MessageRecalled", new { channelId = channelId.Value, messageId = id });

        var audienceUserIds = (await _chatService.GetChannelAudienceUserIdsAsync(Current.TenantId, channelId.Value))
            .Distinct()
            .ToList();

        foreach (var targetUserId in audienceUserIds)
        {
            var unreadState = await _chatService.GetChannelUnreadStateAsync(Current.TenantId, targetUserId, channelId.Value);
            await _chatHubContext.Clients.Group($"user:{targetUserId}")
                .SendAsync("ChannelUnreadChanged", new
                {
                    channelId = unreadState.VoChannelId,
                    unreadCount = unreadState.VoUnreadCount,
                    hasMention = unreadState.VoHasMention
                });
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "撤回成功",
            ResponseData = true
        };
    }
}
