using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立论坛内容赞赏权威事实与唯一约束。</summary>
internal sealed class ContentRewardSchemaMigration : ISchemaMigration
{
    private const string TableName = "ContentReward";

    private static readonly string[] RequiredIndexes =
    [
        "idx_content_reward_sender_target_unique",
        "idx_content_reward_transaction_unique",
        "idx_content_reward_target_time",
        "idx_content_reward_sender_time",
        "idx_content_reward_sender_recipient_time",
        "idx_content_reward_post_time"
    ];

    public static ContentRewardSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260727_014_content_reward";

    public string Scope => "Main";

    public string Description => "建立论坛内容赞赏事实、目标唯一约束与查询索引";

    public string ChecksumSource =>
        "20260727_014_content_reward|Main|" +
        "ContentReward-v1|sender-target-unique-v1|transaction-unique-v1|" +
        "target-sender-recipient-post-query-v1|amount-fixed-one-verify-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<ContentReward>();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(TableName, false))
        {
            return [$"缺少表 {TableName}。"];
        }

        foreach (var columnName in new[]
                 {
                     nameof(ContentReward.TenantId),
                     nameof(ContentReward.TargetType),
                     nameof(ContentReward.TargetId),
                     nameof(ContentReward.PostId),
                     nameof(ContentReward.SenderUserId),
                     nameof(ContentReward.RecipientUserId),
                     nameof(ContentReward.Amount),
                     nameof(ContentReward.ReasonCode),
                     nameof(ContentReward.CoinTransactionId),
                     nameof(ContentReward.CreateTime)
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, TableName, columnName) == null)
            {
                issues.Add($"缺少列 {TableName}.{columnName}。");
            }
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!IndexExists(db, TableName, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        var invalidAmountCount = db.Queryable<ContentReward>()
            .Where(item => item.Amount != 1)
            .Count();
        if (invalidAmountCount > 0)
        {
            issues.Add($"发现 {invalidAmountCount} 条 Amount 不等于 1 的内容赞赏事实。");
        }

        return issues;
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
