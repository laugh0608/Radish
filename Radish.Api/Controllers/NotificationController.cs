using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Filters;
using Radish.Common.HttpContextTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared;
using Radish.Shared.CustomEnum;

namespace Radish.Api.Controllers;

/// <summary>
/// 通知管理控制器
/// </summary>
/// <remarks>
/// 提供通知的查询、标记已读、删除等接口
/// </remarks>
[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
[ApiErrorContract]
[Tags("通知管理")]
[Authorize(Policy = AuthorizationPolicies.Client)]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public NotificationController(
        INotificationService notificationService,
        ICurrentUserAccessor currentUserAccessor)
    {
        _notificationService = notificationService;
        _currentUserAccessor = currentUserAccessor;
    }

    private CurrentUser Current => _currentUserAccessor.Current;

    /// <summary>按服务端分类与 revision cursor 查询权威通知分组。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<NotificationInboxPageVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetInbox([FromQuery] NotificationInboxQueryDto query)
    {
        var result = await _notificationService.GetInboxAsync(Current.TenantId, Current.UserId, query);
        return Success("获取通知收件箱成功", result);
    }

    /// <summary>获取权威通知摘要。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<NotificationInboxSummaryVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetInboxSummary()
    {
        var result = await _notificationService.GetInboxSummaryAsync(Current.TenantId, Current.UserId);
        return Success("获取通知摘要成功", result);
    }

    /// <summary>批量标记通知分组已读。</summary>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel<NotificationInboxMutationVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> MarkInboxGroupsAsRead([FromBody] MarkInboxGroupsAsReadDto dto)
    {
        if (dto.GroupIds.Count == 0)
        {
            return BadRequestMessage("通知分组 ID 列表不能为空");
        }

        var result = await _notificationService.MarkInboxGroupsAsReadAsync(
            Current.TenantId,
            Current.UserId,
            dto.GroupIds);
        return Success("通知分组已标记为已读", result);
    }

    /// <summary>删除当前用户的通知分组。</summary>
    [HttpDelete("{groupId:long}")]
    [ProducesResponseType(typeof(MessageModel<NotificationInboxMutationVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> DeleteInboxGroup(long groupId)
    {
        var result = await _notificationService.DeleteInboxGroupAsync(
            Current.TenantId,
            Current.UserId,
            groupId);
        return result.VoAffectedRows > 0
            ? Success("通知分组已删除", result)
            : NotFoundMessage("通知分组不存在或已删除");
    }

    /// <summary>返回注册表支持分类与当前用户偏好。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<IReadOnlyList<NotificationPreferenceVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetPreferences()
    {
        var result = await _notificationService.GetPreferencesAsync(Current.TenantId, Current.UserId);
        return Success("获取通知偏好成功", result);
    }

    /// <summary>批量更新当前用户的通知分类偏好。</summary>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel<IReadOnlyList<NotificationPreferenceVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> UpdatePreferences([FromBody] UpdateNotificationPreferencesDto dto)
    {
        var result = await _notificationService.UpdatePreferencesAsync(
            Current.TenantId,
            Current.UserId,
            dto,
            Current.UserId,
            Current.UserName);
        return Success("通知偏好已更新", result);
    }

    /// <summary>
    /// 获取当前用户的通知列表（分页）
    /// </summary>
    /// <param name="query">查询参数</param>
    /// <returns>通知列表和总数</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<PageModel<UserNotificationVo>>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetNotificationList([FromQuery] NotificationListQueryDto query)
    {
        var userId = Current.UserId;

        var (notifications, total) = await _notificationService.GetUserNotificationsAsync(
            Current.TenantId,
            userId,
            query);

        var pageModel = new PageModel<UserNotificationVo>
        {
            Page = query.PageIndex,
            DataCount = total,
            PageCount = (int)Math.Ceiling(total / (double)query.PageSize),
            Data = notifications
        };

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = pageModel
        };
    }

    /// <summary>
    /// 获取当前用户的未读通知数量
    /// </summary>
    /// <returns>未读数量</returns>
    [HttpGet]
    [ProducesResponseType(typeof(MessageModel<UnreadCountDto>), StatusCodes.Status200OK)]
    public async Task<MessageModel> GetUnreadCount()
    {
        var userId = Current.UserId;

        var unreadCountDto = await _notificationService.GetUnreadCountDetailAsync(Current.TenantId, userId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "获取成功",
            ResponseData = unreadCountDto
        };
    }

    /// <summary>
    /// 标记通知为已读
    /// </summary>
    /// <param name="dto">要标记已读的通知 ID 列表</param>
    /// <returns>受影响的行数</returns>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel<int>), StatusCodes.Status200OK)]
    public async Task<MessageModel> MarkAsRead([FromBody] MarkAsReadDto dto)
    {
        if (dto == null || dto.NotificationIds == null || dto.NotificationIds.Count == 0)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.BadRequest,
                MessageInfo = "通知 ID 列表不能为空"
            };
        }

        var userId = Current.UserId;

        var affectedRows = await _notificationService.MarkAsReadAsync(
            Current.TenantId,
            userId,
            dto.NotificationIds);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = $"已标记 {affectedRows} 条通知为已读",
            ResponseData = affectedRows
        };
    }

    /// <summary>
    /// 标记所有通知为已读
    /// </summary>
    /// <returns>受影响的行数</returns>
    [HttpPut]
    [ProducesResponseType(typeof(MessageModel<NotificationInboxMutationVo>), StatusCodes.Status200OK)]
    public async Task<MessageModel> MarkAllAsRead([FromBody] MarkAllInboxAsReadDto? dto = null)
    {
        var result = await _notificationService.MarkAllInboxAsReadAsync(
            Current.TenantId,
            Current.UserId,
            dto?.Category);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = $"已标记 {result.VoAffectedRows} 条通知为已读",
            ResponseData = result
        };
    }

    /// <summary>
    /// 删除通知（软删除）
    /// </summary>
    /// <param name="notificationId">通知 ID</param>
    /// <returns>是否成功</returns>
    [HttpDelete("{notificationId}")]
    [ProducesResponseType(typeof(MessageModel<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageModel), StatusCodes.Status404NotFound)]
    public async Task<MessageModel> DeleteNotification(long notificationId)
    {
        var userId = Current.UserId;

        var success = await _notificationService.DeleteNotificationAsync(
            Current.TenantId,
            userId,
            notificationId);

        if (!success)
        {
            return new MessageModel
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCodeEnum.NotFound,
                MessageInfo = "通知不存在或已被删除"
            };
        }

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = "删除成功",
            ResponseData = true
        };
    }

    private static MessageModel Success(string message, object? data) => new()
    {
        IsSuccess = true,
        StatusCode = (int)HttpStatusCodeEnum.Success,
        MessageInfo = message,
        ResponseData = data
    };

    private static MessageModel BadRequestMessage(string message) => new()
    {
        IsSuccess = false,
        StatusCode = (int)HttpStatusCodeEnum.BadRequest,
        MessageInfo = message
    };

    private static MessageModel NotFoundMessage(string message) => new()
    {
        IsSuccess = false,
        StatusCode = (int)HttpStatusCodeEnum.NotFound,
        MessageInfo = message
    };
}
