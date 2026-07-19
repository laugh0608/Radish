using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChatMessagePinServiceTest
{
    [Fact]
    public async Task GetStateAsync_ShouldReturnRevisionedPinsInRepositoryOrder()
    {
        var pinRepository = new Mock<IChatMessagePinRepository>(MockBehavior.Strict);
        pinRepository
            .Setup(repository => repository.GetSnapshotAsync(30000, 70001))
            .ReturnsAsync(new ChatMessagePinSnapshot(30000, 4,
            [
                CreatePin(92002, 90002, new DateTime(2026, 7, 19, 10, 2, 0, DateTimeKind.Utc)),
                CreatePin(92001, 90001, new DateTime(2026, 7, 19, 10, 1, 0, DateTimeKind.Utc))
            ]));
        var chatService = new Mock<IChatService>(MockBehavior.Strict);
        chatService
            .Setup(service => service.GetMessagesByIdsAsync(
                30000,
                20001,
                70001,
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 90002, 90001 }))))
            .ReturnsAsync([CreateMessageVo(90001), CreateMessageVo(90002)]);
        var service = CreateService(
            pinRepository,
            new Mock<IChannelMessageRepository>(MockBehavior.Strict),
            CreateAccessService(CanPinAccess()),
            chatService);

        var result = await service.GetStateAsync(30000, 20001, 70001);

        Assert.Equal(4, result.VoRevision);
        Assert.Equal(new long[] { 90002, 90001 }, result.VoItems.Select(item => item.VoMessageId));
        Assert.Equal(90002, result.VoItems[0].VoMessage.VoId);
    }

    [Fact]
    public async Task SetAsync_ShouldRejectReadableConversationWithoutPinCapability()
    {
        var service = CreateService(
            new Mock<IChatMessagePinRepository>(MockBehavior.Strict),
            new Mock<IChannelMessageRepository>(MockBehavior.Strict),
            CreateAccessService(new ChatChannelAccessResult(
                true,
                ChannelType.Private,
                true,
                false,
                true,
                false,
                true,
                DirectConversationRequestStatus.Accepted,
                DirectBlockedByUserId: 20002)),
            new Mock<IChatService>(MockBehavior.Strict));

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SetAsync(
            30000,
            20001,
            "Tester",
            false,
            CreateSetRequest()));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("Chat.PinNotAllowed", exception.ErrorCode);
        Assert.Equal("error.chat.pin_not_allowed", exception.MessageKey);
    }

    [Fact]
    public async Task SetAsync_ShouldReturnLatestAuthoritativeSnapshotWhenConcurrentWriteAdvancesRevision()
    {
        var now = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);
        var pinRepository = new Mock<IChatMessagePinRepository>(MockBehavior.Strict);
        pinRepository
            .Setup(repository => repository.SetAsync(It.Is<ChatMessagePinSetCommand>(command =>
                command.TenantId == 30000 &&
                command.ChannelId == 70001 &&
                command.MessageId == 90001 &&
                command.UserId == 20001 &&
                command.IsPinned)))
            .ReturnsAsync(new ChatMessagePinWriteResult(1, true));
        pinRepository
            .Setup(repository => repository.GetSnapshotAsync(30000, 70001))
            .ReturnsAsync(new ChatMessagePinSnapshot(30000, 2,
            [
                CreatePin(92001, 90001, now)
            ]));
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        messageRepository
            .Setup(repository => repository.QueryFirstIncludingDeletedAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ChannelMessage, bool>>>() ))
            .ReturnsAsync(CreateMessage());
        var chatService = new Mock<IChatService>(MockBehavior.Strict);
        chatService
            .Setup(service => service.GetMessagesByIdsAsync(
                30000,
                20001,
                70001,
                It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync([CreateMessageVo(90001)]);
        var service = CreateService(
            pinRepository,
            messageRepository,
            CreateAccessService(CanPinAccess()),
            chatService,
            new FixedTimeProvider(now));

        var result = await service.SetAsync(
            30000,
            20001,
            "Tester",
            false,
            CreateSetRequest());

        Assert.True(result.VoChanged);
        Assert.Equal(2, result.VoState.VoRevision);
        Assert.Single(result.VoState.VoItems);
        pinRepository.VerifyAll();
        messageRepository.VerifyAll();
        chatService.VerifyAll();
    }

    private static ChatMessagePinService CreateService(
        Mock<IChatMessagePinRepository> pinRepository,
        Mock<IChannelMessageRepository> messageRepository,
        Mock<IChatChannelAccessService> accessService,
        Mock<IChatService> chatService,
        TimeProvider? timeProvider = null)
    {
        return new ChatMessagePinService(
            pinRepository.Object,
            messageRepository.Object,
            accessService.Object,
            chatService.Object,
            timeProvider ?? TimeProvider.System);
    }

    private static Mock<IChatChannelAccessService> CreateAccessService(ChatChannelAccessResult access)
    {
        var service = new Mock<IChatChannelAccessService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.GetAccessAsync(
                30000,
                20001,
                70001,
                It.IsAny<bool>()))
            .ReturnsAsync(access);
        return service;
    }

    private static ChatChannelAccessResult CanPinAccess() => new(
        true,
        ChannelType.Public,
        true,
        true,
        true,
        true,
        false,
        ChannelMemberRole: MemberRole.Moderator);

    private static ChannelMessage CreateMessage() => new()
    {
        Id = 90001,
        TenantId = 30000,
        ChannelId = 70001,
        UserId = 20002,
        UserName = "Author",
        Type = MessageType.Text,
        Content = "hello",
        CreateTime = DateTime.UtcNow
    };

    private static ChannelMessageVo CreateMessageVo(long messageId) => new()
    {
        VoId = messageId,
        VoChannelId = 70001,
        VoUserId = 20002,
        VoUserName = "Author",
        VoType = MessageType.Text,
        VoContent = $"message-{messageId}",
        VoCreateTime = DateTime.UtcNow
    };

    private static ChatMessagePin CreatePin(long id, long messageId, DateTime pinnedAt) => new()
    {
        Id = id,
        TenantId = 30000,
        ChannelId = 70001,
        MessageId = messageId,
        PinnedByUserId = 20001,
        PinnedByName = "Moderator",
        PinnedAt = pinnedAt
    };

    private static SetChatMessagePinDto CreateSetRequest() => new()
    {
        ChannelId = 70001,
        MessageId = 90001,
        IsPinned = true
    };

    private sealed class FixedTimeProvider(DateTime nowUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(nowUtc, TimeSpan.Zero);
    }
}
