using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>表情回应控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("表情回应")]
public class ReactionController : ControllerBase
{
    private readonly IReactionService _reactionService;
    private readonly IHttpContextUser _httpContextUser;

    public ReactionController(IReactionService reactionService, IHttpContextUser httpContextUser)
    {
        _reactionService = reactionService;
        _httpContextUser = httpContextUser;
    }

    /// <summary>获取单目标回应汇总</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetSummary([FromQuery] string targetType, [FromQuery] long targetId)
    {
        try
        {
            var summary = await _reactionService.GetSummaryAsync(targetType, targetId, _httpContextUser.UserId);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = summary
            };
        }
        catch (BusinessException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = ex.StatusCode,
                MessageInfo = ex.Message,
                Code = ex.ErrorCode
            };
        }
    }

    /// <summary>批量获取多目标回应汇总</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> BatchGetSummary([FromBody] BatchGetReactionSummaryDto request)
    {
        if (!ModelState.IsValid)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "请求参数验证失败",
                Code = "InvalidArgument"
            };
        }

        try
        {
            var summary = await _reactionService.BatchGetSummaryAsync(
                request.TargetType,
                request.TargetIds,
                _httpContextUser.UserId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = summary
            };
        }
        catch (BusinessException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = ex.StatusCode,
                MessageInfo = ex.Message,
                Code = ex.ErrorCode
            };
        }
    }

    /// <summary>切换回应（添加/取消）</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Toggle([FromBody] ToggleReactionDto request)
    {
        if (!ModelState.IsValid)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "请求参数验证失败",
                Code = "InvalidArgument"
            };
        }

        try
        {
            var summary = await _reactionService.ToggleAsync(
                request,
                _httpContextUser.UserId,
                _httpContextUser.UserName,
                _httpContextUser.TenantId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "操作成功",
                ResponseData = summary
            };
        }
        catch (BusinessException ex)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = ex.StatusCode,
                MessageInfo = ex.Message,
                Code = ex.ErrorCode
            };
        }
    }
}
