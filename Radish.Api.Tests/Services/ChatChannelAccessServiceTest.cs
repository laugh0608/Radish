using System;
using System.Linq.Expressions;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class ChatChannelAccessServiceTest
{
    [Fact]
    public async Task GetAccessAsync_ShouldNotAllowAdministratorToBypassPrivateMembership()
    {
        var channelRepository = CreateRepository(new Channel
        {
            Id = 100,
            TenantId = 30000,
            Type = ChannelType.Private,
            IsEnabled = true
        });
        var memberRepository = CreateRepository<ChannelMember>();
        var directRepository = CreateRepository<DirectConversation>();
        var service = CreateService(channelRepository, memberRepository, directRepository);

        var result = await service.GetAccessAsync(30000, 20001, 100, canManageChannel: true);

        Assert.True(result.Exists);
        Assert.False(result.CanView);
        Assert.False(result.CanSend);
        Assert.False(result.CanJoinRealtime);
    }

    [Fact]
    public async Task GetAccessAsync_ShouldAllowAcceptedDirectParticipant()
    {
        var channelRepository = CreateRepository(new Channel
        {
            Id = 100,
            TenantId = 30000,
            Type = ChannelType.Private,
            IsEnabled = true
        });
        var memberRepository = CreateRepository(new ChannelMember
        {
            ChannelId = 100,
            UserId = 20001,
            TenantId = 30000
        });
        var directRepository = CreateRepository(new DirectConversation
        {
            ChannelId = 100,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestStatus = DirectConversationRequestStatus.Accepted,
            TenantId = 30000
        });
        var service = CreateService(channelRepository, memberRepository, directRepository);

        var result = await service.GetAccessAsync(30000, 20001, 100);

        Assert.True(result.IsDirectConversation);
        Assert.True(result.CanView);
        Assert.True(result.CanSend);
        Assert.True(result.CanJoinRealtime);
        Assert.True(result.CanViewMembers);
    }

    [Fact]
    public async Task GetAccessAsync_ShouldKeepHistoryVisibleButStopSendingWhenDirectConversationIsBlocked()
    {
        var channelRepository = CreateRepository(new Channel
        {
            Id = 100,
            TenantId = 30000,
            Type = ChannelType.Private,
            IsEnabled = true
        });
        var memberRepository = CreateRepository(new ChannelMember
        {
            ChannelId = 100,
            UserId = 20002,
            TenantId = 30000
        });
        var directRepository = CreateRepository(new DirectConversation
        {
            ChannelId = 100,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestStatus = DirectConversationRequestStatus.Accepted,
            BlockedByUserId = 20001,
            TenantId = 30000
        });
        var service = CreateService(channelRepository, memberRepository, directRepository);

        var result = await service.GetAccessAsync(30000, 20002, 100);

        Assert.True(result.CanView);
        Assert.False(result.CanSend);
        Assert.True(result.CanJoinRealtime);
        Assert.False(result.CanViewMembers);
    }

    [Fact]
    public async Task CanAccessMessageAttachmentAsync_ShouldReuseChannelPolicy()
    {
        var channelRepository = CreateRepository(new Channel
        {
            Id = 100,
            TenantId = 30000,
            Type = ChannelType.Private,
            IsEnabled = true
        });
        var memberRepository = CreateRepository(new ChannelMember
        {
            ChannelId = 100,
            UserId = 20001,
            TenantId = 30000
        });
        var directRepository = CreateRepository(new DirectConversation
        {
            ChannelId = 100,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestStatus = DirectConversationRequestStatus.Accepted,
            TenantId = 30000
        });
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        messageRepository
            .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>?>()))
            .ReturnsAsync(new ChannelMessage
            {
                Id = 500,
                ChannelId = 100,
                TenantId = 30000
            });
        var service = CreateService(
            channelRepository,
            memberRepository,
            directRepository,
            messageRepository.Object);

        var result = await service.CanAccessMessageAttachmentAsync(30000, 20001, 500);

        Assert.True(result);
    }

    private static ChatChannelAccessService CreateService(
        Mock<IBaseRepository<Channel>> channelRepository,
        Mock<IBaseRepository<ChannelMember>> memberRepository,
        Mock<IBaseRepository<DirectConversation>> directRepository,
        IChannelMessageRepository? messageRepository = null)
    {
        return new ChatChannelAccessService(
            channelRepository.Object,
            memberRepository.Object,
            directRepository.Object,
            messageRepository ?? Mock.Of<IChannelMessageRepository>());
    }

    private static Mock<IBaseRepository<TEntity>> CreateRepository<TEntity>(params TEntity[] entities)
        where TEntity : class
    {
        var repository = new Mock<IBaseRepository<TEntity>>(MockBehavior.Strict);
        repository
            .Setup(item => item.QueryFirstAsync(It.IsAny<Expression<Func<TEntity, bool>>?>()))
            .ReturnsAsync((Expression<Func<TEntity, bool>>? predicate) =>
                predicate == null ? entities.FirstOrDefault() : entities.FirstOrDefault(predicate.Compile()));
        return repository;
    }
}
