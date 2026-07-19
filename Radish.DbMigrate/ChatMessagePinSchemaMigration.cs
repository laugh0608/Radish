using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Chat 共享消息置顶与频道级 revision。</summary>
internal sealed class ChatMessagePinSchemaMigration : ISchemaMigration
{
    private const string ChannelTable = "Channel";
    private const string ChannelMessageTable = "ChannelMessage";
    private const string PinTable = "ChatMessagePin";
    private static readonly string[] RequiredIndexes =
    [
        "idx_chat_message_pin_channel",
        "idx_chat_message_pin_message",
        "idx_chat_message_pin_unique"
    ];

    public static ChatMessagePinSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260719_005_chat_message_pin";

    public string Scope => "Chat";

    public string Description => "建立聊天消息多条置顶、频道级 revision 与状态审计";

    public string ChecksumSource =>
        "20260719_005_chat_message_pin|Chat|Channel.PinRevision-v1|" +
        "ChatMessagePin-soft-delete-v1|max-active-20-v1|desired-state-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<Channel, ChatMessagePin>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ChannelTable, false) ||
            DatabaseIdentifierResolver.ResolveColumn(db, ChannelTable, nameof(Channel.PinRevision)) != null)
        {
            return [];
        }

        var idColumn = DatabaseIdentifierResolver.ResolveColumn(db, ChannelTable, nameof(Channel.Id));
        if (idColumn == null)
        {
            return ["Channel 缺少 Id 列，无法安全增加 PinRevision。"];
        }

        var count = db.Ado.GetInt($"SELECT COUNT(*) FROM {QuoteIdentifier(idColumn.TableName)}");
        return count > 0
            ? [$"发现 {count} 个历史 Chat 频道；迁移将以 0 初始化 PinRevision，不修改消息、搜索、未读或最后消息。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(PinTable, false))
        {
            issues.Add($"缺少表 {PinTable}。");
        }

        if (DatabaseIdentifierResolver.ResolveColumn(db, ChannelTable, nameof(Channel.PinRevision)) == null)
        {
            issues.Add("Channel.PinRevision 列不存在。");
        }
        else
        {
            var invalidRevisionCount = db.Queryable<Channel>()
                .Where(channel => channel.PinRevision < 0)
                .Count();
            if (invalidRevisionCount > 0)
            {
                issues.Add($"发现 {invalidRevisionCount} 个 PinRevision 小于 0 的频道。");
            }
        }

        if (db.DbMaintenance.IsAnyTable(PinTable, false))
        {
            VerifyActivePins(db, issues);
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

    private static void VerifyActivePins(ISqlSugarClient db, List<string> issues)
    {
        var pinId = DatabaseIdentifierResolver.ResolveColumn(db, PinTable, nameof(ChatMessagePin.Id));
        var pinMessageId = DatabaseIdentifierResolver.ResolveColumn(db, PinTable, nameof(ChatMessagePin.MessageId));
        var pinChannelId = DatabaseIdentifierResolver.ResolveColumn(db, PinTable, nameof(ChatMessagePin.ChannelId));
        var pinTenantId = DatabaseIdentifierResolver.ResolveColumn(db, PinTable, nameof(ChatMessagePin.TenantId));
        var pinDeleted = DatabaseIdentifierResolver.ResolveColumn(db, PinTable, nameof(ChatMessagePin.IsDeleted));
        var messageId = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.Id));
        var messageChannelId = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.ChannelId));
        var messageTenantId = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.TenantId));
        var messageDeleted = DatabaseIdentifierResolver.ResolveColumn(db, ChannelMessageTable, nameof(ChannelMessage.IsDeleted));
        if (pinId == null || pinMessageId == null || pinChannelId == null || pinTenantId == null || pinDeleted == null ||
            messageId == null || messageChannelId == null || messageTenantId == null || messageDeleted == null)
        {
            issues.Add("ChatMessagePin 或 ChannelMessage 缺少置顶校验所需列。");
            return;
        }

        var orphanCount = db.Ado.GetInt($$"""
            SELECT COUNT(*)
            FROM {{QuoteIdentifier(pinId.TableName)}} pin
            LEFT JOIN {{QuoteIdentifier(messageId.TableName)}} message
              ON message.{{QuoteIdentifier(messageId.ColumnName)}} = pin.{{QuoteIdentifier(pinMessageId.ColumnName)}}
             AND message.{{QuoteIdentifier(messageChannelId.ColumnName)}} = pin.{{QuoteIdentifier(pinChannelId.ColumnName)}}
             AND message.{{QuoteIdentifier(messageTenantId.ColumnName)}} = pin.{{QuoteIdentifier(pinTenantId.ColumnName)}}
            WHERE pin.{{QuoteIdentifier(pinDeleted.ColumnName)}} = @isDeleted
              AND (message.{{QuoteIdentifier(messageId.ColumnName)}} IS NULL
                   OR message.{{QuoteIdentifier(messageDeleted.ColumnName)}} <> @isDeleted)
            """, new SugarParameter("@isDeleted", false));
        if (orphanCount > 0)
        {
            issues.Add($"发现 {orphanCount} 条引用缺失或已撤回消息的活跃置顶。");
        }

        var overLimitChannelCount = db.Ado.GetInt($$"""
            SELECT COUNT(*)
            FROM (
                SELECT pin.{{QuoteIdentifier(pinTenantId.ColumnName)}},
                       pin.{{QuoteIdentifier(pinChannelId.ColumnName)}}
                FROM {{QuoteIdentifier(pinId.TableName)}} pin
                WHERE pin.{{QuoteIdentifier(pinDeleted.ColumnName)}} = @isDeleted
                GROUP BY pin.{{QuoteIdentifier(pinTenantId.ColumnName)}},
                         pin.{{QuoteIdentifier(pinChannelId.ColumnName)}}
                HAVING COUNT(*) > 20
            ) over_limit
            """, new SugarParameter("@isDeleted", false));
        if (overLimitChannelCount > 0)
        {
            issues.Add($"发现 {overLimitChannelCount} 个频道超过 20 条活跃置顶。");
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
