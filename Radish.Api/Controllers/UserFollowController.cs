using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>用户关系链控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("用户关系链")]
[Authorize(Policy = AuthorizationPolicies.Client)]
[ApiErrorContract]
public class UserFollowController : ControllerBase
{
    private readonly IUserFollowService _userFollowService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public UserFollowController(
        IUserFollowService userFollowService,
        ICurrentUserAccessor currentUserAccessor,
        IStringLocalizer<Errors> errorsLocalizer)
    {
        _userFollowService = userFollowService;
        _currentUserAccessor = currentUserAccessor;
        _errorsLocalizer = errorsLocalizer;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>关注用户</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Follow([FromBody] FollowUserDto dto)
    {
        if (dto.TargetUserId <= 0)
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                "目标用户 ID 无效",
                "UserFollow.InvalidTargetUser",
                "error.user_follow.invalid_target_user");
        }

        if (dto.TargetUserId == Current.UserId)
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                "不能关注自己",
                "UserFollow.SelfFollowRejected",
                "error.user_follow.self_follow_rejected");
        }

        try
        {
            var changed = await _userFollowService.FollowAsync(
                Current.UserId,
                dto.TargetUserId,
                Current.TenantId,
                Current.UserName);
            var status = await _userFollowService.GetFollowStatusAsync(Current.UserId, dto.TargetUserId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = changed ? "关注成功" : "已关注该用户",
                ResponseData = status
            };
        }
        catch (ArgumentException ex)
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                ex.Message,
                "UserFollow.FollowRejected",
                "error.user_follow.follow_rejected");
        }
        catch (InvalidOperationException ex)
        {
            return BuildError(
                HttpStatusCodeEnum.NotFound,
                ex.Message,
                "UserFollow.TargetUnavailable",
                "error.user_follow.target_unavailable");
        }
    }

    /// <summary>取消关注用户</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> Unfollow([FromBody] FollowUserDto dto)
    {
        if (dto.TargetUserId <= 0)
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                "目标用户 ID 无效",
                "UserFollow.InvalidTargetUser",
                "error.user_follow.invalid_target_user");
        }

        if (dto.TargetUserId == Current.UserId)
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                "不能取消关注自己",
                "UserFollow.SelfUnfollowRejected",
                "error.user_follow.self_unfollow_rejected");
        }

        var changed = await _userFollowService.UnfollowAsync(
            Current.UserId,
            dto.TargetUserId,
            Current.UserName);
        var status = await _userFollowService.GetFollowStatusAsync(Current.UserId, dto.TargetUserId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = changed ? "已取消关注" : "当前未关注该用户",
            ResponseData = status
        };
    }

    /// <summary>获取指定用户的关注状态</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> GetFollowStatus(long targetUserId)
    {
        if (targetUserId <= 0)
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                "目标用户 ID 无效",
                "UserFollow.InvalidTargetUser",
                "error.user_follow.invalid_target_user");
        }

        var status = await _userFollowService.GetFollowStatusAsync(Current.UserId, targetUserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = status
        };
    }

    /// <summary>获取我的粉丝列表</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyFollowers(int pageIndex = 1, int pageSize = 20)
    {
        var result = await _userFollowService.GetMyFollowersAsync(Current.UserId, pageIndex, pageSize);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    /// <summary>获取我的关注列表</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyFollowing(int pageIndex = 1, int pageSize = 20)
    {
        var result = await _userFollowService.GetMyFollowingAsync(Current.UserId, pageIndex, pageSize);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    /// <summary>获取我的关系链动态流</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyFollowingFeed(int pageIndex = 1, int pageSize = 20)
    {
        var result = await _userFollowService.GetMyFollowingFeedAsync(Current.UserId, pageIndex, pageSize);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    /// <summary>获取我的分发流（推荐/热门/最新）</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> GetMyDistributionFeed(string streamType = "recommend", int pageIndex = 1, int pageSize = 20)
    {
        var normalizedStreamType = streamType?.Trim().ToLowerInvariant() ?? "recommend";
        if (normalizedStreamType is not ("recommend" or "hot" or "newest" or "hottest"))
        {
            return BuildError(
                HttpStatusCodeEnum.BadRequest,
                "streamType 仅支持 recommend、hot、newest",
                "UserFollow.InvalidStreamType",
                "error.user_follow.invalid_stream_type");
        }

        var result = await _userFollowService.GetMyDistributionFeedAsync(
            Current.UserId,
            normalizedStreamType,
            pageIndex,
            pageSize);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    /// <summary>获取我的关系链汇总</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMySummary()
    {
        var summary = await _userFollowService.GetMyFollowSummaryAsync(Current.UserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = summary
        };
    }

    private MessageModel BuildError(
        HttpStatusCodeEnum statusCode,
        string fallbackMessage,
        string code,
        string messageKey)
    {
        var localizedMessage = _errorsLocalizer[messageKey];
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = localizedMessage.ResourceNotFound ? fallbackMessage : localizedMessage.Value,
            Code = code,
            MessageKey = messageKey
        };
    }
}
