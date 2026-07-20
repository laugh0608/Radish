using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立一对一私聊关系、成员归档与消息发送幂等约束。</summary>
internal sealed class ChatDirectConversationSchemaMigration : ISchemaMigration
{
    private const string DirectConversationTable = "DirectConversation";
    private const string ChannelMemberTable = "ChannelMember";
    private const string ChannelMessageTable = "ChannelMessage";

    public static ChatDirectConversationSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260718_001_chat_direct_conversation";

    public string Scope => "Chat";

    public string Description => "建立一对一私聊关系、成员归档与消息发送幂等约束";

    public string ChecksumSource =>
        "20260718_001_chat_direct_conversation|Chat|" +
        "DirectConversation-v1|ChannelMember.ArchivedAt-v1|ChannelMessage.ClientRequestId-v1|" +
        "tenant-participant-pair-unique-v1|tenant-user-client-request-unique-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<DirectConversation>();
        db.CodeFirst.InitTables<ChannelMember>();
        db.CodeFirst.InitTables<ChannelMessage>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable("Channel", false))
        {
            return [];
        }

        var privateChannelCount = db.Queryable<Channel>()
            .Where(channel => channel.Type == ChannelType.Private && !channel.IsDeleted)
            .Count();
        if (privateChannelCount == 0)
        {
            return [];
        }

        return
        [
            $"发现 {privateChannelCount} 个既有 Private 频道；迁移不会猜测一对一关系，未建立 DirectConversation 的频道继续按私有群组成员 ACL 处理。"
        ];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var (tableName, columnName) in new[]
                 {
                     (DirectConversationTable, "ChannelId"),
                     (DirectConversationTable, "ParticipantLowUserId"),
                     (DirectConversationTable, "ParticipantHighUserId"),
                     (DirectConversationTable, "RequestStatus"),
                     (DirectConversationTable, "BlockedByUserId"),
                     (ChannelMemberTable, "ArchivedAt"),
                     (ChannelMessageTable, "ClientRequestId")
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, tableName, columnName) == null)
            {
                issues.Add($"{tableName}.{columnName} 表或列不存在。");
            }
        }

        foreach (var indexName in new[]
                 {
                     "idx_direct_conversation_tenant_pair",
                     "idx_direct_conversation_tenant_channel",
                     "idx_channel_message_client_request"
                 })
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        var invalidParticipantCount = db.Queryable<DirectConversation>()
            .Where(conversation =>
                conversation.ParticipantLowUserId <= 0 ||
                conversation.ParticipantHighUserId <= 0 ||
                conversation.ParticipantLowUserId >= conversation.ParticipantHighUserId ||
                (conversation.RequestedByUserId != conversation.ParticipantLowUserId &&
                 conversation.RequestedByUserId != conversation.ParticipantHighUserId) ||
                (conversation.BlockedByUserId.HasValue &&
                 conversation.BlockedByUserId != conversation.ParticipantLowUserId &&
                 conversation.BlockedByUserId != conversation.ParticipantHighUserId))
            .Count();
        if (invalidParticipantCount > 0)
        {
            issues.Add($"存在 {invalidParticipantCount} 条参与者、发起者或屏蔽者不合法的一对一会话记录。");
        }

        var directConversations = db.Queryable<DirectConversation>()
            .Where(conversation => !conversation.IsDeleted)
            .ToList();
        if (directConversations.Count > 0)
        {
            var channelIds = directConversations.Select(conversation => conversation.ChannelId).Distinct().ToList();
            var channels = db.Queryable<Channel>()
                .Where(channel => channelIds.Contains(channel.Id) && !channel.IsDeleted)
                .ToList()
                .ToDictionary(channel => channel.Id);
            var members = db.Queryable<ChannelMember>()
                .Where(member => channelIds.Contains(member.ChannelId) && !member.IsDeleted)
                .ToList()
                .GroupBy(member => member.ChannelId)
                .ToDictionary(group => group.Key, group => group.Select(member => member.UserId).ToHashSet());

            foreach (var conversation in directConversations)
            {
                if (!channels.TryGetValue(conversation.ChannelId, out var channel) ||
                    channel.Type != ChannelType.Private ||
                    channel.TenantId != conversation.TenantId)
                {
                    issues.Add($"一对一会话 {conversation.Id} 未关联同租户的有效 Private 频道。");
                    continue;
                }

                if (!members.TryGetValue(conversation.ChannelId, out var participantIds) ||
                    participantIds.Count != 2 ||
                    !participantIds.Contains(conversation.ParticipantLowUserId) ||
                    !participantIds.Contains(conversation.ParticipantHighUserId))
                {
                    issues.Add($"一对一会话 {conversation.Id} 没有且仅有两名匹配的有效频道成员。");
                }
            }
        }

        var duplicatePairCount = db.Queryable<DirectConversation>()
            .Select(conversation => new
            {
                conversation.TenantId,
                conversation.ParticipantLowUserId,
                conversation.ParticipantHighUserId
            })
            .ToList()
            .GroupBy(conversation => new
            {
                conversation.TenantId,
                conversation.ParticipantLowUserId,
                conversation.ParticipantHighUserId
            })
            .Count(group => group.Count() > 1);
        if (duplicatePairCount > 0)
        {
            issues.Add($"存在 {duplicatePairCount} 组重复的一对一参与者组合。");
        }

        return issues;
    }
}
