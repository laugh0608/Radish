using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;

namespace Radish.Repository;

public sealed class NotificationRepository : BaseRepository<Notification>, INotificationRepository
{
    public NotificationRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public async Task<bool> PersistAsync(
        Notification notification,
        IReadOnlyList<UserNotification> userNotifications)
    {
        if (userNotifications.Count == 0)
        {
            throw new ArgumentException("通知接收关系不能为空", nameof(userNotifications));
        }

        var expectedUserIds = userNotifications.Select(item => item.UserId).Distinct().ToList();
        var existingCount = await DbProtectedClient.Queryable<UserNotification>()
            .Where(item =>
                item.TenantId == notification.TenantId &&
                item.NotificationId == notification.Id &&
                expectedUserIds.Contains(item.UserId))
            .CountAsync();
        if (existingCount == expectedUserIds.Count)
        {
            return false;
        }

        DbProtectedClient.Ado.BeginTran();
        try
        {
            existingCount = await DbProtectedClient.Queryable<UserNotification>()
                .Where(item =>
                    item.TenantId == notification.TenantId &&
                    item.NotificationId == notification.Id &&
                    expectedUserIds.Contains(item.UserId))
                .CountAsync();
            if (existingCount == expectedUserIds.Count)
            {
                DbProtectedClient.Ado.CommitTran();
                return false;
            }

            await DbProtectedClient.Insertable(notification)
                .SplitTable()
                .ExecuteCommandAsync();
            await DbProtectedClient.Insertable(userNotifications.ToList()).ExecuteCommandAsync();
            DbProtectedClient.Ado.CommitTran();
            return true;
        }
        catch (Exception ex) when (RepositorySqlHelper.IsUniqueConstraintException(ex))
        {
            DbProtectedClient.Ado.RollbackTran();
            return false;
        }
        catch
        {
            DbProtectedClient.Ado.RollbackTran();
            throw;
        }
    }
}
