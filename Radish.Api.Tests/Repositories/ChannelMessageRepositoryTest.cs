using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ChannelMessageRepositoryTest
{
    [Fact]
    public async Task RecallWithEffectsAsync_ShouldSoftDeleteMessageReactionsAndPinTogether()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-message-recall-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel, ChannelMessage, ChatMessageReaction, ChatMessagePin>();
            var channel = CreateChannel();
            channel.PinRevision = 4;
            chatDb.Insertable(channel).ExecuteCommand();
            var message = CreateMessage(73010, "recall me");
            message.SearchText = "recall me";
            chatDb.Insertable(message).ExecuteCommand();
            chatDb.Insertable(new ChatMessageReaction
            {
                Id = 74010,
                TenantId = message.TenantId,
                ChannelId = message.ChannelId,
                MessageId = message.Id,
                UserId = 20002,
                UserName = "Receiver",
                EmojiType = "unicode",
                EmojiValue = "👍",
                CreateTime = message.CreateTime,
                CreateBy = "Receiver",
                CreateId = 20002
            }).ExecuteCommand();
            chatDb.Insertable(new ChatMessagePin
            {
                Id = 75010,
                TenantId = message.TenantId,
                ChannelId = message.ChannelId,
                MessageId = message.Id,
                PinnedByUserId = 20002,
                PinnedByName = "Receiver",
                PinnedAt = message.CreateTime,
                CreateTime = message.CreateTime,
                CreateBy = "Receiver",
                CreateId = 20002
            }).ExecuteCommand();

            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var repository = new ChannelMessageRepository(unitOfWork);
            var recalledAt = message.CreateTime.AddMinutes(1);

            var result = await repository.RecallWithEffectsAsync(
                message.Id,
                20001,
                "Requester",
                recalledAt);

            Assert.Equal(1, result.AffectedRows);
            Assert.Equal(message.ChannelId, result.ChannelId);
            Assert.True(result.PinsChanged);
            Assert.Equal(5, result.PinRevision);
            var recalledMessage = chatDb.Queryable<ChannelMessage>().InSingle(message.Id);
            var recalledReaction = chatDb.Queryable<ChatMessageReaction>().InSingle(74010);
            var recalledPin = chatDb.Queryable<ChatMessagePin>().InSingle(75010);
            Assert.True(recalledMessage.IsDeleted);
            Assert.Null(recalledMessage.SearchText);
            Assert.Equal(recalledAt, recalledMessage.DeletedAt);
            Assert.True(recalledReaction.IsDeleted);
            Assert.Equal(recalledAt, recalledReaction.DeletedAt);
            Assert.Equal(20001, recalledReaction.ModifyId);
            Assert.True(recalledPin.IsDeleted);
            Assert.Equal(recalledAt, recalledPin.DeletedAt);
            Assert.Equal(20001, recalledPin.DeletedByUserId);
            Assert.Equal(5, chatDb.Queryable<Channel>().InSingle(channel.Id).PinRevision);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task AddWithEffectsAsync_ShouldCommitClaimMessageOutboxesAndPeerUnarchiveTogether()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-chat-message-effects-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);

        try
        {
            var chatDb = db.GetConnectionScope("chat");
            chatDb.CodeFirst.InitTables<Channel>();
            chatDb.CodeFirst.InitTables<ChannelMember>();
            chatDb.CodeFirst.InitTables<DirectConversation>();
            chatDb.CodeFirst.InitTables<ChannelMessage>();
            chatDb.CodeFirst.InitTables<ChatReliableOutboxMessage>();
            chatDb.Insertable(CreateChannel()).ExecuteCommand();
            chatDb.Insertable(new[]
            {
                CreateMember(72001, 20001, archived: false),
                CreateMember(72002, 20002, archived: true)
            }).ExecuteCommand();
            chatDb.Insertable(CreateConversation()).ExecuteCommand();

            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            var repository = new ChannelMessageRepository(unitOfWork, new ReliableOutboxRepository(db));
            var message = CreateMessage(73001, "first request");
            var now = message.CreateTime;
            var outboxes = new[]
            {
                CreateOutbox("task:chat-request:73001", ReliableTaskTypes.NotificationRequested, now),
                CreateOutbox("task:chat-bind:73001", ReliableTaskTypes.ChatAttachmentBinding, now)
            };

            var result = await repository.AddWithEffectsAsync(
                message,
                outboxes,
                unarchiveUserId: 20002,
                claimPendingConversationId: 71001);

            Assert.Equal(message.Id, result.MessageId);
            Assert.True(result.PeerWasUnarchived);
            Assert.NotNull(chatDb.Queryable<ChannelMessage>().InSingle(message.Id));
            Assert.Equal(2, chatDb.Queryable<ChatReliableOutboxMessage>().Count());
            Assert.Equal(
                message.Id,
                chatDb.Queryable<DirectConversation>().InSingle(71001).RequestMessageId);
            Assert.Null(chatDb.Queryable<ChannelMember>().InSingle(72002).ArchivedAt);

            var competingMessage = CreateMessage(73002, "competing request");
            await Assert.ThrowsAsync<DirectConversationRequestClaimException>(() =>
                repository.AddWithEffectsAsync(
                    competingMessage,
                    [CreateOutbox("task:chat-request:73002", ReliableTaskTypes.NotificationRequested, now)],
                    unarchiveUserId: 20002,
                    claimPendingConversationId: 71001));
            Assert.Null(chatDb.Queryable<ChannelMessage>().InSingle(competingMessage.Id));
            Assert.Equal(2, chatDb.Queryable<ChatReliableOutboxMessage>().Count());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "chat",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static Channel CreateChannel()
    {
        return new Channel
        {
            Id = 70001,
            Name = "Direct conversation",
            Slug = "direct-70001",
            Type = ChannelType.Private,
            IsEnabled = true,
            TenantId = 30000,
            CreateTime = DateTime.UtcNow,
            CreateBy = "Requester",
            CreateId = 20001
        };
    }

    private static DirectConversation CreateConversation()
    {
        return new DirectConversation
        {
            Id = 71001,
            ChannelId = 70001,
            ParticipantLowUserId = 20001,
            ParticipantHighUserId = 20002,
            RequestedByUserId = 20001,
            RequestStatus = DirectConversationRequestStatus.Pending,
            TenantId = 30000,
            CreateTime = DateTime.UtcNow,
            CreateBy = "Requester",
            CreateId = 20001
        };
    }

    private static ChannelMember CreateMember(long id, long userId, bool archived)
    {
        return new ChannelMember
        {
            Id = id,
            ChannelId = 70001,
            UserId = userId,
            Role = MemberRole.Member,
            JoinedAt = DateTime.UtcNow,
            ArchivedAt = archived ? DateTime.UtcNow : null,
            TenantId = 30000,
            CreateTime = DateTime.UtcNow,
            CreateBy = "Requester",
            CreateId = 20001
        };
    }

    private static ChannelMessage CreateMessage(long id, string content)
    {
        return new ChannelMessage
        {
            Id = id,
            ChannelId = 70001,
            UserId = 20001,
            UserName = "Requester",
            Type = MessageType.Text,
            Content = content,
            TenantId = 30000,
            CreateTime = DateTime.UtcNow
        };
    }

    private static ReliableOutboxDraft CreateOutbox(string idempotencyKey, string taskType, DateTime now)
    {
        return new ReliableOutboxDraft(
            ReliableOutboxSources.Chat,
            30000,
            taskType,
            1,
            idempotencyKey,
            "ChannelMessage",
            "73001",
            "{}",
            now);
    }
}
