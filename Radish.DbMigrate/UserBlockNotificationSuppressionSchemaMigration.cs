using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>为 Message 通知关系增加不可逆的用户屏蔽抑制状态。</summary>
internal sealed class UserBlockNotificationSuppressionSchemaMigration : ISchemaMigration
{
    public static UserBlockNotificationSuppressionSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260725_011_user_block_notification_suppression";

    public string Scope => "Message";

    public string Description => "建立关系型通知的用户屏蔽抑制状态与查询索引";

    public string ChecksumSource =>
        "20260725_011_user_block_notification_suppression|Message|" +
        "UserNotification.SuppressedByUserBlock-v1|SuppressedAtUtc-v1|group-unread-index-v2";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<UserNotification>();
        var column = DatabaseIdentifierResolver.ResolveColumn(
            db,
            nameof(UserNotification),
            nameof(UserNotification.SuppressedByUserBlock));
        if (column != null)
        {
            var value = db.CurrentConnectionConfig.DbType == DbType.PostgreSQL ? "FALSE" : "0";
            var invalidCondition = db.CurrentConnectionConfig.DbType == DbType.PostgreSQL
                ? $"{Quote(column.ColumnName)} IS NULL"
                : $"{Quote(column.ColumnName)} IS NULL OR typeof({Quote(column.ColumnName)}) <> 'integer'";
            db.Ado.ExecuteCommand(
                $"UPDATE {Quote(column.TableName)} SET {Quote(column.ColumnName)}={value} " +
                $"WHERE {invalidCondition}");
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(nameof(UserNotification), false))
        {
            return [$"缺少表 {nameof(UserNotification)}。"];
        }

        var issues = new List<string>();
        foreach (var column in new[]
                 {
                     nameof(UserNotification.SuppressedByUserBlock),
                     nameof(UserNotification.SuppressedAtUtc)
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, nameof(UserNotification), column) == null)
            {
                issues.Add($"缺少列 {nameof(UserNotification)}.{column}。");
            }
        }

        return issues;
    }

    private static string Quote(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
