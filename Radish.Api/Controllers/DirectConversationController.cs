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

/// <summary>一对一私聊生命周期控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("一对一私聊")]
[Authorize]
public sealed class DirectConversationController : ControllerBase
{
    private readonly IDirectConversationService _directConversationService;
    private readonly IHubContext<ChatHub> _chatHubContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public DirectConversationController(
        IDirectConversationService directConversationService,
        IHubContext<ChatHub> chatHubContext,
        ICurrentUserAccessor currentUserAccessor)
    {
        _directConversationService = directConversationService;
        _chatHubContext = chatHubContext;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>幂等获取或创建一对一私聊</summary>
    [HttpPost]
    public async Task<MessageModel> GetOrCreate([FromBody] GetOrCreateDirectConversationDto request)
    {
        var result = await _directConversationService.GetOrCreateAsync(
            Current.TenantId,
            Current.UserId,
            request.TargetUserId,
            Current.UserName);
        if (result.Changed && result.Conversation.VoDirectRequestStatus == "accepted")
        {
            await PushStateChangedAsync(result.Conversation.VoChannelId, [request.TargetUserId]);
        }

        return Success("获取会话成功", result.Conversation);
    }

    /// <summary>接受私聊请求</summary>
    [HttpPost("{channelId:long}")]
    public async Task<MessageModel> Accept(long channelId)
    {
        var result = await _directConversationService.AcceptAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            Current.UserName);
        await PushMutationAsync(result);
        return Success("接受私聊请求成功", result.Conversation);
    }

    /// <summary>拒绝私聊请求</summary>
    [HttpPost("{channelId:long}")]
    public async Task<MessageModel> Decline(long channelId)
    {
        var result = await _directConversationService.DeclineAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            Current.UserName);
        await PushMutationAsync(result);
        return Success("拒绝私聊请求成功", result.Conversation);
    }

    /// <summary>阻断当前一对一会话</summary>
    [HttpPost("{channelId:long}")]
    public async Task<MessageModel> Block(long channelId)
    {
        var result = await _directConversationService.BlockAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            Current.UserName);
        await PushMutationAsync(result);
        return Success("阻断会话成功", result.Conversation);
    }

    /// <summary>解除当前用户发起的会话阻断</summary>
    [HttpPost("{channelId:long}")]
    public async Task<MessageModel> Unblock(long channelId)
    {
        var result = await _directConversationService.UnblockAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            Current.UserName);
        await PushMutationAsync(result);
        return Success("解除会话阻断成功", result.Conversation);
    }

    /// <summary>设置当前用户的会话归档状态</summary>
    [HttpPut("{channelId:long}")]
    public async Task<MessageModel> SetArchived(
        long channelId,
        [FromBody] SetDirectConversationArchivedDto request)
    {
        var result = await _directConversationService.SetArchivedAsync(
            Current.TenantId,
            Current.UserId,
            channelId,
            request.Archived,
            Current.UserName);
        if (result.Changed)
        {
            await PushStateChangedAsync(channelId, [Current.UserId]);
        }

        return Success(request.Archived ? "归档会话成功" : "取消归档成功", result.Conversation);
    }

    private async Task PushMutationAsync(DirectConversationMutationResult result)
    {
        if (!result.Changed)
        {
            return;
        }

        await PushStateChangedAsync(
            result.Conversation.VoChannelId,
            [Current.UserId, result.Conversation.VoPeerUserId]);
    }

    private async Task PushStateChangedAsync(long channelId, IReadOnlyCollection<long> userIds)
    {
        foreach (var userId in userIds.Where(id => id > 0).Distinct())
        {
            await _chatHubContext.Clients.Group($"user:{userId}")
                .SendAsync("ConversationStateChanged", new { channelId });
        }
    }

    private static MessageModel Success(string message, object responseData)
    {
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = message,
            ResponseData = responseData
        };
    }
}
