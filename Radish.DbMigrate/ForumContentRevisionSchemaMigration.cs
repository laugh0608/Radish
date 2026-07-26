using Radish.Common;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立论坛帖子与评论的完整内容版本真相。</summary>
internal sealed class ForumContentRevisionSchemaMigration : ISchemaMigration
{
    private const string PostRevisionTable = "PostContentRevision";
    private const string PostRevisionTagTable = "PostContentRevisionTag";
    private const string CommentRevisionTable = "CommentContentRevision";
    private const string RevisionAttachmentTable = "ForumContentRevisionAttachment";

    public static ForumContentRevisionSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260726_013_forum_content_revision";

    public string Scope => "Main";

    public string Description => "建立论坛内容 Revision、当前版本 CAS 与历史基线";

    public string ChecksumSource =>
        "20260726_013_forum_content_revision|Main|" +
        "Post.ContentRevision-v1|Comment.ContentRevision-v1|" +
        "PostContentRevision-v1|PostContentRevisionTag-v1|" +
        "CommentContentRevision-v1|ForumContentRevisionAttachment-v1|" +
        "current-state-baseline-v1|legacy-incomplete-explicit-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<Post>();
        db.CodeFirst.InitTables<Comment>();
        db.CodeFirst.InitTables<PostContentRevision>();
        db.CodeFirst.InitTables<PostContentRevisionTag>();
        db.CodeFirst.InitTables<CommentContentRevision>();
        db.CodeFirst.InitTables<ForumContentRevisionAttachment>();

        if (!HasSourceTables(db))
        {
            return;
        }

        db.Updateable<Post>()
            .SetColumns(post => post.ContentRevision == 1)
            .Where(post => post.ContentRevision <= 0)
            .ExecuteCommand();
        db.Updateable<Comment>()
            .SetColumns(comment => comment.ContentRevision == 1)
            .Where(comment => comment.ContentRevision <= 0)
            .ExecuteCommand();

        var attachments = db.Queryable<Attachment>().ToList().ToDictionary(item => item.Id);
        var categories = db.Queryable<Category>().ToList().ToDictionary(item => item.Id);
        var tags = db.Queryable<Tag>().ToList().ToDictionary(item => item.Id);
        var postTags = db.Queryable<PostTag>()
            .ToList()
            .GroupBy(item => item.PostId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.TagId).Distinct().ToList());

        foreach (var post in db.Queryable<Post>().ToList())
        {
            EnsurePostBaseline(db, post, categories, tags, postTags, attachments);
        }

        foreach (var comment in db.Queryable<Comment>().ToList())
        {
            EnsureCommentBaseline(db, comment, attachments);
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var table in new[]
                 {
                     PostRevisionTable,
                     PostRevisionTagTable,
                     CommentRevisionTable,
                     RevisionAttachmentTable
                 })
        {
            if (!db.DbMaintenance.IsAnyTable(table, false))
            {
                issues.Add($"缺少表 {table}。");
            }
        }

        if (issues.Count > 0 || !HasSourceTables(db))
        {
            return issues;
        }

        if (DatabaseIdentifierResolver.ResolveColumn(db, nameof(Post), nameof(Post.ContentRevision)) == null)
        {
            issues.Add($"缺少列 {nameof(Post)}.{nameof(Post.ContentRevision)}。");
        }
        if (DatabaseIdentifierResolver.ResolveColumn(db, nameof(Comment), nameof(Comment.ContentRevision)) == null)
        {
            issues.Add($"缺少列 {nameof(Comment)}.{nameof(Comment.ContentRevision)}。");
        }
        if (issues.Count > 0)
        {
            return issues;
        }

        foreach (var (table, index) in new[]
                 {
                     (PostRevisionTable, "idx_postcontentrevision_tenant_post_revision"),
                     (PostRevisionTable, "idx_postcontentrevision_tenant_restore_source"),
                     (PostRevisionTagTable, "idx_postcontentrevisiontag_tenant_revision_tag"),
                     (PostRevisionTagTable, "idx_postcontentrevisiontag_tenant_revision_sort"),
                     (CommentRevisionTable, "idx_commentcontentrevision_tenant_comment_revision"),
                     (CommentRevisionTable, "idx_commentcontentrevision_tenant_restore_source"),
                     (RevisionAttachmentTable, "idx_forumrevisionattachment_revision_attachment"),
                     (RevisionAttachmentTable, "idx_forumrevisionattachment_attachment"),
                     (RevisionAttachmentTable, "idx_forumrevisionattachment_target")
                 })
        {
            if (!IndexExists(db, table, index))
            {
                issues.Add($"缺少索引 {index}。");
            }
        }

        var posts = db.Queryable<Post>().ToList().ToDictionary(item => item.Id);
        var comments = db.Queryable<Comment>().ToList().ToDictionary(item => item.Id);
        var postRevisions = db.Queryable<PostContentRevision>().ToList();
        var commentRevisions = db.Queryable<CommentContentRevision>().ToList();
        var postRevisionMap = postRevisions.ToDictionary(item => item.Id);
        var commentRevisionMap = commentRevisions.ToDictionary(item => item.Id);

        foreach (var post in posts.Values)
        {
            if (post.ContentRevision <= 0)
            {
                issues.Add($"Post {post.Id} 的 ContentRevision 非法。");
                continue;
            }

            var current = postRevisions.FirstOrDefault(item =>
                item.PostId == post.Id &&
                item.TenantId == post.TenantId &&
                item.RevisionNumber == post.ContentRevision);
            if (current == null)
            {
                issues.Add($"Post {post.Id} 缺少当前 Revision {post.ContentRevision}。");
            }
        }

        foreach (var comment in comments.Values)
        {
            if (comment.ContentRevision <= 0)
            {
                issues.Add($"Comment {comment.Id} 的 ContentRevision 非法。");
                continue;
            }

            var current = commentRevisions.FirstOrDefault(item =>
                item.CommentId == comment.Id &&
                item.TenantId == comment.TenantId &&
                item.RevisionNumber == comment.ContentRevision);
            if (current == null)
            {
                issues.Add($"Comment {comment.Id} 缺少当前 Revision {comment.ContentRevision}。");
            }
        }

        foreach (var revision in postRevisions)
        {
            if (!posts.TryGetValue(revision.PostId, out var post) || post.TenantId != revision.TenantId)
            {
                issues.Add($"Post Revision {revision.Id} 指向不存在或跨租户的 Post {revision.PostId}。");
            }
            ValidateRestoreSource(
                issues,
                revision.Id,
                revision.SourceType,
                revision.RestoredFromRevisionId,
                revision.PostId,
                revision.TenantId,
                postRevisionMap,
                static item => item.PostId,
                static item => item.TenantId);
        }

        foreach (var revision in commentRevisions)
        {
            if (!comments.TryGetValue(revision.CommentId, out var comment) || comment.TenantId != revision.TenantId)
            {
                issues.Add($"Comment Revision {revision.Id} 指向不存在或跨租户的 Comment {revision.CommentId}。");
            }
            ValidateRestoreSource(
                issues,
                revision.Id,
                revision.SourceType,
                revision.RestoredFromRevisionId,
                revision.CommentId,
                revision.TenantId,
                commentRevisionMap,
                static item => item.CommentId,
                static item => item.TenantId);
        }

        VerifyRevisionRelations(
            db,
            issues,
            posts,
            comments,
            postRevisionMap,
            commentRevisionMap);

        return issues;
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!HasSourceTables(db))
        {
            return [];
        }

        var issues = new List<string>();
        var duplicatePostTags = db.Queryable<PostTag>()
            .ToList()
            .GroupBy(item => new { item.PostId, item.TagId })
            .Where(group => group.Count() > 1)
            .Take(20)
            .ToList();
        foreach (var duplicate in duplicatePostTags)
        {
            issues.Add($"PostTag 存在重复关系 Post={duplicate.Key.PostId}, Tag={duplicate.Key.TagId}。");
        }

        return issues;
    }

    private static void EnsurePostBaseline(
        ISqlSugarClient db,
        Post post,
        IReadOnlyDictionary<long, Category> categories,
        IReadOnlyDictionary<long, Tag> tags,
        IReadOnlyDictionary<long, List<long>> postTags,
        IReadOnlyDictionary<long, Attachment> attachments)
    {
        var existing = db.Queryable<PostContentRevision>()
            .Where(item =>
                item.TenantId == post.TenantId &&
                item.PostId == post.Id &&
                item.RevisionNumber == post.ContentRevision)
            .First();
        if (existing != null)
        {
            return;
        }

        var integrityStatus = ForumContentRevisionIntegrityStatuses.Complete;
        categories.TryGetValue(post.CategoryId, out var category);
        if (category == null || category.IsDeleted || !category.IsEnabled)
        {
            integrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete;
        }

        var tagSnapshots = new List<Tag>();
        foreach (var tagId in postTags.GetValueOrDefault(post.Id) ?? [])
        {
            if (!tags.TryGetValue(tagId, out var tag) || tag.IsDeleted || !tag.IsEnabled)
            {
                integrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete;
                continue;
            }
            tagSnapshots.Add(tag);
        }
        if (tagSnapshots.Count == 0)
        {
            integrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete;
        }

        var contentAttachmentIds = AttachmentReferenceParser.ExtractAttachmentIds(post.Content);
        if (AttachmentReferenceParser.ExtractLegacyAttachmentUrls(post.Content).Count > 0)
        {
            integrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete;
        }

        var validContentAttachmentIds = ValidateAttachments(
            contentAttachmentIds,
            post.TenantId,
            ForumContentRevisionTargetTypes.Post,
            post.Id,
            attachments,
            ref integrityStatus);
        var validCoverAttachmentIds = post.CoverAttachmentId.HasValue
            ? ValidateAttachments(
                [post.CoverAttachmentId.Value],
                post.TenantId,
                ForumContentRevisionTargetTypes.Post,
                post.Id,
                attachments,
                ref integrityStatus)
            : [];

        var revisionId = SnowFlakeSingle.Instance.NextId();
        var editorId = post.ModifyId.GetValueOrDefault(post.AuthorId);
        var editorName = string.IsNullOrWhiteSpace(post.ModifyBy) ? post.AuthorName : post.ModifyBy;
        var revision = new PostContentRevision
        {
            Id = revisionId,
            TenantId = post.TenantId,
            PostId = post.Id,
            RevisionNumber = post.ContentRevision,
            SourceType = ForumContentRevisionSourceTypes.Baseline,
            IntegrityStatus = integrityStatus,
            Title = post.Title,
            Content = post.Content,
            ContentType = post.ContentType,
            CategoryId = post.CategoryId,
            CategoryNameSnapshot = category?.Name ?? string.Empty,
            CoverAttachmentId = post.CoverAttachmentId,
            EditorId = editorId,
            EditorName = editorName ?? "Migration",
            CreateTime = post.ModifyTime ?? post.CreateTime,
            CreateBy = "ForumRevisionMigration",
            CreateId = editorId
        };
        db.Insertable(revision).ExecuteCommand();

        var orderedTags = tagSnapshots
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id)
            .Select((tag, index) => new PostContentRevisionTag
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = post.TenantId,
                RevisionId = revisionId,
                TagId = tag.Id,
                TagNameSnapshot = tag.Name,
                SortOrder = index,
                CreateTime = revision.CreateTime,
                CreateBy = "ForumRevisionMigration",
                CreateId = editorId
            })
            .ToList();
        if (orderedTags.Count > 0)
        {
            db.Insertable(orderedTags).ExecuteCommand();
        }

        InsertAttachmentReferences(
            db,
            post.TenantId,
            ForumContentRevisionTargetTypes.Post,
            post.Id,
            revisionId,
            validContentAttachmentIds,
            validCoverAttachmentIds,
            editorId,
            revision.CreateTime);
    }

    private static void EnsureCommentBaseline(
        ISqlSugarClient db,
        Comment comment,
        IReadOnlyDictionary<long, Attachment> attachments)
    {
        var existing = db.Queryable<CommentContentRevision>()
            .Where(item =>
                item.TenantId == comment.TenantId &&
                item.CommentId == comment.Id &&
                item.RevisionNumber == comment.ContentRevision)
            .First();
        if (existing != null)
        {
            return;
        }

        var integrityStatus = ForumContentRevisionIntegrityStatuses.Complete;
        var contentAttachmentIds = AttachmentReferenceParser.ExtractAttachmentIds(comment.Content);
        if (AttachmentReferenceParser.ExtractLegacyAttachmentUrls(comment.Content).Count > 0)
        {
            integrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete;
        }
        var validContentAttachmentIds = ValidateAttachments(
            contentAttachmentIds,
            comment.TenantId,
            ForumContentRevisionTargetTypes.Comment,
            comment.Id,
            attachments,
            ref integrityStatus);

        var revisionId = SnowFlakeSingle.Instance.NextId();
        var editorId = comment.ModifyId.GetValueOrDefault(comment.AuthorId);
        var editorName = string.IsNullOrWhiteSpace(comment.ModifyBy) ? comment.AuthorName : comment.ModifyBy;
        var revision = new CommentContentRevision
        {
            Id = revisionId,
            TenantId = comment.TenantId,
            CommentId = comment.Id,
            PostId = comment.PostId,
            RevisionNumber = comment.ContentRevision,
            SourceType = ForumContentRevisionSourceTypes.Baseline,
            IntegrityStatus = integrityStatus,
            Content = comment.Content,
            EditorId = editorId,
            EditorName = editorName ?? "Migration",
            CreateTime = comment.ModifyTime ?? comment.CreateTime,
            CreateBy = "ForumRevisionMigration",
            CreateId = editorId
        };
        db.Insertable(revision).ExecuteCommand();

        InsertAttachmentReferences(
            db,
            comment.TenantId,
            ForumContentRevisionTargetTypes.Comment,
            comment.Id,
            revisionId,
            validContentAttachmentIds,
            [],
            editorId,
            revision.CreateTime);
    }

    private static List<long> ValidateAttachments(
        IEnumerable<long> attachmentIds,
        long tenantId,
        string targetType,
        long targetId,
        IReadOnlyDictionary<long, Attachment> attachments,
        ref string integrityStatus)
    {
        var validIds = new List<long>();
        foreach (var attachmentId in attachmentIds.Distinct())
        {
            if (!attachments.TryGetValue(attachmentId, out var attachment) ||
                attachment.TenantId != tenantId ||
                attachment.IsDeleted ||
                !attachment.IsEnabled ||
                !string.Equals(attachment.BusinessType, targetType, StringComparison.Ordinal) ||
                attachment.BusinessId != targetId)
            {
                integrityStatus = ForumContentRevisionIntegrityStatuses.LegacyIncomplete;
                continue;
            }
            validIds.Add(attachmentId);
        }
        return validIds;
    }

    private static void InsertAttachmentReferences(
        ISqlSugarClient db,
        long tenantId,
        string targetType,
        long targetId,
        long revisionId,
        IReadOnlyCollection<long> contentAttachmentIds,
        IReadOnlyCollection<long> coverAttachmentIds,
        long editorId,
        DateTime createTime)
    {
        var references = contentAttachmentIds
            .Select(attachmentId => BuildAttachmentReference(
                tenantId,
                targetType,
                targetId,
                revisionId,
                attachmentId,
                ForumContentRevisionReferenceKinds.Content,
                editorId,
                createTime))
            .Concat(coverAttachmentIds.Select(attachmentId => BuildAttachmentReference(
                tenantId,
                targetType,
                targetId,
                revisionId,
                attachmentId,
                ForumContentRevisionReferenceKinds.Cover,
                editorId,
                createTime)))
            .ToList();
        if (references.Count > 0)
        {
            db.Insertable(references).ExecuteCommand();
        }
    }

    private static ForumContentRevisionAttachment BuildAttachmentReference(
        long tenantId,
        string targetType,
        long targetId,
        long revisionId,
        long attachmentId,
        string referenceKind,
        long editorId,
        DateTime createTime)
    {
        return new ForumContentRevisionAttachment
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = tenantId,
            TargetType = targetType,
            TargetId = targetId,
            RevisionId = revisionId,
            AttachmentId = attachmentId,
            ReferenceKind = referenceKind,
            CreateTime = createTime,
            CreateBy = "ForumRevisionMigration",
            CreateId = editorId
        };
    }

    private static void VerifyRevisionRelations(
        ISqlSugarClient db,
        ICollection<string> issues,
        IReadOnlyDictionary<long, Post> posts,
        IReadOnlyDictionary<long, Comment> comments,
        IReadOnlyDictionary<long, PostContentRevision> postRevisions,
        IReadOnlyDictionary<long, CommentContentRevision> commentRevisions)
    {
        var tags = db.Queryable<Tag>().ToList().ToDictionary(item => item.Id);
        foreach (var relation in db.Queryable<PostContentRevisionTag>().ToList())
        {
            if (!postRevisions.TryGetValue(relation.RevisionId, out var revision) ||
                revision.TenantId != relation.TenantId)
            {
                issues.Add($"Post RevisionTag {relation.Id} 指向不存在或跨租户的 Revision {relation.RevisionId}。");
                continue;
            }
            if (!tags.TryGetValue(relation.TagId, out var tag) ||
                tag.IsDeleted ||
                !tag.IsEnabled)
            {
                if (revision.IntegrityStatus == ForumContentRevisionIntegrityStatuses.Complete)
                {
                    issues.Add($"完整 Post Revision {revision.Id} 引用了不可用 Tag {relation.TagId}。");
                }
            }
        }

        var attachments = db.Queryable<Attachment>().ToList().ToDictionary(item => item.Id);
        foreach (var relation in db.Queryable<ForumContentRevisionAttachment>().ToList())
        {
            var revisionExists = relation.TargetType switch
            {
                ForumContentRevisionTargetTypes.Post =>
                    postRevisions.TryGetValue(relation.RevisionId, out var postRevision) &&
                    postRevision.PostId == relation.TargetId &&
                    postRevision.TenantId == relation.TenantId &&
                    posts.ContainsKey(relation.TargetId),
                ForumContentRevisionTargetTypes.Comment =>
                    commentRevisions.TryGetValue(relation.RevisionId, out var commentRevision) &&
                    commentRevision.CommentId == relation.TargetId &&
                    commentRevision.TenantId == relation.TenantId &&
                    comments.ContainsKey(relation.TargetId),
                _ => false
            };
            if (!revisionExists)
            {
                issues.Add($"RevisionAttachment {relation.Id} 的目标或 Revision 关系无效。");
                continue;
            }

            if (!attachments.TryGetValue(relation.AttachmentId, out var attachment) ||
                attachment.TenantId != relation.TenantId)
            {
                issues.Add($"RevisionAttachment {relation.Id} 指向不存在或跨租户的附件。");
            }
        }
    }

    private static void ValidateRestoreSource<TRevision>(
        ICollection<string> issues,
        long revisionId,
        string sourceType,
        long? restoredFromRevisionId,
        long targetId,
        long tenantId,
        IReadOnlyDictionary<long, TRevision> revisions,
        Func<TRevision, long> targetIdSelector,
        Func<TRevision, long> tenantIdSelector)
    {
        var isRestore = string.Equals(sourceType, ForumContentRevisionSourceTypes.Restore, StringComparison.Ordinal);
        if (!isRestore && restoredFromRevisionId.HasValue)
        {
            issues.Add($"Revision {revisionId} 非 Restore 却设置了恢复来源。");
            return;
        }
        if (!isRestore)
        {
            return;
        }
        if (!restoredFromRevisionId.HasValue ||
            !revisions.TryGetValue(restoredFromRevisionId.Value, out var source) ||
            targetIdSelector(source) != targetId ||
            tenantIdSelector(source) != tenantId)
        {
            issues.Add($"Restore Revision {revisionId} 的恢复来源无效。");
        }
    }

    private static bool HasSourceTables(ISqlSugarClient db)
    {
        return db.DbMaintenance.IsAnyTable(nameof(Post), false) &&
               db.DbMaintenance.IsAnyTable(nameof(Comment), false) &&
               db.DbMaintenance.IsAnyTable(nameof(Category), false) &&
               db.DbMaintenance.IsAnyTable(nameof(Tag), false) &&
               db.DbMaintenance.IsAnyTable(nameof(PostTag), false) &&
               db.DbMaintenance.IsAnyTable(nameof(Attachment), false);
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
