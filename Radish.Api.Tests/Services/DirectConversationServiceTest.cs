using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.Model;
using Radish.Service;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class DirectConversationServiceTest
{
    [Fact]
    public async Task GetOrCreateAsync_ShouldNormalizePairAndAcceptMutualFollowConversation()
    {
        var fixture = new Fixture();
        fixture.Users.Add(new User
        {
            Id = 20001,
            UserName = "Lower",
            PublicIndex = 1,
            TenantId = 30000,
            IsEnable = true
        });
        fixture.Users.Add(new User
        {
            Id = 20002,
            UserName = "Higher",
            PublicIndex = 2,
            TenantId = 30000,
            IsEnable = true
        });
        fixture.Follows.Add(new UserFollow
        {
            Id = 1,
            FollowerUserId = 20002,
            FollowingUserId = 20001,
            TenantId = 30000
        });
        fixture.Follows.Add(new UserFollow
        {
            Id = 2,
            FollowerUserId = 20001,
            FollowingUserId = 20002,
            TenantId = 30000
        });
        DirectConversation? createdConversation = null;
        fixture.ConversationRepository
            .Setup(repository => repository.CreateOrGetAsync(
                It.IsAny<DirectConversation>(),
                It.IsAny<Channel>(),
                It.IsAny<ChannelMember>(),
                It.IsAny<ChannelMember>()))
            .ReturnsAsync((DirectConversation conversation, Channel _, ChannelMember low, ChannelMember high) =>
            {
                createdConversation = conversation;
                fixture.Conversations.Add(conversation);
                fixture.Members.Add(low);
                fixture.Members.Add(high);
                return new DirectConversationCreateResult(conversation, true);
            });

        var result = await fixture.Service.GetOrCreateAsync(30000, 20002, 20001, "Higher");

        Assert.True(result.Changed);
        Assert.NotNull(createdConversation);
        Assert.Equal(20001, createdConversation.ParticipantLowUserId);
        Assert.Equal(20002, createdConversation.ParticipantHighUserId);
        Assert.Equal(DirectConversationRequestStatus.Accepted, createdConversation.RequestStatus);
        Assert.NotNull(createdConversation.AcceptedAt);
        Assert.Equal("accepted", result.Conversation.VoDirectRequestStatus);
        Assert.Equal("mutual", result.Conversation.VoConversationKind);
        Assert.True(result.Conversation.VoCanSend);
        Assert.True(result.Conversation.VoWasCreated);
    }

    [Fact]
    public async Task AcceptAsync_ShouldAllowReceiverToAcceptPreviouslyDeclinedRequest()
    {
        var fixture = Fixture.CreateWithConversation(DirectConversationRequestStatus.Declined);
        fixture.ConversationRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<DirectConversation, DirectConversation>>>(),
                It.IsAny<Expression<Func<DirectConversation, bool>>>() ))
            .Callback<Expression<Func<DirectConversation, DirectConversation>>, Expression<Func<DirectConversation, bool>>>((update, _) =>
            {
                var values = update.Compile()(fixture.Conversations[0]);
                fixture.Conversations[0].RequestStatus = values.RequestStatus;
                fixture.Conversations[0].AcceptedAt = values.AcceptedAt;
                fixture.Conversations[0].DeclinedAt = values.DeclinedAt;
            })
            .ReturnsAsync(1);

        var result = await fixture.Service.AcceptAsync(30000, 20002, 70001, "Receiver");

        Assert.True(result.Changed);
        Assert.Equal(DirectConversationRequestStatus.Accepted, fixture.Conversations[0].RequestStatus);
        Assert.Equal("accepted", result.Conversation.VoDirectRequestStatus);
        Assert.True(result.Conversation.VoCanSend);
    }

    [Fact]
    public async Task UnblockAsync_ShouldRejectParticipantWhoDidNotBlockConversation()
    {
        var fixture = Fixture.CreateWithConversation(DirectConversationRequestStatus.Accepted);
        fixture.Conversations[0].BlockedByUserId = 20001;

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.UnblockAsync(30000, 20002, 70001, "Receiver"));

        Assert.Equal(StatusCodes.Status403Forbidden, exception.StatusCode);
        Assert.Equal("Chat.DirectUnblockForbidden", exception.ErrorCode);
        fixture.ConversationRepository.Verify(repository => repository.UpdateColumnsAsync(
            It.IsAny<Expression<Func<DirectConversation, DirectConversation>>>(),
            It.IsAny<Expression<Func<DirectConversation, bool>>>()), Times.Never);
    }

    [Fact]
    public async Task SetArchivedAsync_ShouldOnlyArchiveCurrentParticipantsMemberState()
    {
        var fixture = Fixture.CreateWithConversation(DirectConversationRequestStatus.Accepted);
        fixture.MemberRepository
            .Setup(repository => repository.UpdateColumnsAsync(
                It.IsAny<Expression<Func<ChannelMember, ChannelMember>>>(),
                It.IsAny<Expression<Func<ChannelMember, bool>>>() ))
            .Callback<Expression<Func<ChannelMember, ChannelMember>>, Expression<Func<ChannelMember, bool>>>((update, _) =>
            {
                var values = update.Compile()(fixture.Members[0]);
                fixture.Members[0].ArchivedAt = values.ArchivedAt;
            })
            .ReturnsAsync(1);

        var result = await fixture.Service.SetArchivedAsync(30000, 20002, 70001, true, "Receiver");

        Assert.True(result.Changed);
        Assert.NotNull(fixture.Members[0].ArchivedAt);
        Assert.True(result.Conversation.VoIsArchived);
    }

    [Fact]
    public async Task AcceptAsync_ShouldHidePendingConversationFromReceiverBeforeFirstMessage()
    {
        var fixture = Fixture.CreateWithConversation(DirectConversationRequestStatus.Pending);

        var exception = await Assert.ThrowsAsync<BusinessException>(() =>
            fixture.Service.AcceptAsync(30000, 20002, 70001, "Receiver"));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
        Assert.Equal("Chat.ChannelUnavailable", exception.ErrorCode);
        fixture.ConversationRepository.Verify(repository => repository.UpdateColumnsAsync(
            It.IsAny<Expression<Func<DirectConversation, DirectConversation>>>(),
            It.IsAny<Expression<Func<DirectConversation, bool>>>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Mock<IDirectConversationRepository> ConversationRepository { get; } = new(MockBehavior.Loose);
        public Mock<IBaseRepository<ChannelMember>> MemberRepository { get; } = new(MockBehavior.Loose);
        public Mock<IBaseRepository<User>> UserRepository { get; } = new(MockBehavior.Loose);
        public Mock<IBaseRepository<UserFollow>> FollowRepository { get; } = new(MockBehavior.Loose);
        public Mock<IBaseRepository<Attachment>> AttachmentRepository { get; } = new(MockBehavior.Loose);
        public Mock<IAttachmentUrlResolver> AttachmentUrlResolver { get; } = new(MockBehavior.Loose);
        public List<DirectConversation> Conversations { get; } = [];
        public List<ChannelMember> Members { get; } = [];
        public List<User> Users { get; } = [];
        public List<UserFollow> Follows { get; } = [];
        public DirectConversationService Service { get; }

        public Fixture()
        {
            ConversationRepository
                .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<DirectConversation, bool>>?>()))
                .ReturnsAsync((Expression<Func<DirectConversation, bool>>? predicate) =>
                    predicate == null ? Conversations.FirstOrDefault() : Conversations.FirstOrDefault(predicate.Compile()));
            ConversationRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<DirectConversation, bool>>?>()))
                .ReturnsAsync((Expression<Func<DirectConversation, bool>>? predicate) =>
                    predicate == null ? Conversations.ToList() : Conversations.Where(predicate.Compile()).ToList());
            MemberRepository
                .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<ChannelMember, bool>>?>()))
                .ReturnsAsync((Expression<Func<ChannelMember, bool>>? predicate) =>
                    predicate == null ? Members.FirstOrDefault() : Members.FirstOrDefault(predicate.Compile()));
            MemberRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<ChannelMember, bool>>?>()))
                .ReturnsAsync((Expression<Func<ChannelMember, bool>>? predicate) =>
                    predicate == null ? Members.ToList() : Members.Where(predicate.Compile()).ToList());
            UserRepository
                .Setup(repository => repository.QueryFirstAsync(It.IsAny<Expression<Func<User, bool>>?>()))
                .ReturnsAsync((Expression<Func<User, bool>>? predicate) =>
                    predicate == null ? Users.FirstOrDefault() : Users.FirstOrDefault(predicate.Compile()));
            UserRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<User, bool>>?>()))
                .ReturnsAsync((Expression<Func<User, bool>>? predicate) =>
                    predicate == null ? Users.ToList() : Users.Where(predicate.Compile()).ToList());
            FollowRepository
                .Setup(repository => repository.QueryExistsAsync(It.IsAny<Expression<Func<UserFollow, bool>>>() ))
                .ReturnsAsync((Expression<Func<UserFollow, bool>> predicate) => Follows.Any(predicate.Compile()));
            FollowRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<UserFollow, bool>>?>()))
                .ReturnsAsync((Expression<Func<UserFollow, bool>>? predicate) =>
                    predicate == null ? Follows.ToList() : Follows.Where(predicate.Compile()).ToList());
            AttachmentRepository
                .Setup(repository => repository.QueryAsync(It.IsAny<Expression<Func<Attachment, bool>>?>()))
                .ReturnsAsync([]);
            Service = new DirectConversationService(
                ConversationRepository.Object,
                MemberRepository.Object,
                UserRepository.Object,
                FollowRepository.Object,
                AttachmentRepository.Object,
                AttachmentUrlResolver.Object);
        }

        public static Fixture CreateWithConversation(DirectConversationRequestStatus status)
        {
            var fixture = new Fixture();
            fixture.Conversations.Add(new DirectConversation
            {
                Id = 71001,
                ChannelId = 70001,
                ParticipantLowUserId = 20001,
                ParticipantHighUserId = 20002,
                RequestedByUserId = 20001,
                RequestStatus = status,
                TenantId = 30000,
                CreateTime = DateTime.UtcNow,
                CreateBy = "Requester",
                CreateId = 20001
            });
            fixture.Members.Add(new ChannelMember
            {
                Id = 72001,
                ChannelId = 70001,
                UserId = 20002,
                TenantId = 30000
            });
            fixture.Users.Add(new User
            {
                Id = 20001,
                UserName = "Requester",
                PublicIndex = 1,
                TenantId = 30000,
                IsEnable = true
            });
            return fixture;
        }

    }
}
