using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立频道匿名公开摘要资格、并发版本与追加式审计事件。</summary>
internal sealed class ChatChannelDiscoverabilitySchemaMigration : ISchemaMigration
{
    private const string ChannelTable = "Channel";
    private const string EventTable = "ChannelDiscoverVisibilityEvent";

    private static readonly string[] RequiredIndexes =
    [
        "idx_channel_tenant_discover",
        "idx_channel_discover_event_version",
        "idx_channel_discover_event_time"
    ];

    public static ChatChannelDiscoverabilitySchemaMigration Instance { get; } = new();

    public string MigrationId => "20260805_018_chat_channel_discoverability";

    public string Scope => "Chat";

    public string Description => "建立频道匿名公开摘要显式资格、并发版本与追加式审计";

    public string ChecksumSource =>
        "20260805_018_chat_channel_discoverability|Chat|" +
        "Channel.DiscoverVisibility-hidden-default-v1|" +
        "Channel.DiscoverVisibilityVersion-v1|" +
        "ChannelDiscoverVisibilityEvent-append-only-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<Channel, ChannelDiscoverVisibilityEvent>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ChannelTable, false) ||
            DatabaseIdentifierResolver.ResolveColumn(db, ChannelTable, nameof(Channel.DiscoverVisibility)) != null)
        {
            return [];
        }

        var idColumn = DatabaseIdentifierResolver.ResolveColumn(db, ChannelTable, nameof(Channel.Id));
        if (idColumn == null)
        {
            return ["Channel 缺少 Id 列，无法安全增加匿名公开摘要资格。"];
        }

        var count = db.Ado.GetInt($"SELECT COUNT(*) FROM {QuoteIdentifier(idColumn.TableName)}");
        return count > 0
            ? [$"发现 {count} 个历史 Chat 频道；迁移将全部以 Hidden / version 0 初始化，不自动公开频道，也不修改消息、成员或在线状态。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(ChannelTable, false))
        {
            issues.Add($"缺少表 {ChannelTable}。");
            return issues;
        }

        var visibilityColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelTable,
            nameof(Channel.DiscoverVisibility));
        var versionColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelTable,
            nameof(Channel.DiscoverVisibilityVersion));
        if (visibilityColumn == null)
        {
            issues.Add($"缺少列 {ChannelTable}.{nameof(Channel.DiscoverVisibility)}。");
        }

        if (versionColumn == null)
        {
            issues.Add($"缺少列 {ChannelTable}.{nameof(Channel.DiscoverVisibilityVersion)}。");
        }

        if (visibilityColumn != null && versionColumn != null)
        {
            var invalidChannelCount = db.Queryable<Channel>()
                .Where(channel =>
                    channel.DiscoverVisibility != ChannelDiscoverVisibility.Hidden &&
                    channel.DiscoverVisibility != ChannelDiscoverVisibility.Summary ||
                    channel.DiscoverVisibilityVersion < 0)
                .Count();
            if (invalidChannelCount > 0)
            {
                issues.Add($"发现 {invalidChannelCount} 个公开摘要状态或版本无效的频道。");
            }
        }

        if (!db.DbMaintenance.IsAnyTable(EventTable, false))
        {
            issues.Add($"缺少表 {EventTable}。");
        }
        else
        {
            var invalidEventCount = db.Queryable<ChannelDiscoverVisibilityEvent>()
                .Where(change =>
                    change.FromVisibility == change.ToVisibility ||
                    change.ExpectedVersion < 0 ||
                    change.ResultVersion != change.ExpectedVersion + 1 ||
                    change.Reason == "")
                .Count();
            if (invalidEventCount > 0)
            {
                issues.Add($"发现 {invalidEventCount} 条无效的频道公开摘要变更事件。");
            }
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!IndexExists(db, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        return issues;
    }

    private static bool IndexExists(ISqlSugarClient db, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        var channelIndexes = db.DbMaintenance.IsAnyTable(ChannelTable, false)
            ? db.DbMaintenance.GetIndexList(ChannelTable)
            : [];
        var eventIndexes = db.DbMaintenance.IsAnyTable(EventTable, false)
            ? db.DbMaintenance.GetIndexList(EventTable)
            : [];
        return channelIndexes
                   .Concat(eventIndexes)
                   .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
