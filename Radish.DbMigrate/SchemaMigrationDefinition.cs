using SqlSugar;

namespace Radish.DbMigrate;

internal interface ISchemaMigration
{
    string MigrationId { get; }

    string Scope { get; }

    string Description { get; }

    string ChecksumSource { get; }

    void Apply(ISqlSugarClient db, IServiceProvider services);

    IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services);
}

internal static class SchemaMigrationRegistry
{
    public static IReadOnlyList<ISchemaMigration> All { get; } =
    [
        ExperienceNaturalDateSchemaMigration.Instance
    ];
}
