using System.Globalization;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>移除通知接收关系中已经退出产品语义的虚假 delivery 字段。</summary>
internal sealed class NotificationDeliveryCleanupSchemaMigration : ISchemaMigration
{
    private const string UserNotificationTable = "UserNotification";
    private static readonly string[] LegacyColumns =
    [
        "DeliveryStatus",
        "DeliveredAt",
        "RetryCount",
        "LastRetryAt"
    ];

    public static NotificationDeliveryCleanupSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260718_004_notification_delivery_cleanup";

    public string Scope => "Message";

    public string Description => "移除通知关系中的旧 delivery 字段并恢复当前写入契约";

    public string ChecksumSource =>
        "20260718_004_notification_delivery_cleanup|Message|" +
        "user-notification-default-delivery-validation-v1|drop-legacy-delivery-columns-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = Diagnose(db, services);
        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "通知 delivery 字段清理前置诊断未通过：" + string.Join("；", issues));
        }

        if (!db.DbMaintenance.IsAnyTable(UserNotificationTable, false))
        {
            return;
        }

        foreach (var columnName in LegacyColumns)
        {
            var column = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, columnName);
            if (column == null)
            {
                continue;
            }

            db.Ado.ExecuteCommand(
                $"ALTER TABLE {Quote(column.TableName)} DROP COLUMN {Quote(column.ColumnName)}");
        }
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(UserNotificationTable, false))
        {
            return [];
        }

        var conditions = new List<string>();
        AddCondition("DeliveryStatus", column => $"{Quote(column.ColumnName)} <> 'Created'");
        AddCondition("DeliveredAt", column => $"{Quote(column.ColumnName)} IS NOT NULL");
        AddCondition("RetryCount", column => $"{Quote(column.ColumnName)} <> 0");
        AddCondition("LastRetryAt", column => $"{Quote(column.ColumnName)} IS NOT NULL");
        if (conditions.Count == 0)
        {
            return [];
        }

        var table = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, "Id")
                    ?? throw new InvalidOperationException($"{UserNotificationTable}.Id 不存在。");
        var nonDefaultCount = Convert.ToInt64(
            db.Ado.GetScalar(
                $"SELECT COUNT(*) FROM {Quote(table.TableName)} WHERE {string.Join(" OR ", conditions)}"),
            CultureInfo.InvariantCulture);
        return nonDefaultCount > 0
            ? [$"发现 {nonDefaultCount} 条非默认 delivery 状态，不能静默删除旧字段"]
            : [];

        void AddCondition(string columnName, Func<DatabaseColumnReference, string> buildCondition)
        {
            var column = DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, columnName);
            if (column != null)
            {
                conditions.Add(buildCondition(column));
            }
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(UserNotificationTable, false))
        {
            return [];
        }

        return LegacyColumns
            .Where(columnName =>
                DatabaseIdentifierResolver.ResolveColumn(db, UserNotificationTable, columnName) != null)
            .Select(columnName => $"{UserNotificationTable}.{columnName} 旧字段仍然存在。")
            .ToList();
    }

    private static string Quote(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
