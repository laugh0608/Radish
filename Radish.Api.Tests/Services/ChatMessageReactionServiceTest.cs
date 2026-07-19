using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChatMessageReactionServiceTest
{
    [Fact]
    public async Task GetStatesAsync_ShouldReturnFullRevisionedAggregationForCurrentUser()
    {
        var reactionRepository = new Mock<IChatMessageReactionRepository>(MockBehavior.Strict);
        reactionRepository
            .Setup(repository => repository.QueryActiveByMessageIdsAsync(
                30000,
                It.Is<IReadOnlyCollection<long>>(ids => ids.Count == 1 && ids.Contains(90001))))
            .ReturnsAsync(new List<ChatMessageReaction>
            {
                CreateReaction(91001, 20001, "👍"),
                CreateReaction(91002, 20002, "👍"),
                CreateReaction(91003, 20002, "❤️")
            });
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        messageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(
                It.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 90001)))
            .ReturnsAsync([CreateMessage()]);
        var accessService = CreateAccessService(
            new ChatChannelAccessResult(true, ChannelType.Public, true, true, true, true, false));
        var service = CreateService(reactionRepository, messageRepository, accessService);

        var result = await service.GetStatesAsync(30000, 20001, new GetChatMessageReactionStatesDto
        {
            ChannelId = 70001,
            MessageIds = [90001]
        });

        var state = Assert.Single(result);
        Assert.Equal(90001, state.VoMessageId);
        Assert.Equal(7, state.VoRevision);
        Assert.Equal(2, state.VoItems.Count);
        var thumbsUp = Assert.Single(state.VoItems, item => item.VoEmojiValue == "👍");
        Assert.Equal(2, thumbsUp.VoCount);
        Assert.True(thumbsUp.VoIsReacted);
        Assert.False(Assert.Single(state.VoItems, item => item.VoEmojiValue == "❤️").VoIsReacted);
    }

    [Fact]
    public async Task SetAsync_ShouldRejectReadableDirectConversationWhenCanReactIsFalse()
    {
        var reactionRepository = new Mock<IChatMessageReactionRepository>(MockBehavior.Strict);
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        var accessService = CreateAccessService(new ChatChannelAccessResult(
            true,
            ChannelType.Private,
            true,
            false,
            true,
            false,
            true,
            DirectConversationRequestStatus.Accepted,
            DirectBlockedByUserId: 20002));
        var service = CreateService(reactionRepository, messageRepository, accessService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SetAsync(
            30000,
            20001,
            "Tester",
            CreateSetRequest()));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("Chat.ReactionNotAllowed", exception.ErrorCode);
        Assert.Equal("error.chat.reaction_not_allowed", exception.MessageKey);
    }

    [Fact]
    public async Task SetAsync_ShouldMapRepositoryConcurrentConflictToStableBusinessError()
    {
        var reactionRepository = new Mock<IChatMessageReactionRepository>(MockBehavior.Strict);
        reactionRepository
            .Setup(repository => repository.SetAsync(It.IsAny<ChatMessageReactionSetCommand>()))
            .ThrowsAsync(new ChatMessageReactionConcurrentConflictException());
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        messageRepository
            .Setup(repository => repository.QueryByIdsIncludingDeletedAsync(It.IsAny<List<long>>()))
            .ReturnsAsync([CreateMessage()]);
        var accessService = CreateAccessService(
            new ChatChannelAccessResult(true, ChannelType.Public, true, true, true, true, false));
        var service = CreateService(reactionRepository, messageRepository, accessService);

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SetAsync(
            30000,
            20001,
            "Tester",
            CreateSetRequest()));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Chat.ReactionConcurrentConflict", exception.ErrorCode);
        Assert.Equal("error.chat.reaction_concurrent_conflict", exception.MessageKey);
    }

    private static ChatMessageReactionService CreateService(
        Mock<IChatMessageReactionRepository> reactionRepository,
        Mock<IChannelMessageRepository> messageRepository,
        Mock<IChatChannelAccessService> accessService)
    {
        return new ChatMessageReactionService(
            reactionRepository.Object,
            messageRepository.Object,
            accessService.Object,
            Mock.Of<IBaseRepository<StickerGroup>>(),
            Mock.Of<IBaseRepository<Sticker>>(),
            Mock.Of<IAttachmentUrlResolver>(),
            TimeProvider.System);
    }

    private static Mock<IChatChannelAccessService> CreateAccessService(ChatChannelAccessResult access)
    {
        var service = new Mock<IChatChannelAccessService>(MockBehavior.Strict);
        service
            .Setup(candidate => candidate.GetAccessAsync(30000, 20001, 70001, false))
            .ReturnsAsync(access);
        return service;
    }

    private static ChannelMessage CreateMessage()
    {
        return new ChannelMessage
        {
            Id = 90001,
            TenantId = 30000,
            ChannelId = 70001,
            UserId = 20002,
            UserName = "Author",
            Type = MessageType.Text,
            Content = "hello",
            ReactionRevision = 7,
            CreateTime = DateTime.UtcNow
        };
    }

    private static ChatMessageReaction CreateReaction(long id, long userId, string emojiValue)
    {
        return new ChatMessageReaction
        {
            Id = id,
            TenantId = 30000,
            ChannelId = 70001,
            MessageId = 90001,
            UserId = userId,
            UserName = $"User-{userId}",
            EmojiType = "unicode",
            EmojiValue = emojiValue
        };
    }

    private static SetChatMessageReactionDto CreateSetRequest()
    {
        return new SetChatMessageReactionDto
        {
            ChannelId = 70001,
            MessageId = 90001,
            EmojiType = "unicode",
            EmojiValue = "👍",
            IsActive = true,
            ClientOperationId = "operation-90001"
        };
    }
}
