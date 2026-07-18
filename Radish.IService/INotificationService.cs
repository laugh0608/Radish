using Radish.Model.DtoModels;
using Radish.Model.ViewModels;

namespace Radish.IService;

public interface INotificationService
{
    Task<long> CreateNotificationAsync(CreateNotificationDto dto);

    Task<NotificationInboxPageVo> GetInboxAsync(
        long tenantId,
        long userId,
        NotificationInboxQueryDto query);

    Task<NotificationInboxSummaryVo> GetInboxSummaryAsync(long tenantId, long userId);

    Task<NotificationInboxMutationVo> MarkInboxGroupsAsReadAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<long> groupIds);

    Task<NotificationInboxMutationVo> MarkAllInboxAsReadAsync(
        long tenantId,
        long userId,
        string? category = null);

    Task<NotificationInboxMutationVo> DeleteInboxGroupAsync(
        long tenantId,
        long userId,
        long groupId);

    Task<IReadOnlyList<NotificationPreferenceVo>> GetPreferencesAsync(long tenantId, long userId);

    Task<IReadOnlyList<NotificationPreferenceVo>> UpdatePreferencesAsync(
        long tenantId,
        long userId,
        UpdateNotificationPreferencesDto dto,
        long operatorId,
        string operatorName);

    Task<(List<UserNotificationVo> notifications, int total)> GetUserNotificationsAsync(
        long tenantId,
        long userId,
        NotificationListQueryDto query);

    Task<long> GetUnreadCountAsync(long tenantId, long userId);

    Task<int> MarkAsReadAsync(long tenantId, long userId, List<long> notificationIds);

    Task<int> MarkAllAsReadAsync(long tenantId, long userId);

    Task<bool> DeleteNotificationAsync(long tenantId, long userId, long notificationId);

    Task<UnreadCountDto> GetUnreadCountDetailAsync(long tenantId, long userId);
}
