using Radish.Common;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>补齐回答分页索引，并严格验证回答生命周期权威数据。</summary>
internal sealed class ForumAnswerLifecycleStrictSchemaMigration : ISchemaMigration
{
    private const string AnswerPublicIdIndex = "idx_postanswer_public_id";
    private const string AnswerPageIndex = "idx_postanswer_tenant_post_visibility_page";
    private const string AnswerAuthorIndex = "idx_postanswer_tenant_author_history";

    public static ForumAnswerLifecycleStrictSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260728_016_forum_answer_lifecycle_strict";

    public string Scope => "Main";

    public string Description => "补齐回答分页索引并严格验证采纳、计数与附件归属";

    public string ChecksumSource =>
        "20260728_016_forum_answer_lifecycle_strict|Main|" +
        "Attachment-schema-presence-v1|" +
        "PostAnswer-page-author-index-v1|" +
        "answer-publicid-revision-acceptance-count-attachment-strict-verify-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<Attachment>();
        if (!db.DbMaintenance.IsAnyTable(nameof(PostAnswer), false))
        {
            return;
        }

        EnsureIndex(
            db,
            nameof(PostAnswer),
            AnswerPublicIdIndex,
            true,
            (nameof(PostAnswer.PublicId), false));
        EnsureIndex(
            db,
            nameof(PostAnswer),
            AnswerPageIndex,
            false,
            (nameof(PostAnswer.TenantId), false),
            (nameof(PostAnswer.PostId), false),
            (nameof(PostAnswer.IsDeleted), false),
            (nameof(PostAnswer.IsEnabled), false),
            (nameof(PostAnswer.IsAccepted), false),
            (nameof(PostAnswer.CreateTime), false),
            (nameof(PostAnswer.Id), false));
        EnsureIndex(
            db,
            nameof(PostAnswer),
            AnswerAuthorIndex,
            false,
            (nameof(PostAnswer.TenantId), false),
            (nameof(PostAnswer.AuthorId), false),
            (nameof(PostAnswer.IsDeleted), false),
            (nameof(PostAnswer.CreateTime), false));
        EnsureIndex(
            db,
            nameof(PostAnswerContentRevision),
            "idx_postanswerrevision_tenant_answer_revision",
            true,
            (nameof(PostAnswerContentRevision.TenantId), false),
            (nameof(PostAnswerContentRevision.AnswerId), false),
            (nameof(PostAnswerContentRevision.RevisionNumber), true));
        EnsureIndex(
            db,
            nameof(PostAnswerContentRevision),
            "idx_postanswerrevision_tenant_restore_source",
            false,
            (nameof(PostAnswerContentRevision.TenantId), false),
            (nameof(PostAnswerContentRevision.RestoredFromRevisionId), false));
        EnsureIndex(
            db,
            nameof(PostAnswerAcceptanceEvent),
            "idx_answeracceptanceevent_question_revision",
            true,
            (nameof(PostAnswerAcceptanceEvent.TenantId), false),
            (nameof(PostAnswerAcceptanceEvent.PostQuestionId), false),
            (nameof(PostAnswerAcceptanceEvent.AcceptanceRevision), false));
        ForumContentRevisionSchemaMigration.EnsureRevisionAttachmentIndexes(db);
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        List<string> issues = [];
        foreach (string table in new[]
                 {
                     nameof(PostAnswer),
                     nameof(PostQuestion),
                     nameof(PostAnswerContentRevision),
                     nameof(PostAnswerAcceptanceEvent),
                     nameof(ForumContentRevisionAttachment),
                     nameof(Attachment)
                 })
        {
            if (!db.DbMaintenance.IsAnyTable(table, false))
            {
                issues.Add($"缺少表 {table}。");
            }
        }
        if (issues.Count > 0)
        {
            return issues;
        }

        foreach ((string table, string index) in new[]
                 {
                     (nameof(PostAnswer), AnswerPublicIdIndex),
                     (nameof(PostAnswer), AnswerPageIndex),
                     (nameof(PostAnswer), AnswerAuthorIndex),
                     (nameof(PostAnswerContentRevision), "idx_postanswerrevision_tenant_answer_revision"),
                     (nameof(PostAnswerContentRevision), "idx_postanswerrevision_tenant_restore_source"),
                     (nameof(PostAnswerAcceptanceEvent), "idx_answeracceptanceevent_question_revision"),
                     (nameof(ForumContentRevisionAttachment), "idx_forumrevisionattachment_revision_attachment"),
                     (nameof(ForumContentRevisionAttachment), "idx_forumrevisionattachment_attachment"),
                     (nameof(ForumContentRevisionAttachment), "idx_forumrevisionattachment_target")
                 })
        {
            if (!IndexExists(db, table, index))
            {
                issues.Add($"缺少索引 {index}。");
            }
        }

        List<PostAnswer> answers = db.Queryable<PostAnswer>().ToList();
        List<PostQuestion> questions = db.Queryable<PostQuestion>().ToList();
        List<PostAnswerContentRevision> revisions = db.Queryable<PostAnswerContentRevision>().ToList();
        List<PostAnswerAcceptanceEvent> acceptanceEvents =
            db.Queryable<PostAnswerAcceptanceEvent>().ToList();
        List<ForumContentRevisionAttachment> revisionAttachments =
            db.Queryable<ForumContentRevisionAttachment>().ToList();
        List<Attachment> attachments = db.Queryable<Attachment>().ToList();

        VerifyAnswerIdentityAndRevisions(issues, answers, revisions);
        VerifyAcceptanceAndCounts(issues, answers, questions);
        VerifyAcceptanceEvents(issues, questions, acceptanceEvents);
        VerifyAnswerAttachments(issues, answers, revisions, revisionAttachments, attachments);
        return issues.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void VerifyAnswerIdentityAndRevisions(
        ICollection<string> issues,
        IReadOnlyCollection<PostAnswer> answers,
        IReadOnlyCollection<PostAnswerContentRevision> revisions)
    {
        foreach (IGrouping<string, PostAnswer> duplicate in answers
                     .GroupBy(answer => answer.PublicId, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            issues.Add($"PostAnswer PublicId {duplicate.Key} 存在 {duplicate.Count()} 条重复记录。");
        }

        foreach (IGrouping<(long TenantId, long AnswerId, int RevisionNumber), PostAnswerContentRevision>
                 duplicate in revisions
                     .GroupBy(revision => (
                         revision.TenantId,
                         revision.AnswerId,
                         revision.RevisionNumber))
                     .Where(group => group.Count() > 1))
        {
            issues.Add(
                $"PostAnswer {duplicate.Key.AnswerId} 的 Revision {duplicate.Key.RevisionNumber} " +
                $"存在 {duplicate.Count()} 条重复记录。");
        }

        Dictionary<long, PostAnswerContentRevision> revisionById =
            revisions.ToDictionary(revision => revision.Id);
        Dictionary<long, PostAnswer> answerById = answers.ToDictionary(answer => answer.Id);
        ILookup<(long TenantId, long AnswerId, long PostId, int RevisionNumber), PostAnswerContentRevision>
            revisionByAnswerVersion = revisions.ToLookup(revision => (
                revision.TenantId,
                revision.AnswerId,
                revision.PostId,
                revision.RevisionNumber));
        foreach (PostAnswer answer in answers)
        {
            if (!PostAnswer.HasPublicIdFormat(answer.PublicId))
            {
                issues.Add($"PostAnswer {answer.Id} 的 PublicId 非法。");
            }

            PostAnswerContentRevision? currentRevision = revisionByAnswerVersion[(
                answer.TenantId,
                answer.Id,
                answer.PostId,
                answer.ContentRevision)].FirstOrDefault();
            if (answer.ContentRevision <= 0 || currentRevision == null)
            {
                issues.Add($"PostAnswer {answer.Id} 缺少匹配当前实体的 Revision {answer.ContentRevision}。");
            }
        }

        foreach (PostAnswerContentRevision revision in revisions)
        {
            bool answerValid =
                answerById.TryGetValue(revision.AnswerId, out PostAnswer? answer) &&
                answer.TenantId == revision.TenantId &&
                answer.PostId == revision.PostId;
            if (!answerValid)
            {
                issues.Add(
                    $"PostAnswer Revision {revision.Id} 指向不存在、跨租户或跨帖的回答 {revision.AnswerId}。");
            }

            bool isRestore = string.Equals(
                revision.SourceType,
                ForumContentRevisionSourceTypes.Restore,
                StringComparison.Ordinal);
            if (!isRestore && revision.RestoredFromRevisionId.HasValue)
            {
                issues.Add($"PostAnswer Revision {revision.Id} 非 Restore 却设置了恢复来源。");
                continue;
            }
            if (!isRestore)
            {
                continue;
            }
            if (!revision.RestoredFromRevisionId.HasValue ||
                !revisionById.TryGetValue(revision.RestoredFromRevisionId.Value, out PostAnswerContentRevision? source) ||
                source.TenantId != revision.TenantId ||
                source.AnswerId != revision.AnswerId)
            {
                issues.Add($"PostAnswer Restore Revision {revision.Id} 的恢复来源无效。");
            }
        }
    }

    private static void VerifyAcceptanceAndCounts(
        ICollection<string> issues,
        IReadOnlyCollection<PostAnswer> answers,
        IReadOnlyCollection<PostQuestion> questions)
    {
        foreach (IGrouping<(long TenantId, long PostId), PostQuestion> duplicate in questions
                     .GroupBy(question => (question.TenantId, question.PostId))
                     .Where(group => group.Count() > 1))
        {
            issues.Add(
                $"租户 {duplicate.Key.TenantId} 的帖子 {duplicate.Key.PostId} " +
                $"存在 {duplicate.Count()} 条 PostQuestion。");
        }

        HashSet<(long TenantId, long PostId, long AnswerId)> acceptedPointers = questions
            .Where(question => question.AcceptedAnswerId.HasValue)
            .Select(question => (
                question.TenantId,
                question.PostId,
                question.AcceptedAnswerId!.Value))
            .ToHashSet();
        ILookup<(long TenantId, long PostId), PostAnswer> answersByQuestion = answers
            .ToLookup(answer => (answer.TenantId, answer.PostId));
        HashSet<(long TenantId, long PostId)> questionKeys = questions
            .Select(question => (question.TenantId, question.PostId))
            .ToHashSet();

        foreach (PostQuestion question in questions)
        {
            List<PostAnswer> questionAnswers = answersByQuestion[(
                question.TenantId,
                question.PostId)].ToList();
            int visibleAnswerCount = questionAnswers.Count(answer =>
                !answer.IsDeleted && answer.IsEnabled);
            if (question.AnswerCount != visibleAnswerCount)
            {
                issues.Add(
                    $"PostQuestion {question.Id} 的 AnswerCount={question.AnswerCount}，" +
                    $"可见回答重建值为 {visibleAnswerCount}。");
            }

            if (!question.AcceptedAnswerId.HasValue)
            {
                if (question.IsSolved || question.AcceptedAnswerContentRevision.HasValue)
                {
                    issues.Add($"PostQuestion {question.Id} 的未采纳状态字段不一致。");
                }
                continue;
            }

            PostAnswer? acceptedAnswer = questionAnswers.FirstOrDefault(answer =>
                answer.Id == question.AcceptedAnswerId.Value);
            if (!question.IsSolved ||
                question.AcceptanceRevision <= 0 ||
                !question.AcceptedAnswerContentRevision.HasValue ||
                acceptedAnswer == null ||
                acceptedAnswer.IsDeleted ||
                !acceptedAnswer.IsEnabled ||
                !acceptedAnswer.IsAccepted ||
                question.AcceptedAnswerContentRevision != acceptedAnswer.ContentRevision)
            {
                issues.Add($"PostQuestion {question.Id} 的当前采纳指向或状态投影不一致。");
            }

            int acceptedProjectionCount = questionAnswers.Count(answer => answer.IsAccepted);
            if (acceptedProjectionCount != 1)
            {
                issues.Add(
                    $"PostQuestion {question.Id} 存在 {acceptedProjectionCount} 个 IsAccepted 回答。");
            }
        }

        foreach (PostAnswer answer in answers)
        {
            bool shouldBeAccepted = acceptedPointers.Contains((
                answer.TenantId,
                answer.PostId,
                answer.Id));
            if (answer.IsAccepted != shouldBeAccepted)
            {
                issues.Add($"PostAnswer {answer.Id} 的 IsAccepted 与 PostQuestion 指针不一致。");
            }

            if (!questionKeys.Contains((answer.TenantId, answer.PostId)))
            {
                issues.Add($"PostAnswer {answer.Id} 缺少同租户同帖的 PostQuestion。");
            }
        }
    }

    private static void VerifyAcceptanceEvents(
        ICollection<string> issues,
        IReadOnlyCollection<PostQuestion> questions,
        IReadOnlyCollection<PostAnswerAcceptanceEvent> acceptanceEvents)
    {
        HashSet<(long TenantId, long QuestionId, long PostId)> questionKeys = questions
            .Select(question => (question.TenantId, question.Id, question.PostId))
            .ToHashSet();
        foreach (IGrouping<(long TenantId, long QuestionId, int Revision), PostAnswerAcceptanceEvent>
                 duplicate in acceptanceEvents
                     .GroupBy(item => (
                         item.TenantId,
                         item.PostQuestionId,
                         item.AcceptanceRevision))
                     .Where(group => group.Count() > 1))
        {
            issues.Add(
                $"PostQuestion {duplicate.Key.QuestionId} 的采纳事件版本 {duplicate.Key.Revision} " +
                $"存在 {duplicate.Count()} 条重复记录。");
        }

        foreach (PostAnswerAcceptanceEvent acceptanceEvent in acceptanceEvents)
        {
            if (!questionKeys.Contains((
                    acceptanceEvent.TenantId,
                    acceptanceEvent.PostQuestionId,
                    acceptanceEvent.PostId)))
            {
                issues.Add(
                    $"采纳事件 {acceptanceEvent.Id} 指向不存在、跨租户或跨帖的 PostQuestion。");
            }
        }
    }

    private static void VerifyAnswerAttachments(
        ICollection<string> issues,
        IReadOnlyCollection<PostAnswer> answers,
        IReadOnlyCollection<PostAnswerContentRevision> revisions,
        IReadOnlyCollection<ForumContentRevisionAttachment> revisionAttachments,
        IReadOnlyCollection<Attachment> attachments)
    {
        Dictionary<long, PostAnswer> answerById = answers.ToDictionary(answer => answer.Id);
        Dictionary<long, PostAnswerContentRevision> revisionById =
            revisions.ToDictionary(revision => revision.Id);
        Dictionary<long, Attachment> attachmentById =
            attachments.ToDictionary(attachment => attachment.Id);

        foreach (Attachment attachment in attachments)
        {
            if (!attachment.BusinessId.HasValue ||
                !answerById.TryGetValue(attachment.BusinessId.Value, out PostAnswer? answer) ||
                answer.TenantId != attachment.TenantId)
            {
                if (string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.PostAnswer,
                        StringComparison.Ordinal))
                {
                    issues.Add($"回答附件 {attachment.Id} 指向不存在或跨租户的回答。");
                }
                continue;
            }

            if (string.Equals(
                    attachment.BusinessType,
                    AttachmentBusinessTypes.Comment,
                    StringComparison.Ordinal))
            {
                issues.Add($"回答 {answer.Id} 的历史附件 {attachment.Id} 仍占用 Comment 业务类型。");
            }
        }

        HashSet<(long TenantId, long RevisionId, long AttachmentId)> relationKeys = [];
        foreach (ForumContentRevisionAttachment relation in revisionAttachments.Where(relation =>
                     string.Equals(
                         relation.TargetType,
                         ForumContentRevisionTargetTypes.PostAnswer,
                         StringComparison.Ordinal)))
        {
            if (!relationKeys.Add((relation.TenantId, relation.RevisionId, relation.AttachmentId)))
            {
                issues.Add(
                    $"回答 Revision {relation.RevisionId} 与附件 {relation.AttachmentId} 存在重复引用。");
            }

            bool revisionValid =
                revisionById.TryGetValue(relation.RevisionId, out PostAnswerContentRevision? revision) &&
                revision.TenantId == relation.TenantId &&
                revision.AnswerId == relation.TargetId;
            bool attachmentValid =
                attachmentById.TryGetValue(relation.AttachmentId, out Attachment? attachment) &&
                attachment.TenantId == relation.TenantId &&
                attachment.BusinessId == relation.TargetId &&
                string.Equals(
                    attachment.BusinessType,
                    AttachmentBusinessTypes.PostAnswer,
                    StringComparison.Ordinal);
            if (!revisionValid || !attachmentValid)
            {
                issues.Add($"回答附件引用 {relation.Id} 的目标、Revision 或附件归属无效。");
            }
        }

        foreach (PostAnswerContentRevision revision in revisions.Where(revision =>
                     string.Equals(
                         revision.IntegrityStatus,
                         ForumContentRevisionIntegrityStatuses.Complete,
                         StringComparison.Ordinal)))
        {
            foreach (long attachmentId in AttachmentReferenceParser.ExtractAttachmentIds(revision.Content))
            {
                bool attachmentValid =
                    attachmentById.TryGetValue(attachmentId, out Attachment? attachment) &&
                    attachment.TenantId == revision.TenantId &&
                    attachment.BusinessId == revision.AnswerId &&
                    string.Equals(
                        attachment.BusinessType,
                        AttachmentBusinessTypes.PostAnswer,
                        StringComparison.Ordinal);
                bool relationValid = relationKeys.Contains((
                    revision.TenantId,
                    revision.Id,
                    attachmentId));
                if (!attachmentValid || !relationValid)
                {
                    issues.Add(
                        $"完整回答 Revision {revision.Id} 引用的附件 {attachmentId} 缺少有效归属或持久引用。");
                }
            }
        }
    }

    private static void EnsureIndex(
        ISqlSugarClient db,
        string configuredTableName,
        string indexName,
        bool isUnique,
        params (string ColumnName, bool Descending)[] columns)
    {
        var physicalTableName = DatabaseIdentifierResolver.ResolveTable(db, configuredTableName)
                                ?? throw new InvalidOperationException(
                                    $"{configuredTableName} 不存在，无法创建索引 {indexName}。");
        var physicalColumns = columns.Select(column =>
        {
            var physicalColumn = DatabaseIdentifierResolver.ResolveColumn(
                db,
                physicalTableName,
                column.ColumnName)
                ?? throw new InvalidOperationException(
                    $"{configuredTableName}.{column.ColumnName} 不存在，无法创建索引 {indexName}。");
            return $"{QuoteIdentifier(physicalColumn.ColumnName)}{(column.Descending ? " DESC" : string.Empty)}";
        });
        db.Ado.ExecuteCommand(
            $"CREATE {(isUnique ? "UNIQUE " : string.Empty)}INDEX IF NOT EXISTS " +
            $"{QuoteIdentifier(indexName)} ON {QuoteIdentifier(physicalTableName)} " +
            $"({string.Join(", ", physicalColumns)})");
    }

    private static bool IndexExists(ISqlSugarClient db, string tableName, string indexName)
    {
        if (db.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return db.DbMaintenance.IsAnyIndex(indexName);
        }

        var physicalTableName = DatabaseIdentifierResolver.ResolveTable(db, tableName);
        return physicalTableName != null && db.DbMaintenance.GetIndexList(physicalTableName)
            .Any(index => string.Equals(index, indexName, StringComparison.OrdinalIgnoreCase));
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
