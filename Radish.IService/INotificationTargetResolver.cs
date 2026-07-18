using Radish.Model;
using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>按当前资源状态与接收者权限解析通知结构化目标。</summary>
public interface INotificationTargetResolver
{
    Task<IReadOnlyDictionary<long, NotificationTargetVo>> ResolveAsync(
        long tenantId,
        long userId,
        IReadOnlyCollection<Notification> notifications);
}
