using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Radish.Api.Tests.TestCollections;

public sealed class PostgreSqlIntegrationCollectionConventionTest
{
    [Fact]
    public void PostgreSqlTests_ShouldUseExclusiveCollection()
    {
        var missingCollectionTypes = typeof(PostgreSqlIntegrationCollectionConventionTest)
            .Assembly
            .GetTypes()
            .Where(ContainsPostgreSqlTest)
            .Where(type => type
                .GetCustomAttributes<CollectionAttribute>()
                .All(attribute => !string.Equals(
                    attribute.Name,
                    PostgreSqlIntegrationCollection.CollectionName,
                    StringComparison.Ordinal)))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(missingCollectionTypes);
    }

    private static bool ContainsPostgreSqlTest(Type type)
    {
        return type.GetCustomAttributes<TraitAttribute>().Any(IsPostgreSqlTrait) ||
               type
                   .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   .SelectMany(method => method.GetCustomAttributes<TraitAttribute>())
                   .Any(IsPostgreSqlTrait);
    }

    private static bool IsPostgreSqlTrait(TraitAttribute attribute)
    {
        return string.Equals(attribute.Name, "Database", StringComparison.Ordinal) &&
               string.Equals(attribute.Value, "PostgreSQL", StringComparison.Ordinal);
    }
}
