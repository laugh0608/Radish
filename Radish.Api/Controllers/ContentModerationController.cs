using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Api.Resources;
using Radish.Common.Exceptions;
using Radish.Common.HttpContextTool;
using Radish.Common.PermissionTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>内容治理控制器</summary>
[ApiController]
[ApiErrorContract]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("内容治理")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class ContentModerationController : ControllerBase
{
    private readonly IContentModerationService _contentModerationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public ContentModerationController(
        IContentModerationService contentModerationService,
        ICurrentUserAccessor currentUserAccessor,
        IStringLocalizer<Errors> errorsLocalizer)
    {
        _contentModerationService = contentModerationService;
        _currentUserAccessor = currentUserAccessor;
        _errorsLocalizer = errorsLocalizer;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>提交举报</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Report([FromBody] SubmitContentReportDto dto)
    {
        if (dto.TargetContentId <= 0)
        {
            return BuildValidationError();
        }

        if (string.IsNullOrWhiteSpace(dto.TargetType))
        {
            return BuildValidationError();
        }

        try
        {
            var reportId = await _contentModerationService.SubmitReportAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "举报已提交，等待审核",
                ResponseData = reportId
            };
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
        catch (BusinessException ex)
        {
            return BuildError((HttpStatusCodeEnum)ex.StatusCode, ex.Message, ex.ErrorCode, ex.MessageKey);
        }
    }

    /// <summary>获取我的治理状态</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyModerationStatus()
    {
        var status = await _contentModerationService.GetUserModerationStatusAsync(Current.UserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = status
        };
    }

    /// <summary>获取我的发布权限</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetMyPublishPermission()
    {
        var permission = await _contentModerationService.GetPublishPermissionAsync(Current.UserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = permission
        };
    }

    /// <summary>获取审核队列（管理端）</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ModerationView)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> GetReviewQueue([FromQuery] ContentReportQueueQueryDto? query = null)
    {
        try
        {
            var result = await _contentModerationService.GetReportQueueAsync(query ?? new ContentReportQueueQueryDto());
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = result
            };
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
    }

    /// <summary>审核举报（管理端）</summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ModerationReview)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Review([FromBody] ReviewContentReportDto dto)
    {
        if (dto.ReportId <= 0)
        {
            return BuildValidationError();
        }

        try
        {
            var result = await _contentModerationService.ReviewReportAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "审核成功",
                ResponseData = result
            };
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
        catch (BusinessException ex)
        {
            return BuildError((HttpStatusCodeEnum)ex.StatusCode, ex.Message, ex.ErrorCode, ex.MessageKey);
        }
    }

    /// <summary>执行用户治理动作（管理端）</summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ModerationReview)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> ApplyUserAction([FromBody] ApplyUserModerationActionDto dto)
    {
        if (dto.TargetUserId <= 0)
        {
            return BuildValidationError();
        }

        try
        {
            var result = await _contentModerationService.ApplyUserActionAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId);
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "操作成功",
                ResponseData = result
            };
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
        catch (BusinessException ex)
        {
            return BuildError((HttpStatusCodeEnum)ex.StatusCode, ex.Message, ex.ErrorCode, ex.MessageKey);
        }
    }

    /// <summary>获取治理动作记录（管理端）</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ModerationView)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    public async Task<MessageModel> GetActionLogs([FromQuery] ContentModerationActionLogQueryDto? query = null)
    {
        try
        {
            var result = await _contentModerationService.GetActionLogsAsync(query ?? new ContentModerationActionLogQueryDto());
            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "获取成功",
                ResponseData = result
            };
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
    }

    private MessageModel BuildValidationError()
    {
        return BuildError(
            HttpStatusCodeEnum.BadRequest,
            "治理请求参数无效，请检查后重试",
            "Moderation.ValidationFailed",
            "error.moderation.validation_failed");
    }

    private MessageModel BuildError(
        HttpStatusCodeEnum statusCode,
        string message,
        string? code = null,
        string? messageKey = null)
    {
        var localizedMessage = messageKey is null ? null : _errorsLocalizer[messageKey];
        return new MessageModel
        {
            IsSuccess = false,
            StatusCode = (int)statusCode,
            MessageInfo = localizedMessage is { ResourceNotFound: false }
                ? localizedMessage.Value
                : message,
            Code = code,
            MessageKey = messageKey
        };
    }
}
