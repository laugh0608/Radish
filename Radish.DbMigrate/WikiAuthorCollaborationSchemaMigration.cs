using Radish.Model;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Wiki 普通作者、工作草稿、协作者与审核事件结构。</summary>
internal sealed class WikiAuthorCollaborationSchemaMigration : ISchemaMigration
{
    private const string WikiDocumentTable = "WikiDocument";
    private const string DraftTable = "WikiDocumentDraft";
    private const string CollaboratorTable = "WikiDocumentCollaborator";
    private const string ReviewEventTable = "WikiDocumentReviewEvent";

    public static WikiAuthorCollaborationSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260720_007_wiki_author_collaboration";

    public string Scope => "Main";

    public string Description => "建立 Wiki 普通作者草稿、协作者与审核事件结构";

    public string ChecksumSource =>
        "20260720_007_wiki_author_collaboration|Main|" +
        "WikiDocument(OwnerUserId,ActiveDraftId)-v1|" +
        "WikiDocumentDraft-v1|WikiDocumentCollaborator-v2|WikiDocumentReviewEvent-v1|" +
        "custom-owner-safe-backfill-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<WikiDocument>();
        db.CodeFirst.InitTables<WikiDocumentDraft>();
        db.CodeFirst.InitTables<WikiDocumentCollaborator>();
        db.CodeFirst.InitTables<WikiDocumentReviewEvent>();

        if (!db.DbMaintenance.IsAnyTable("User", false))
        {
            return;
        }

        var users = db.Queryable<User>()
            .Where(user => !user.IsDeleted)
            .Select(user => new { user.Id, user.TenantId })
            .ToList()
            .ToHashSet();
        var candidates = db.Queryable<WikiDocument>()
            .Where(document =>
                document.OwnerUserId == null &&
                document.CreateId > 0 &&
                (document.SourceType == "Custom" || document.SourceType == "ImportedSnapshot"))
            .Select(document => new { document.Id, document.TenantId, document.CreateId })
            .ToList();

        foreach (var candidate in candidates.Where(candidate => users.Contains(new { Id = candidate.CreateId, candidate.TenantId })))
        {
            db.Updateable<WikiDocument>()
                .SetColumns(document => document.OwnerUserId == candidate.CreateId)
                .Where(document => document.Id == candidate.Id && document.OwnerUserId == null)
                .ExecuteCommand();
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var table in new[] { WikiDocumentTable, DraftTable, CollaboratorTable, ReviewEventTable })
        {
            if (!db.DbMaintenance.IsAnyTable(table, false))
            {
                issues.Add($"缺少表 {table}。");
            }
        }

        if (!db.DbMaintenance.IsAnyTable(WikiDocumentTable, false))
        {
            return issues;
        }

        foreach (var propertyName in new[] { nameof(WikiDocument.OwnerUserId), nameof(WikiDocument.ActiveDraftId) })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, WikiDocumentTable, propertyName) == null)
            {
                issues.Add($"{WikiDocumentTable}.{propertyName} 列不存在。");
            }
        }

        foreach (var (table, index) in new[]
                 {
                     (WikiDocumentTable, "idx_wikidoc_owner_time"),
                     (WikiDocumentTable, "idx_wikidoc_active_draft"),
                     (DraftTable, "idx_wikidraft_document_state"),
                     (DraftTable, "idx_wikidraft_review_time"),
                     (CollaboratorTable, "idx_wikicollab_document_user"),
                     (CollaboratorTable, "idx_wikicollab_user_state"),
                     (ReviewEventTable, "idx_wikireview_document_time"),
                     (ReviewEventTable, "idx_wikireview_draft_time")
                 })
        {
            if (!IndexExists(db, table, index))
            {
                issues.Add($"缺少索引 {index}。");
            }
        }

        if (db.DbMaintenance.IsAnyTable("User", false))
        {
            var users = db.Queryable<User>()
                .Where(user => !user.IsDeleted)
                .Select(user => new { user.Id, user.TenantId })
                .ToList()
                .ToHashSet();
            var unresolvedSafeOwners = db.Queryable<WikiDocument>()
                .Where(document =>
                    document.OwnerUserId == null &&
                    document.CreateId > 0 &&
                    (document.SourceType == "Custom" || document.SourceType == "ImportedSnapshot"))
                .Select(document => new { document.TenantId, document.CreateId })
                .ToList()
                .Count(candidate => users.Contains(new { Id = candidate.CreateId, candidate.TenantId }));
            if (unresolvedSafeOwners > 0)
            {
                issues.Add($"仍有 {unresolvedSafeOwners} 篇可安全归属的历史 Wiki 文档未回填 OwnerUserId。");
            }
        }

        return issues;
    }

    private static bool IndexExists(ISqlSugarClient db, string tableName, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }
        return db.DbMaintenance.GetIndexList(tableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }
}
