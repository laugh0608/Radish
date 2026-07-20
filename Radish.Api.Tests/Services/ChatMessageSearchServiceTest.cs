using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
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

public sealed class ChatMessageSearchServiceTest
{
    [Fact]
    public async Task SearchAsync_ShouldReturnStableCursorAndNeverExposeMentionUserId()
    {
        var searchRepository = new Mock<IChannelMessageSearchRepository>(MockBehavior.Strict);
        searchRepository
            .Setup(repository => repository.GetSnapshotMaxMessageIdAsync(30000, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(9003);
        ChannelMessageSearchQuery? capturedQuery = null;
        searchRepository
            .Setup(repository => repository.SearchAsync(It.IsAny<ChannelMessageSearchQuery>()))
            .Callback((ChannelMessageSearchQuery query) => capturedQuery = query)
            .ReturnsAsync(new List<ChannelMessage>
            {
                CreateMessage(9003, "Hello @[萝卜](123456)", DateTime.UtcNow),
                CreateMessage(9002, "hello second", DateTime.UtcNow.AddSeconds(-1)),
                CreateMessage(9001, "hello third", DateTime.UtcNow.AddSeconds(-2))
            });
        var channelAccess = new Mock<IChatChannelAccessService>(MockBehavior.Strict);
        channelAccess
            .Setup(service => service.GetAccessAsync(30000, 20001, 7001, false))
            .ReturnsAsync(new ChatChannelAccessResult(
                true, ChannelType.Public, true, true, true, true, false));
        var channelRepository = CreateChannelRepository(new Channel
        {
            Id = 7001,
            Name = "General",
            IconEmoji = "💬",
            Type = ChannelType.Public,
            IsEnabled = true,
            TenantId = 30000
        });
        var service = CreateService(searchRepository, channelAccess, channelRepository);

        var result = await service.SearchAsync(30000, 20001, new SearchChannelMessagesDto
        {
            Scope = ChatMessageSearchScope.CurrentChannel,
            ChannelId = 7001,
            Keyword = "HELLO",
            PageSize = 2
        });

        Assert.True(result.VoHasMore);
        Assert.NotNull(result.VoNextCursor);
        Assert.Equal(2, result.VoItems.Count);
        Assert.Contains("@萝卜", result.VoItems[0].VoSnippet, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", result.VoItems[0].VoSnippet, StringComparison.Ordinal);
        Assert.NotNull(capturedQuery);
        Assert.Equal("hello", capturedQuery!.NormalizedKeyword);
        Assert.Equal(3, capturedQuery.Take);
        Assert.Equal(9003, capturedQuery.SnapshotMaxMessageId);

        await service.SearchAsync(30000, 20001, new SearchChannelMessagesDto
        {
            Scope = ChatMessageSearchScope.CurrentChannel,
            ChannelId = 7001,
            Keyword = "hello",
            PageSize = 2,
            Cursor = result.VoNextCursor
        });

        Assert.Equal(9002, capturedQuery.LastMessageId);
        Assert.NotNull(capturedQuery.LastCreateTimeUtc);
    }

    [Fact]
    public async Task SearchAsync_ShouldInvalidateCursorWhenVisibleChannelSetChanges()
    {
        var firstChannel = CreateSnapshotItem(7001, "General");
        var secondChannel = CreateSnapshotItem(7002, "Private");
        var channelAccess = new Mock<IChatChannelAccessService>(MockBehavior.Strict);
        channelAccess
            .SetupSequence(service => service.GetReadableChannelSnapshotAsync(30000, 20001))
            .ReturnsAsync(new List<ReadableChatChannelSnapshotItem> { firstChannel })
            .ReturnsAsync(new List<ReadableChatChannelSnapshotItem> { firstChannel, secondChannel });
        var searchRepository = new Mock<IChannelMessageSearchRepository>(MockBehavior.Strict);
        searchRepository
            .Setup(repository => repository.GetSnapshotMaxMessageIdAsync(30000, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(9002);
        searchRepository
            .Setup(repository => repository.SearchAsync(It.IsAny<ChannelMessageSearchQuery>()))
            .ReturnsAsync(new List<ChannelMessage>
            {
                CreateMessage(9002, "hello first", DateTime.UtcNow),
                CreateMessage(9001, "hello second", DateTime.UtcNow.AddSeconds(-1))
            });
        var service = CreateService(searchRepository, channelAccess, CreateChannelRepository());
        var firstPage = await service.SearchAsync(30000, 20001, new SearchChannelMessagesDto
        {
            Scope = ChatMessageSearchScope.AllVisibleChannels,
            Keyword = "hello",
            PageSize = 1
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SearchAsync(
            30000,
            20001,
            new SearchChannelMessagesDto
            {
                Scope = ChatMessageSearchScope.AllVisibleChannels,
                Keyword = "hello",
                PageSize = 1,
                Cursor = firstPage.VoNextCursor
            }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Chat.SearchCursorInvalid", exception.ErrorCode);
        Assert.Equal("error.chat.search_cursor_invalid", exception.MessageKey);
    }

    [Fact]
    public async Task SearchAsync_ShouldInvalidateCurrentChannelCursorWhenReadAccessIsRevoked()
    {
        var channelAccess = new Mock<IChatChannelAccessService>(MockBehavior.Strict);
        channelAccess
            .SetupSequence(service => service.GetAccessAsync(30000, 20001, 7001, false))
            .ReturnsAsync(new ChatChannelAccessResult(true, ChannelType.Private, true, false, true, false, true))
            .ReturnsAsync(new ChatChannelAccessResult(true, ChannelType.Private, false, false, false, false, true));
        var searchRepository = new Mock<IChannelMessageSearchRepository>(MockBehavior.Strict);
        searchRepository
            .Setup(repository => repository.GetSnapshotMaxMessageIdAsync(30000, It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(9002);
        searchRepository
            .Setup(repository => repository.SearchAsync(It.IsAny<ChannelMessageSearchQuery>()))
            .ReturnsAsync(new List<ChannelMessage>
            {
                CreateMessage(9002, "hello first", DateTime.UtcNow),
                CreateMessage(9001, "hello second", DateTime.UtcNow.AddSeconds(-1))
            });
        var service = CreateService(searchRepository, channelAccess, CreateChannelRepository(new Channel
        {
            Id = 7001,
            Name = "Private",
            Type = ChannelType.Private,
            IsEnabled = true,
            TenantId = 30000
        }));
        var firstPage = await service.SearchAsync(30000, 20001, new SearchChannelMessagesDto
        {
            Scope = ChatMessageSearchScope.CurrentChannel,
            ChannelId = 7001,
            Keyword = "hello",
            PageSize = 1
        });

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SearchAsync(
            30000,
            20001,
            new SearchChannelMessagesDto
            {
                Scope = ChatMessageSearchScope.CurrentChannel,
                ChannelId = 7001,
                Keyword = "hello",
                PageSize = 1,
                Cursor = firstPage.VoNextCursor
            }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("Chat.SearchCursorInvalid", exception.ErrorCode);
    }

    [Theory]
    [InlineData(ChatMessageSearchScope.CurrentChannel, null)]
    [InlineData(ChatMessageSearchScope.AllVisibleChannels, 7001L)]
    public async Task SearchAsync_ShouldRejectInvalidScopeFieldCombination(
        ChatMessageSearchScope scope,
        long? channelId)
    {
        var service = CreateService(
            new Mock<IChannelMessageSearchRepository>(MockBehavior.Strict),
            new Mock<IChatChannelAccessService>(MockBehavior.Strict),
            CreateChannelRepository());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SearchAsync(
            30000,
            20001,
            new SearchChannelMessagesDto
            {
                Scope = scope,
                ChannelId = channelId,
                Keyword = "valid"
            }));

        Assert.Equal("Chat.SearchScopeInvalid", exception.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task SearchAsync_ShouldRejectOutOfRangePageSize(int pageSize)
    {
        var service = CreateService(
            new Mock<IChannelMessageSearchRepository>(MockBehavior.Strict),
            new Mock<IChatChannelAccessService>(MockBehavior.Strict),
            CreateChannelRepository());

        var exception = await Assert.ThrowsAsync<BusinessException>(() => service.SearchAsync(
            30000,
            20001,
            new SearchChannelMessagesDto
            {
                Scope = ChatMessageSearchScope.AllVisibleChannels,
                Keyword = "valid",
                PageSize = pageSize
            }));

        Assert.Equal("Chat.SearchPageSizeInvalid", exception.ErrorCode);
    }

    private static ChatMessageSearchService CreateService(
        Mock<IChannelMessageSearchRepository> searchRepository,
        Mock<IChatChannelAccessService> channelAccess,
        Mock<IBaseRepository<Channel>> channelRepository)
    {
        var directConversationService = new Mock<IDirectConversationService>(MockBehavior.Strict);
        directConversationService
            .Setup(service => service.GetChannelSummariesAsync(
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<IReadOnlyCollection<long>>()))
            .ReturnsAsync(new Dictionary<long, DirectConversationVo>());
        var attachmentUrlResolver = new Mock<IAttachmentUrlResolver>(MockBehavior.Strict);
        attachmentUrlResolver
            .Setup(resolver => resolver.ResolveAttachmentUrl(It.IsAny<long>()))
            .Returns((long attachmentId) => $"/attachment/{attachmentId}");
        return new ChatMessageSearchService(
            searchRepository.Object,
            channelAccess.Object,
            directConversationService.Object,
            channelRepository.Object,
            attachmentUrlResolver.Object,
            NullLogger<ChatMessageSearchService>.Instance);
    }

    private static Mock<IBaseRepository<Channel>> CreateChannelRepository(params Channel[] channels)
    {
        var repository = new Mock<IBaseRepository<Channel>>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<Channel, bool>>?>()))
            .ReturnsAsync((Expression<Func<Channel, bool>>? predicate) =>
                predicate == null ? channels.FirstOrDefault() : channels.FirstOrDefault(predicate.Compile()));
        return repository;
    }

    private static ReadableChatChannelSnapshotItem CreateSnapshotItem(long channelId, string name)
    {
        return new ReadableChatChannelSnapshotItem(
            channelId,
            name,
            "💬",
            ChannelType.Public,
            new ChatChannelAccessResult(true, ChannelType.Public, true, true, true, true, false));
    }

    private static ChannelMessage CreateMessage(long id, string content, DateTime createTime)
    {
        return new ChannelMessage
        {
            Id = id,
            ChannelId = 7001,
            UserId = 20002,
            UserName = "Peer",
            Type = MessageType.Text,
            Content = content,
            SearchText = ChatMessageSearchTextNormalizer.Normalize(content),
            TenantId = 30000,
            CreateTime = createTime
        };
    }
}
