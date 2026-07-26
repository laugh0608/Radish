using Radish.Model.ViewModels;

namespace Radish.IService;

public interface INotificationPushService
{
    Task PushInboxChangedAsync(long userId, NotificationInboxChangedVo change);

}
