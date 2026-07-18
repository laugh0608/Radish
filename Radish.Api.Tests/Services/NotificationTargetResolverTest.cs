using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class NotificationTargetResolverTest
{
    [Fact(DisplayName = "通知回复目标的评论已删除时不得继续返回可点击链接")]
    public async Task ResolveAsync_ShouldReturnDeletedWhenCommentNoLongerExists()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        postRepository
            .Setup(item => item.QueryByIdsAsync(It.Is<List<long>>(ids => ids.SequenceEqual(new long[] { 5001 }))))
            .ReturnsAsync([new Post { Id = 5001, TenantId = 9, PublicId = "pst_5001" }]);
        commentRepository
            .Setup(item => item.QueryByIdsAsync(It.Is<List<long>>(ids => ids.SequenceEqual(new long[] { 6001 }))))
            .ReturnsAsync([new Comment
            {
                Id = 6001,
                PostId = 5001,
                TenantId = 9,
                IsDeleted = true
            }]);
        var resolver = CreateResolver(postRepository.Object, commentRepository.Object);

        var result = await resolver.ResolveAsync(9, 1001,
        [
            CreateNotification(7001, new NotificationTargetData
            {
                PostId = 5001,
                PostPublicId = "pst_5001",
                CommentId = 6001
            })
        ]);

        result[7001].VoKind.ShouldBe(NotificationTargetKind.None);
        result[7001].VoUnavailableReason.ShouldBe("Notification.Target.Deleted");
        result[7001].VoPostId.ShouldBeNull();
        postRepository.VerifyAll();
        commentRepository.VerifyAll();
    }

    [Fact(DisplayName = "通知回复目标仅在评论仍属于对应帖子时返回结构化目标")]
    public async Task ResolveAsync_ShouldKeepValidForumCommentTarget()
    {
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var commentRepository = new Mock<IBaseRepository<Comment>>(MockBehavior.Strict);
        postRepository
            .Setup(item => item.QueryByIdsAsync(It.IsAny<List<long>>()))
            .ReturnsAsync([new Post { Id = 5001, TenantId = 9, PublicId = "pst_5001" }]);
        commentRepository
            .Setup(item => item.QueryByIdsAsync(It.IsAny<List<long>>()))
            .ReturnsAsync([new Comment { Id = 6001, PostId = 5001, TenantId = 9 }]);
        var resolver = CreateResolver(postRepository.Object, commentRepository.Object);

        var result = await resolver.ResolveAsync(9, 1001,
        [
            CreateNotification(7002, new NotificationTargetData
            {
                PostId = 5001,
                PostPublicId = "pst_5001",
                CommentId = 6001
            })
        ]);

        result[7002].VoKind.ShouldBe(NotificationTargetKind.ForumPost);
        result[7002].VoPostId.ShouldBe("5001");
        result[7002].VoPostPublicId.ShouldBe("pst_5001");
        result[7002].VoCommentId.ShouldBe("6001");
        result[7002].VoUnavailableReason.ShouldBeNull();
    }

    private static NotificationTargetResolver CreateResolver(
        IBaseRepository<Post> postRepository,
        IBaseRepository<Comment> commentRepository)
    {
        return new NotificationTargetResolver(
            postRepository,
            commentRepository,
            Mock.Of<IBaseRepository<User>>(),
            Mock.Of<IBaseRepository<Order>>(),
            Mock.Of<IBaseRepository<UserBenefit>>(),
            Mock.Of<IBaseRepository<WikiDocument>>(),
            Mock.Of<IChannelMessageRepository>(),
            Mock.Of<IChatChannelAccessService>());
    }

    private static Notification CreateNotification(long id, NotificationTargetData target)
    {
        return new Notification(new NotificationInitializationOptions(NotificationType.CommentReplied, "评论回复")
        {
            Category = NotificationCategory.Discussion,
            TemplateKey = "notification.CommentReplied",
            TargetKind = NotificationTargetKind.ForumPost,
            TargetDataJson = target.ToJson(),
            OccurredAtUtc = DateTime.UtcNow,
            TenantId = 9
        })
        {
            Id = id,
            BusinessKey = $"notification:test:{id}",
            CreateTime = DateTime.UtcNow
        };
    }
}
