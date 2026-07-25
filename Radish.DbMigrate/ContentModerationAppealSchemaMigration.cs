using Radish.Common;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立治理申诉、申诉事件和目标动作来源。</summary>
internal sealed class ContentModerationAppealSchemaMigration : ISchemaMigration
{
    private static readonly string[] RequiredTables =
    [
        "ContentModerationAppeal",
        "ContentModerationAppealEvent",
        "ContentModerationTargetAction"
    ];

    public static ContentModerationAppealSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260725_009_content_moderation_appeal";
    public string Scope => "Main";
    public string Description => "建立治理申诉、目标动作来源与对称纠正基础";
    public string ChecksumSource =>
        "20260725_009_content_moderation_appeal|Main|" +
        "Appeal-v1|AppealEvent-operation-v1|TargetAction-v1|Evidence-AppealId-v1|" +
        "CaseEvent-Appeal-TargetAction-v1|UserAction-AppealId-v1|Main-target-source-v1|" +
        "backfill-main-restrict-v2|historical-chat-doctor-v1";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        db.CodeFirst.InitTables<ContentModerationAppeal>();
        db.CodeFirst.InitTables<ContentModerationAppealEvent>();
        db.CodeFirst.InitTables<ContentModerationTargetAction>();
        db.CodeFirst.InitTables<ContentModerationEvidence>();
        db.CodeFirst.InitTables<ContentModerationCaseEvent>();
        db.CodeFirst.InitTables<UserModerationAction>();
        db.CodeFirst.InitTables<Post>();
        db.CodeFirst.InitTables<Comment>();
        db.CodeFirst.InitTables<PostQuickReply>();
        db.CodeFirst.InitTables<Product>();

        BackfillTargetActions(db);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        if (!db.DbMaintenance.IsAnyTable("ContentModerationCase", false) ||
            !db.DbMaintenance.IsAnyTable("ContentModerationCaseEvent", false))
        {
            return [];
        }

        var restrictedCases = db.Queryable<ContentModerationCase>()
            .Where(item =>
                item.Status == (int)ContentModerationCaseStatus.Resolved &&
                item.Decision == (int)ContentModerationDecision.Violation &&
                item.TargetDisposition == (int)ContentModerationTargetDisposition.Restricted &&
                !item.IsDeleted)
            .Count();
        if (restrictedCases == 0)
        {
            return [];
        }

        var historicalChatCases = db.Queryable<ContentModerationCase>()
            .Where(item =>
                item.Status == (int)ContentModerationCaseStatus.Resolved &&
                item.Decision == (int)ContentModerationDecision.Violation &&
                item.TargetDisposition == (int)ContentModerationTargetDisposition.Restricted &&
                item.TargetType == (int)ContentReportTargetTypeEnum.ChatMessage &&
                !item.IsDeleted)
            .Count();
        var issues = new List<string>
        {
            $"发现 {restrictedCases} 个历史限制案件；迁移只为有明确 DecisionRecorded 结果的 Main 目标回填可恢复来源。"
        };
        if (historicalChatCases > 0)
        {
            issues.Add(
                $"发现 {historicalChatCases} 个历史 Chat 限制案件；Main migration 无法跨库证明消息当前撤回来源，保留为不可自动恢复。");
        }

        return issues;
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var table in RequiredTables)
        {
            if (!db.DbMaintenance.IsAnyTable(table, false))
            {
                issues.Add($"缺少表 {table}。");
            }
        }

        foreach (var (table, column) in new[]
                 {
                     ("ContentModerationEvidence", nameof(ContentModerationEvidence.AppealId)),
                     ("ContentModerationCaseEvent", nameof(ContentModerationCaseEvent.RelatedAppealId)),
                     ("ContentModerationCaseEvent", nameof(ContentModerationCaseEvent.RelatedTargetActionId)),
                     ("UserModerationAction", nameof(UserModerationAction.AppealId)),
                     ("Post", nameof(Post.ModerationTargetActionId)),
                     ("Comment", nameof(Comment.ModerationTargetActionId)),
                     ("PostQuickReply", nameof(PostQuickReply.ModerationTargetActionId)),
                     ("ShopProduct", nameof(Product.ModerationTargetActionId))
                 })
        {
            if (DatabaseIdentifierResolver.ResolveColumn(db, table, column) == null)
            {
                issues.Add($"缺少列 {table}.{column}。");
            }
        }

        if (issues.Count > 0)
        {
            return issues;
        }

        var duplicateAppeals = db.Queryable<ContentModerationAppeal>()
            .GroupBy(item => new { item.TenantId, item.CaseId, item.AppellantUserId })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateAppeals > 0)
        {
            issues.Add($"存在 {duplicateAppeals} 组同案同用户重复申诉。");
        }

        var duplicateTargetKeys = db.Queryable<ContentModerationTargetAction>()
            .GroupBy(item => new { item.TenantId, item.OperationKey })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateTargetKeys > 0)
        {
            issues.Add($"存在 {duplicateTargetKeys} 组重复目标动作业务键。");
        }

        var invalidRestoreSources = db.Queryable<ContentModerationTargetAction>()
            .Where(item =>
                item.ActionType == (int)ContentModerationTargetActionType.Restore &&
                item.SourceTargetActionId == null)
            .Count();
        if (invalidRestoreSources > 0)
        {
            issues.Add($"存在 {invalidRestoreSources} 条缺少来源限制动作的恢复记录。");
        }

        return issues;
    }

    private static void BackfillTargetActions(ISqlSugarClient db)
    {
        if (!db.DbMaintenance.IsAnyTable("ContentModerationCase", false) ||
            !db.DbMaintenance.IsAnyTable("ContentModerationCaseEvent", false))
        {
            return;
        }

        var cases = db.Queryable<ContentModerationCase>()
            .Where(item =>
                item.Status == (int)ContentModerationCaseStatus.Resolved &&
                item.Decision == (int)ContentModerationDecision.Violation &&
                item.TargetDisposition == (int)ContentModerationTargetDisposition.Restricted &&
                !item.IsDeleted)
            .ToList();
        foreach (var moderationCase in cases)
        {
            if (db.Queryable<ContentModerationTargetAction>()
                .Any(item =>
                    item.TenantId == moderationCase.TenantId &&
                    item.CaseId == moderationCase.Id &&
                    item.ActionType == (int)ContentModerationTargetActionType.Restrict))
            {
                continue;
            }

            var decisionEvent = db.Queryable<ContentModerationCaseEvent>()
                .Where(item => item.TenantId == moderationCase.TenantId && item.CaseId == moderationCase.Id)
                .OrderByDescending(item => item.EventSequence)
                .First(item => item.EventType == "DecisionRecorded" || item.EventType == "ActionSucceeded");
            if (decisionEvent == null)
            {
                continue;
            }

            var hasRestrictedResult =
                string.Equals(decisionEvent.ResultCode, "Restricted", StringComparison.Ordinal);
            var changed = hasRestrictedResult &&
                          moderationCase.TargetType != (int)ContentReportTargetTypeEnum.ChatMessage;
            var action = new ContentModerationTargetAction
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = moderationCase.TenantId,
                CaseId = moderationCase.Id,
                TargetType = moderationCase.TargetType,
                TargetContentId = moderationCase.TargetContentId,
                TargetUserId = moderationCase.TargetUserId,
                ActionType = (int)ContentModerationTargetActionType.Restrict,
                OperationKey = $"migration:moderation-restrict:{moderationCase.Id}",
                Status = changed
                    ? (int)ContentModerationTargetActionStatus.Succeeded
                    : (int)ContentModerationTargetActionStatus.NoEffect,
                ChangedTargetState = changed,
                ResultTargetVersion = changed &&
                                      moderationCase.TargetType == (int)ContentReportTargetTypeEnum.Product
                    ? db.Queryable<Product>()
                        .Where(item =>
                            item.Id == moderationCase.TargetContentId &&
                            item.TenantId == moderationCase.TenantId)
                        .Select(item => (int?)item.Version)
                        .First()
                    : null,
                ResultCode = hasRestrictedResult && !changed
                    ? "LegacyCrossDatabaseSourceUnavailable"
                    : decisionEvent.ResultCode ?? "LegacyNoEffect",
                RequestedAt = moderationCase.ResolvedAt ?? decisionEvent.CreateTime,
                CompletedAt = moderationCase.ResolvedAt ?? decisionEvent.CreateTime,
                OperatorUserId = moderationCase.ResolvedById ?? decisionEvent.ActorUserId,
                OperatorName = moderationCase.ResolvedByName ?? decisionEvent.ActorName,
                CreateTime = moderationCase.ResolvedAt ?? decisionEvent.CreateTime,
                CreateBy = moderationCase.ResolvedByName ?? decisionEvent.ActorName,
                CreateId = moderationCase.ResolvedById ?? decisionEvent.ActorUserId
            };
            db.Insertable(action).ExecuteCommand();
            if (changed)
            {
                BackfillMainTargetMarker(db, moderationCase, action.Id);
            }
        }
    }

    private static void BackfillMainTargetMarker(
        ISqlSugarClient db,
        ContentModerationCase moderationCase,
        long targetActionId)
    {
        switch ((ContentReportTargetTypeEnum)moderationCase.TargetType)
        {
            case ContentReportTargetTypeEnum.Post:
                db.Updateable<Post>()
                    .SetColumns(item => item.ModerationTargetActionId == targetActionId)
                    .Where(item =>
                        item.Id == moderationCase.TargetContentId &&
                        item.TenantId == moderationCase.TenantId &&
                        item.IsDeleted &&
                        item.ModerationTargetActionId == null)
                    .ExecuteCommand();
                break;
            case ContentReportTargetTypeEnum.Comment:
                db.Updateable<Comment>()
                    .SetColumns(item => item.ModerationTargetActionId == targetActionId)
                    .Where(item =>
                        item.Id == moderationCase.TargetContentId &&
                        item.TenantId == moderationCase.TenantId &&
                        item.IsDeleted &&
                        item.ModerationTargetActionId == null)
                    .ExecuteCommand();
                break;
            case ContentReportTargetTypeEnum.PostQuickReply:
                db.Updateable<PostQuickReply>()
                    .SetColumns(item => item.ModerationTargetActionId == targetActionId)
                    .Where(item =>
                        item.Id == moderationCase.TargetContentId &&
                        item.TenantId == moderationCase.TenantId &&
                        item.IsDeleted &&
                        item.ModerationTargetActionId == null)
                    .ExecuteCommand();
                break;
            case ContentReportTargetTypeEnum.Product:
                db.Updateable<Product>()
                    .SetColumns(item => item.ModerationTargetActionId == targetActionId)
                    .Where(item =>
                        item.Id == moderationCase.TargetContentId &&
                        item.TenantId == moderationCase.TenantId &&
                        !item.IsOnSale &&
                        item.ModerationTargetActionId == null)
                    .ExecuteCommand();
                break;
        }
    }
}
