using System.Diagnostics.CodeAnalysis;
using Radish.Model;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class ContentModerationMapper
{
    // ContentReport → ContentReportVo
    [MapProperty(nameof(ContentReport.Id), nameof(ContentReportVo.VoId))]
    [MapperIgnoreTarget(nameof(ContentReportVo.VoTargetType))]
    [MapProperty(nameof(ContentReport.TargetContentId), nameof(ContentReportVo.VoTargetContentId))]
    [MapProperty(nameof(ContentReport.TargetSnapshotPostId), nameof(ContentReportVo.VoTargetSnapshotPostId))]
    [MapProperty(nameof(ContentReport.TargetSnapshotChannelId), nameof(ContentReportVo.VoTargetSnapshotChannelId))]
    [MapProperty(nameof(ContentReport.TargetSnapshotProductId), nameof(ContentReportVo.VoTargetSnapshotProductId))]
    [MapProperty(nameof(ContentReport.TargetSnapshotTitle), nameof(ContentReportVo.VoTargetSnapshotTitle))]
    [MapProperty(nameof(ContentReport.TargetSnapshotSummary), nameof(ContentReportVo.VoTargetSnapshotSummary))]
    [MapProperty(nameof(ContentReport.TargetUserId), nameof(ContentReportVo.VoTargetUserId))]
    [MapProperty(nameof(ContentReport.TargetUserName), nameof(ContentReportVo.VoTargetUserName))]
    [MapProperty(nameof(ContentReport.ReporterUserId), nameof(ContentReportVo.VoReporterUserId))]
    [MapProperty(nameof(ContentReport.ReporterUserName), nameof(ContentReportVo.VoReporterUserName))]
    [MapProperty(nameof(ContentReport.ReasonType), nameof(ContentReportVo.VoReasonType))]
    [MapProperty(nameof(ContentReport.ReasonDetail), nameof(ContentReportVo.VoReasonDetail))]
    [MapProperty(nameof(ContentReport.Status), nameof(ContentReportVo.VoStatus))]
    [MapProperty(nameof(ContentReport.ReviewActionType), nameof(ContentReportVo.VoReviewActionType))]
    [MapProperty(nameof(ContentReport.ReviewDurationHours), nameof(ContentReportVo.VoReviewDurationHours))]
    [MapProperty(nameof(ContentReport.ReviewRemark), nameof(ContentReportVo.VoReviewRemark))]
    [MapProperty(nameof(ContentReport.ReviewedById), nameof(ContentReportVo.VoReviewedById))]
    [MapProperty(nameof(ContentReport.ReviewedByName), nameof(ContentReportVo.VoReviewedByName))]
    [MapProperty(nameof(ContentReport.ReviewedAt), nameof(ContentReportVo.VoReviewedAt))]
    [MapProperty(nameof(ContentReport.CreateTime), nameof(ContentReportVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial ContentReportVo? ToContentReportVo(ContentReport? source);

    public static partial List<ContentReportVo> ToContentReportVoList(IEnumerable<ContentReport>? source);

    // UserModerationAction → UserModerationActionVo
    [MapProperty(nameof(UserModerationAction.Id), nameof(UserModerationActionVo.VoActionId))]
    [MapProperty(nameof(UserModerationAction.TargetUserId), nameof(UserModerationActionVo.VoTargetUserId))]
    [MapProperty(nameof(UserModerationAction.TargetUserName), nameof(UserModerationActionVo.VoTargetUserName))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoActionType))]
    [MapProperty(nameof(UserModerationAction.Reason), nameof(UserModerationActionVo.VoReason))]
    [MapProperty(nameof(UserModerationAction.SourceReportId), nameof(UserModerationActionVo.VoSourceReportId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetType))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetContentId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetPostId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetCommentId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetChannelId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetMessageId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetProductId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetNavigationStatus))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetNavigationMessage))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetSnapshotTitle))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetSnapshotSummary))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoSourceReportTargetSnapshotIsPersisted))]
    [MapProperty(nameof(UserModerationAction.DurationHours), nameof(UserModerationActionVo.VoDurationHours))]
    [MapProperty(nameof(UserModerationAction.StartTime), nameof(UserModerationActionVo.VoStartTime))]
    [MapProperty(nameof(UserModerationAction.EndTime), nameof(UserModerationActionVo.VoEndTime))]
    [MapProperty(nameof(UserModerationAction.IsActive), nameof(UserModerationActionVo.VoIsActive))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoOperatorUserId))]
    [MapperIgnoreTarget(nameof(UserModerationActionVo.VoOperatorUserName))]
    [MapProperty(nameof(UserModerationAction.CreateTime), nameof(UserModerationActionVo.VoCreateTime))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial UserModerationActionVo? ToUserModerationActionVo(UserModerationAction? source);

    public static partial List<UserModerationActionVo> ToUserModerationActionVoList(IEnumerable<UserModerationAction>? source);

}
