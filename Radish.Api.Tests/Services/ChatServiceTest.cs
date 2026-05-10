using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AutoMapper;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Service;
using Shouldly;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public class ChatServiceTest
{
    [Fact]
    public async Task GetHistoryAsync_Should_Return_Recalled_Placeholder_Message()
    {
        var channelRepository = new Mock<IBaseRepository<Channel>>(MockBehavior.Strict);
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(channelRepository, messageRepository, attachmentRepository);

        channelRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<Channel, bool>>?>()))
            .ReturnsAsync(new Channel { Id = 1, Name = "General", IsEnabled = true, IsDeleted = false });
        messageRepository
            .Setup(repo => repo.QueryPageIncludingDeletedAsync(
                It.IsAny<Expression<Func<ChannelMessage, bool>>?>(),
                1,
                50,
                It.IsAny<Expression<Func<ChannelMessage, object>>?>(),
                OrderByType.Desc))
            .ReturnsAsync((
                new List<ChannelMessage>
                {
                    new()
                    {
                        Id = 90002,
                        ChannelId = 1,
                        UserId = 10002,
                        UserName = "recalled",
                        Type = MessageType.Text,
                        Content = "should-be-cleared",
                        IsDeleted = true,
                        CreateTime = new DateTime(2026, 5, 10, 12, 2, 0)
                    },
                    new()
                    {
                        Id = 90001,
                        ChannelId = 1,
                        UserId = 10001,
                        UserName = "alive",
                        Type = MessageType.Text,
                        Content = "hello",
                        IsDeleted = false,
                        CreateTime = new DateTime(2026, 5, 10, 12, 1, 0)
                    }
                },
                2));
        messageRepository
            .Setup(repo => repo.QueryByIdsIncludingDeletedAsync(It.IsAny<List<long>>()))
            .ReturnsAsync(new List<ChannelMessage>());
        attachmentRepository
            .Setup(repo => repo.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync(new List<Attachment>());

        var result = await service.GetHistoryAsync(0, 10001, 1, null, null, 50);

        result.Count.ShouldBe(2);
        result.Select(item => item.VoId).ShouldBe(new long[] { 90001, 90002 });
        var recalledMessage = result.Single(item => item.VoId == 90002);
        recalledMessage.VoIsRecalled.ShouldBeTrue();
        recalledMessage.VoContent.ShouldBeNull();
    }

    [Fact]
    public async Task GetMessageWindowAsync_Should_Return_Recalled_Anchor_And_Window_Flags()
    {
        var channelRepository = new Mock<IBaseRepository<Channel>>(MockBehavior.Strict);
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var service = CreateService(channelRepository, messageRepository, attachmentRepository);

        channelRepository
            .Setup(repo => repo.QueryFirstAsync(It.IsAny<Expression<Func<Channel, bool>>?>()))
            .ReturnsAsync(new Channel { Id = 1, Name = "General", IsEnabled = true, IsDeleted = false });
        messageRepository
            .Setup(repo => repo.QueryFirstIncludingDeletedAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>()))
            .ReturnsAsync(new ChannelMessage
            {
                Id = 90002,
                ChannelId = 1,
                UserId = 10002,
                UserName = "anchor",
                Type = MessageType.Text,
                Content = "anchor",
                IsDeleted = true,
                CreateTime = new DateTime(2026, 5, 10, 12, 2, 0)
            });
        messageRepository
            .Setup(repo => repo.QueryPageIncludingDeletedAsync(
                It.IsAny<Expression<Func<ChannelMessage, bool>>?>(),
                1,
                1,
                It.IsAny<Expression<Func<ChannelMessage, object>>?>(),
                It.IsAny<OrderByType>()))
            .ReturnsAsync((
                Expression<Func<ChannelMessage, bool>>? whereExpression,
                int pageIndex,
                int pageSize,
                Expression<Func<ChannelMessage, object>>? orderByExpression,
                OrderByType orderByType) => orderByType == OrderByType.Desc
                ? (
                    new List<ChannelMessage>
                    {
                        new()
                        {
                            Id = 90001,
                            ChannelId = 1,
                            UserId = 10001,
                            UserName = "older",
                            Type = MessageType.Text,
                            Content = "older",
                            IsDeleted = false,
                            CreateTime = new DateTime(2026, 5, 10, 12, 1, 0)
                        }
                    },
                    1)
                : (
                    new List<ChannelMessage>
                    {
                        new()
                        {
                            Id = 90003,
                            ChannelId = 1,
                            UserId = 10003,
                            UserName = "newer",
                            Type = MessageType.Text,
                            Content = "newer",
                            IsDeleted = false,
                            CreateTime = new DateTime(2026, 5, 10, 12, 3, 0)
                        }
                    },
                    1));
        messageRepository
            .Setup(repo => repo.QueryByIdsIncludingDeletedAsync(It.IsAny<List<long>>()))
            .ReturnsAsync(new List<ChannelMessage>());
        messageRepository
            .SetupSequence(repo => repo.QueryExistsIncludingDeletedAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        attachmentRepository
            .Setup(repo => repo.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync(new List<Attachment>());

        var result = await service.GetMessageWindowAsync(0, 10001, 1, 90002, 1, 1);

        result.ShouldNotBeNull();
        result.VoChannelId.ShouldBe(1);
        result.VoAnchorMessageId.ShouldBe(90002);
        result.VoHasMoreBefore.ShouldBeTrue();
        result.VoHasMoreAfter.ShouldBeFalse();
        result.VoMessages.Select(item => item.VoId).ShouldBe(new long[] { 90001, 90002, 90003 });
        var anchor = result.VoMessages.Single(item => item.VoId == 90002);
        anchor.VoIsRecalled.ShouldBeTrue();
        anchor.VoContent.ShouldBeNull();
    }

    [Fact]
    public async Task RecallMessageAsync_Should_Return_ChannelId_When_Message_Already_Recalled()
    {
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var service = CreateService(messageRepository: messageRepository);

        messageRepository
            .Setup(repo => repo.QueryFirstIncludingDeletedAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>()))
            .ReturnsAsync(new ChannelMessage
            {
                Id = 90002,
                ChannelId = 1,
                UserId = 10002,
                UserName = "anchor",
                Type = MessageType.Text,
                Content = null,
                IsDeleted = true,
                CreateTime = new DateTime(2026, 5, 10, 12, 2, 0)
            });

        var result = await service.RecallMessageAsync(0, 10001, "tester", 90002, false);

        result.ShouldBe(1);
    }

    private static ChatService CreateService(
        Mock<IBaseRepository<Channel>>? channelRepository = null,
        Mock<IChannelMessageRepository>? messageRepository = null,
        Mock<IBaseRepository<Attachment>>? attachmentRepository = null)
    {
        var mapper = new Mock<IMapper>(MockBehavior.Strict);
        mapper
            .Setup(instance => instance.Map<ChannelMessageVo>(It.IsAny<ChannelMessage>()))
            .Returns((ChannelMessage message) => new ChannelMessageVo
            {
                VoId = message.Id,
                VoChannelId = message.ChannelId,
                VoUserId = message.UserId,
                VoUserName = message.UserName,
                VoUserAvatarAttachmentId = message.UserAvatarAttachmentIdSnapshot,
                VoType = message.Type,
                VoContent = message.Content,
                VoReplyToId = message.ReplyToId,
                VoAttachmentId = message.AttachmentId,
                VoCreateTime = message.CreateTime
            });

        var baseChannelRepository = channelRepository ?? new Mock<IBaseRepository<Channel>>(MockBehavior.Strict);
        var baseMessageRepository = messageRepository ?? new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var memberRepository = new Mock<IBaseRepository<ChannelMember>>(MockBehavior.Strict);
        var baseAttachmentRepository = attachmentRepository ?? new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var chatPresenceService = new Mock<IChatPresenceService>(MockBehavior.Strict);
        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(It.IsAny<long>()))
            .Returns(string.Empty);
        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(It.IsAny<long>(), It.IsAny<AttachmentUrlVariant>()))
            .Returns(string.Empty);

        return new ChatService(
            mapper.Object,
            baseChannelRepository.Object,
            baseMessageRepository.Object,
            memberRepository.Object,
            baseAttachmentRepository.Object,
            userRepository.Object,
            chatPresenceService.Object,
            notificationService.Object,
            attachmentUrlResolver.Object);
    }
}
