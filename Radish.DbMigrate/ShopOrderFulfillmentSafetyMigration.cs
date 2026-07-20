using System.Globalization;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>补齐商城订单履约安全字段并治理历史混合资源标识。</summary>
internal sealed class ShopOrderFulfillmentSafetyMigration : ISchemaMigration
{
    private const string TableName = "ShopOrder";

    public static ShopOrderFulfillmentSafetyMigration Instance { get; } = new();

    public string MigrationId => "20260713_001_shop_order_fulfillment_safety";

    public string Scope => "Main";

    public string Description => "区分订单支付/履约失败并固化订单履约快照与资源归属";

    public string ChecksumSource =>
        "20260713_001_shop_order_fulfillment_safety|Main|" +
        "FailureStage,FixedExpiresAt,GrantedBenefitId,GrantedInventoryId|" +
        "backfill-failure-stage-v1|split-legacy-resource-id-v1|fixed-expiry-snapshot-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        var idColumn = RequireColumn(db, "Id");
        var statusColumn = RequireColumn(db, "Status");
        var benefitExpiresAtColumn = RequireColumn(db, "BenefitExpiresAt");

        EnsureColumn(db, idColumn, "FailureStage", statusColumn.DataType, "NOT NULL DEFAULT 0");
        EnsureColumn(db, idColumn, "FixedExpiresAt", benefitExpiresAtColumn.DataType, "NULL");
        EnsureColumn(db, idColumn, "GrantedBenefitId", idColumn.DataType, "NULL");
        EnsureColumn(db, idColumn, "GrantedInventoryId", idColumn.DataType, "NULL");

        BackfillHistoricalOrders(db);
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        var issues = new List<string>();
        var requiredColumns = new[]
        {
            "FailureStage",
            "FixedExpiresAt",
            "GrantedBenefitId",
            "GrantedInventoryId"
        };

        foreach (var columnName in requiredColumns)
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, TableName, columnName) == null)
            {
                issues.Add($"{TableName}.{columnName} 表或列不存在。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        var columns = ResolveColumns(db);
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "Status")} = {(int)OrderStatus.Failed} AND " +
            $"{Column(columns, "FailureStage")} = {(int)OrderFailureStage.None}",
            "失败订单仍未标记失败阶段");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "Status")} <> {(int)OrderStatus.Failed} AND " +
            $"{Column(columns, "FailureStage")} <> {(int)OrderFailureStage.None}",
            "非失败订单错误保留失败阶段");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "Status")} IN ({(int)OrderStatus.Paid}, {(int)OrderStatus.Completed}) AND " +
            $"({Column(columns, "PaidTime")} IS NULL OR {Column(columns, "CoinTransactionId")} IS NULL)",
            "已支付或已完成订单缺少基础支付证据");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "FailureStage")} = {(int)OrderFailureStage.Fulfillment} AND " +
            $"({Column(columns, "PaidTime")} IS NULL OR {Column(columns, "CoinTransactionId")} IS NULL)",
            "履约失败订单缺少基础支付证据");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "ProductType")} = {(int)ProductType.Benefit} AND " +
            $"{Column(columns, "GrantedInventoryId")} IS NOT NULL",
            "权益商品错误关联背包资源");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "ProductType")} = {(int)ProductType.Consumable} AND " +
            $"{Column(columns, "GrantedBenefitId")} IS NOT NULL",
            "消耗品错误关联持续权益资源");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "Status")} = {(int)OrderStatus.Completed} AND " +
            $"{Column(columns, "ProductType")} = {(int)ProductType.Benefit} AND " +
            $"{Column(columns, "GrantedBenefitId")} IS NULL",
            "已完成权益订单缺少履约资源");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "Status")} = {(int)OrderStatus.Completed} AND " +
            $"{Column(columns, "ProductType")} = {(int)ProductType.Consumable} AND " +
            $"{Column(columns, "GrantedInventoryId")} IS NULL",
            "已完成消耗品订单缺少履约资源");
        AddCountIssue(
            db,
            issues,
            columns,
            $"{Column(columns, "DurationType")} = {(int)DurationType.FixedDate} AND " +
            $"({Column(columns, "Status")} IN ({(int)OrderStatus.Paid}, {(int)OrderStatus.Completed}) OR " +
            $"{Column(columns, "FailureStage")} = {(int)OrderFailureStage.Fulfillment}) AND " +
            $"{Column(columns, "FixedExpiresAt")} IS NULL",
            "固定日期订单缺少到期时间快照");

        return issues;
    }

    private static void BackfillHistoricalOrders(ISqlSugarClient db)
    {
        var columns = ResolveColumns(db);
        var table = QuoteIdentifier(columns["Id"].TableName);

        db.Ado.ExecuteCommand(
            $"UPDATE {table} SET {Column(columns, "FailureStage")} = CASE " +
            $"WHEN {Column(columns, "Status")} = {(int)OrderStatus.Failed} " +
            $"THEN CASE WHEN {Column(columns, "PaidTime")} IS NOT NULL " +
            $"AND {Column(columns, "CoinTransactionId")} IS NOT NULL " +
            $"THEN {(int)OrderFailureStage.Fulfillment} ELSE {(int)OrderFailureStage.Payment} END " +
            $"ELSE {(int)OrderFailureStage.None} END");

        db.Ado.ExecuteCommand(
            $"UPDATE {table} SET {Column(columns, "FixedExpiresAt")} = {Column(columns, "BenefitExpiresAt")} " +
            $"WHERE {Column(columns, "DurationType")} = {(int)DurationType.FixedDate} " +
            $"AND {Column(columns, "FixedExpiresAt")} IS NULL " +
            $"AND {Column(columns, "BenefitExpiresAt")} IS NOT NULL");

        db.Ado.ExecuteCommand(
            $"UPDATE {table} SET {Column(columns, "GrantedBenefitId")} = {Column(columns, "UserBenefitId")} " +
            $"WHERE {Column(columns, "ProductType")} = {(int)ProductType.Benefit} " +
            $"AND {Column(columns, "GrantedBenefitId")} IS NULL " +
            $"AND {Column(columns, "UserBenefitId")} IS NOT NULL");

        db.Ado.ExecuteCommand(
            $"UPDATE {table} SET {Column(columns, "GrantedInventoryId")} = {Column(columns, "UserBenefitId")} " +
            $"WHERE {Column(columns, "ProductType")} = {(int)ProductType.Consumable} " +
            $"AND {Column(columns, "GrantedInventoryId")} IS NULL " +
            $"AND {Column(columns, "UserBenefitId")} IS NOT NULL");
    }

    private static void EnsureColumn(
        ISqlSugarClient db,
        DatabaseColumnReference idColumn,
        string configuredColumnName,
        string dataType,
        string constraints)
    {
        if (DatabaseIdentifierResolver.ResolveColumn(db, TableName, configuredColumnName) != null)
        {
            return;
        }

        var physicalColumnName = IsLowercaseIdentifier(idColumn.ColumnName)
            ? configuredColumnName.ToLowerInvariant()
            : configuredColumnName;
        db.Ado.ExecuteCommand(
            $"ALTER TABLE {QuoteIdentifier(idColumn.TableName)} " +
            $"ADD COLUMN {QuoteIdentifier(physicalColumnName)} {NormalizeDataType(dataType)} {constraints}");
    }

    private static void AddCountIssue(
        ISqlSugarClient db,
        ICollection<string> issues,
        IReadOnlyDictionary<string, DatabaseColumnReference> columns,
        string predicate,
        string description)
    {
        var count = Convert.ToInt64(
            db.Ado.GetScalar(
                $"SELECT COUNT(*) FROM {QuoteIdentifier(columns["Id"].TableName)} WHERE {predicate}"),
            CultureInfo.InvariantCulture);
        if (count > 0)
        {
            issues.Add($"{description}，共 {count} 条。");
        }
    }

    private static Dictionary<string, DatabaseColumnReference> ResolveColumns(ISqlSugarClient db)
    {
        var names = new[]
        {
            "Id",
            "Status",
            "FailureStage",
            "PaidTime",
            "CoinTransactionId",
            "ProductType",
            "DurationType",
            "BenefitExpiresAt",
            "FixedExpiresAt",
            "UserBenefitId",
            "GrantedBenefitId",
            "GrantedInventoryId"
        };
        return names.ToDictionary(name => name, name => RequireColumn(db, name));
    }

    private static DatabaseColumnReference RequireColumn(ISqlSugarClient db, string columnName)
    {
        return DatabaseIdentifierResolver.ResolveColumn(db, TableName, columnName)
               ?? throw new InvalidOperationException($"{TableName}.{columnName} 表或列不存在。");
    }

    private static string Column(
        IReadOnlyDictionary<string, DatabaseColumnReference> columns,
        string configuredColumnName)
    {
        return QuoteIdentifier(columns[configuredColumnName].ColumnName);
    }

    private static string NormalizeDataType(string dataType)
    {
        var normalized = dataType.Trim().ToLowerInvariant();
        if (normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character is not ' ' and not '_' and not '(' and not ')' and not ','))
        {
            throw new InvalidOperationException($"不支持用于商城订单迁移的数据库类型：{dataType}");
        }

        return normalized;
    }

    private static bool IsLowercaseIdentifier(string identifier)
    {
        return string.Equals(identifier, identifier.ToLowerInvariant(), StringComparison.Ordinal);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
