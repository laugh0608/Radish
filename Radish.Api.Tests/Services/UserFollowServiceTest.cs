using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.OptionTool;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class UserFollowServiceTest
{
    [Fact]
    public async Task FollowAsync_ShouldRestoreSoftDeletedPairAndQueueNotification()
    {
        var outboxService = new Mock<IReliableOutboxService>();
        var harness = CreateHarness(outboxService);
        var deletedFollow = new UserFollow
        {
            Id = 301,
            FollowerUserId = 10001,
            FollowingUserId = 20002,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            DeletedBy = "alice",
            TenantId = 0
        };
        harness.UserRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync((Expression<Func<User, bool>> predicate) =>
            {
                var users = new[]
                {
                    new User { Id = 10001, UserName = "alice", PublicId = "usr_alice", IsEnable = true },
                    new User { Id = 20002, UserName = "bob", PublicId = "usr_bob", IsEnable = true }
                };
                return users.FirstOrDefault(predicate.Compile());
            });
        harness.UserFollowRepository
            .Setup(repository => repository.QueryPairIncludingDeletedAsync(10001, 20002, 0))
            .ReturnsAsync(deletedFollow);
        harness.UserFollowRepository
            .Setup(repository => repository.UpdateAsync(deletedFollow))
            .ReturnsAsync(true);
        harness.AttachmentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync([]);
        outboxService
            .Setup(service => service.AddAsync(
                ReliableOutboxSources.Main,
                0,
                ReliableTaskTypes.NotificationRequested,
                It.IsAny<string>(),
                "UserFollow",
                "10001:20002",
                It.IsAny<NotificationRequestedTaskPayload>(),
                It.IsAny<DateTime>(),
                1))
            .ReturnsAsync(401L);

        var result = await harness.Service.FollowAsync(10001, 20002, 0, "alice");

        Assert.True(result);
        Assert.False(deletedFollow.IsDeleted);
        Assert.Null(deletedFollow.DeletedAt);
        Assert.Null(deletedFollow.DeletedBy);
        harness.UserFollowRepository.Verify(repository => repository.UpdateAsync(deletedFollow), Times.Once);
        outboxService.Verify(service => service.AddAsync(
            ReliableOutboxSources.Main,
            0,
            ReliableTaskTypes.NotificationRequested,
            It.IsAny<string>(),
            "UserFollow",
            "10001:20002",
            It.Is<NotificationRequestedTaskPayload>(payload =>
                payload.Notification.Type == NotificationType.Followed &&
                payload.Notification.ReceiverUserIds.SequenceEqual(new[] { 20002L }) &&
                payload.Notification.Target != null &&
                payload.Notification.Target.UserId == 10001),
            It.IsAny<DateTime>(),
            1), Times.Once);
    }

    [Fact]
    public async Task GetMyFollowingAsync_ShouldReturnUserPublicId()
    {
        var harness = CreateHarness();
        var followTime = DateTime.UtcNow;
        var publicId = "usr_018f6b6f7c7d70008f8f8f8f8f8f8f8f";

        harness.UserFollowRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<UserFollow, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<UserFollow, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<UserFollow>
            {
                new()
                {
                    Id = 1,
                    FollowerUserId = 10001,
                    FollowingUserId = 20002,
                    FollowTime = followTime
                }
            }, 1));
        harness.UserRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User>
            {
                new()
                {
                    Id = 20002,
                    UserName = "alice",
                    PublicId = publicId,
                    PublicIndex = 1086,
                    IsEnable = true
                }
            });
        harness.AttachmentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(new List<Attachment>());
        harness.UserFollowRepository
            .Setup(repository => repository.QueryDistinctAsync(
                It.IsAny<Expression<Func<UserFollow, long>>>(),
                It.IsAny<Expression<Func<UserFollow, bool>>>()))
            .ReturnsAsync(new List<long>());

        var result = await harness.Service.GetMyFollowingAsync(10001, 1, 20);

        Assert.Single(result.VoItems);
        Assert.Equal(publicId, result.VoItems[0].VoPublicId);
        Assert.Equal(1086, result.VoItems[0].VoPublicIndex);
        Assert.Equal("alice#1086", result.VoItems[0].VoDisplayHandle);
    }

    [Fact]
    public async Task GetMyFollowingAsync_ShouldBackfillUserPublicId_WhenMissing()
    {
        var harness = CreateHarness();
        var followedUser = new User
        {
            Id = 20002,
            UserName = "alice",
            PublicId = null,
            IsEnable = true
        };

        harness.UserFollowRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<UserFollow, bool>>>(),
                1,
                20,
                It.IsAny<Expression<Func<UserFollow, object>>>(),
                OrderByType.Desc))
            .ReturnsAsync((new List<UserFollow>
            {
                new()
                {
                    Id = 1,
                    FollowerUserId = 10001,
                    FollowingUserId = 20002,
                    FollowTime = DateTime.UtcNow
                }
            }, 1));
        harness.UserRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(new List<User> { followedUser });
        harness.UserRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<User, User>>>(),
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(1);
        harness.UserRepository
            .Setup(repository => repository.QueryMaxAsync(
                It.IsAny<Expression<Func<User, long?>>>(),
                It.IsAny<Expression<Func<User, bool>>>()))
            .ReturnsAsync(1085);
        harness.AttachmentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>>()))
            .ReturnsAsync(new List<Attachment>());
        harness.UserFollowRepository
            .Setup(repository => repository.QueryDistinctAsync(
                It.IsAny<Expression<Func<UserFollow, long>>>(),
                It.IsAny<Expression<Func<UserFollow, bool>>>()))
            .ReturnsAsync(new List<long>());

        var result = await harness.Service.GetMyFollowingAsync(10001, 1, 20);

        Assert.Single(result.VoItems);
        Assert.StartsWith("usr_", result.VoItems[0].VoPublicId);
        Assert.Equal(1086, result.VoItems[0].VoPublicIndex);
        harness.UserRepository.Verify(repository => repository.UpdateColumnsAsync(
            It.IsAny<Expression<Func<User, User>>>(),
            It.IsAny<Expression<Func<User, bool>>>()), Times.Once);
    }

    private static UserFollowServiceHarness CreateHarness(Mock<IReliableOutboxService>? outboxService = null)
    {
        var mapper = new Mock<IMapper>();
        var userFollowRepository = new Mock<IUserFollowRepository>();
        var userRepository = new Mock<IBaseRepository<User>>();
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>();
        var postService = new Mock<IPostService>();
        var notificationService = new Mock<INotificationService>();
        var logger = new Mock<ILogger<UserFollowService>>();
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>();

        var service = new UserFollowService(
            mapper.Object,
            userFollowRepository.Object,
            userRepository.Object,
            postService.Object,
            attachmentRepository.Object,
            notificationService.Object,
            logger.Object,
            Options.Create(new FeedDistributionOptions()),
            attachmentUrlResolver.Object,
            outboxService?.Object);

        return new UserFollowServiceHarness(
            service,
            userFollowRepository,
            userRepository,
            attachmentRepository);
    }

    private sealed record UserFollowServiceHarness(
        UserFollowService Service,
        Mock<IUserFollowRepository> UserFollowRepository,
        Mock<IBaseRepository<User>> UserRepository,
        Mock<IBaseRepository<Attachment>> AttachmentRepository);
}
