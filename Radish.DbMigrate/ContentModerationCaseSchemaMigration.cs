using Microsoft.Extensions.DependencyInjection;
using Radish.Common;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

/// <summary>建立内容治理案件、证据、事件和用户治理当前状态。</summary>
internal sealed class ContentModerationCaseSchemaMigration : ISchemaMigration
{
    private static readonly string[] NewTables =
    [
        "ContentModerationCase",
        "ContentModerationEvidence",
        "ContentModerationCaseEvent",
        "UserModerationState"
    ];

    public static ContentModerationCaseSchemaMigration Instance { get; } = new();

    public string MigrationId => "20260721_008_content_moderation_case";
    public string Scope => "Main";
    public string Description => "建立治理案件、追加式证据与用户治理唯一当前状态";
    public string ChecksumSource =>
        "20260721_008_content_moderation_case|Main|" +
        "ContentModerationCase-v1|ContentModerationEvidence-v1|ContentModerationCaseEvent-v1|" +
        "UserModerationState-v1|ContentReport-case-public-v1|Case-decision-operation-v1|UserModerationAction-ledger-v1|legacy-backfill-v2";

    public void Apply(ISqlSugarClient db, IServiceProvider services)
    {
        db.CodeFirst.InitTables<ContentModerationCase>();
        db.CodeFirst.InitTables<ContentModerationEvidence>();
        db.CodeFirst.InitTables<ContentModerationCaseEvent>();
        db.CodeFirst.InitTables<UserModerationState>();
        db.CodeFirst.InitTables<ContentReport>();
        db.CodeFirst.InitTables<UserModerationAction>();

        if (!db.DbMaintenance.IsAnyTable("ContentReport", false))
        {
            return;
        }

        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        BackfillReports(db, nowUtc);
        BackfillActionsAndStates(db, nowUtc);
    }

    public IReadOnlyList<string> Diagnose(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var warnings = new List<string>();
        if (db.DbMaintenance.IsAnyTable("ContentReport", false))
        {
            var reports = db.Queryable<ContentReport>()
                .Select(report => new ContentReport
                {
                    Id = report.Id,
                    TenantId = report.TenantId,
                    ReportTargetType = report.ReportTargetType,
                    TargetContentId = report.TargetContentId,
                    TargetUserId = report.TargetUserId,
                    ReporterUserId = report.ReporterUserId,
                    Status = report.Status
                })
                .ToList();
            var invalidReports = reports.Count(report => report.TargetContentId <= 0 || report.ReporterUserId <= 0);
            if (invalidReports > 0)
            {
                warnings.Add($"发现 {invalidReports} 条目标或举报人无效的历史举报；apply 将保留事实并建立独立案件。");
            }

            var invalidReportEnums = reports.Count(report =>
                !Enum.IsDefined((ContentReportTargetTypeEnum)report.ReportTargetType) ||
                !Enum.IsDefined((ContentReportStatusEnum)report.Status));
            if (invalidReportEnums > 0)
            {
                warnings.Add($"发现 {invalidReportEnums} 条目标类型或状态无法解释的历史举报。");
            }

            if (db.DbMaintenance.IsAnyTable("UserModerationAction", false))
            {
                var reportById = reports.ToDictionary(report => report.Id);
                var actions = db.Queryable<UserModerationAction>()
                    .Select(action => new UserModerationAction
                    {
                        Id = action.Id,
                        TenantId = action.TenantId,
                        TargetUserId = action.TargetUserId,
                        ActionType = action.ActionType,
                        SourceReportId = action.SourceReportId,
                        StartTime = action.StartTime,
                        EndTime = action.EndTime
                    })
                    .ToList();
                var invalidActions = actions.Count(action =>
                    !Enum.IsDefined((ModerationActionTypeEnum)action.ActionType) ||
                    action.ActionType == (int)ModerationActionTypeEnum.None ||
                    (action.EndTime.HasValue && action.EndTime.Value < action.StartTime));
                if (invalidActions > 0)
                {
                    warnings.Add($"发现 {invalidActions} 条动作类型或时间范围无法解释的历史治理动作。");
                }

                var mismatchedSources = actions.Count(action =>
                    action.SourceReportId.HasValue &&
                    (!reportById.TryGetValue(action.SourceReportId.Value, out var source) ||
                     source.TenantId != action.TenantId ||
                     source.TargetUserId != action.TargetUserId));
                if (mismatchedSources > 0)
                {
                    warnings.Add($"发现 {mismatchedSources} 条来源举报缺失、跨租户或目标用户不匹配的历史治理动作。");
                }
            }
        }

        return warnings;
    }

    public IReadOnlyList<string> Verify(ISqlSugarClient db, IServiceProvider services)
    {
        _ = services;
        var issues = new List<string>();
        foreach (var table in NewTables)
        {
            if (!db.DbMaintenance.IsAnyTable(table, false))
            {
                issues.Add($"缺少表 {table}。");
            }
        }

        foreach (var (table, column) in new[]
                 {
                     ("ContentReport", nameof(ContentReport.CaseId)),
                     ("ContentReport", nameof(ContentReport.PublicId)),
                     ("ContentReport", nameof(ContentReport.ReporterState)),
                     ("ContentModerationCase", nameof(ContentModerationCase.DecisionOperationKey)),
                     ("UserModerationAction", nameof(UserModerationAction.CaseId)),
                     ("UserModerationAction", nameof(UserModerationAction.OperationKey)),
                     ("UserModerationAction", nameof(UserModerationAction.ResultStateVersion))
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

        var unassignedReports = db.Queryable<ContentReport>()
            .Where(report => report.CaseId == null || report.PublicId == null || report.PublicId == string.Empty)
            .Count();
        if (unassignedReports > 0)
        {
            issues.Add($"仍有 {unassignedReports} 条举报未归属案件或缺少 PublicId。");
        }

        var duplicateOpenCases = db.Queryable<ContentModerationCase>()
            .Where(item => item.OpenTargetKey != null && !item.IsDeleted)
            .GroupBy(item => new { item.TenantId, item.OpenTargetKey })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateOpenCases > 0)
        {
            issues.Add($"存在 {duplicateOpenCases} 组重复开放案件。");
        }

        var duplicateStates = db.Queryable<UserModerationState>()
            .GroupBy(item => new { item.TenantId, item.TargetUserId, item.PolicyType })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateStates > 0)
        {
            issues.Add($"存在 {duplicateStates} 组重复用户治理当前状态。");
        }

        var duplicateReporterCases = db.Queryable<ContentReport>()
            .Where(report => report.CaseId != null && !report.IsDeleted)
            .GroupBy(report => new { report.TenantId, report.CaseId, report.ReporterUserId })
            .Having(report => SqlFunc.AggregateCount(report.Id) > 1)
            .Count();
        if (duplicateReporterCases > 0)
        {
            issues.Add($"存在 {duplicateReporterCases} 组同一举报人在同案内的重复举报。");
        }

        var duplicateEvidenceSequences = db.Queryable<ContentModerationEvidence>()
            .GroupBy(item => new { item.CaseId, item.EvidenceSequence })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateEvidenceSequences > 0)
        {
            issues.Add($"存在 {duplicateEvidenceSequences} 组重复案件证据序号。");
        }

        var duplicateEventSequences = db.Queryable<ContentModerationCaseEvent>()
            .GroupBy(item => new { item.CaseId, item.EventSequence })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateEventSequences > 0)
        {
            issues.Add($"存在 {duplicateEventSequences} 组重复案件事件序号。");
        }

        var duplicateActionKeys = db.Queryable<UserModerationAction>()
            .Where(action => action.OperationKey != null && action.OperationKey != string.Empty)
            .GroupBy(action => new { action.TenantId, action.OperationKey })
            .Having(action => SqlFunc.AggregateCount(action.Id) > 1)
            .Count();
        if (duplicateActionKeys > 0)
        {
            issues.Add($"存在 {duplicateActionKeys} 组重复治理动作业务键。");
        }

        var duplicateDecisionKeys = db.Queryable<ContentModerationCase>()
            .Where(item => item.DecisionOperationKey != null && item.DecisionOperationKey != string.Empty)
            .GroupBy(item => new { item.TenantId, item.DecisionOperationKey })
            .Having(item => SqlFunc.AggregateCount(item.Id) > 1)
            .Count();
        if (duplicateDecisionKeys > 0)
        {
            issues.Add($"存在 {duplicateDecisionKeys} 组重复案件决定业务键。");
        }

        return issues;
    }

    private static void BackfillReports(ISqlSugarClient db, DateTime nowUtc)
    {
        var reports = db.Queryable<ContentReport>()
            .Where(report => report.CaseId == null || report.PublicId == null || report.PublicId == string.Empty)
            .OrderBy(report => report.CreateTime)
            .OrderBy(report => report.Id)
            .ToList();
        if (reports.Count == 0)
        {
            return;
        }

        var pendingValue = (int)ContentReportStatusEnum.Pending;
        var pendingGroups = reports
            .Where(report => report.Status == pendingValue)
            .GroupBy(report => new { report.TenantId, report.ReportTargetType, report.TargetContentId })
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var group in pendingGroups.Values)
        {
            var first = group[0];
            var openTargetKey = BuildOpenTargetKey(first.ReportTargetType, first.TargetContentId);
            var moderationCase = db.Queryable<ContentModerationCase>()
                .First(item => item.TenantId == first.TenantId && item.OpenTargetKey == openTargetKey && !item.IsDeleted)
                ?? CreateCase(first, openTargetKey, ContentModerationCaseStatus.Open, ContentModerationDecision.None, nowUtc);
            if (moderationCase.Id == 0)
            {
                moderationCase.Id = SnowFlakeSingle.Instance.NextId();
                db.Insertable(moderationCase).ExecuteCommand();
            }

            foreach (var report in group)
            {
                AttachReport(db, report, moderationCase, nowUtc);
            }
        }

        foreach (var report in reports.Where(report => report.Status != pendingValue))
        {
            var decision = report.Status == (int)ContentReportStatusEnum.Approved
                ? ContentModerationDecision.Violation
                : ContentModerationDecision.NoViolation;
            var moderationCase = CreateCase(
                report,
                null,
                ContentModerationCaseStatus.Resolved,
                decision,
                nowUtc);
            moderationCase.Id = SnowFlakeSingle.Instance.NextId();
            moderationCase.TargetDisposition = (int)ContentModerationTargetDisposition.Keep;
            moderationCase.PublicResultCode = report.Status == (int)ContentReportStatusEnum.Approved
                ? "ActionTaken"
                : "NoViolation";
            moderationCase.InternalRemark = report.ReviewRemark;
            moderationCase.ResolvedAt = report.ReviewedAt ?? report.ModifyTime ?? report.CreateTime;
            moderationCase.ResolvedById = report.ReviewedById;
            moderationCase.ResolvedByName = report.ReviewedByName;
            db.Insertable(moderationCase).ExecuteCommand();
            AttachReport(db, report, moderationCase, nowUtc);
        }
    }

    private static ContentModerationCase CreateCase(
        ContentReport report,
        string? openTargetKey,
        ContentModerationCaseStatus status,
        ContentModerationDecision decision,
        DateTime nowUtc)
    {
        return new ContentModerationCase
        {
            TenantId = report.TenantId,
            PublicId = ContentModerationCase.GeneratePublicId(),
            TargetType = report.ReportTargetType,
            TargetContentId = report.TargetContentId,
            TargetUserId = report.TargetUserId,
            OpenTargetKey = openTargetKey,
            Status = (int)status,
            Decision = (int)decision,
            TargetDisposition = (int)ContentModerationTargetDisposition.None,
            Version = 1,
            OpenedAt = report.CreateTime,
            CreateTime = report.CreateTime,
            CreateBy = "DbMigrate",
            CreateId = 0,
            ModifyTime = nowUtc,
            ModifyBy = "DbMigrate",
            ModifyId = 0
        };
    }

    private static void AttachReport(
        ISqlSugarClient db,
        ContentReport report,
        ContentModerationCase moderationCase,
        DateTime nowUtc)
    {
        report.CaseId = moderationCase.Id;
        report.PublicId = string.IsNullOrWhiteSpace(report.PublicId)
            ? $"rpt_{Guid.CreateVersion7():N}"
            : report.PublicId;
        report.ReporterState = report.Status == (int)ContentReportStatusEnum.Pending ? "Submitted" : "Resolved";
        db.Updateable(report).UpdateColumns(item => new
        {
            item.CaseId,
            item.PublicId,
            item.ReporterState
        }).ExecuteCommand();

        var evidenceExists = db.Queryable<ContentModerationEvidence>()
            .Any(item => item.CaseId == moderationCase.Id && item.SourceReportId == report.Id);
        if (!evidenceExists)
        {
            var nextSequence = db.Queryable<ContentModerationEvidence>()
                .Where(item => item.CaseId == moderationCase.Id)
                .Max(item => (int?)item.EvidenceSequence) ?? 0;
            db.Insertable(new ContentModerationEvidence
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = report.TenantId,
                CaseId = moderationCase.Id,
                EvidenceSequence = nextSequence + 1,
                EvidenceType = (int)ContentModerationEvidenceType.ReportSnapshot,
                SourceReportId = report.Id,
                TargetState = (int)ContentModerationTargetState.Available,
                SnapshotTitle = report.TargetSnapshotTitle,
                SnapshotSummary = report.TargetSnapshotSummary,
                TargetUserId = report.TargetUserId,
                TargetUserName = report.TargetUserName,
                TargetPostId = report.TargetSnapshotPostId,
                TargetChannelId = report.TargetSnapshotChannelId,
                SnapshotHash = ComputeSnapshotHash(report),
                CapturedAt = report.CreateTime,
                CapturedById = report.ReporterUserId,
                CapturedByName = report.ReporterUserName,
                CreateTime = nowUtc,
                CreateBy = "DbMigrate",
                CreateId = 0
            }).ExecuteCommand();
        }

        var eventExists = db.Queryable<ContentModerationCaseEvent>()
            .Any(item => item.CaseId == moderationCase.Id && item.RelatedReportId == report.Id && item.EventType == "ReportAttached");
        if (!eventExists)
        {
            var nextSequence = db.Queryable<ContentModerationCaseEvent>()
                .Where(item => item.CaseId == moderationCase.Id)
                .Max(item => (int?)item.EventSequence) ?? 0;
            db.Insertable(new ContentModerationCaseEvent
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = report.TenantId,
                CaseId = moderationCase.Id,
                EventSequence = nextSequence + 1,
                EventType = "ReportAttached",
                ExpectedCaseVersion = moderationCase.Version,
                ResultCaseVersion = moderationCase.Version,
                RelatedReportId = report.Id,
                FromStatus = moderationCase.Status,
                ToStatus = moderationCase.Status,
                ResultCode = "LegacyBackfill",
                ActorUserId = report.ReporterUserId,
                ActorName = report.ReporterUserName,
                CreateTime = report.CreateTime
            }).ExecuteCommand();
        }
    }

    private static void BackfillActionsAndStates(ISqlSugarClient db, DateTime nowUtc)
    {
        if (!db.DbMaintenance.IsAnyTable("UserModerationAction", false))
        {
            return;
        }

        var actions = db.Queryable<UserModerationAction>().OrderBy(item => item.CreateTime).OrderBy(item => item.Id).ToList();
        foreach (var action in actions.Where(item => string.IsNullOrWhiteSpace(item.OperationKey)))
        {
            action.OperationKey = $"legacy:{action.Id}";
            action.ResultCode = "LegacyBackfill";
            db.Updateable(action).UpdateColumns(item => new { item.OperationKey, item.ResultCode }).ExecuteCommand();
        }

        var activeActions = actions
            .Where(item => item.IsActive && !item.IsDeleted)
            .Where(item => !item.EndTime.HasValue || item.EndTime.Value > nowUtc)
            .Where(item => item.ActionType is (int)ModerationActionTypeEnum.Mute or (int)ModerationActionTypeEnum.Ban)
            .GroupBy(item => new { item.TenantId, item.TargetUserId, item.ActionType });
        foreach (var group in activeActions)
        {
            var policyType = group.Key.ActionType == (int)ModerationActionTypeEnum.Mute
                ? UserModerationPolicyType.Mute
                : UserModerationPolicyType.Ban;
            if (db.Queryable<UserModerationState>().Any(item =>
                    item.TenantId == group.Key.TenantId &&
                    item.TargetUserId == group.Key.TargetUserId &&
                    item.PolicyType == (int)policyType))
            {
                continue;
            }

            var ordered = group.OrderByDescending(item => item.StartTime).ThenByDescending(item => item.Id).ToList();
            var permanent = ordered.Any(item => !item.EndTime.HasValue);
            var currentAction = ordered[0];
            var effectiveFrom = ordered.Min(item => item.StartTime);
            var effectiveUntil = permanent ? null : ordered.Max(item => item.EndTime);
            var calibrationAction = new UserModerationAction
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = group.Key.TenantId,
                OperationKey = $"migration-state:{group.Key.TenantId}:{group.Key.TargetUserId}:{(int)policyType}",
                PreviousStateVersion = 0,
                ResultStateVersion = 1,
                SupersedesActionId = currentAction.Id,
                ResultCode = "LegacyStateCalibrated",
                TargetUserId = group.Key.TargetUserId,
                TargetUserName = currentAction.TargetUserName,
                ActionType = group.Key.ActionType,
                Reason = "DbMigrate 校准历史有效治理状态",
                StartTime = effectiveFrom,
                EndTime = effectiveUntil,
                IsActive = true,
                CreateTime = nowUtc,
                CreateBy = "DbMigrate",
                CreateId = 0
            };
            db.Insertable(calibrationAction).ExecuteCommand();
            db.Insertable(new UserModerationState
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                TenantId = group.Key.TenantId,
                TargetUserId = group.Key.TargetUserId,
                PolicyType = (int)policyType,
                State = (int)UserModerationStateValue.Active,
                EffectiveFrom = effectiveFrom,
                EffectiveUntil = effectiveUntil,
                Version = 1,
                CurrentActionId = calibrationAction.Id,
                SourceCaseId = currentAction.CaseId,
                CreateTime = nowUtc,
                CreateBy = "DbMigrate",
                CreateId = 0
            }).ExecuteCommand();
        }
    }

    private static string BuildOpenTargetKey(int targetType, long targetContentId) => $"{targetType}:{targetContentId}";

    private static string ComputeSnapshotHash(ContentReport report)
    {
        var value = string.Join("\n", report.ReportTargetType, report.TargetContentId, report.TargetUserId,
            report.TargetSnapshotTitle ?? string.Empty, report.TargetSnapshotSummary ?? string.Empty);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }
}
