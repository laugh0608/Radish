using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public class NotificationServiceTest
{
    [Fact(DisplayName = "新通知应按数据库事实刷新未读缓存并推送")]
    public async Task CreateNotificationAsync_ShouldRefreshUnreadCount_WhenMessageWriteSucceeds()
    {
        const long userId = 1001;
        var harness = CreateHarness();
        harness.NotificationCustomRepository
            .Setup(repository => repository.PersistAsync(
                It.Is<Notification>(notification => notification.Id == 7001),
                It.IsAny<IReadOnlyList<UserNotification>>()))
            .ReturnsAsync(true);
        harness.CacheService
            .Setup(service => service.RefreshUnreadCountAsync(userId))
            .ReturnsAsync(3);
        harness.PushService
            .Setup(service => service.PushUnreadCountAsync(userId, 3))
            .Returns(Task.CompletedTask);
        harness.PushService
            .Setup(service => service.PushNotificationAsync(userId, It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        await harness.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = 7001,
            BusinessKey = "notification:test:7001",
            Type = NotificationType.SystemAnnouncement,
            Title = "测试通知",
            ReceiverUserIds = [userId]
        });

        harness.NotificationCustomRepository.VerifyAll();
        harness.CacheService.VerifyAll();
        harness.PushService.VerifyAll();
    }

    [Fact(DisplayName = "重复可靠通知不应重复增量未读缓存或推送")]
    public async Task CreateNotificationAsync_ShouldSkipPush_WhenMessageWriteAlreadyExists()
    {
        var harness = CreateHarness();
        harness.NotificationCustomRepository
            .Setup(repository => repository.PersistAsync(
                It.Is<Notification>(notification => notification.Id == 7001),
                It.IsAny<IReadOnlyList<UserNotification>>()))
            .ReturnsAsync(false);

        var notificationId = await harness.Service.CreateNotificationAsync(new CreateNotificationDto
        {
            NotificationId = 7001,
            BusinessKey = "notification:test:7001",
            Type = NotificationType.SystemAnnouncement,
            Title = "测试通知",
            ReceiverUserIds = [1001]
        });

        notificationId.ShouldBe(7001);
        harness.NotificationCustomRepository.VerifyAll();
        harness.CacheService.VerifyNoOtherCalls();
        harness.PushService.VerifyNoOtherCalls();
    }

    [Fact(DisplayName = "全部已读在无更新行时仍应清零未读缓存并推送")]
    public async Task MarkAllAsReadAsync_ShouldResetUnreadCache_WhenNoUnreadRowsAffected()
    {
        const long userId = 1001;
        var harness = CreateHarness();

        harness.UserNotificationRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserNotification, UserNotification>>>(),
                It.IsAny<Expression<Func<UserNotification, bool>>>()))
            .ReturnsAsync(0);

        harness.CacheService
            .Setup(service => service.SetUnreadCountAsync(userId, 0))
            .Returns(Task.CompletedTask);

        harness.PushService
            .Setup(service => service.PushUnreadCountAsync(userId, 0))
            .Returns(Task.CompletedTask);

        var affectedRows = await harness.Service.MarkAllAsReadAsync(userId);

        affectedRows.ShouldBe(0);
        harness.CacheService.Verify(service => service.SetUnreadCountAsync(userId, 0), Times.Once);
        harness.PushService.Verify(service => service.PushUnreadCountAsync(userId, 0), Times.Once);
    }

    [Fact(DisplayName = "全部已读在有更新行时应清零未读缓存并推送")]
    public async Task MarkAllAsReadAsync_ShouldResetUnreadCache_WhenUnreadRowsAffected()
    {
        const long userId = 1002;
        var harness = CreateHarness();

        harness.UserNotificationRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<UserNotification, UserNotification>>>(),
                It.IsAny<Expression<Func<UserNotification, bool>>>()))
            .ReturnsAsync(3);

        harness.CacheService
            .Setup(service => service.SetUnreadCountAsync(userId, 0))
            .Returns(Task.CompletedTask);

        harness.PushService
            .Setup(service => service.PushUnreadCountAsync(userId, 0))
            .Returns(Task.CompletedTask);

        var affectedRows = await harness.Service.MarkAllAsReadAsync(userId);

        affectedRows.ShouldBe(3);
        harness.CacheService.Verify(service => service.SetUnreadCountAsync(userId, 0), Times.Once);
        harness.PushService.Verify(service => service.PushUnreadCountAsync(userId, 0), Times.Once);
    }

    private static NotificationServiceHarness CreateHarness()
    {
        var notificationRepository = new Mock<IBaseRepository<Notification>>(MockBehavior.Strict);
        var userNotificationRepository = new Mock<IBaseRepository<UserNotification>>(MockBehavior.Strict);
        var notificationCustomRepository = new Mock<INotificationRepository>(MockBehavior.Strict);
        var pushService = new Mock<INotificationPushService>(MockBehavior.Strict);
        var cacheService = new Mock<INotificationCacheService>(MockBehavior.Strict);
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var logger = new Mock<ILogger<NotificationService>>();

        var service = new NotificationService(
            notificationRepository.Object,
            userNotificationRepository.Object,
            notificationCustomRepository.Object,
            pushService.Object,
            cacheService.Object,
            mapper.Object,
            logger.Object);

        return new NotificationServiceHarness(
            service,
            notificationCustomRepository,
            userNotificationRepository,
            pushService,
            cacheService);
    }

    private sealed record NotificationServiceHarness(
        NotificationService Service,
        Mock<INotificationRepository> NotificationCustomRepository,
        Mock<IBaseRepository<UserNotification>> UserNotificationRepository,
        Mock<INotificationPushService> PushService,
        Mock<INotificationCacheService> CacheService);
}
