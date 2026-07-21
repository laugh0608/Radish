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
    private readonly IConsoleAuthorizationService? _consoleAuthorizationService;

    public ContentModerationController(
        IContentModerationService contentModerationService,
        ICurrentUserAccessor currentUserAccessor,
        IStringLocalizer<Errors> errorsLocalizer,
        IConsoleAuthorizationService? consoleAuthorizationService = null)
    {
        _contentModerationService = contentModerationService;
        _currentUserAccessor = currentUserAccessor;
        _errorsLocalizer = errorsLocalizer;
        _consoleAuthorizationService = consoleAuthorizationService;
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
            var receipt = await _contentModerationService.SubmitCaseReportAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId);

            return new MessageModel
            {
                IsSuccess = true,
                StatusCode = (int)HttpStatusCodeEnum.Success,
                MessageInfo = "举报已提交，等待审核",
                ResponseData = receipt
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

    /// <summary>获取本人举报结果。</summary>
    [HttpGet]
    public async Task<MessageModel> GetMyReports([FromQuery] MyContentReportQueryDto? query = null)
    {
        try
        {
            var result = await _contentModerationService.GetMyReportsAsync(
                query ?? new MyContentReportQueryDto(),
                Current.UserId,
                Current.TenantId);
            return BuildSuccess(result);
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
    }

    /// <summary>获取本人单份举报结果。</summary>
    [HttpGet("{reportPublicId}")]
    public async Task<MessageModel> GetMyReport(string reportPublicId)
    {
        try
        {
            return BuildSuccess(await _contentModerationService.GetMyReportAsync(
                reportPublicId,
                Current.UserId,
                Current.TenantId));
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

    /// <summary>获取治理案件队列（管理端）。</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ModerationView)]
    public async Task<MessageModel> GetCaseQueue([FromQuery] ContentModerationCaseQueueDto? query = null)
    {
        try
        {
            return BuildSuccess(await _contentModerationService.GetCaseQueueAsync(
                query ?? new ContentModerationCaseQueueDto(),
                Current.TenantId));
        }
        catch (ArgumentException)
        {
            return BuildValidationError();
        }
    }

    /// <summary>获取治理案件详情（管理端）。</summary>
    [HttpGet("{casePublicId}")]
    [RequireConsolePermission(ConsolePermissions.ModerationView)]
    public async Task<MessageModel> GetCase(string casePublicId)
    {
        try
        {
            return BuildSuccess(await _contentModerationService.GetCaseAsync(casePublicId, Current.TenantId));
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

    /// <summary>获取治理案件事件（管理端）。</summary>
    [HttpGet]
    [RequireConsolePermission(ConsolePermissions.ModerationView)]
    public async Task<MessageModel> GetCaseEvents([FromQuery] string casePublicId)
    {
        try
        {
            var detail = await _contentModerationService.GetCaseAsync(casePublicId, Current.TenantId);
            return BuildSuccess(detail.VoEvents);
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

    /// <summary>追加案件证据（管理端）。</summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ModerationReview)]
    public async Task<MessageModel> CaptureEvidence([FromBody] CaptureContentModerationEvidenceDto dto)
    {
        try
        {
            return BuildSuccess(await _contentModerationService.CaptureEvidenceAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId));
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

    /// <summary>登记案件决定与动作（管理端）。</summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ModerationReview)]
    public async Task<MessageModel> ReviewCase([FromBody] ReviewContentModerationCaseDto dto)
    {
        if (dto.UserAction != null && !await HasModerationActionPermissionAsync())
        {
            return BuildError(
                HttpStatusCodeEnum.Forbidden,
                "当前账号无权执行治理动作",
                "Auth.Forbidden",
                "error.auth.forbidden");
        }

        try
        {
            return BuildSuccess(await _contentModerationService.ReviewCaseAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId));
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

    /// <summary>对已结案件追加纠正动作（管理端）。</summary>
    [HttpPost]
    [RequireConsolePermission(ConsolePermissions.ModerationAction)]
    public async Task<MessageModel> ApplyCorrectiveAction([FromBody] ApplyContentModerationCorrectiveActionDto dto)
    {
        try
        {
            return BuildSuccess(await _contentModerationService.ApplyCorrectiveActionAsync(
                dto,
                Current.UserId,
                Current.UserName,
                Current.TenantId));
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

        if (dto.IsApproved && dto.ActionType != (int)ModerationActionTypeEnum.None &&
            !await HasModerationActionPermissionAsync())
        {
            return BuildError(
                HttpStatusCodeEnum.Forbidden,
                "当前账号无权执行治理动作",
                "Auth.Forbidden",
                "error.auth.forbidden");
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
    [RequireConsolePermission(ConsolePermissions.ModerationAction)]
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

    private static MessageModel BuildSuccess(object? responseData)
    {
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = responseData
        };
    }

    private async Task<bool> HasModerationActionPermissionAsync()
    {
        if (Current.IsSystemOrAdmin())
        {
            return true;
        }

        if (_consoleAuthorizationService == null)
        {
            return false;
        }

        var permissionKeys = await _consoleAuthorizationService.GetPermissionKeysByRolesAsync(Current.Roles);
        return permissionKeys.Contains(ConsolePermissions.ModerationAction, StringComparer.OrdinalIgnoreCase);
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
