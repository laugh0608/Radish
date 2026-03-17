using SqlSugar;

namespace Radish.DbMigrate;

internal static class DbMigrateEntityRegistry
{
    private static readonly IReadOnlyList<Type> AllEntityTypes = typeof(Radish.Model.Root.RootEntityTKey<>).Assembly
        .GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic)
        .Where(type =>
            type.GetCustomAttributes(typeof(SugarTable), inherit: true).Any() ||
            (type.BaseType != null && type.BaseType.IsGenericType &&
             type.BaseType.GetGenericTypeDefinition() == typeof(Radish.Model.Root.RootEntityTKey<>)))
        .Concat([typeof(Radish.Model.UserRole)])
        .Distinct()
        .ToList();

    public static IReadOnlyList<Type> GetEntityTypesForConfig(string? configId)
    {
        var normalizedConfigId = string.IsNullOrWhiteSpace(configId) ? "Main" : configId;

        return AllEntityTypes
            .Where(type =>
            {
                var tenantAttr = type.GetCustomAttributes(typeof(TenantAttribute), inherit: true)
                    .Cast<TenantAttribute>()
                    .FirstOrDefault();

                if (tenantAttr?.configId != null)
                {
                    var tenantConfigId = tenantAttr.configId.ToString();
                    if (!string.IsNullOrWhiteSpace(tenantConfigId))
                    {
                        return string.Equals(tenantConfigId, normalizedConfigId, StringComparison.OrdinalIgnoreCase);
                    }
                }

                return string.Equals(normalizedConfigId, "Main", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    public static IReadOnlyList<string> GetTableNamesForConfig(string? configId)
    {
        return GetEntityTypesForConfig(configId)
            .Select(type => type.GetCustomAttributes(typeof(SugarTable), inherit: true)
                .Cast<SugarTable>()
                .FirstOrDefault()?.TableName ?? type.Name)
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
