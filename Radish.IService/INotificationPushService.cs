namespace Radish.IService;

/// <summary>
/// 通知推送服务接口
/// </summary>
public interface INotificationPushService
{
    /// <summary>
    /// 推送未读数变更到指定用户
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="unreadCount">未读数量</param>
    /// <returns></returns>
    Task PushUnreadCountAsync(long userId, int unreadCount);

    /// <summary>
    /// 批量推送未读数变更
    /// </summary>
    /// <param name="userUnreadCounts">用户 ID → 未读数量映射</param>
    /// <returns></returns>
    Task PushUnreadCountBatchAsync(Dictionary<long, int> userUnreadCounts);

    /// <summary>
    /// 推送通知到指定用户（P1 阶段使用）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="notification">通知对象</param>
    /// <returns></returns>
    Task PushNotificationAsync(long userId, object notification);

    /// <summary>
    /// 推送已读状态变更到指定用户的其他端（多端同步）
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="notificationIds">已读的通知 ID 列表</param>
    /// <returns></returns>
    Task PushNotificationReadAsync(long userId, long[] notificationIds);

    /// <summary>
    /// 获取用户的未读通知数量
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>未读数量</returns>
    Task<int> GetUnreadCountAsync(long userId);
}
