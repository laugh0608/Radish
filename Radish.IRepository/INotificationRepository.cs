using Radish.Model;

namespace Radish.IRepository;

public interface INotificationRepository
{
    Task<bool> PersistAsync(Notification notification, IReadOnlyList<UserNotification> userNotifications);
}
