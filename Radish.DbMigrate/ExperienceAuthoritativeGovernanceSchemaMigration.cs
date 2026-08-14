using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立经验治理版本快照与等级重算追加式审计。</summary>
internal sealed class ExperienceAuthoritativeGovernanceSchemaMigration : ISchemaMigration
{
    private const string ActionTable = "UserExperienceGovernanceAction";
    private const string AuditTable = "ExperienceLevelRecalculationAudit";

    private static readonly string[] RequiredActionIndexes =
    [
        "idx_exp_governance_target_createtime"
    ];

    private static readonly string[] RequiredAuditIndexes =
    [
        "idx_experience_level_recalc_time"
    ];

    public static ExperienceAuthoritativeGovernanceSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260813_022_experience_authoritative_governance";

    public string Scope => "Main";

    public string Description => "建立经验治理版本快照与等级重算追加式审计";

    public string ChecksumSource =>
        "20260813_022_experience_authoritative_governance|Main|" +
        "UserExperienceGovernanceAction-version-snapshot-v1|" +
        "ExperienceLevelRecalculationAudit-append-only-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            db.CodeFirst.InitTables<UserExperienceGovernanceAction, ExperienceLevelRecalculationAudit>();
            return;
        }

        PrepareLegacyPostgreSqlActionTable(db);
        InitTable<UserExperienceGovernanceAction>(db, ActionTable);
        InitTable<ExperienceLevelRecalculationAudit>(db, AuditTable);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var actionTable = DatabaseIdentifierResolver.ResolveTable(db, ActionTable);
        if (actionTable == null)
        {
            return [];
        }

        var expectedVersionColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ActionTable,
            nameof(UserExperienceGovernanceAction.ExpectedVersion));
        var resultVersionColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            ActionTable,
            nameof(UserExperienceGovernanceAction.ResultVersion));
        if (expectedVersionColumn != null && resultVersionColumn != null)
        {
            return [];
        }

        var count = db.Ado.GetInt($"SELECT COUNT(*) FROM {QuoteIdentifier(actionTable)}");
        return count > 0
            ? [$"发现 {count} 条历史经验治理动作；迁移将保留动作事实，并以空版本快照标记为迁移前记录。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        var actionTable = DatabaseIdentifierResolver.ResolveTable(db, ActionTable);
        if (actionTable == null)
        {
            issues.Add($"缺少表 {ActionTable}。");
        }
        else
        {
            var expectedVersionColumn = DatabaseIdentifierResolver.ResolveColumn(
                db,
                ActionTable,
                nameof(UserExperienceGovernanceAction.ExpectedVersion));
            var resultVersionColumn = DatabaseIdentifierResolver.ResolveColumn(
                db,
                ActionTable,
                nameof(UserExperienceGovernanceAction.ResultVersion));
            if (expectedVersionColumn == null)
            {
                issues.Add($"缺少列 {ActionTable}.{nameof(UserExperienceGovernanceAction.ExpectedVersion)}。");
            }

            if (resultVersionColumn == null)
            {
                issues.Add($"缺少列 {ActionTable}.{nameof(UserExperienceGovernanceAction.ResultVersion)}。");
            }

            if (expectedVersionColumn != null && resultVersionColumn != null)
            {
                var expectedVersionIdentifier = QuoteIdentifier(expectedVersionColumn.ColumnName);
                var resultVersionIdentifier = QuoteIdentifier(resultVersionColumn.ColumnName);
                var invalidActionCount = db.Ado.GetInt(
                    $"SELECT COUNT(*) FROM {QuoteIdentifier(actionTable)} " +
                    $"WHERE ({expectedVersionIdentifier} IS NULL AND {resultVersionIdentifier} IS NOT NULL) " +
                    $"OR ({expectedVersionIdentifier} IS NOT NULL AND {resultVersionIdentifier} IS NULL) " +
                    $"OR ({expectedVersionIdentifier} IS NOT NULL AND " +
                    $"({expectedVersionIdentifier} < 0 OR " +
                    $"{resultVersionIdentifier} <> {expectedVersionIdentifier} + 1))");
                if (invalidActionCount > 0)
                {
                    issues.Add($"发现 {invalidActionCount} 条版本快照无效的经验治理动作。");
                }
            }

            VerifyIndexes(db, actionTable, RequiredActionIndexes, issues);
        }

        var auditTable = DatabaseIdentifierResolver.ResolveTable(db, AuditTable);
        if (auditTable == null)
        {
            issues.Add($"缺少表 {AuditTable}。");
        }
        else
        {
            var auditColumns = new[]
            {
                nameof(ExperienceLevelRecalculationAudit.FormulaType),
                nameof(ExperienceLevelRecalculationAudit.FormulaSummary),
                nameof(ExperienceLevelRecalculationAudit.PreviewFingerprint),
                nameof(ExperienceLevelRecalculationAudit.ChangedLevelCount),
                nameof(ExperienceLevelRecalculationAudit.Reason)
            }.Select(column => DatabaseIdentifierResolver.ResolveColumn(db, auditTable, column)).ToList();
            if (auditColumns.Any(column => column == null))
            {
                issues.Add($"表 {AuditTable} 缺少必要审计列。");
            }
            else
            {
                var identifiers = auditColumns
                    .Select(column => QuoteIdentifier(column!.ColumnName))
                    .ToList();
                var invalidAuditCount = db.Ado.GetInt(
                    $"SELECT COUNT(*) FROM {QuoteIdentifier(auditTable)} " +
                    $"WHERE TRIM(COALESCE({identifiers[0]}, '')) = '' " +
                    $"OR TRIM(COALESCE({identifiers[1]}, '')) = '' " +
                    $"OR LENGTH(COALESCE({identifiers[2]}, '')) <> 64 " +
                    $"OR {identifiers[3]} <= 0 " +
                    $"OR TRIM(COALESCE({identifiers[4]}, '')) = ''");
                if (invalidAuditCount > 0)
                {
                    issues.Add($"发现 {invalidAuditCount} 条无效的经验等级重算审计记录。");
                }
            }

            VerifyIndexes(db, auditTable, RequiredAuditIndexes, issues);
        }

        return issues;
    }

    private static void VerifyIndexes(
        ISqlSugarClient db,
        string tableName,
        IEnumerable<string> indexNames,
        ICollection<string> issues)
    {
        foreach (var indexName in indexNames)
        {
            var exists = db.CurrentConnectionConfig.DbType == DbType.PostgreSQL
                ? db.DbMaintenance.GetIndexList(tableName)
                    .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase))
                : db.DbMaintenance.IsAnyIndex(indexName);
            if (!exists)
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }
    }

    private static void InitTable<TEntity>(ISqlSugarClient db, string configuredTableName)
    {
        var physicalTableName = DatabaseIdentifierResolver.ResolveTable(db, configuredTableName);
        if (physicalTableName == null)
        {
            db.CodeFirst.InitTables<TEntity>();
            return;
        }

        db.MappingTables.Add(typeof(TEntity).Name, physicalTableName);
        var moreSettings = db.CurrentConnectionConfig.MoreSettings ??= new ConnMoreSettings();
        var previousAutoLower = moreSettings.PgSqlIsAutoToLower;
        var previousAutoLowerCodeFirst = moreSettings.PgSqlIsAutoToLowerCodeFirst;
        var usesLowercaseIdentifiers = string.Equals(
            physicalTableName,
            physicalTableName.ToLowerInvariant(),
            StringComparison.Ordinal);
        moreSettings.PgSqlIsAutoToLower = usesLowercaseIdentifiers;
        moreSettings.PgSqlIsAutoToLowerCodeFirst = usesLowercaseIdentifiers;
        try
        {
            db.CodeFirst.InitTables<TEntity>();
        }
        finally
        {
            moreSettings.PgSqlIsAutoToLower = previousAutoLower;
            moreSettings.PgSqlIsAutoToLowerCodeFirst = previousAutoLowerCodeFirst;
        }
    }

    private static void PrepareLegacyPostgreSqlActionTable(ISqlSugarClient db)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var tableName = DatabaseIdentifierResolver.ResolveTable(db, ActionTable);
        if (tableName == null)
        {
            return;
        }

        var idColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            tableName,
            nameof(UserExperienceGovernanceAction.Id));
        var usesLowercaseIdentifiers = idColumn != null && string.Equals(
            idColumn.ColumnName,
            idColumn.ColumnName.ToLowerInvariant(),
            StringComparison.Ordinal);

        var requiredColumns = new (string Name, string Definition)[]
        {
            (nameof(UserExperienceGovernanceAction.TenantId), "bigint NOT NULL DEFAULT 0"),
            (nameof(UserExperienceGovernanceAction.TargetUserId), "bigint NOT NULL DEFAULT 0"),
            (nameof(UserExperienceGovernanceAction.ActionType), "integer NOT NULL DEFAULT 0"),
            (nameof(UserExperienceGovernanceAction.Remark), "character varying(500) NOT NULL DEFAULT ''"),
            (nameof(UserExperienceGovernanceAction.IsDeleted), "boolean NOT NULL DEFAULT false"),
            (nameof(UserExperienceGovernanceAction.CreateTime),
                "timestamp without time zone NOT NULL DEFAULT TIMESTAMP '1970-01-01 00:00:00'"),
            (nameof(UserExperienceGovernanceAction.CreateBy),
                "character varying(50) NOT NULL DEFAULT 'System'"),
            (nameof(UserExperienceGovernanceAction.CreateId), "bigint NOT NULL DEFAULT 0")
        };

        foreach (var column in requiredColumns)
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, tableName, column.Name) != null)
            {
                continue;
            }

            var physicalColumnName = usesLowercaseIdentifiers
                ? column.Name.ToLowerInvariant()
                : column.Name;
            db.Ado.ExecuteCommand(
                $"ALTER TABLE {QuoteIdentifier(tableName)} " +
                $"ADD COLUMN {QuoteIdentifier(physicalColumnName)} {column.Definition}");
            db.Ado.ExecuteCommand(
                $"ALTER TABLE {QuoteIdentifier(tableName)} " +
                $"ALTER COLUMN {QuoteIdentifier(physicalColumnName)} DROP DEFAULT");
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
