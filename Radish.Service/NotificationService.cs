using AutoMapper;
using Microsoft.Extensions.Logging;
using Radish.Common;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using SqlSugar;

namespace Radish.Service;

/// <summary>
/// 通知服务实现
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IBaseRepository<Notification> _notificationRepository;
    private readonly IBaseRepository<UserNotification> _userNotificationRepository;
    private readonly INotificationPushService _pushService;
    private readonly INotificationCacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IBaseRepository<Notification> notificationRepository,
        IBaseRepository<UserNotification> userNotificationRepository,
        INotificationPushService pushService,
        INotificationCacheService cacheService,
        IMapper mapper,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _userNotificationRepository = userNotificationRepository;
        _pushService = pushService;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// 创建通知并发送给指定用户
    /// </summary>
    public async Task<long> CreateNotificationAsync(CreateNotificationDto dto)
    {
        if (dto == null || dto.ReceiverUserIds == null || dto.ReceiverUserIds.Count == 0)
        {
            throw new ArgumentException("接收者用户 ID 列表不能为空");
        }

        try
        {
            // 1. 创建通知实体
            var notification = new Notification(
                new NotificationInitializationOptions(dto.Type, dto.Title)
                {
                    Content = dto.Content,
                    Priority = dto.Priority,
                    BusinessType = dto.BusinessType,
                    BusinessId = dto.BusinessId,
                    TriggerId = dto.TriggerId,
                    TriggerName = dto.TriggerName,
                    TriggerAvatar = dto.TriggerAvatar,
                    ExtData = dto.ExtData,
                    TenantId = dto.TenantId ?? 0
                });

            // 使用雪花 ID
            notification.Id = SnowFlakeSingle.Instance.NextId();

            // 2. 插入通知（支持分表）
            await _notificationRepository.AddSplitAsync(notification);

            _logger.LogInformation(
                "[NotificationService] 创建通知成功，NotificationId: {NotificationId}, Type: {Type}",
                notification.Id, notification.Type);

            // 3. 为每个接收者创建用户通知关联
            var userNotifications = dto.ReceiverUserIds.Select(userId =>
            {
                var userNotification = new UserNotification(
                    new UserNotificationInitializationOptions(userId, notification.Id)
                    {
                        TenantId = dto.TenantId ?? 0
                    });
                userNotification.Id = SnowFlakeSingle.Instance.NextId();
                return userNotification;
            }).ToList();

            // 批量插入用户通知关系
            await _userNotificationRepository.AddRangeAsync(userNotifications);

            _logger.LogInformation(
                "[NotificationService] 创建用户通知关联成功，接收者数量: {Count}",
                userNotifications.Count);

            // 4. 异步推送未读数变更到所有接收者
            _ = Task.Run(async () =>
            {
                try
                {
                    foreach (var userId in dto.ReceiverUserIds)
                    {
                        // 使用缓存服务增量更新未读数
                        var unreadCount = await _cacheService.IncrementUnreadCountAsync(userId);
                        await _pushService.PushUnreadCountAsync(userId, (int)unreadCount);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "[NotificationService] 推送未读数失败，NotificationId: {NotificationId}",
                        notification.Id);
                }
            });

            return notification.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 创建通知失败，Type: {Type}, Title: {Title}",
                dto.Type, dto.Title);
            throw;
        }
    }

    /// <summary>
    /// 获取用户的通知列表（分页）
    /// </summary>
    public async Task<(List<UserNotificationVo> notifications, int total)> GetUserNotificationsAsync(
        long userId,
        NotificationListQueryDto query)
    {
        try
        {
            // 查询用户的通知关系（分页）
            var result = await _userNotificationRepository.QueryPageAsync(
                un => un.UserId == userId && !un.IsDeleted,
                query.PageIndex,
                query.PageSize,
                un => un.CreateTime,
                OrderByType.Desc); // 按创建时间倒序

            if (result.data == null || result.data.Count == 0)
            {
                return (new List<UserNotificationVo>(), result.totalCount);
            }

            // 获取所有通知 ID
            var notificationIds = result.data.Select(un => un.NotificationId).ToList();

            // 批量查询关联的通知详情（使用分表查询）
            var notifications = await _notificationRepository.QuerySplitAsync(
                n => notificationIds.Contains(n.Id),
                "Id");
            var notificationDict = notifications.ToDictionary(n => n.Id);

            // 映射为 ViewModel 并填充通知详情
            var voList = result.data.Select(un =>
            {
                var vo = _mapper.Map<UserNotificationVo>(un);

                // 填充关联的通知详情
                if (notificationDict.TryGetValue(un.NotificationId, out var notification))
                {
                    vo.VoNotification = _mapper.Map<NotificationVo>(notification);
                }

                return vo;
            }).ToList();

            _logger.LogDebug(
                "[NotificationService] 查询用户通知列表，UserId: {UserId}, Total: {Total}",
                userId, result.totalCount);

            return (voList, result.totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 查询用户通知列表失败，UserId: {UserId}",
                userId);
            throw;
        }
    }

    /// <summary>
    /// 获取用户的未读通知数量
    /// </summary>
    public async Task<long> GetUnreadCountAsync(long userId)
    {
        try
        {
            // 使用缓存服务（优先从缓存读取）
            var count = await _cacheService.GetUnreadCountAsync(userId);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 查询未读数失败，UserId: {UserId}",
                userId);
            return 0;
        }
    }

    /// <summary>
    /// 标记通知为已读
    /// </summary>
    public async Task<int> MarkAsReadAsync(long userId, List<long> notificationIds)
    {
        if (notificationIds == null || notificationIds.Count == 0)
        {
            return 0;
        }

        try
        {
            // 更新指定通知为已读
            var affectedRows = await _userNotificationRepository.UpdateColumnsAsync(
                un => new UserNotification
                {
                    IsRead = true,
                    ReadAt = DateTime.Now
                },
                un => un.UserId == userId && notificationIds.Contains(un.NotificationId) && !un.IsRead);

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "[NotificationService] 标记已读成功，UserId: {UserId}, Count: {Count}",
                    userId, affectedRows);

                // 使用缓存服务减量更新未读数
                var unreadCount = await _cacheService.DecrementUnreadCountAsync(userId, affectedRows);
                await _pushService.PushUnreadCountAsync(userId, (int)unreadCount);

                // 推送已读状态变更（多端同步）
                await _pushService.PushNotificationReadAsync(userId, notificationIds.ToArray());
            }

            return affectedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 标记已读失败，UserId: {UserId}",
                userId);
            throw;
        }
    }

    /// <summary>
    /// 标记用户的所有通知为已读
    /// </summary>
    public async Task<int> MarkAllAsReadAsync(long userId)
    {
        try
        {
            var affectedRows = await _userNotificationRepository.UpdateColumnsAsync(
                un => new UserNotification
                {
                    IsRead = true,
                    ReadAt = DateTime.Now
                },
                un => un.UserId == userId && !un.IsRead && !un.IsDeleted);

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "[NotificationService] 标记全部已读成功，UserId: {UserId}, Count: {Count}",
                    userId, affectedRows);

                // 使用缓存服务设置未读数为 0
                await _cacheService.SetUnreadCountAsync(userId, 0);
                await _pushService.PushUnreadCountAsync(userId, 0);
            }

            return affectedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 标记全部已读失败，UserId: {UserId}",
                userId);
            throw;
        }
    }

    /// <summary>
    /// 删除通知（软删除）
    /// </summary>
    public async Task<bool> DeleteNotificationAsync(long userId, long notificationId)
    {
        try
        {
            // 先查询通知是否未读
            var userNotification = await _userNotificationRepository.QueryFirstAsync(
                un => un.UserId == userId && un.NotificationId == notificationId && !un.IsDeleted);

            if (userNotification == null)
            {
                return false;
            }

            var wasUnread = !userNotification.IsRead;

            // 删除通知
            var affectedRows = await _userNotificationRepository.UpdateColumnsAsync(
                un => new UserNotification
                {
                    IsDeleted = true,
                    DeletedAt = DateTime.Now
                },
                un => un.UserId == userId && un.NotificationId == notificationId && !un.IsDeleted);

            if (affectedRows > 0)
            {
                _logger.LogInformation(
                    "[NotificationService] 删除通知成功，UserId: {UserId}, NotificationId: {NotificationId}",
                    userId, notificationId);

                // 如果删除的是未读通知，减量更新未读数
                if (wasUnread)
                {
                    var unreadCount = await _cacheService.DecrementUnreadCountAsync(userId, 1);
                    await _pushService.PushUnreadCountAsync(userId, (int)unreadCount);
                }
            }

            return affectedRows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 删除通知失败，UserId: {UserId}, NotificationId: {NotificationId}",
                userId, notificationId);
            throw;
        }
    }

    /// <summary>
    /// 获取用户的未读数量（按类型分组）
    /// </summary>
    public async Task<UnreadCountDto> GetUnreadCountDetailAsync(long userId)
    {
        try
        {
            var totalCount = await GetUnreadCountAsync(userId);

            // TODO: 实现按类型分组的未读数统计
            // 需要联表查询 UserNotification 和 Notification
            // 由于 BaseRepository 没有提供联表查询方法，这里暂时返回总数

            return new UnreadCountDto
            {
                UserId = userId,
                UnreadCount = totalCount,
                UnreadCountByType = null // 后续实现
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[NotificationService] 查询未读数详情失败，UserId: {UserId}",
                userId);
            throw;
        }
    }
}
