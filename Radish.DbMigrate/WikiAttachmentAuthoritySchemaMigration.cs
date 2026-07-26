using Radish.Common;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立 Wiki 附件权威引用、私有默认与历史兼容迁移。</summary>
internal sealed class WikiAttachmentAuthoritySchemaMigration : ISchemaMigration
{
    private const string ReferenceTable = "WikiAttachmentReference";
    private static readonly string[] RequiredReferenceColumns =
    [
        nameof(WikiAttachmentReference.Id),
        nameof(WikiAttachmentReference.TenantId),
        nameof(WikiAttachmentReference.DocumentId),
        nameof(WikiAttachmentReference.AttachmentId),
        nameof(WikiAttachmentReference.ReferenceKind),
        nameof(WikiAttachmentReference.ReferenceSourceId),
        nameof(WikiAttachmentReference.IsDeleted),
        nameof(WikiAttachmentReference.DeletedAt),
        nameof(WikiAttachmentReference.DeletedBy),
        nameof(WikiAttachmentReference.CreateTime),
        nameof(WikiAttachmentReference.CreateBy),
        nameof(WikiAttachmentReference.CreateId),
        nameof(WikiAttachmentReference.ModifyTime),
        nameof(WikiAttachmentReference.ModifyBy),
        nameof(WikiAttachmentReference.ModifyId)
    ];

    public static WikiAttachmentAuthoritySchemaMigration Instance { get; } = new();

    public string MigrationId => "20260725_012_wiki_attachment_authority";

    public string Scope => "Main";

    public string Description => "建立 Wiki 附件权威引用、私有默认与历史兼容迁移";

    public string ChecksumSource =>
        "20260725_012_wiki_attachment_authority|Main|" +
        "WikiAttachmentReference-v1|" +
        "document-draft-revision-backfill-v2-renderable-markdown-source-convergent|" +
        "wiki-private-and-proven-document-compat-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable(ReferenceTable, false))
        {
            db.CodeFirst.InitTables<WikiAttachmentReference>();
        }

        if (!HasSourceTables(db))
        {
            return;
        }

        var sourceRelationshipIssues = InspectSourceRelationships(db);
        if (sourceRelationshipIssues.Count > 0)
        {
            throw new InvalidOperationException(sourceRelationshipIssues[0]);
        }

        var attachments = db.Queryable<Attachment>().ToList().ToDictionary(attachment => attachment.Id);
        var provenLegacyDocumentIds = new HashSet<long>();
        foreach (var source in EnumerateSources(db))
        {
            var validAttachmentIds = new HashSet<long>();
            foreach (var attachmentId in source.AttachmentIds)
            {
                if (!attachments.TryGetValue(attachmentId, out var attachment))
                {
                    continue;
                }

                if (attachment.TenantId != source.TenantId)
                {
                    throw new InvalidOperationException(
                        $"Wiki 附件 {attachmentId} 与来源 {source.ReferenceSourceId} 跨租户，迁移已阻断。");
                }
                if (!string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Wiki,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Document,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用了业务类型 {attachment.BusinessType} 的附件 {attachmentId}，迁移已阻断。");
                }

                validAttachmentIds.Add(attachmentId);
                if (string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Document,
                        StringComparison.Ordinal))
                {
                    provenLegacyDocumentIds.Add(attachmentId);
                }
            }

            SyncSourceReferences(db, source, validAttachmentIds);
        }

        db.Updateable<Attachment>()
            .SetColumns(attachment => attachment.IsPublic == false)
            .SetColumns(attachment => attachment.ModifyTime == DateTime.UtcNow)
            .SetColumns(attachment => attachment.ModifyBy == "Migration")
            .Where(attachment =>
                attachment.BusinessType == AttachmentBusinessTypes.Wiki &&
                attachment.IsPublic)
            .ExecuteCommand();

        if (provenLegacyDocumentIds.Count > 0)
        {
            var ids = provenLegacyDocumentIds.ToList();
            db.Updateable<Attachment>()
                .SetColumns(attachment => attachment.IsPublic == false)
                .SetColumns(attachment => attachment.ModifyTime == DateTime.UtcNow)
                .SetColumns(attachment => attachment.ModifyBy == "Migration")
                .Where(attachment => ids.Contains(attachment.Id) && attachment.IsPublic)
                .ExecuteCommand();
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        if (!db.DbMaintenance.IsAnyTable(ReferenceTable, false))
        {
            issues.Add($"缺少表 {ReferenceTable}。");
            return issues;
        }

        var missingReferenceColumn = false;
        foreach (var column in RequiredReferenceColumns)
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, ReferenceTable, column) == null)
            {
                issues.Add($"缺少列 {ReferenceTable}.{column}。");
                missingReferenceColumn = true;
            }
        }
        if (missingReferenceColumn)
        {
            return issues;
        }

        foreach (var indexName in new[]
                 {
                     "idx_wikiattachment_source_attachment",
                     "idx_wikiattachment_attachment_active",
                     "idx_wikiattachment_document_kind_active"
                 })
        {
            if (!IndexExists(db, ReferenceTable, indexName))
            {
                issues.Add($"缺少索引 {indexName}。");
            }
        }

        if (!HasSourceTables(db))
        {
            return issues;
        }

        issues.AddRange(InspectSourceRelationships(db));
        var attachments = db.Queryable<Attachment>().ToList().ToDictionary(attachment => attachment.Id);
        var references = db.Queryable<WikiAttachmentReference>().ToList();
        var activeKeys = references
            .Where(reference => !reference.IsDeleted)
            .Select(reference => new ReferenceKey(
                reference.TenantId,
                reference.ReferenceKind,
                reference.ReferenceSourceId,
                reference.AttachmentId))
            .ToHashSet();

        var publicWikiCount = attachments.Values.Count(attachment =>
            string.Equals(
                attachment.BusinessType,
                AttachmentBusinessTypes.Wiki,
                StringComparison.Ordinal) &&
            attachment.IsPublic);
        if (publicWikiCount > 0)
        {
            issues.Add($"仍有 {publicWikiCount} 个 Wiki 附件处于公开状态。");
        }

        foreach (var source in EnumerateSources(db))
        {
            foreach (var legacyUrl in source.LegacyUrls)
            {
                issues.Add(
                    $"Wiki 来源 {source.ReferenceSourceId} 仍包含未迁移的站内旧附件 URL：{legacyUrl}。");
            }

            foreach (var attachmentId in source.AttachmentIds)
            {
                if (!attachments.TryGetValue(attachmentId, out var attachment))
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用的附件 {attachmentId} 不存在。");
                    continue;
                }

                if (attachment.TenantId != source.TenantId)
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 与附件 {attachmentId} 跨租户。");
                }
                if (attachment.IsDeleted || !attachment.IsEnabled)
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用的附件 {attachmentId} 已删除或停用。");
                }
                if (!string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Wiki,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Document,
                        StringComparison.Ordinal))
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用了错误业务类型附件 {attachmentId}。");
                }
                if (attachment.IsPublic)
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用的附件 {attachmentId} 仍公开。");
                }
                if (!activeKeys.Contains(new ReferenceKey(
                        source.TenantId,
                        source.ReferenceKind,
                        source.ReferenceSourceId,
                        attachmentId)))
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 缺少附件 {attachmentId} 的权威引用。");
                }
            }
        }

        var duplicateCount = references
            .GroupBy(reference => new ReferenceKey(
                reference.TenantId,
                reference.ReferenceKind,
                reference.ReferenceSourceId,
                reference.AttachmentId))
            .Count(group => group.Count() > 1);
        if (duplicateCount > 0)
        {
            issues.Add($"存在 {duplicateCount} 组重复 Wiki 附件引用。");
        }

        foreach (var reference in references.Where(reference => !reference.IsDeleted))
        {
            if (!attachments.TryGetValue(reference.AttachmentId, out var attachment))
            {
                issues.Add($"权威引用 {reference.Id} 指向不存在的附件 {reference.AttachmentId}。");
                continue;
            }
            if (attachment.TenantId != reference.TenantId)
            {
                issues.Add($"权威引用 {reference.Id} 与附件 {reference.AttachmentId} 跨租户。");
            }
        }

        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!HasSourceTables(db))
        {
            return [];
        }

        var issues = new List<string>();
        issues.AddRange(InspectSourceRelationships(db));
        var attachments = db.Queryable<Attachment>().ToList().ToDictionary(attachment => attachment.Id);
        foreach (var source in EnumerateSources(db))
        {
            foreach (var legacyUrl in source.LegacyUrls)
            {
                issues.Add(
                    $"Wiki 来源 {source.ReferenceSourceId} 仍包含未迁移的站内旧附件 URL：{legacyUrl}。");
            }

            foreach (var attachmentId in source.AttachmentIds)
            {
                if (!attachments.TryGetValue(attachmentId, out var attachment))
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用的附件 {attachmentId} 不存在。");
                    continue;
                }
                if (attachment.TenantId != source.TenantId)
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 与附件 {attachmentId} 跨租户。");
                }
                if (attachment.IsDeleted || !attachment.IsEnabled)
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用的附件 {attachmentId} 已删除或停用。");
                }
                if (!string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Wiki,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.Document,
                        StringComparison.Ordinal))
                {
                    issues.Add(
                        $"Wiki 来源 {source.ReferenceSourceId} 引用了业务类型 {attachment.BusinessType} 的附件 {attachmentId}。");
                }
            }
        }
        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<SourceReferenceSet> EnumerateSources(ISqlSugarClient db)
    {
        foreach (var document in db.Queryable<WikiDocument>().ToList())
        {
            yield return new SourceReferenceSet(
                document.TenantId,
                document.Id,
                (int)WikiAttachmentReferenceKind.DocumentContent,
                document.Id,
                AttachmentReferenceParser.ExtractAttachmentIds(document.MarkdownContent),
                AttachmentReferenceParser.ExtractLegacyAttachmentUrls(document.MarkdownContent));
            yield return new SourceReferenceSet(
                document.TenantId,
                document.Id,
                (int)WikiAttachmentReferenceKind.DocumentCover,
                document.Id,
                document.CoverAttachmentId is > 0 ? [document.CoverAttachmentId.Value] : [],
                []);
        }

        foreach (var draft in db.Queryable<WikiDocumentDraft>().ToList())
        {
            yield return new SourceReferenceSet(
                draft.TenantId,
                draft.DocumentId,
                (int)WikiAttachmentReferenceKind.DraftContent,
                draft.Id,
                AttachmentReferenceParser.ExtractAttachmentIds(draft.MarkdownContent),
                AttachmentReferenceParser.ExtractLegacyAttachmentUrls(draft.MarkdownContent));
            yield return new SourceReferenceSet(
                draft.TenantId,
                draft.DocumentId,
                (int)WikiAttachmentReferenceKind.DraftCover,
                draft.Id,
                draft.CoverAttachmentId is > 0 ? [draft.CoverAttachmentId.Value] : [],
                []);
        }

        foreach (var revision in db.Queryable<WikiDocumentRevision>().ToList())
        {
            yield return new SourceReferenceSet(
                revision.TenantId,
                revision.DocumentId,
                (int)WikiAttachmentReferenceKind.RevisionContent,
                revision.Id,
                AttachmentReferenceParser.ExtractAttachmentIds(revision.MarkdownContent),
                AttachmentReferenceParser.ExtractLegacyAttachmentUrls(revision.MarkdownContent));
        }
    }

    private static void SyncSourceReferences(
        ISqlSugarClient db,
        SourceReferenceSet source,
        IReadOnlySet<long> targetAttachmentIds)
    {
        var existingReferences = db.Queryable<WikiAttachmentReference>()
            .Where(reference =>
                reference.TenantId == source.TenantId &&
                reference.ReferenceKind == source.ReferenceKind &&
                reference.ReferenceSourceId == source.ReferenceSourceId)
            .ToList();
        var staleIds = existingReferences
            .Where(reference =>
                !reference.IsDeleted &&
                !targetAttachmentIds.Contains(reference.AttachmentId))
            .Select(reference => reference.Id)
            .ToList();
        if (staleIds.Count > 0)
        {
            db.Updateable<WikiAttachmentReference>()
                .SetColumns(reference => new WikiAttachmentReference
                {
                    IsDeleted = true,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = "Migration",
                    ModifyTime = DateTime.UtcNow,
                    ModifyBy = "Migration"
                })
                .Where(reference => staleIds.Contains(reference.Id) && !reference.IsDeleted)
                .ExecuteCommand();
        }

        foreach (var attachmentId in targetAttachmentIds)
        {
            var existing = existingReferences.FirstOrDefault(
                reference => reference.AttachmentId == attachmentId);
            if (existing == null)
            {
                db.Insertable(new WikiAttachmentReference
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = source.TenantId,
                    DocumentId = source.DocumentId,
                    AttachmentId = attachmentId,
                    ReferenceKind = source.ReferenceKind,
                    ReferenceSourceId = source.ReferenceSourceId,
                    CreateTime = DateTime.UtcNow,
                    CreateBy = "Migration"
                }).ExecuteCommand();
                continue;
            }

            if (!existing.IsDeleted && existing.DocumentId == source.DocumentId)
            {
                continue;
            }

            db.Updateable<WikiAttachmentReference>()
                .SetColumns(reference => new WikiAttachmentReference
                {
                    DocumentId = source.DocumentId,
                    IsDeleted = false,
                    DeletedAt = null,
                    DeletedBy = null,
                    ModifyTime = DateTime.UtcNow,
                    ModifyBy = "Migration"
                })
                .Where(reference => reference.Id == existing.Id)
                .ExecuteCommand();
        }
    }

    private static List<string> InspectSourceRelationships(ISqlSugarClient db)
    {
        var issues = new List<string>();
        var documents = db.Queryable<WikiDocument>()
            .ToList()
            .ToDictionary(document => document.Id);
        foreach (var draft in db.Queryable<WikiDocumentDraft>().ToList())
        {
            if (!documents.TryGetValue(draft.DocumentId, out var document))
            {
                issues.Add($"Wiki 草稿 {draft.Id} 指向不存在的文档 {draft.DocumentId}。");
                continue;
            }

            if (draft.TenantId != document.TenantId)
            {
                issues.Add($"Wiki 草稿 {draft.Id} 与文档 {draft.DocumentId} 跨租户。");
            }
        }

        foreach (var revision in db.Queryable<WikiDocumentRevision>().ToList())
        {
            if (!documents.TryGetValue(revision.DocumentId, out var document))
            {
                issues.Add($"Wiki Revision {revision.Id} 指向不存在的文档 {revision.DocumentId}。");
                continue;
            }

            if (revision.TenantId != document.TenantId)
            {
                issues.Add($"Wiki Revision {revision.Id} 与文档 {revision.DocumentId} 跨租户。");
            }
        }

        return issues;
    }

    private static bool HasSourceTables(ISqlSugarClient db)
    {
        return db.DbMaintenance.IsAnyTable("Attachment", false) &&
               db.DbMaintenance.IsAnyTable("WikiDocument", false) &&
               db.DbMaintenance.IsAnyTable("WikiDocumentDraft", false) &&
               db.DbMaintenance.IsAnyTable("WikiDocumentRevision", false);
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

    private sealed record SourceReferenceSet(
        long TenantId,
        long DocumentId,
        int ReferenceKind,
        long ReferenceSourceId,
        IReadOnlyCollection<long> AttachmentIds,
        IReadOnlyCollection<string> LegacyUrls);

    private sealed record ReferenceKey(
        long TenantId,
        int ReferenceKind,
        long ReferenceSourceId,
        long AttachmentId);
}
