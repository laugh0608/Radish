using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Tags("通知管理")]
[Authorize] // 所有接口都需要登录
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IHttpContextUser _httpContextUser;

    public NotificationController(
        INotificationService notificationService,
        IHttpContextUser httpContextUser)
    {
        _notificationService = notificationService;
        _httpContextUser = httpContextUser;
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
        var userId = _httpContextUser.UserId;

        var (notifications, total) = await _notificationService.GetUserNotificationsAsync(userId, query);

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
        var userId = _httpContextUser.UserId;

        var unreadCountDto = await _notificationService.GetUnreadCountDetailAsync(userId);

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

        var userId = _httpContextUser.UserId;

        var affectedRows = await _notificationService.MarkAsReadAsync(userId, dto.NotificationIds);

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
    [ProducesResponseType(typeof(MessageModel<int>), StatusCodes.Status200OK)]
    public async Task<MessageModel> MarkAllAsRead()
    {
        var userId = _httpContextUser.UserId;

        var affectedRows = await _notificationService.MarkAllAsReadAsync(userId);

        return new MessageModel
        {
            IsSuccess = true,
            StatusCode = (int)HttpStatusCodeEnum.Success,
            MessageInfo = $"已标记 {affectedRows} 条通知为已读",
            ResponseData = affectedRows
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
        var userId = _httpContextUser.UserId;

        var success = await _notificationService.DeleteNotificationAsync(userId, notificationId);

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
}
