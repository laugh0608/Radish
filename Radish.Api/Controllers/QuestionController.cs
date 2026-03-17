using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 论坛问答控制器
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("论坛问答管理")]
public class QuestionController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IContentModerationService _contentModerationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public QuestionController(
        IPostService postService,
        IContentModerationService contentModerationService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _postService = postService;
        _contentModerationService = contentModerationService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>
    /// 为问答帖提交回答
    /// </summary>
    /// <param name="request">回答请求</param>
    /// <returns>最新问答详情</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Answer([FromBody] CreateAnswerDto request)
    {
        if (request.PostId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "帖子ID必须大于0"
            };
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "回答内容不能为空"
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
            var question = await _postService.AddAnswerAsync(
                request.PostId,
                request.Content,
                Current.UserId,
                Current.UserName,
                Current.TenantId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "回答成功",
                ResponseData = question
            };
        }
        catch (AggregateException ex) when (TryBuildAnswerErrorResponse(ex, out var response))
        {
            return response;
        }
        catch (ArgumentException ex)
        {
            return BuildAnswerErrorResponse(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BuildAnswerErrorResponse(ex);
        }
    }

    /// <summary>
    /// 采纳问答帖中的回答
    /// </summary>
    /// <param name="request">采纳请求</param>
    /// <returns>最新问答详情</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Accept([FromBody] AcceptAnswerDto request)
    {
        if (request.PostId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "帖子ID必须大于0"
            };
        }

        if (request.AnswerId <= 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "回答ID必须大于0"
            };
        }

        try
        {
            var question = await _postService.AcceptAnswerAsync(
                request.PostId,
                request.AnswerId,
                Current.UserId,
                Current.UserName);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "采纳成功",
                ResponseData = question
            };
        }
        catch (AggregateException ex) when (TryBuildAcceptErrorResponse(ex, out var response))
        {
            return response;
        }
        catch (ArgumentException ex)
        {
            return BuildAcceptErrorResponse(ex);
        }
        catch (InvalidOperationException ex)
        {
            return BuildAcceptErrorResponse(ex);
        }
    }

    private static bool TryBuildAnswerErrorResponse(AggregateException exception, out MessageModel response)
    {
        var knownException = UnwrapKnownException(exception);
        if (knownException is ArgumentException or InvalidOperationException)
        {
            response = BuildAnswerErrorResponse(knownException);
            return true;
        }

        response = null!;
        return false;
    }

    private static bool TryBuildAcceptErrorResponse(AggregateException exception, out MessageModel response)
    {
        var knownException = UnwrapKnownException(exception);
        if (knownException is ArgumentException or InvalidOperationException)
        {
            response = BuildAcceptErrorResponse(knownException);
            return true;
        }

        response = null!;
        return false;
    }

    private static Exception UnwrapKnownException(AggregateException exception)
    {
        var flattened = exception.Flatten();
        return flattened.InnerExceptions.Count == 1
            ? flattened.InnerExceptions[0]
            : flattened.GetBaseException();
    }

    private static MessageModel BuildAnswerErrorResponse(Exception exception)
    {
        var statusCode = exception switch
        {
            ArgumentException => HttpStatusCodeEnum.BadRequest,
            InvalidOperationException invalidOperationException when invalidOperationException.Message.Contains("不存在") =>
                HttpStatusCodeEnum.NotFound,
            InvalidOperationException => HttpStatusCodeEnum.BadRequest,
            _ => HttpStatusCodeEnum.BadRequest
        };

        return BuildErrorResponse(statusCode, exception.Message);
    }

    private static MessageModel BuildAcceptErrorResponse(Exception exception)
    {
        var statusCode = exception switch
        {
            ArgumentException => HttpStatusCodeEnum.BadRequest,
            InvalidOperationException invalidOperationException when invalidOperationException.Message.Contains("只有提问者") =>
                HttpStatusCodeEnum.Forbidden,
            InvalidOperationException invalidOperationException when invalidOperationException.Message.Contains("不存在") =>
                HttpStatusCodeEnum.NotFound,
            InvalidOperationException => HttpStatusCodeEnum.BadRequest,
            _ => HttpStatusCodeEnum.BadRequest
        };

        return BuildErrorResponse(statusCode, exception.Message);
    }

    private static MessageModel BuildErrorResponse(HttpStatusCodeEnum statusCode, string message)
    {
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = message
        };
    }
}
