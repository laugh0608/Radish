using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>帖子轻回应控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛轻回应")]
public class PostQuickReplyController : ControllerBase
{
    private readonly IPostQuickReplyService _postQuickReplyService;
    private readonly IContentModerationService _contentModerationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PostQuickReplyController(
        IPostQuickReplyService postQuickReplyService,
        IContentModerationService contentModerationService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _postQuickReplyService = postQuickReplyService;
        _contentModerationService = contentModerationService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>获取帖子最近的轻回应</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> GetRecentByPostId(long postId, int take = 30)
    {
        try
        {
            var wall = await _postQuickReplyService.GetRecentByPostIdAsync(postId, take);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = wall
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
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = ex.Message
            };
        }
    }

    /// <summary>获取当前登录用户的轻回应分页</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.Client)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMine(int pageIndex = 1, int pageSize = 20)
    {
        var safePageIndex = pageIndex < 1 ? 1 : pageIndex;
        var safePageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

        var (items, total) = await _postQuickReplyService.GetMinePageAsync(Current.UserId, safePageIndex, safePageSize);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = new VoPagedResult<UserPostQuickReplyVo>
            {
                VoItems = items,
                VoTotal = total,
                VoPageIndex = safePageIndex,
                VoPageSize = safePageSize
            }
        };
    }

    /// <summary>创建轻回应</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    public async Task<MessageModel> Create([FromBody] CreatePostQuickReplyDto request)
    {
        if (Current.UserId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Unauthorized,
                MessageInfo = "请先登录后再发布轻回应"
            };
        }

        var publishPermission = await _contentModerationService.GetPublishPermissionAsync(Current.UserId);
        if (!publishPermission.VoCanPublish)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Forbidden,
                MessageInfo = publishPermission.VoDenyReason ?? "当前状态无法发布内容"
            };
        }

        try
        {
            var quickReply = await _postQuickReplyService.CreateAsync(
                request,
                Current.UserId,
                Current.UserName,
                Current.TenantId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "发布成功",
                ResponseData = quickReply
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
            var statusCode = ex.Message.Contains("无权", StringComparison.Ordinal)
                ? HttpStatusCodeEnum.Forbidden
                : HttpStatusCodeEnum.BadRequest;

            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)statusCode,
                MessageInfo = ex.Message
            };
        }
    }

    /// <summary>删除轻回应</summary>
    [HttpDelete]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Delete(long quickReplyId)
    {
        if (Current.UserId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Unauthorized,
                MessageInfo = "请先登录后再删除轻回应"
            };
        }

        try
        {
            await _postQuickReplyService.DeleteAsync(
                quickReplyId,
                Current.UserId,
                Current.UserName,
                Current.IsSystemOrAdmin());

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "删除成功"
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
            var statusCode = ex.Message.Contains("不存在", StringComparison.Ordinal)
                ? HttpStatusCodeEnum.NotFound
                : HttpStatusCodeEnum.Forbidden;

            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)statusCode,
                MessageInfo = ex.Message
            };
        }
    }
}
