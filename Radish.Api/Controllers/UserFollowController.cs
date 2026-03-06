using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Authorize(Policy = "Client")]
public class UserFollowController : ControllerBase
{
    private readonly IUserFollowService _userFollowService;
    private readonly IHttpContextUser _httpContextUser;

    public UserFollowController(IUserFollowService userFollowService, IHttpContextUser httpContextUser)
    {
        _userFollowService = userFollowService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>关注用户</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Follow([FromBody] FollowUserDto dto)
    {
        if (dto.TargetUserId <= 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "目标用户 ID 无效");
        }

        if (dto.TargetUserId == _httpContextUser.UserId)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "不能关注自己");
        }

        try
        {
            var changed = await _userFollowService.FollowAsync(
                _httpContextUser.UserId,
                dto.TargetUserId,
                _httpContextUser.TenantId,
                _httpContextUser.UserName);
            var status = await _userFollowService.GetFollowStatusAsync(_httpContextUser.UserId, dto.TargetUserId);

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
            return BuildError(HttpStatusCodeEnum.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BuildError(HttpStatusCodeEnum.NotFound, ex.Message);
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
            return BuildError(HttpStatusCodeEnum.BadRequest, "目标用户 ID 无效");
        }

        if (dto.TargetUserId == _httpContextUser.UserId)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "不能取消关注自己");
        }

        var changed = await _userFollowService.UnfollowAsync(
            _httpContextUser.UserId,
            dto.TargetUserId,
            _httpContextUser.UserName);
        var status = await _userFollowService.GetFollowStatusAsync(_httpContextUser.UserId, dto.TargetUserId);

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
            return BuildError(HttpStatusCodeEnum.BadRequest, "目标用户 ID 无效");
        }

        var status = await _userFollowService.GetFollowStatusAsync(_httpContextUser.UserId, targetUserId);
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
        var result = await _userFollowService.GetMyFollowersAsync(_httpContextUser.UserId, pageIndex, pageSize);
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
        var result = await _userFollowService.GetMyFollowingAsync(_httpContextUser.UserId, pageIndex, pageSize);
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
        var result = await _userFollowService.GetMyFollowingFeedAsync(_httpContextUser.UserId, pageIndex, pageSize);
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
        var summary = await _userFollowService.GetMyFollowSummaryAsync(_httpContextUser.UserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = summary
        };
    }

    private static MessageModel BuildError(HttpStatusCodeEnum statusCode, string message)
    {
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = message
        };
    }
}
