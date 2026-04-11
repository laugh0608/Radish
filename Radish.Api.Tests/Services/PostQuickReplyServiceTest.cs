using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Radish.Common.CacheTool;
using Radish.Common.OptionTool;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Services;

/// <summary>
/// 轻回应通知测试
/// </summary>
public class PostQuickReplyServiceTest
{
    [Fact(DisplayName = "创建轻回应后应给帖子作者发送通知")]
    public async Task CreateAsync_ShouldSendNotification_WhenPostAuthorIsDifferent()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var quickReplyRepository = new Mock<IBaseRepository<PostQuickReply>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var caching = new Mock<ICaching>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostQuickReplyService>>();

        var request = new CreatePostQuickReplyDto
        {
            PostId = 1001,
            Content = "好耶🙂"
        };
        var post = new Post(new PostInitializationOptions("测试帖子", "正文"))
        {
            Id = 1001,
            AuthorId = 2001,
            AuthorName = "post-author",
            IsPublished = true,
            IsEnabled = true
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1001))
            .ReturnsAsync(post);

        quickReplyRepository
            .Setup(repository => repository.AddAsync(It.Is<PostQuickReply>(reply =>
                reply.PostId == 1001 &&
                reply.AuthorId == 3001 &&
                reply.AuthorName == "quick-user" &&
                reply.Content == "好耶🙂" &&
                reply.TenantId == 1)))
            .ReturnsAsync(5001);

        caching
            .Setup(cache => cache.ExistsAsync("post_quick_reply:cooldown:3001:1001"))
            .ReturnsAsync(false);
        caching
            .Setup(cache => cache.ExistsAsync(It.Is<string>(key =>
                key.StartsWith("post_quick_reply:dedup:3001:1001:", StringComparison.Ordinal))))
            .ReturnsAsync(false);
        caching
            .Setup(cache => cache.SetStringAsync("post_quick_reply:cooldown:3001:1001", "1", It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        caching
            .Setup(cache => cache.SetStringAsync(
                It.Is<string>(key => key.StartsWith("post_quick_reply:dedup:3001:1001:", StringComparison.Ordinal)),
                "1",
                It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        mapper
            .Setup(m => m.Map<PostQuickReplyVo>(It.IsAny<PostQuickReply>()))
            .Returns((PostQuickReply reply) => new PostQuickReplyVo
            {
                VoId = reply.Id,
                VoPostId = reply.PostId,
                VoAuthorId = reply.AuthorId,
                VoAuthorName = reply.AuthorName,
                VoContent = reply.Content,
                VoStatus = reply.Status,
                VoCreateTime = reply.CreateTime
            });

        notificationService
            .Setup(service => service.CreateNotificationAsync(It.Is<CreateNotificationDto>(dto =>
                dto.Type == NotificationType.PostQuickReplied &&
                dto.Title == "帖子收到轻回应" &&
                dto.Content == "好耶🙂" &&
                dto.Priority == (int)NotificationPriority.Normal &&
                dto.BusinessType == BusinessType.Post &&
                dto.BusinessId == 1001 &&
                dto.TriggerId == 3001 &&
                dto.TriggerName == "quick-user" &&
                dto.ReceiverUserIds.Count == 1 &&
                dto.ReceiverUserIds.Contains(2001) &&
                dto.ExtData != null &&
                dto.ExtData.Contains("\"app\":\"forum\"") &&
                dto.ExtData.Contains("\"postId\":\"1001\""))))
            .ReturnsAsync(7001);

        var service = CreateService(
            mapper,
            quickReplyRepository,
            postRepository,
            caching,
            notificationService,
            logger);

        var result = await service.CreateAsync(request, 3001, "quick-user", 1);

        result.VoId.ShouldBe(5001);
        result.VoPostId.ShouldBe(1001);
        result.VoAuthorId.ShouldBe(3001);
        result.VoContent.ShouldBe("好耶🙂");

        notificationService.VerifyAll();
        quickReplyRepository.VerifyAll();
        postRepository.VerifyAll();
        caching.VerifyAll();
    }

    [Fact(DisplayName = "帖子作者自己发轻回应时不应给自己发送通知")]
    public async Task CreateAsync_ShouldNotSendNotification_WhenAuthorRepliesOwnPost()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var quickReplyRepository = new Mock<IBaseRepository<PostQuickReply>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var caching = new Mock<ICaching>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostQuickReplyService>>();

        var request = new CreatePostQuickReplyDto
        {
            PostId = 1002,
            Content = "作者路过"
        };
        var post = new Post(new PostInitializationOptions("作者帖子", "正文"))
        {
            Id = 1002,
            AuthorId = 4001,
            AuthorName = "author",
            IsPublished = true,
            IsEnabled = true
        };

        postRepository
            .Setup(repository => repository.QueryByIdAsync(1002))
            .ReturnsAsync(post);

        quickReplyRepository
            .Setup(repository => repository.AddAsync(It.IsAny<PostQuickReply>()))
            .ReturnsAsync(5002);

        caching
            .Setup(cache => cache.ExistsAsync("post_quick_reply:cooldown:4001:1002"))
            .ReturnsAsync(false);
        caching
            .Setup(cache => cache.ExistsAsync(It.Is<string>(key =>
                key.StartsWith("post_quick_reply:dedup:4001:1002:", StringComparison.Ordinal))))
            .ReturnsAsync(false);
        caching
            .Setup(cache => cache.SetStringAsync("post_quick_reply:cooldown:4001:1002", "1", It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        caching
            .Setup(cache => cache.SetStringAsync(
                It.Is<string>(key => key.StartsWith("post_quick_reply:dedup:4001:1002:", StringComparison.Ordinal)),
                "1",
                It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        mapper
            .Setup(m => m.Map<PostQuickReplyVo>(It.IsAny<PostQuickReply>()))
            .Returns((PostQuickReply reply) => new PostQuickReplyVo
            {
                VoId = reply.Id,
                VoPostId = reply.PostId,
                VoAuthorId = reply.AuthorId,
                VoAuthorName = reply.AuthorName,
                VoContent = reply.Content,
                VoStatus = reply.Status,
                VoCreateTime = reply.CreateTime
            });

        var service = CreateService(
            mapper,
            quickReplyRepository,
            postRepository,
            caching,
            notificationService,
            logger);

        var result = await service.CreateAsync(request, 4001, "author", 1);

        result.VoId.ShouldBe(5002);
        notificationService.Verify(
            service => service.CreateNotificationAsync(It.IsAny<CreateNotificationDto>()),
            Times.Never);
    }

    private static PostQuickReplyService CreateService(
        Mock<IMapper> mapper,
        Mock<IBaseRepository<PostQuickReply>> quickReplyRepository,
        Mock<IBaseRepository<Post>> postRepository,
        Mock<ICaching> caching,
        Mock<INotificationService> notificationService,
        Mock<ILogger<PostQuickReplyService>> logger)
    {
        return new PostQuickReplyService(
            mapper.Object,
            quickReplyRepository.Object,
            postRepository.Object,
            caching.Object,
            Options.Create(new ForumQuickReplyOptions
            {
                Enable = true,
                MaxContentLength = 10,
                PerPostCooldownSeconds = 30,
                DuplicateWindowSeconds = 300,
                DefaultTake = 30,
                MaxTake = 60
            }),
            attachmentService: null,
            notificationService: notificationService.Object,
            logger: logger.Object);
    }
}
