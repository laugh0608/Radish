using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立陌生请求首条消息声明与 Chat 附件唯一引用约束。</summary>
internal sealed class ChatReliableMessageSchemaMigration : ISchemaMigration
{
    private const string ChannelMessageTable = "ChannelMessage";
    private const string AttachmentIndex = "idx_channel_message_attachment";

    public static ChatReliableMessageSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260718_002_chat_reliable_message";

    public string Scope => "Chat";

    public string Description => "建立陌生请求首条消息声明与 Chat 附件唯一引用约束";

    public string ChecksumSource =>
        "20260718_002_chat_reliable_message|Chat|DirectConversation.RequestMessageId-v1|" +
        "ChannelMessage.tenant-attachment-unique-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<DirectConversation>();
        db.CodeFirst.InitTables<ChannelMessage>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ChannelMessageTable, false))
        {
            return [];
        }

        var duplicateCount = CountDuplicateAttachmentReferences(db);
        return duplicateCount > 0
            ? [$"发现 {duplicateCount} 组 Chat 消息重复引用同一附件；需先修复后再应用唯一索引。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        var requestMessageColumnExists =
            DatabaseIdentifierResolver.ResolveColumn(db, "DirectConversation", "RequestMessageId") != null;
        if (!requestMessageColumnExists)
        {
            issues.Add("DirectConversation.RequestMessageId 表或列不存在。");
        }

        if (!db.DbMaintenance.IsAnyIndex(AttachmentIndex))
        {
            issues.Add($"缺少索引 {AttachmentIndex}。");
        }

        var duplicateCount = CountDuplicateAttachmentReferences(db);
        if (duplicateCount > 0)
        {
            issues.Add($"仍存在 {duplicateCount} 组 Chat 消息重复引用同一附件。");
        }

        var claimedConversations = requestMessageColumnExists
            ? db.Queryable<DirectConversation>()
                .Where(conversation => conversation.RequestMessageId.HasValue && !conversation.IsDeleted)
                .ToList()
            : [];
        if (claimedConversations.Count > 0)
        {
            var requestMessageIds = claimedConversations
                .Select(conversation => conversation.RequestMessageId!.Value)
                .Distinct()
                .ToList();
            var requestMessages = db.Queryable<ChannelMessage>()
                .Where(message => requestMessageIds.Contains(message.Id))
                .Select(message => new ChannelMessage
                {
                    Id = message.Id,
                    ChannelId = message.ChannelId,
                    UserId = message.UserId,
                    TenantId = message.TenantId
                })
                .ToList()
                .ToDictionary(message => message.Id);
            foreach (var conversation in claimedConversations)
            {
                if (!requestMessages.TryGetValue(conversation.RequestMessageId!.Value, out var requestMessage) ||
                    requestMessage.ChannelId != conversation.ChannelId ||
                    requestMessage.UserId != conversation.RequestedByUserId ||
                    requestMessage.TenantId != conversation.TenantId)
                {
                    issues.Add($"一对一会话 {conversation.Id} 的首条请求消息声明与消息事实不一致。");
                }
            }
        }

        return issues;
    }

    private static int CountDuplicateAttachmentReferences(ISqlSugarClient db)
    {
        if (!db.DbMaintenance.IsAnyTable(ChannelMessageTable, false))
        {
            return 0;
        }

        return db.Queryable<ChannelMessage>()
            .Where(message => message.AttachmentId.HasValue)
            .Select(message => new { message.TenantId, message.AttachmentId })
            .ToList()
            .GroupBy(message => new { message.TenantId, message.AttachmentId })
            .Count(group => group.Count() > 1);
    }
}
