using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>
/// 通知服务接口
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// 创建通知并发送给指定用户
    /// </summary>
    /// <param name="dto">通知创建 DTO</param>
    /// <returns>创建的通知 ID</returns>
    Task<long> CreateNotificationAsync(CreateNotificationDto dto);

    /// <summary>
    /// 获取用户的通知列表（分页）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="query">查询参数</param>
    /// <returns>通知列表和总数</returns>
    Task<(List<UserNotificationVo> notifications, int total)> GetUserNotificationsAsync(
        long userId, 
        NotificationListQueryDto query);

    /// <summary>
    /// 获取用户的未读通知数量
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>未读数量</returns>
    Task<long> GetUnreadCountAsync(long userId);

    /// <summary>
    /// 标记通知为已读
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="notificationIds">通知 ID 列表</param>
    /// <returns>受影响的行数</returns>
    Task<int> MarkAsReadAsync(long userId, List<long> notificationIds);

    /// <summary>
    /// 标记用户的所有通知为已读
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>受影响的行数</returns>
    Task<int> MarkAllAsReadAsync(long userId);

    /// <summary>
    /// 删除通知（软删除）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="notificationId">通知 ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteNotificationAsync(long userId, long notificationId);

    /// <summary>
    /// 获取用户的未读数量（按类型分组）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>未读数量DTO</returns>
    Task<UnreadCountDto> GetUnreadCountDetailAsync(long userId);
}
