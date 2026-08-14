using SqlSugar;

namespace Radish.DbMigrate;

internal sealed record DatabaseColumnReference(
    string TableName,
    string ColumnName,
    string DataType);

internal static class DatabaseIdentifierResolver
{
    public static string? ResolveTable(ISqlSugarClient db, string configuredTableName)
    {
        return ResolveName(
            db.DbMaintenance.GetTableInfoList(false).Select(table => table.Name),
            configuredTableName);
    }

    public static DatabaseColumnReference? ResolveColumn(
        ISqlSugarClient db,
        string configuredTableName,
        string configuredColumnName)
    {
        var tableName = ResolveTable(db, configuredTableName);
        if (tableName == null)
        {
            return null;
        }

        var column = db.DbMaintenance
            .GetColumnInfosByTableName(tableName, false)
            .OrderBy(item => string.Equals(
                item.DbColumnName,
                configuredColumnName,
                StringComparison.Ordinal) ? 0 : 1)
            .FirstOrDefault(item => string.Equals(
                item.DbColumnName,
                configuredColumnName,
                StringComparison.OrdinalIgnoreCase));
        if (column == null)
        {
            return null;
        }

        return new DatabaseColumnReference(
            tableName,
            column.DbColumnName,
            column.DataType?.Trim().ToLowerInvariant() ?? "unknown");
    }

    private static string? ResolveName(IEnumerable<string> physicalNames, string configuredName)
    {
        var names = physicalNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        return names.FirstOrDefault(name => string.Equals(name, configuredName, StringComparison.Ordinal))
               ?? names.FirstOrDefault(name => string.Equals(
                   name,
                   configuredName,
                   StringComparison.OrdinalIgnoreCase));
    }
}
