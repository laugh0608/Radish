using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.LogTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Api.Controllers;

/// <summary>论坛投票控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("论坛投票管理")]
public class PollController : ControllerBase
{
    private readonly IPostPollService _postPollService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public PollController(IPostPollService postPollService, ICurrentUserAccessor currentUserAccessor)
    {
        _postPollService = postPollService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>
    /// 按帖子获取投票详情
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetByPostId(long postId)
    {
        try
        {
            var viewerUserId = Current.UserId > 0 ? (long?)Current.UserId : null;
            var result = await _postPollService.GetByPostIdAsync(postId, viewerUserId);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = result
            };
        }
        catch (ArgumentException ex)
        {
            return BuildErrorResponse(ex);
        }
        catch (BusinessException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    /// <summary>
    /// 提交投票
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Vote([FromBody] VotePollDto request)
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

        if (Current.UserId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Unauthorized,
                MessageInfo = "请先登录后再投票"
            };
        }

        try
        {
            var result = await _postPollService.VoteAsync(Current.UserId, Current.UserName, request);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "投票成功",
                ResponseData = result
            };
        }
        catch (ArgumentException ex)
        {
            return BuildErrorResponse(ex);
        }
        catch (BusinessException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    /// <summary>
    /// 手动结束投票
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Close([FromBody] ClosePollDto request)
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

        if (Current.UserId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.Unauthorized,
                MessageInfo = "请先登录后再结束投票"
            };
        }

        try
        {
            var result = await _postPollService.CloseAsync(request.PostId, Current.UserId, Current.UserName);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "结束投票成功",
                ResponseData = result
            };
        }
        catch (ArgumentException ex)
        {
            return BuildErrorResponse(ex);
        }
        catch (BusinessException ex)
        {
            return BuildErrorResponse(ex);
        }
    }

    private static MessageModel BuildErrorResponse(ArgumentException exception)
    {
        var response = new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)HttpStatusCodeEnum.BadRequest,
            MessageInfo = exception.Message
        };
        if (exception is not PollInputValidationException)
        {
            Log.ForContext("EventCode", "poll.request_failed")
                .Error("Poll request consumed a failure ({failureKind})", RuntimeFailureSummary.Classify(exception));
        }
        return response;
    }

    private static MessageModel BuildErrorResponse(BusinessException exception)
    {
        var response = new MessageModel
        {
            IsSuccess = false,
            StatusCode = exception.StatusCode,
            MessageInfo = exception.Message,
            Code = exception.ErrorCode,
            MessageKey = exception.MessageKey
        };
        if (exception.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            Log.ForContext("EventCode", "http.failed")
                .ForContext("SourceCategory", "http")
                .Error("Request failed with {statusCode}; kind={failureKind}", exception.StatusCode, RuntimeFailureSummary.Classify(exception));
        }
        return response;
    }
}
