using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChatReadReceiptServiceTest
{
    [Fact]
    public async Task AdvanceAsync_ShouldSubmitExactTargetAndExposeNoPublicReceiptEvent()
    {
        var message = CreateMessage(120, 20002);
        var fixture = CreateFixture(PublicAccess(), [message]);
        AdvanceChatReadStateCommand? captured = null;
        fixture.ReadRepository
            .Setup(repository => repository.AdvanceAsync(It.IsAny<AdvanceChatReadStateCommand>()))
            .Callback((AdvanceChatReadStateCommand command) => captured = command)
            .ReturnsAsync(new AdvanceChatReadStateResult(120, true));

        var result = await fixture.Service.AdvanceAsync(
            30000,
            20002,
            "Reader",
            new AdvanceChannelReadStateDto
            {
                ChannelId = 70001,
                ReadThroughMessageId = 120
            });

        Assert.NotNull(captured);
        Assert.Equal(120, captured.ReadThroughMessageId);
        Assert.True(captured.AllowCreate);
        Assert.True(result.State.VoChanged);
        Assert.Equal(120, result.State.VoLastReadMessageId);
        Assert.Equal(3, result.State.VoUnreadCount);
        Assert.False(result.ReceiptsChanged);
    }

    [Fact]
    public async Task GetSummariesAsync_ShouldAggregateOnlyOwnedPrivateMessages()
    {
        var messages = new[]
        {
            CreateMessage(100, 20001),
            CreateMessage(200, 20001)
        };
        var fixture = CreateFixture(PrivateGroupAccess(), messages);
        fixture.ReadRepository
            .Setup(repository => repository.GetReadCountsAsync(
                70001,
                20001,
                It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync([
                new ChatReadCountAggregate { MessageId = 100, ReadCount = 2 }
            ]);

        var result = await fixture.Service.GetSummariesAsync(
            30000,
            20001,
            new GetChatReadReceiptSummariesDto
            {
                ChannelId = 70001,
                MessageIds = [100, 100, 200]
            });

        Assert.Equal(ChatReadReceiptModes.PrivateGroup, result.VoMode);
        Assert.Equal(2, result.VoItems.Count);
        Assert.Equal(2, result.VoItems[0].VoReadCount);
        Assert.Equal(0, result.VoItems[1].VoReadCount);

        var notOwned = CreateFixture(PrivateGroupAccess(), [CreateMessage(100, 20002)]);
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            notOwned.Service.GetSummariesAsync(
                30000,
                20001,
                new GetChatReadReceiptSummariesDto
                {
                    ChannelId = 70001,
                    MessageIds = [100]
                }));
        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("Chat.ReceiptMessageNotOwned", exception.ErrorCode);
    }

    [Theory]
    [InlineData(ChannelType.Public)]
    [InlineData(ChannelType.Announcement)]
    public async Task GetSummariesAsync_ShouldSuppressOpenChannelReceipts(ChannelType channelType)
    {
        var fixture = CreateFixture(OpenChannelAccess(channelType), [CreateMessage(100, 20001)]);

        var result = await fixture.Service.GetSummariesAsync(
            30000,
            20001,
            new GetChatReadReceiptSummariesDto
            {
                ChannelId = 70001,
                MessageIds = [100]
            });

        Assert.Equal(ChatReadReceiptModes.None, result.VoMode);
        Assert.Empty(result.VoItems);
        fixture.ReadRepository.Verify(
            repository => repository.GetReadCountsAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<IReadOnlyCollection<long>>()),
            Times.Never);
        fixture.ReadRepository.Verify(
            repository => repository.GetMemberReadCursorAsync(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Theory]
    [InlineData(DirectConversationRequestStatus.Pending, false, true)]
    [InlineData(DirectConversationRequestStatus.Declined, false, true)]
    [InlineData(DirectConversationRequestStatus.Accepted, true, true)]
    [InlineData(DirectConversationRequestStatus.Accepted, false, false)]
    public async Task GetSummariesAsync_ShouldSuppressUnavailableDirectReceipts(
        DirectConversationRequestStatus status,
        bool blocked,
        bool peerAvailable)
    {
        var fixture = CreateFixture(
            DirectAccess(status, blocked, peerAvailable),
            [CreateMessage(100, 20001)]);

        var result = await fixture.Service.GetSummariesAsync(
            30000,
            20001,
            new GetChatReadReceiptSummariesDto
            {
                ChannelId = 70001,
                MessageIds = [100]
            });

        Assert.Equal(ChatReadReceiptModes.None, result.VoMode);
        Assert.Empty(result.VoItems);
        fixture.ReadRepository.Verify(
            repository => repository.GetMemberReadCursorAsync(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSummariesAsync_ShouldSuppressDirectStatesAndExposeAcceptedPeerBoundary()
    {
        var messages = new[]
        {
            CreateMessage(100, 20001),
            CreateMessage(200, 20001)
        };
        var pending = CreateFixture(DirectAccess(DirectConversationRequestStatus.Pending), messages);
        var pendingResult = await pending.Service.GetSummariesAsync(
            30000,
            20001,
            new GetChatReadReceiptSummariesDto
            {
                ChannelId = 70001,
                MessageIds = [100, 200]
            });
        Assert.Equal(ChatReadReceiptModes.None, pendingResult.VoMode);
        Assert.Empty(pendingResult.VoItems);
        pending.ReadRepository.Verify(
            repository => repository.GetMemberReadCursorAsync(It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);

        var accepted = CreateFixture(DirectAccess(DirectConversationRequestStatus.Accepted), messages);
        accepted.ReadRepository
            .Setup(repository => repository.GetMemberReadCursorAsync(70001, 20002))
            .ReturnsAsync(150);
        var acceptedResult = await accepted.Service.GetSummariesAsync(
            30000,
            20001,
            new GetChatReadReceiptSummariesDto
            {
                ChannelId = 70001,
                MessageIds = [100, 200]
            });
        Assert.Equal(ChatReadReceiptModes.Direct, acceptedResult.VoMode);
        Assert.True(acceptedResult.VoItems[0].VoPeerHasRead);
        Assert.False(acceptedResult.VoItems[1].VoPeerHasRead);
    }

    [Fact]
    public async Task GetReadersAsync_ShouldReturnBoundKeysetCursor()
    {
        var fixture = CreateFixture(PrivateGroupAccess(), [CreateMessage(100, 20001)]);
        fixture.ReadRepository
            .Setup(repository => repository.GetReaderUserIdsAsync(
                70001,
                20001,
                100,
                It.IsAny<DateTime>(),
                null,
                2))
            .ReturnsAsync([20002, 20003]);
        fixture.UserRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync([
                new User
                {
                    Id = 20002,
                    TenantId = 30000,
                    PublicId = "usr_0123456789abcdef0123456789abcdef",
                    PublicIndex = 1002,
                    UserName = "Reader",
                    IsEnable = true
                }
            ]);

        var firstPage = await fixture.Service.GetReadersAsync(
            30000,
            20001,
            70001,
            100,
            null,
            1);

        Assert.True(firstPage.VoHasMore);
        Assert.NotNull(firstPage.VoNextCursor);
        Assert.Single(firstPage.VoItems);
        Assert.Equal("Reader#1002", firstPage.VoItems[0].VoDisplayName);

        var otherMessageFixture = CreateFixture(
            PrivateGroupAccess(),
            [CreateMessage(200, 20001)]);
        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            otherMessageFixture.Service.GetReadersAsync(
                30000,
                20001,
                70001,
                200,
                firstPage.VoNextCursor,
                1));
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("Chat.ReceiptCursorInvalid", exception.ErrorCode);
    }

    private static Fixture CreateFixture(
        ChatChannelAccessResult access,
        IReadOnlyCollection<ChannelMessage> messages)
    {
        var readRepository = new Mock<IChatReadReceiptRepository>(MockBehavior.Strict);
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var accessService = new Mock<IChatChannelAccessService>(MockBehavior.Strict);
        var chatService = new Mock<IChatService>(MockBehavior.Strict);
        var userRepository = new Mock<IBaseRepository<User>>(MockBehavior.Strict);
        var attachmentRepository = new Mock<IBaseRepository<Attachment>>(MockBehavior.Strict);
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);

        accessService
            .Setup(service => service.GetAccessAsync(30000, It.IsAny<long>(), 70001, false))
            .ReturnsAsync(access);
        messageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.IsAny<List<long>>()))
            .ReturnsAsync((List<long> ids) => messages.Where(message => ids.Contains(message.Id)).ToList());
        messageRepository
            .Setup(repository => repository.QueryFirstIncludingDeletedAsync(
                It.IsAny<Expression<Func<ChannelMessage, bool>>>() ))
            .ReturnsAsync((Expression<Func<ChannelMessage, bool>> expression) =>
                messages.AsQueryable().FirstOrDefault(expression));
        chatService
            .Setup(service => service.GetChannelUnreadStateAsync(30000, It.IsAny<long>(), 70001))
            .ReturnsAsync(new ChannelUnreadStateVo
            {
                VoChannelId = 70001,
                VoUnreadCount = 3,
                VoHasMention = true
            });
        userRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
            .ReturnsAsync([]);
        attachmentRepository
            .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
            .ReturnsAsync([]);

        return new Fixture(
            new ChatReadReceiptService(
                readRepository.Object,
                messageRepository.Object,
                accessService.Object,
                chatService.Object,
                userRepository.Object,
                attachmentRepository.Object,
                attachmentUrlResolver.Object,
                TimeProvider.System),
            readRepository,
            userRepository);
    }

    private static ChannelMessage CreateMessage(long id, long senderUserId) => new()
    {
        Id = id,
        TenantId = 30000,
        ChannelId = 70001,
        UserId = senderUserId,
        UserName = $"User-{senderUserId}",
        Type = MessageType.Text,
        Content = $"message-{id}",
        CreateTime = new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc).AddMinutes(id)
    };

    private static ChatChannelAccessResult PublicAccess() => OpenChannelAccess(ChannelType.Public);

    private static ChatChannelAccessResult OpenChannelAccess(ChannelType channelType) => new(
        true,
        channelType,
        true,
        true,
        true,
        true,
        false);

    private static ChatChannelAccessResult PrivateGroupAccess() => new(
        true,
        ChannelType.Private,
        true,
        true,
        true,
        true,
        false,
        ChannelMemberRole: MemberRole.Member);

    private static ChatChannelAccessResult DirectAccess(
        DirectConversationRequestStatus status,
        bool blocked = false,
        bool peerAvailable = true) => new(
        true,
        ChannelType.Private,
        true,
        status == DirectConversationRequestStatus.Accepted,
        true,
        status == DirectConversationRequestStatus.Accepted,
        true,
        status,
        20001,
        blocked ? 20001 : null,
        20002,
        80001,
        true,
        peerAvailable,
        MemberRole.Member);

    private sealed record Fixture(
        ChatReadReceiptService Service,
        Mock<IChatReadReceiptRepository> ReadRepository,
        Mock<IBaseRepository<User>> UserRepository);
}
