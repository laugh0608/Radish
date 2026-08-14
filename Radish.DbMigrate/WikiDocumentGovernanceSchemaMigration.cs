using Radish.Model;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Wiki 文档独立治理版本与追加式治理事件。</summary>
internal sealed class WikiDocumentGovernanceSchemaMigration : ISchemaMigration
{
    private const string DocumentTable = "WikiDocument";
    private const string EventTable = "WikiDocumentGovernanceEvent";

    private static readonly string[] RequiredIndexes =
    [
        "idx_wikigovernance_document_version",
        "idx_wikigovernance_document_time"
    ];

    public static WikiDocumentGovernanceSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260813_021_wiki_document_governance";

    public string Scope => "Main";

    public string Description => "建立 Wiki 文档独立治理版本与追加式治理事件";

    public string ChecksumSource =>
        "20260813_021_wiki_document_governance|Main|" +
        "WikiDocument.GovernanceVersion-zero-default-v1|" +
        "WikiDocumentGovernanceEvent-append-only-snapshot-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<WikiDocument, WikiDocumentGovernanceEvent>();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(DocumentTable, false) ||
            DatabaseIdentifierResolver.ResolveColumn(db, DocumentTable, nameof(WikiDocument.GovernanceVersion)) != null)
        {
            return [];
        }

        var count = db.Queryable<WikiDocument>().Count();
        return count > 0
            ? [$"发现 {count} 篇历史 Wiki 文档；迁移将以 GovernanceVersion 0 初始化，不改变正文版本、审核状态、发布状态或访问策略。"]
            : [];
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(DocumentTable, false))
        {
            issues.Add($"缺少表 {DocumentTable}。");
            return issues;
        }

        var governanceVersionColumn = DatabaseIdentifierResolver.ResolveColumn(
            db,
            DocumentTable,
            nameof(WikiDocument.GovernanceVersion));
        if (governanceVersionColumn == null)
        {
            issues.Add($"缺少列 {DocumentTable}.{nameof(WikiDocument.GovernanceVersion)}。");
        }
        else
        {
            var invalidDocumentCount = db.Queryable<WikiDocument>()
                .Where(document => document.GovernanceVersion < 0)
                .Count();
            if (invalidDocumentCount > 0)
            {
                issues.Add($"发现 {invalidDocumentCount} 篇治理版本无效的 Wiki 文档。");
            }
        }

        if (!db.DbMaintenance.IsAnyTable(EventTable, false))
        {
            issues.Add($"缺少表 {EventTable}。");
            return issues;
        }

        var events = db.Queryable<WikiDocumentGovernanceEvent>().ToList();
        var invalidEventCount = events.Count(governanceEvent =>
            !WikiDocumentGovernanceActions.All.Contains(governanceEvent.Action) ||
            governanceEvent.ExpectedGovernanceVersion < 0 ||
            governanceEvent.ResultGovernanceVersion != governanceEvent.ExpectedGovernanceVersion + 1 ||
            governanceEvent.FromDocumentVersion < 1 ||
            governanceEvent.ToDocumentVersion < 1 ||
            string.IsNullOrWhiteSpace(governanceEvent.Reason));
        if (invalidEventCount > 0)
        {
            issues.Add($"发现 {invalidEventCount} 条无效的 Wiki 文档治理事件。");
        }

        foreach (var indexName in RequiredIndexes)
        {
            if (!IndexExists(db, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        return issues;
    }

    private static bool IndexExists(ISqlSugarClient db, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        return db.DbMaintenance.GetIndexList(EventTable)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }
}
