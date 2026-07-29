using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>当前用户的私有帖子收藏。</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("帖子收藏")]
[Authorize(Policy = AuthorizationPolicies.Client)]
[ApiErrorContract]
public sealed class PostBookmarkController : ControllerBase
{
    private readonly IUserPostBookmarkService _bookmarkService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PostBookmarkController(
        IUserPostBookmarkService bookmarkService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _bookmarkService = bookmarkService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    [HttpPost]
    [ProducesResponseType(typeof(MessageModel<PostBookmarkStateVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> SetState([FromBody] SetPostBookmarkStateDto request)
    {
        var result = long.TryParse(request.PostIdentifier.Trim(), out var legacyPostId) &&
                     legacyPostId > 0
            ? await _bookmarkService.SetStateByLegacyPostIdAsync(
                Current.TenantId,
                Current.UserId,
                Current.UserName,
                legacyPostId,
                request.IsBookmarked)
            : await _bookmarkService.SetStateAsync(
                Current.TenantId,
                Current.UserId,
                Current.UserName,
                request.PostIdentifier,
                request.IsBookmarked);
        return Success(result.VoIsBookmarked ? "收藏成功" : "已取消收藏", result);
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(MessageModel<VoPagedResult<UserPostBookmarkVo>>),
        StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMine(int pageIndex = 1, int pageSize = 20)
    {
        var result = await _bookmarkService.GetMineAsync(
            Current.TenantId,
            Current.UserId,
            pageIndex,
            pageSize);
        return Success("获取收藏列表成功", result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MessageModel<PostBookmarkRemoveVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> Remove([FromBody] RemovePostBookmarkDto request)
    {
        var result = await _bookmarkService.RemoveAsync(
            Current.TenantId,
            Current.UserId,
            Current.UserName,
            request.BookmarkIdentifier);
        return Success("已移除收藏", result);
    }

    private static MessageModel Success(string message, object data) => new()
    {
        IsSuccess = true,
        StatusCode = (int)HttpStatusCodeEnum.Success,
        MessageInfo = message,
        ResponseData = data
    };
}
