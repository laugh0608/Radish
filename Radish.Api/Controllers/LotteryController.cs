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

/// <summary>论坛抽奖控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("论坛抽奖管理")]
public class LotteryController : ControllerBase
{
    private readonly IPostLotteryService _postLotteryService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public LotteryController(IPostLotteryService postLotteryService, ICurrentUserAccessor currentUserAccessor)
    {
        _postLotteryService = postLotteryService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>按帖子获取抽奖详情</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetByPostId(long postId)
    {
        try
        {
            var viewerUserId = Current.UserId > 0 ? (long?)Current.UserId : null;
            var result = await _postLotteryService.GetByPostIdAsync(postId, viewerUserId);
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

    /// <summary>手动开奖</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> Draw([FromBody] DrawLotteryDto request)
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
                MessageInfo = "请先登录后再开奖"
            };
        }

        try
        {
            var result = await _postLotteryService.DrawAsync(request.PostId, Current.UserId, Current.UserName);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "开奖成功",
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
        if (exception is not LotteryInputValidationException)
        {
            Log.ForContext("EventCode", "lottery.request_failed")
                .Error("Lottery request consumed a failure ({failureKind})", RuntimeFailureSummary.Classify(exception));
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
