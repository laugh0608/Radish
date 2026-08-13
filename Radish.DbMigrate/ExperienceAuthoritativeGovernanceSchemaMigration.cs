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
        db.CodeFirst.InitTables<UserExperienceGovernanceAction, ExperienceLevelRecalculationAudit>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ActionTable, false))
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

        var count = db.Ado.GetInt($"SELECT COUNT(*) FROM \"{ActionTable}\"");
        return count > 0
            ? [$"发现 {count} 条历史经验治理动作；迁移将保留动作事实，并以空版本快照标记为迁移前记录。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(ActionTable, false))
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
                var invalidActionCount = db.Queryable<UserExperienceGovernanceAction>()
                    .ToList()
                    .Count(action =>
                        action.ExpectedVersion.HasValue != action.ResultVersion.HasValue ||
                        action.ExpectedVersion.HasValue &&
                        (action.ExpectedVersion.Value < 0 ||
                         action.ResultVersion != action.ExpectedVersion.Value + 1));
                if (invalidActionCount > 0)
                {
                    issues.Add($"发现 {invalidActionCount} 条版本快照无效的经验治理动作。");
                }
            }

            VerifyIndexes(db, ActionTable, RequiredActionIndexes, issues);
        }

        if (!db.DbMaintenance.IsAnyTable(AuditTable, false))
        {
            issues.Add($"缺少表 {AuditTable}。");
        }
        else
        {
            var invalidAuditCount = db.Queryable<ExperienceLevelRecalculationAudit>()
                .ToList()
                .Count(audit =>
                    string.IsNullOrWhiteSpace(audit.FormulaType) ||
                    string.IsNullOrWhiteSpace(audit.FormulaSummary) ||
                    audit.PreviewFingerprint.Length != 64 ||
                    audit.ChangedLevelCount <= 0 ||
                    string.IsNullOrWhiteSpace(audit.Reason));
            if (invalidAuditCount > 0)
            {
                issues.Add($"发现 {invalidAuditCount} 条无效的经验等级重算审计记录。");
            }

            VerifyIndexes(db, AuditTable, RequiredAuditIndexes, issues);
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
}
