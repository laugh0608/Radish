using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Chat 消息回应、幂等事实和消息级 revision。</summary>
internal sealed class ChatMessageReactionSchemaMigration : ISchemaMigration
{
    private const string ChannelMessageTable = "ChannelMessage";
    private const string ReactionTable = "ChatMessageReaction";
    private const string OperationTable = "ChatMessageReactionOperation";
    private static readonly string[] RequiredIndexes =
    [
        "idx_chat_reaction_message",
        "idx_chat_reaction_channel_message",
        "idx_chat_reaction_unique",
        "idx_chat_reaction_operation_unique",
        "idx_chat_reaction_operation_expiry"
    ];

    public static ChatMessageReactionSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260719_004_chat_message_reaction";

    public string Scope => "Chat";

    public string Description => "建立聊天消息回应、幂等事实和消息级 revision";

    public string ChecksumSource =>
        "20260719_004_chat_message_reaction|Chat|" +
        "ChannelMessage.ReactionRevision-v1|ChatMessageReaction-v1|" +
        "ChatMessageReactionOperation-30d-v1|desired-state-idempotency-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<ChannelMessage, ChatMessageReaction, ChatMessageReactionOperation>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ChannelMessageTable, false) ||
            DatabaseIdentifierResolver.ResolveColumn(
                db,
                ChannelMessageTable,
                nameof(ChannelMessage.ReactionRevision)) != null)
        {
            return [];
        }

        var table = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.Id));
        if (table == null)
        {
            return ["ChannelMessage 缺少 Id 列，无法安全增加 ReactionRevision。"];
        }

        var count = db.Ado.GetInt($"SELECT COUNT(*) FROM {QuoteIdentifier(table.TableName)}");
        return count > 0
            ? [$"发现 {count} 条历史 Chat 消息；迁移将以 0 初始化 ReactionRevision，不修改消息正文、搜索或未读状态。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(ReactionTable, false))
        {
            issues.Add($"缺少表 {ReactionTable}。");
        }

        if (!db.DbMaintenance.IsAnyTable(OperationTable, false))
        {
            issues.Add($"缺少表 {OperationTable}。");
        }

        if (DatabaseIdentifierResolver.ResolveColumn(
                db,
                ChannelMessageTable,
                nameof(ChannelMessage.ReactionRevision)) == null)
        {
            issues.Add("ChannelMessage.ReactionRevision 列不存在。");
        }
        else
        {
            var invalidCount = db.Queryable<ChannelMessage>()
                .Where(message => message.ReactionRevision < 0)
                .Count();
            if (invalidCount > 0)
            {
                issues.Add($"发现 {invalidCount} 条 ReactionRevision 小于 0 的消息。");
            }
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        return issues;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
