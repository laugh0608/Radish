using System.Globalization;
using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立商城消耗品使用流水及其幂等约束。</summary>
internal sealed class ShopEntitlementOperationSchemaMigration : ISchemaMigration
{
    private const string TableName = "ShopEntitlementOperation";
    private const string IdempotencyIndexName = "idx_shop_entitlement_operation_idempotency";
    private const string UserTimeIndexName = "idx_shop_entitlement_operation_user_time";
    private const string InventoryTimeIndexName = "idx_shop_entitlement_operation_inventory_time";

    public static ShopEntitlementOperationSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260713_002_shop_entitlement_operation";

    public string Scope => "Main";

    public string Description => "建立消耗品使用流水、幂等结果与效果资源关联";

    public string ChecksumSource =>
        "20260713_002_shop_entitlement_operation|Main|" +
        "ShopEntitlementOperation-v1|idempotency-user-inventory-indexes-v1|successful-operation-only-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        // 该迁移只增加独立业务流水表，不对历史背包扣减做不可靠反推。
        db.CodeFirst.InitTables<ShopEntitlementOperation>();
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = new List<string>();
        var requiredColumns = new[]
        {
            "Id",
            "TenantId",
            "UserId",
            "InventoryId",
            "OperationType",
            "ConsumableType",
            "Quantity",
            "ItemValue",
            "IdempotencyKey",
            "RequestHash",
            "EffectType",
            "ResultPayload",
            "CreateTime"
        };
        var resolvedColumns = new Dictionary<string, DatabaseColumnReference>(StringComparer.Ordinal);
        foreach (var columnName in requiredColumns)
        {
            var column = DatabaseIdentifierResolver.ResolveColumn(db, TableName, columnName);
            if (column == null)
            {
                issues.Add($"{TableName}.{columnName} 表或列不存在。");
                continue;
            }

            resolvedColumns[columnName] = column;
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        foreach (var indexName in new[] { IdempotencyIndexName, UserTimeIndexName, InventoryTimeIndexName })
        {
            if (!db.DbMaintenance.IsAnyIndex(indexName))
            {
                issues.Add($"{TableName} 缺少索引 {indexName}。");
            }
        }

        var table = QuoteIdentifier(resolvedColumns["Id"].TableName);
        var duplicateCount = Convert.ToInt64(
            db.Ado.GetScalar(
                $"SELECT COUNT(*) FROM (" +
                $"SELECT 1 FROM {table} GROUP BY " +
                $"{Column(resolvedColumns, "TenantId")}, " +
                $"{Column(resolvedColumns, "UserId")}, " +
                $"{Column(resolvedColumns, "OperationType")}, " +
                $"{Column(resolvedColumns, "IdempotencyKey")} HAVING COUNT(*) > 1) duplicated"),
            CultureInfo.InvariantCulture);
        if (duplicateCount > 0)
        {
            issues.Add($"消耗品使用流水存在 {duplicateCount} 组重复幂等键。");
        }

        var invalidCount = Convert.ToInt64(
            db.Ado.GetScalar(
                $"SELECT COUNT(*) FROM {table} WHERE " +
                $"{Column(resolvedColumns, "Quantity")} <= 0 OR " +
                $"{Column(resolvedColumns, "IdempotencyKey")} = '' OR " +
                $"{Column(resolvedColumns, "RequestHash")} = '' OR " +
                $"{Column(resolvedColumns, "ResultPayload")} = ''"),
            CultureInfo.InvariantCulture);
        if (invalidCount > 0)
        {
            issues.Add($"消耗品使用流水存在 {invalidCount} 条无效成功记录。");
        }

        return issues;
    }

    private static string Column(
        IReadOnlyDictionary<string, DatabaseColumnReference> columns,
        string configuredColumnName)
    {
        return QuoteIdentifier(columns[configuredColumnName].ColumnName);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
