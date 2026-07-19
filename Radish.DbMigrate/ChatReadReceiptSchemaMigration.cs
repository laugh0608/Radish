using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Chat 成员已读游标聚合索引。</summary>
internal sealed class ChatReadReceiptSchemaMigration : ISchemaMigration
{
    private const string ChannelMemberTable = "ChannelMember";
    private const string ChannelUserIndex = "idx_channel_member_channel_user";
    private const string UserIndex = "idx_channel_member_user";
    private const string ReadCursorIndex = "idx_channel_member_read_cursor";

    public static ChatReadReceiptSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260719_006_chat_read_receipt";

    public string Scope => "Chat";

    public string Description => "建立聊天成员已读游标聚合索引";

    public string ChecksumSource =>
        "20260719_006_chat_read_receipt|Chat|" +
        "ChannelMember(ChannelId,UserId)-unique-repair-v1|" +
        "ChannelMember(UserId)-repair-v1|" +
        "ChannelMember(ChannelId,IsDeleted,LastReadMessageId,UserId)-v1|" +
        "monotonic-cursor-v1|sender-restricted-aggregation-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<ChannelMember>();

        var channelId = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelMemberTable,
            nameof(ChannelMember.ChannelId));
        var isDeleted = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelMemberTable,
            nameof(ChannelMember.IsDeleted));
        var lastReadMessageId = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelMemberTable,
            nameof(ChannelMember.LastReadMessageId));
        var userId = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelMemberTable,
            nameof(ChannelMember.UserId));
        if (channelId == null || isDeleted == null || lastReadMessageId == null || userId == null)
        {
            throw new InvalidOperationException("ChannelMember 缺少阅读回执索引所需列。");
        }

        EnsureIndex(
            db,
            channelId.TableName,
            [channelId.ColumnName, userId.ColumnName],
            ChannelUserIndex,
            true);
        EnsureIndex(db, channelId.TableName, [userId.ColumnName], UserIndex, false);
        EnsureIndex(
            db,
            channelId.TableName,
            [
                channelId.ColumnName,
                isDeleted.ColumnName,
                lastReadMessageId.ColumnName,
                userId.ColumnName
            ],
            ReadCursorIndex,
            false);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ChannelMemberTable, false))
        {
            return [];
        }

        var duplicateMemberGroups = db.Queryable<ChannelMember>()
            .GroupBy(member => new { member.ChannelId, member.UserId })
            .Having(member => SqlFunc.AggregateCount(member.Id) > 1)
            .Select(member => new { member.ChannelId, member.UserId })
            .ToList()
            .Count;
        var invalidCursorCount = db.Queryable<ChannelMember>()
            .Where(member => member.LastReadMessageId < 0)
            .Count();
        var issues = new List<string>();
        if (duplicateMemberGroups > 0)
        {
            issues.Add($"发现 {duplicateMemberGroups} 组重复频道成员；迁移不会猜测保留记录，必须先清理。");
        }

        if (invalidCursorCount > 0)
        {
            issues.Add($"发现 {invalidCursorCount} 个负数已读游标；迁移不会静默改写业务状态，必须先清理。");
        }

        return issues;
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(ChannelMemberTable, false))
        {
            issues.Add($"缺少表 {ChannelMemberTable}。");
            return issues;
        }

        var channelId = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ChannelMemberTable,
            nameof(ChannelMember.ChannelId));
        if (channelId == null)
        {
            issues.Add("ChannelMember.ChannelId 列不存在。");
            return issues;
        }

        foreach (var indexName in new[] { ChannelUserIndex, UserIndex, ReadCursorIndex })
        {
            if (!IndexExists(db, channelId.TableName, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        var invalidCursorCount = db.Queryable<ChannelMember>()
            .Where(member => member.LastReadMessageId < 0)
            .Count();
        if (invalidCursorCount > 0)
        {
            issues.Add($"发现 {invalidCursorCount} 个负数已读游标。");
        }

        return issues;
    }

    private static void EnsureIndex(
        ISqlSugarClient db,
        string tableName,
        string[] columnNames,
        string indexName,
        bool unique)
    {
        if (IndexExists(db, tableName, indexName))
        {
            return;
        }

        var created = db.DbMaintenance.CreateIndex(
            tableName,
            columnNames,
            indexName,
            unique);
        if (!created && !IndexExists(db, tableName, indexName))
        {
            throw new InvalidOperationException($"创建索引 {indexName} 失败。");
        }
    }

    private static bool IndexExists(ISqlSugarClient db, string tableName, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        return db.DbMaintenance.GetIndexList(tableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }
}
