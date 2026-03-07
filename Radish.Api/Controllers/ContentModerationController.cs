using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>内容治理控制器</summary>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[Tags("内容治理")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class ContentModerationController : ControllerBase
{
    private readonly IContentModerationService _contentModerationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ContentModerationController(IContentModerationService contentModerationService, ICurrentUserAccessor currentUserAccessor)
    {
        _contentModerationService = contentModerationService;
        _currentUserAccessor = currentUserAccessor;
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
            return BuildError(HttpStatusCodeEnum.BadRequest, "举报目标 ID 无效");
        }

        if (string.IsNullOrWhiteSpace(dto.TargetType))
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "举报目标类型不能为空");
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
        catch (ArgumentException ex)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var statusCode = ex.Message.Contains("不存在") ? HttpStatusCodeEnum.NotFound : HttpStatusCodeEnum.BadRequest;
            return BuildError(statusCode, ex.Message);
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
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetReviewQueue(int? status = 0, int pageIndex = 1, int pageSize = 20)
    {
        var result = await _contentModerationService.GetReportQueueAsync(status, pageIndex, pageSize);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
        };
    }

    /// <summary>审核举报（管理端）</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> Review([FromBody] ReviewContentReportDto dto)
    {
        if (dto.ReportId <= 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "举报单 ID 无效");
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
        catch (ArgumentException ex)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var statusCode = ex.Message.Contains("不存在") ? HttpStatusCodeEnum.NotFound : HttpStatusCodeEnum.BadRequest;
            return BuildError(statusCode, ex.Message);
        }
    }

    /// <summary>执行用户治理动作（管理端）</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> ApplyUserAction([FromBody] ApplyUserModerationActionDto dto)
    {
        if (dto.TargetUserId <= 0)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, "目标用户 ID 无效");
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
        catch (ArgumentException ex)
        {
            return BuildError(HttpStatusCodeEnum.BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            var statusCode = ex.Message.Contains("不存在") ? HttpStatusCodeEnum.NotFound : HttpStatusCodeEnum.BadRequest;
            return BuildError(statusCode, ex.Message);
        }
    }

    /// <summary>获取治理动作记录（管理端）</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.SystemOrAdmin)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetActionLogs(int pageIndex = 1, int pageSize = 20, long? targetUserId = null)
    {
        var result = await _contentModerationService.GetActionLogsAsync(pageIndex, pageSize, targetUserId);
        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = result
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
