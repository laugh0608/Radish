using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Chat 历史消息派生搜索文本与稳定搜索顺序索引。</summary>
internal sealed class ChatMessageSearchSchemaMigration : ISchemaMigration
{
    private const string ChannelMessageTable = "ChannelMessage";
    private const int BackfillBatchSize = 200;
    private static readonly string[] RequiredIndexes =
    [
        "idx_channel_message_channel_search_order",
        "idx_channel_message_tenant_search_order"
    ];

    public static ChatMessageSearchSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260718_003_chat_message_search";

    public string Scope => "Chat";

    public string Description => "建立聊天历史消息派生搜索文本与稳定搜索顺序索引";

    public string ChecksumSource =>
        "20260718_003_chat_message_search|Chat|ChannelMessage.SearchText-v1|" +
        "visible-mention-whitespace-control-resource-lower-invariant-4000-v1|" +
        "channel-create-id-order-v1|tenant-create-id-order-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<ChannelMessage>();
        BackfillSearchText(db);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ChannelMessageTable, false))
        {
            return [];
        }

        var searchColumn = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.SearchText));
        if (searchColumn != null)
        {
            return [];
        }

        var idColumn = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.Id));
        if (idColumn == null)
        {
            return ["ChannelMessage 缺少 Id 列，无法安全回填搜索文本。"];
        }

        var count = db.Ado.GetInt($"SELECT COUNT(*) FROM {QuoteIdentifier(idColumn.TableName)}");
        return count > 0
            ? [$"发现 {count} 条历史 Chat 消息；迁移将分批生成 SearchText，不修改原 Content。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.SearchText)) == null)
        {
            issues.Add("ChannelMessage.SearchText 表或列不存在。");
            return issues;
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        var lastId = 0L;
        while (true)
        {
            var messages = db.Queryable<ChannelMessage>()
                .Where(message => message.Id > lastId)
                .OrderBy(message => message.Id)
                .Take(BackfillBatchSize)
                .ToList();
            if (messages.Count == 0)
            {
                break;
            }

            foreach (var message in messages)
            {
                var expected = DeriveSearchText(message);
                if (!string.Equals(message.SearchText, expected, StringComparison.Ordinal))
                {
                    issues.Add($"ChannelMessage {message.Id} 的 SearchText 与当前派生规则不一致。");
                    if (issues.Count >= 20)
                    {
                        issues.Add("SearchText 不一致项超过 20 条，已停止继续列举。");
                        return issues;
                    }
                }
            }

            lastId = messages[^1].Id;
        }

        return issues;
    }

    private static void BackfillSearchText(ISqlSugarClient db)
    {
        var lastId = 0L;
        while (true)
        {
            var messages = db.Queryable<ChannelMessage>()
                .Where(message => message.Id > lastId)
                .OrderBy(message => message.Id)
                .Take(BackfillBatchSize)
                .ToList();
            if (messages.Count == 0)
            {
                return;
            }

            foreach (var message in messages)
            {
                var expected = DeriveSearchText(message);
                if (string.Equals(message.SearchText, expected, StringComparison.Ordinal))
                {
                    continue;
                }

                message.SearchText = expected;
                db.Updateable(message)
                    .UpdateColumns(candidate => candidate.SearchText)
                    .ExecuteCommand();
            }

            lastId = messages[^1].Id;
        }
    }

    private static string? DeriveSearchText(ChannelMessage message)
    {
        if (message.IsDeleted || message.Type is not MessageType.Text and not MessageType.Image)
        {
            return null;
        }

        return ChatMessageSearchTextNormalizer.Normalize(message.Content);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
