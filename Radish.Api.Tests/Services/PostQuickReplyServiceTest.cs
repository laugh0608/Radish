using System;
using System.Collections.Generic;
using System.Linq.Expressions;
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
using SqlSugar;
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

    [Fact(DisplayName = "我的轻回应列表应并行返回帖子 PublicId")]
    public async Task GetMinePageAsync_ShouldReturnPostPublicId_WhenPostHasPublicId()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var quickReplyRepository = new Mock<IBaseRepository<PostQuickReply>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var caching = new Mock<ICaching>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostQuickReplyService>>();

        quickReplyRepository
            .Setup(repository => repository.QueryPageAsync(
                It.IsAny<Expression<Func<PostQuickReply, bool>>?>(),
                1,
                10,
                It.IsAny<Expression<Func<PostQuickReply, object>>?>(),
                OrderByType.Desc,
                It.IsAny<Expression<Func<PostQuickReply, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<PostQuickReply>
                {
                    new()
                    {
                        Id = 5003,
                        PostId = 1003,
                        AuthorId = 3001,
                        Content = "回看这条",
                        CreateTime = new DateTime(2026, 5, 16, 8, 0, 0, DateTimeKind.Utc)
                    }
                },
                1));

        postRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Post, bool>>?>()))
            .ReturnsAsync(new List<Post>
            {
                new(new PostInitializationOptions("PublicId 帖子", "正文"))
                {
                    Id = 1003,
                    PublicId = "pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f"
                }
            });

        var service = CreateService(
            mapper,
            quickReplyRepository,
            postRepository,
            caching,
            notificationService,
            logger);

        var (items, total) = await service.GetMinePageAsync(3001, 1, 10);

        total.ShouldBe(1);
        items.Count.ShouldBe(1);
        items[0].VoId.ShouldBe(5003);
        items[0].VoPostId.ShouldBe(1003);
        items[0].VoPostPublicId.ShouldBe("pst_018f6b6f7c7d70008f8f8f8f8f8f8f8f");
        items[0].VoPostTitle.ShouldBe("PublicId 帖子");
        items[0].VoContent.ShouldBe("回看这条");

        quickReplyRepository.VerifyAll();
        postRepository.VerifyAll();
    }

    [Fact(DisplayName = "创建轻回应应使用系统设置中的内容长度上限")]
    public async Task CreateAsync_ShouldUseSystemSettingMaxContentLength()
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        var quickReplyRepository = new Mock<IBaseRepository<PostQuickReply>>(MockBehavior.Strict);
        var postRepository = new Mock<IBaseRepository<Post>>(MockBehavior.Strict);
        var caching = new Mock<ICaching>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PostQuickReplyService>>();

        var service = CreateService(
            mapper,
            quickReplyRepository,
            postRepository,
            caching,
            notificationService,
            logger,
            maxContentLength: 3);

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.CreateAsync(new CreatePostQuickReplyDto
            {
                PostId = 1004,
                Content = "abcd"
            }, 3001, "quick-user", 1));

        exception.Message.ShouldBe("轻回应内容不能超过3个字符");
    }

    private static PostQuickReplyService CreateService(
        Mock<IMapper> mapper,
        Mock<IBaseRepository<PostQuickReply>> quickReplyRepository,
        Mock<IBaseRepository<Post>> postRepository,
        Mock<ICaching> caching,
        Mock<INotificationService> notificationService,
        Mock<ILogger<PostQuickReplyService>> logger,
        int maxContentLength = 10)
    {
        var systemSettingProvider = new Mock<ISystemSettingProvider>(MockBehavior.Strict);
        systemSettingProvider
            .Setup(provider => provider.GetInt32Async(SystemConfigDefaults.QuickReplyMaxContentLengthKey))
            .ReturnsAsync(maxContentLength);

        return new PostQuickReplyService(
            mapper.Object,
            quickReplyRepository.Object,
            postRepository.Object,
            caching.Object,
            systemSettingProvider.Object,
            Options.Create(new ForumQuickReplyOptions
            {
                Enable = true,
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
