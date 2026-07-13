using SqlSugar;

namespace Radish.Repository;

internal static class RepositorySqlHelper
{
    public static string ResolvePhysicalTableName(ISqlSugarClient db, string configuredTableName)
    {
        return ResolvePhysicalName(
                   db.DbMaintenance.GetTableInfoList(false).Select(table => table.Name),
                   configuredTableName)
               ?? throw new InvalidOperationException($"数据库中不存在表 {configuredTableName}");
    }

    public static string ResolvePhysicalColumnName(
        IEnumerable<string> physicalColumnNames,
        string physicalTableName,
        string configuredColumnName)
    {
        return ResolvePhysicalName(
                   physicalColumnNames,
                   configuredColumnName)
               ?? throw new InvalidOperationException(
                   $"数据库表 {physicalTableName} 中不存在列 {configuredColumnName}");
    }

    public static string QuoteIdentifier(string identifier)
    {
        return string.Join(".", identifier
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => $"\"{part.Replace("\"", "\"\"")}\""));
    }

    public static bool IsUniqueConstraintException(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("unique", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolvePhysicalName(IEnumerable<string> physicalNames, string configuredName)
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
