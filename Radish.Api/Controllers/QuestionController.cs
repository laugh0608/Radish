using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
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
[ApiErrorContract]
[Tags("论坛问答管理")]
public class QuestionController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IContentModerationService _contentModerationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IForumQuestionService _forumQuestionService;

    public QuestionController(
        IPostService postService,
        IContentModerationService contentModerationService,
        ICurrentUserAccessor currentUserAccessor,
        IForumQuestionService forumQuestionService)
    {
        _postService = postService;
        _contentModerationService = contentModerationService;
        _currentUserAccessor = currentUserAccessor;
        _forumQuestionService = forumQuestionService;
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
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status429TooManyRequests)]
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
            var answerResult = await _forumQuestionService.CreateAnswerAsync(
                Current.TenantId,
                request.PostId,
                request.Content,
                Current.UserId,
                Current.UserName,
                request.ClientSubmissionId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "回答成功",
                ResponseData = answerResult
            };
        }
        catch (AggregateException ex) when (TryBuildKnownErrorResponse(ex, out var response))
        {
            return response;
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
    /// 采纳问答帖中的回答
    /// </summary>
    /// <param name="request">采纳请求</param>
    /// <returns>最新问答详情</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status409Conflict)]
    public async Task<MessageModel> Accept([FromBody] ChangePostAnswerAcceptanceDto request)
    {
        try
        {
            var question = await _forumQuestionService.AcceptAnswerAsync(
                Current.TenantId,
                request.PostIdentifier,
                request.AnswerPublicId,
                request.ExpectedAcceptanceRevision,
                Current.UserId,
                Current.UserName,
                request.ClientSubmissionId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "采纳成功",
                ResponseData = question
            };
        }
        catch (AggregateException ex) when (TryBuildKnownErrorResponse(ex, out var response))
        {
            return response;
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

    /// <summary>服务端分页查询回答。</summary>
    [HttpGet]
    public async Task<MessageModel> Page([FromQuery] GetPostAnswerPageDto request)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.GetAnswerPageAsync(
                Current.TenantId,
                request.PostIdentifier,
                request.PageIndex,
                request.PageSize,
                request.Sort,
                Current.UserId),
            "获取回答成功");
    }

    /// <summary>编辑回答。</summary>
    [HttpPost]
    public async Task<MessageModel> Edit([FromBody] UpdatePostAnswerDto request)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.UpdateAnswerAsync(
                Current.TenantId,
                request.AnswerPublicId,
                request.Content,
                request.ExpectedContentRevision,
                Current.UserId,
                Current.UserName,
                request.ClientSubmissionId),
            "回答已更新");
    }

    /// <summary>删除未采纳回答。</summary>
    [HttpPost]
    public async Task<MessageModel> Delete([FromBody] DeletePostAnswerDto request)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.DeleteAnswerAsync(
                Current.TenantId,
                request.AnswerPublicId,
                request.ExpectedContentRevision,
                Current.UserId,
                Current.UserName,
                request.ClientSubmissionId),
            "回答已删除");
    }

    /// <summary>查询回答历史版本。</summary>
    [HttpGet]
    public async Task<MessageModel> Revisions([FromQuery] string answerPublicId)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.GetAnswerRevisionsAsync(
                Current.TenantId,
                answerPublicId,
                Current.UserId),
            "获取回答版本成功");
    }

    /// <summary>查询回答历史版本详情。</summary>
    [HttpGet]
    public async Task<MessageModel> Revision(
        [FromQuery] string answerPublicId,
        [FromQuery] int revisionNumber)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.GetAnswerRevisionAsync(
                Current.TenantId,
                answerPublicId,
                revisionNumber,
                Current.UserId),
            "获取回答版本成功");
    }

    /// <summary>将历史版本恢复为回答的新版本。</summary>
    [HttpPost]
    public async Task<MessageModel> Restore([FromBody] RestorePostAnswerRevisionDto request)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.RestoreAnswerRevisionAsync(
                Current.TenantId,
                request.AnswerPublicId,
                request.RevisionNumber,
                request.ExpectedContentRevision,
                Current.UserId,
                Current.UserName,
                request.ClientSubmissionId),
            "回答版本已恢复");
    }

    /// <summary>撤销当前采纳状态。</summary>
    [HttpPost]
    public async Task<MessageModel> Revoke([FromBody] RevokePostAnswerAcceptanceDto request)
    {
        return await ExecuteAsync(
            () => _forumQuestionService.RevokeAcceptanceAsync(
                Current.TenantId,
                request.PostIdentifier,
                request.ExpectedAcceptanceRevision,
                Current.UserId,
                Current.UserName,
                request.ClientSubmissionId),
            "采纳状态已撤销");
    }

    private static async Task<MessageModel> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string successMessage)
    {
        try
        {
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = successMessage,
                ResponseData = await operation()
            };
        }
        catch (AggregateException ex) when (TryBuildKnownErrorResponse(ex, out var response))
        {
            return response;
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

    private static bool TryBuildKnownErrorResponse(AggregateException exception, out MessageModel response)
    {
        var knownException = UnwrapKnownException(exception);
        if (knownException is ArgumentException or BusinessException)
        {
            response = BuildErrorResponse(knownException);
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

    private static MessageModel BuildErrorResponse(Exception exception)
    {
        return exception switch
        {
            BusinessException businessException => BuildBusinessErrorResponse(businessException),
            _ => BuildErrorResponse(HttpStatusCodeEnum.BadRequest, exception.Message)
        };
    }

    private static MessageModel BuildBusinessErrorResponse(BusinessException exception)
    {
        var messageArguments = ApiMessageArgumentNormalizer.Normalize(exception.MessageArguments);
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = exception.StatusCode,
            MessageInfo = exception.Message,
            Code = exception.ErrorCode,
            MessageKey = exception.MessageKey,
            MessageArguments = messageArguments.Length > 0 ? messageArguments : null
        };
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
