using Radish.Common;
using Radish.Model;
using Radish.Shared;
using Radish.Shared.Constants;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立问答回答 PublicId、Revision、采纳事件与附件业务类型权威。</summary>
internal sealed class ForumAnswerLifecycleSchemaMigration : ISchemaMigration
{
    public static ForumAnswerLifecycleSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260727_015_forum_answer_lifecycle";

    public string Scope => "Main";

    public string Description => "建立问答回答生命周期、采纳 CAS 与回答附件权威";

    public string ChecksumSource =>
        "20260727_015_forum_answer_lifecycle|Main|" +
        "PostAnswer.PublicId-Revision-Enabled-ModerationTargetAction-v1|" +
        "PostQuestion.AcceptanceRevision-v1|" +
        "PostAnswerContentRevision-v1|PostAnswerAcceptanceEvent-v1|" +
        "PostAnswerAttachment-and-revision-reference-backfill-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<PostAnswer>();
        db.CodeFirst.InitTables<PostQuestion>();
        db.CodeFirst.InitTables<PostAnswerContentRevision>();
        db.CodeFirst.InitTables<PostAnswerAcceptanceEvent>();
        db.CodeFirst.InitTables<ForumContentRevisionAttachment>();
        ForumContentRevisionSchemaMigration.EnsureRevisionAttachmentIndexes(db);

        if (!db.DbMaintenance.IsAnyTable(nameof(PostAnswer), false))
        {
            return;
        }

        var answers = db.Queryable<PostAnswer>().ToList();
        foreach (var answer in answers)
        {
            var changed = false;
            if (!PostAnswer.HasPublicIdFormat(answer.PublicId))
            {
                answer.PublicId = PostAnswer.GeneratePublicId();
                changed = true;
            }
            if (answer.ContentRevision <= 0)
            {
                answer.ContentRevision = 1;
                changed = true;
            }
            if (changed)
            {
                db.Updateable(answer)
                    .UpdateColumns(item => new { item.PublicId, item.ContentRevision })
                    .ExecuteCommand();
            }

            var baseline = db.Queryable<PostAnswerContentRevision>()
                .Where(item =>
                    item.TenantId == answer.TenantId &&
                    item.AnswerId == answer.Id &&
                    item.RevisionNumber == answer.ContentRevision)
                .First();
            if (baseline == null)
            {
                db.Insertable(new PostAnswerContentRevision
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = answer.TenantId,
                    AnswerId = answer.Id,
                    PostId = answer.PostId,
                    RevisionNumber = answer.ContentRevision,
                    SourceType = ForumContentRevisionSourceTypes.Baseline,
                    IntegrityStatus = ForumContentRevisionIntegrityStatuses.Complete,
                    Content = answer.Content,
                    EditorId = answer.AuthorId,
                    EditorName = answer.AuthorName,
                    CreateTime = answer.CreateTime,
                    CreateBy = answer.CreateBy,
                    CreateId = answer.CreateId
                }).ExecuteCommand();
            }
        }

        if (db.DbMaintenance.IsAnyTable(nameof(Attachment), false))
        {
            foreach (var answer in answers)
            {
                db.Updateable<Attachment>()
                    .SetColumns(item => new Attachment { BusinessType = AttachmentBusinessTypes.PostAnswer })
                    .Where(item =>
                        item.TenantId == answer.TenantId &&
                        item.BusinessType == AttachmentBusinessTypes.Comment &&
                        item.BusinessId == answer.Id &&
                        !item.IsDeleted)
                    .ExecuteCommand();
            }

            foreach (var answer in answers)
            {
                var revision = db.Queryable<PostAnswerContentRevision>()
                    .Where(item =>
                        item.TenantId == answer.TenantId &&
                        item.AnswerId == answer.Id &&
                        item.RevisionNumber == answer.ContentRevision)
                    .First();
                if (revision == null)
                {
                    continue;
                }
                var attachmentIds = AttachmentReferenceParser.ExtractAttachmentIds(answer.Content);
                var attachments = db.Queryable<Attachment>()
                    .Where(item =>
                        attachmentIds.Contains(item.Id) &&
                        item.TenantId == answer.TenantId &&
                        item.BusinessType == AttachmentBusinessTypes.PostAnswer &&
                        item.BusinessId == answer.Id &&
                        item.IsEnabled &&
                        !item.IsDeleted)
                    .ToList();
                foreach (var attachment in attachments)
                {
                    if (db.Queryable<ForumContentRevisionAttachment>().Any(item =>
                            item.TenantId == answer.TenantId &&
                            item.TargetType == ForumContentRevisionTargetTypes.PostAnswer &&
                            item.RevisionId == revision.Id &&
                            item.AttachmentId == attachment.Id &&
                            item.ReferenceKind == ForumContentRevisionReferenceKinds.Content))
                    {
                        continue;
                    }
                    db.Insertable(new ForumContentRevisionAttachment
                    {
                        Id = SnowFlakeSingle.Instance.NextId(),
                        TenantId = answer.TenantId,
                        TargetType = ForumContentRevisionTargetTypes.PostAnswer,
                        TargetId = answer.Id,
                        RevisionId = revision.Id,
                        AttachmentId = attachment.Id,
                        ReferenceKind = ForumContentRevisionReferenceKinds.Content,
                        CreateTime = revision.CreateTime,
                        CreateBy = revision.CreateBy,
                        CreateId = revision.CreateId
                    }).ExecuteCommand();
                }
            }
        }

        foreach (var question in db.Queryable<PostQuestion>()
                     .Where(item => item.AcceptedAnswerId.HasValue)
                     .ToList())
        {
            var accepted = answers.FirstOrDefault(item =>
                item.Id == question.AcceptedAnswerId &&
                item.TenantId == question.TenantId &&
                item.PostId == question.PostId);
            if (accepted == null)
            {
                continue;
            }

            question.AcceptanceRevision = Math.Max(1, question.AcceptanceRevision);
            question.AcceptedAnswerContentRevision = accepted.ContentRevision;
            db.Updateable(question)
                .UpdateColumns(item => new
                {
                    item.AcceptanceRevision,
                    item.AcceptedAnswerContentRevision
                })
                .ExecuteCommand();

            if (!db.Queryable<PostAnswerAcceptanceEvent>().Any(item =>
                    item.TenantId == question.TenantId &&
                    item.PostQuestionId == question.Id &&
                    item.AcceptanceRevision == question.AcceptanceRevision))
            {
                db.Insertable(new PostAnswerAcceptanceEvent
                {
                    Id = SnowFlakeSingle.Instance.NextId(),
                    TenantId = question.TenantId,
                    PostId = question.PostId,
                    PostQuestionId = question.Id,
                    AcceptanceRevision = question.AcceptanceRevision,
                    EventType = PostAnswerAcceptanceEventTypes.Accepted,
                    CurrentAnswerId = accepted.Id,
                    CurrentAnswerContentRevision = accepted.ContentRevision,
                    OperatorId = question.ModifyId ?? question.CreateId,
                    OperatorName = question.ModifyBy ?? question.CreateBy,
                    ReasonCode = "LegacyBaseline",
                    CreateTime = question.ModifyTime ?? question.CreateTime
                }).ExecuteCommand();
            }
        }
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var table in new[]
                 {
                     nameof(PostAnswer),
                     nameof(PostQuestion),
                     nameof(PostAnswerContentRevision),
                     nameof(PostAnswerAcceptanceEvent),
                     nameof(ForumContentRevisionAttachment)
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

        foreach (var column in new[]
                 {
                     nameof(PostAnswer.PublicId),
                     nameof(PostAnswer.ContentRevision),
                     nameof(PostAnswer.EditCount),
                     nameof(PostAnswer.IsEnabled),
                     nameof(PostAnswer.ModerationTargetActionId)
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, nameof(PostAnswer), column) == null)
            {
                issues.Add($"缺少列 {nameof(PostAnswer)}.{column}。");
            }
        }
        foreach (var column in new[]
                 {
                     nameof(PostQuestion.AcceptanceRevision),
                     nameof(PostQuestion.AcceptedAnswerContentRevision)
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, nameof(PostQuestion), column) == null)
            {
                issues.Add($"缺少列 {nameof(PostQuestion)}.{column}。");
            }
        }
        if (issues.Count > 0)
        {
            return issues;
        }

        var revisions = db.Queryable<PostAnswerContentRevision>().ToList();
        foreach (var answer in db.Queryable<PostAnswer>().ToList())
        {
            if (!PostAnswer.HasPublicIdFormat(answer.PublicId))
            {
                issues.Add($"PostAnswer {answer.Id} 的 PublicId 非法。");
            }
            if (answer.ContentRevision <= 0 ||
                !revisions.Any(item =>
                    item.TenantId == answer.TenantId &&
                    item.AnswerId == answer.Id &&
                    item.RevisionNumber == answer.ContentRevision))
            {
                issues.Add($"PostAnswer {answer.Id} 缺少当前 Revision。");
            }
        }

        foreach (var question in db.Queryable<PostQuestion>()
                     .Where(item => item.AcceptedAnswerId.HasValue)
                     .ToList())
        {
            if (question.AcceptanceRevision <= 0 || !question.AcceptedAnswerContentRevision.HasValue)
            {
                issues.Add($"PostQuestion {question.Id} 的采纳版本基线不完整。");
            }
        }

        return issues;
    }
}
