using System.Security.Cryptography;
using System.Text;
using Radish.IRepository;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.Repository;

public sealed partial class ContentModerationCaseRepository
{
    private static ContentModerationCase CreateCase(ContentModerationReportWriteCommand command, string openTargetKey)
    {
        return new ContentModerationCase
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = command.TenantId,
            TargetType = command.TargetType,
            TargetContentId = command.TargetContentId,
            TargetUserId = command.TargetUserId,
            OpenTargetKey = openTargetKey,
            OpenedAt = command.NowUtc,
            CreateTime = command.NowUtc,
            CreateBy = command.ReporterUserName,
            CreateId = command.ReporterUserId
        };
    }

    private static ContentReport CreateReport(ContentModerationReportWriteCommand command, long caseId)
    {
        return new ContentReport
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = command.TenantId,
            CaseId = caseId,
            PublicId = $"rpt_{Guid.CreateVersion7():N}",
            ReporterState = "Submitted",
            ReportTargetType = command.TargetType,
            TargetContentId = command.TargetContentId,
            TargetSnapshotPostId = command.TargetPostId,
            TargetSnapshotChannelId = command.TargetChannelId,
            TargetSnapshotTitle = command.SnapshotTitle,
            TargetSnapshotSummary = command.SnapshotSummary,
            TargetUserId = command.TargetUserId,
            TargetUserName = command.TargetUserName,
            ReporterUserId = command.ReporterUserId,
            ReporterUserName = command.ReporterUserName,
            ReasonType = command.ReasonType,
            ReasonDetail = command.ReasonDetail,
            Status = (int)ContentReportStatusEnum.Pending,
            ReviewActionType = (int)ModerationActionTypeEnum.None,
            CreateTime = command.NowUtc,
            CreateBy = command.ReporterUserName,
            CreateId = command.ReporterUserId
        };
    }

    private static ContentModerationEvidence CreateReportEvidence(
        ContentModerationReportWriteCommand command,
        long caseId,
        long reportId,
        int sequence)
    {
        return new ContentModerationEvidence
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = command.TenantId,
            CaseId = caseId,
            EvidenceSequence = sequence,
            EvidenceType = (int)ContentModerationEvidenceType.ReportSnapshot,
            SourceReportId = reportId,
            TargetState = (int)ContentModerationTargetState.Available,
            SnapshotTitle = command.SnapshotTitle,
            SnapshotSummary = command.SnapshotSummary,
            TargetUserId = command.TargetUserId,
            TargetUserName = command.TargetUserName,
            TargetPostId = command.TargetPostId,
            TargetChannelId = command.TargetChannelId,
            TargetMessageId = command.TargetType == (int)ContentReportTargetTypeEnum.ChatMessage
                ? command.TargetContentId
                : null,
            SnapshotHash = command.SnapshotHash,
            CapturedAt = command.NowUtc,
            CapturedById = command.ReporterUserId,
            CapturedByName = command.ReporterUserName,
            CreateTime = command.NowUtc,
            CreateBy = command.ReporterUserName,
            CreateId = command.ReporterUserId
        };
    }

    private static ContentModerationCaseEvent CreateEvent(
        ContentModerationCase moderationCase,
        int sequence,
        string eventType,
        long actorUserId,
        string actorName,
        long? relatedReportId = null,
        long? relatedActionId = null,
        int? fromStatus = null,
        int? toStatus = null,
        string? resultCode = null,
        string? remark = null,
        int? resultVersion = null)
    {
        return new ContentModerationCaseEvent
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            TenantId = moderationCase.TenantId,
            CaseId = moderationCase.Id,
            EventSequence = sequence,
            EventType = eventType,
            ExpectedCaseVersion = moderationCase.Version,
            ResultCaseVersion = resultVersion ?? moderationCase.Version,
            RelatedReportId = relatedReportId,
            RelatedActionId = relatedActionId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ResultCode = resultCode,
            Remark = remark,
            ActorUserId = actorUserId,
            ActorName = actorName,
            CreateTime = DateTime.UtcNow
        };
    }

    private static bool IsSameOperation(
        UserModerationAction existingAction,
        long caseId,
        ContentModerationCaseReviewWriteCommand command)
    {
        var requested = command.UserAction;
        return requested != null &&
               existingAction.CaseId == caseId &&
               existingAction.ActionType == requested.ActionType &&
               existingAction.TargetUserId == requested.TargetUserId &&
               existingAction.DurationHours == requested.DurationHours &&
               existingAction.Reason == requested.Reason;
    }

    private static bool IsSameStandaloneOperation(
        UserModerationAction existingAction,
        ContentModerationStandaloneUserActionCommand command)
    {
        return existingAction.CaseId == null &&
               existingAction.SourceReportId == command.SourceReportId &&
               existingAction.ActionType == command.ActionType &&
               existingAction.TargetUserId == command.TargetUserId &&
               existingAction.DurationHours == command.DurationHours &&
               existingAction.Reason == command.Reason;
    }

    private static bool IsSameDecisionOperation(
        ContentModerationCase moderationCase,
        ContentModerationCaseReviewWriteCommand command)
    {
        return moderationCase.Decision == command.Decision &&
               (moderationCase.TargetDisposition == command.TargetDisposition ||
                (moderationCase.TargetType == (int)ContentReportTargetTypeEnum.ChatMessage &&
                 moderationCase.TargetDisposition is
                     (int)ContentModerationTargetDisposition.ActionPending or
                     (int)ContentModerationTargetDisposition.Restricted or
                     (int)ContentModerationTargetDisposition.Unavailable)) &&
               moderationCase.PublicResultCode == command.PublicResultCode &&
               moderationCase.InternalRemark == command.InternalRemark;
    }

    private static string BuildOpenTargetKey(int targetType, long targetContentId) => $"{targetType}:{targetContentId}";

    private static string BuildOperationFingerprint(string operationKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationKey)))[..24].ToLowerInvariant();
    }

    private static void EnsureSingleRow(int affectedRows)
    {
        if (affectedRows != 1)
        {
            throw new ContentModerationConcurrencyException();
        }
    }
}
