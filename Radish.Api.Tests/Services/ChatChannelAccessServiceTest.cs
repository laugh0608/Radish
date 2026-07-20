using System;
using System.Collections.Generic;
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
        Assert.False(result.CanPinMessages);
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
        var userRepository = CreateRepository(new User
        {
            Id = 20002,
            TenantId = 30000,
            IsEnable = true
        });
        var service = CreateService(channelRepository, memberRepository, directRepository, userRepository);

        var result = await service.GetAccessAsync(30000, 20001, 100);

        Assert.True(result.IsDirectConversation);
        Assert.True(result.CanView);
        Assert.True(result.CanSend);
        Assert.True(result.CanJoinRealtime);
        Assert.True(result.CanViewMembers);
        Assert.True(result.CanReact);
        Assert.True(result.CanPinMessages);
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
        Assert.False(result.CanReact);
        Assert.False(result.CanPinMessages);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task GetAccessAsync_ShouldExposePendingConversationToReceiverOnlyAfterFirstMessage(
        bool hasMessages,
        bool expectedCanView)
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
            Id = 700,
            ChannelId = 100,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestedByUserId = 20001,
            RequestStatus = DirectConversationRequestStatus.Pending,
            TenantId = 30000
        });
        var userRepository = CreateRepository(new User
        {
            Id = 20001,
            TenantId = 30000,
            IsEnable = true
        });
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        messageRepository
            .Setup(repository => repository.QueryExistsAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>() ))
            .ReturnsAsync(hasMessages);
        var service = CreateService(
            channelRepository,
            memberRepository,
            directRepository,
            userRepository,
            messageRepository.Object);

        var result = await service.GetAccessAsync(30000, 20002, 100);

        Assert.Equal(expectedCanView, result.CanView);
        Assert.Equal(expectedCanView, result.CanJoinRealtime);
        Assert.False(result.CanSend);
        Assert.False(result.CanViewMembers);
        Assert.False(result.CanReact);
        Assert.False(result.CanPinMessages);
    }

    [Theory]
    [InlineData(MemberRole.Member, false)]
    [InlineData(MemberRole.Moderator, true)]
    [InlineData(MemberRole.Owner, true)]
    public async Task GetAccessAsync_ShouldDerivePrivateGroupPinCapabilityFromMembershipRole(
        MemberRole role,
        bool expected)
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
            Role = role,
            TenantId = 30000
        });
        var service = CreateService(
            channelRepository,
            memberRepository,
            CreateRepository<DirectConversation>());

        var result = await service.GetAccessAsync(30000, 20001, 100, canManageChannel: true);

        Assert.True(result.CanView);
        Assert.Equal(expected, result.CanPinMessages);
    }

    [Fact]
    public async Task GetAccessAsync_ShouldAllowAdministratorToPinInPublicChannel()
    {
        var channelRepository = CreateRepository(new Channel
        {
            Id = 100,
            TenantId = 30000,
            Type = ChannelType.Public,
            IsEnabled = true
        });
        var service = CreateService(
            channelRepository,
            CreateRepository<ChannelMember>(),
            CreateRepository<DirectConversation>());

        var result = await service.GetAccessAsync(30000, 20001, 100, canManageChannel: true);

        Assert.True(result.CanPinMessages);
    }

    [Fact]
    public async Task CanAccessChatAttachmentAsync_ShouldReuseChannelPolicy()
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
                AttachmentId = 600,
                TenantId = 30000
            });
        messageRepository
            .Setup(repository => repository.QueryExistsAsync(It.IsAny<Expression<Func<ChannelMessage, bool>>>() ))
            .ReturnsAsync(true);
        var userRepository = CreateRepository(new User
        {
            Id = 20002,
            TenantId = 30000,
            IsEnable = true
        });
        var service = CreateService(
            channelRepository,
            memberRepository,
            directRepository,
            userRepository,
            messageRepository.Object);

        var result = await service.CanAccessChatAttachmentAsync(30000, 20001, 600, 500);

        Assert.True(result);
    }

    [Fact]
    public async Task GetReadableChannelSnapshotAsync_ShouldReuseAclAndNeverExposePrivateNonMember()
    {
        var channelRepository = CreateRepository(
            new Channel { Id = 100, Name = "Public", TenantId = 30000, Type = ChannelType.Public, IsEnabled = true },
            new Channel { Id = 101, Name = "Private", TenantId = 30000, Type = ChannelType.Private, IsEnabled = true },
            new Channel { Id = 102, Name = "Request", TenantId = 30000, Type = ChannelType.Private, IsEnabled = true });
        var memberRepository = CreateRepository(new ChannelMember
        {
            ChannelId = 102,
            UserId = 20002,
            TenantId = 30000
        });
        var directRepository = CreateRepository(new DirectConversation
        {
            Id = 700,
            ChannelId = 102,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestedByUserId = 20001,
            RequestStatus = DirectConversationRequestStatus.Pending,
            TenantId = 30000
        });
        var userRepository = CreateRepository(new User
        {
            Id = 20001,
            TenantId = 30000,
            IsEnable = true
        });
        var messageRepository = new Mock<IChannelMessageRepository>(MockBehavior.Strict);
        messageRepository
            .Setup(repository => repository.QueryDistinctAsync(
                It.IsAny<Expression<Func<ChannelMessage, long>>>(),
                It.IsAny<Expression<Func<ChannelMessage, bool>>?>()))
            .ReturnsAsync(new List<long> { 102 });
        var service = CreateService(
            channelRepository,
            memberRepository,
            directRepository,
            userRepository,
            messageRepository.Object);

        var result = await service.GetReadableChannelSnapshotAsync(30000, 20002);

        Assert.Equal(new long[] { 100, 102 }, result.Select(item => item.ChannelId).OrderBy(id => id));
        Assert.DoesNotContain(result, item => item.ChannelId == 101);
        Assert.True(result.Single(item => item.ChannelId == 102).Access.CanView);
    }

    private static ChatChannelAccessService CreateService(
        Mock<IBaseRepository<Channel>> channelRepository,
        Mock<IBaseRepository<ChannelMember>> memberRepository,
        Mock<IBaseRepository<DirectConversation>> directRepository,
        Mock<IBaseRepository<User>>? userRepository = null,
        IChannelMessageRepository? messageRepository = null)
    {
        return new ChatChannelAccessService(
            channelRepository.Object,
            memberRepository.Object,
            directRepository.Object,
            userRepository?.Object ?? Mock.Of<IBaseRepository<User>>(),
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
        repository
            .Setup(item => item.QueryAsync(It.IsAny<Expression<Func<TEntity, bool>>?>()))
            .ReturnsAsync((Expression<Func<TEntity, bool>>? predicate) =>
                predicate == null ? entities.ToList() : entities.Where(predicate.Compile()).ToList());
        return repository;
    }
}
