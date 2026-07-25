export type ModerationLongId = string;
export type ContentModerationTargetType = 'Post' | 'Comment' | 'PostQuickReply' | 'ChatMessage' | 'Product';
export type ContentModerationCaseStatus = 'Open' | 'Reviewing' | 'Resolved';
export type ContentModerationDecision = 'None' | 'NoViolation' | 'Violation' | 'InsufficientEvidence';
export type ContentModerationTargetDisposition =
  | 'None'
  | 'Keep'
  | 'Restricted'
  | 'Unavailable'
  | 'ActionPending'
  | 'ActionFailed';

export interface ContentReportReceiptVo {
  voReportPublicId: string;
  voTargetType: ContentModerationTargetType | 'Unknown';
  voTargetContentId: ModerationLongId;
  voTargetPostId?: ModerationLongId | null;
  voTargetCommentId?: ModerationLongId | null;
  voTargetChannelId?: ModerationLongId | null;
  voTargetMessageId?: ModerationLongId | null;
  voTargetNavigationStatus: 'Ready' | 'Fallback' | 'Unavailable' | 'Unsupported';
  voTargetNavigationMessage?: string | null;
  voTargetSnapshotTitle?: string | null;
  voTargetSnapshotSummary?: string | null;
  voReasonType: string;
  voReporterState: 'Submitted' | 'Resolved';
  voPublicResultCode?: string | null;
  voSubmittedAt: string;
  voResolvedAt?: string | null;
  voIsDuplicate: boolean;
}

export interface ContentModerationCaseQueueItemVo {
  voCasePublicId: string;
  voTargetType: ContentModerationTargetType | 'Unknown';
  voTargetContentId: ModerationLongId;
  voTargetUserId: ModerationLongId;
  voStatus: ContentModerationCaseStatus;
  voDecision: ContentModerationDecision;
  voTargetDisposition: ContentModerationTargetDisposition;
  voVersion: number;
  voReportCount: number;
  voOpenedAt: string;
  voModifiedAt?: string | null;
}

export interface ContentModerationCaseReportVo {
  voReportPublicId: string;
  voReporterUserId: ModerationLongId;
  voReporterUserName: string;
  voReasonType: string;
  voReasonDetail?: string | null;
  voCreateTime: string;
}

export interface ContentModerationEvidenceVo {
  voSequence: number;
  voEvidenceType: 'ReportSnapshot' | 'CurrentTargetSnapshot' | 'ModeratorNote' | 'ActionResult';
  voTargetState: 'Available' | 'Deleted' | 'Recalled' | 'Disabled' | 'Unavailable';
  voSnapshotTitle?: string | null;
  voSnapshotSummary?: string | null;
  voContentRevision?: number | null;
  voTargetModifiedAt?: string | null;
  voSnapshotHash: string;
  voCapturedAt: string;
}

export interface ContentModerationCaseEventVo {
  voSequence: number;
  voEventType: string;
  voExpectedCaseVersion: number;
  voResultCaseVersion: number;
  voResultCode?: string | null;
  voRemark?: string | null;
  voActorUserId: ModerationLongId;
  voActorName: string;
  voCreateTime: string;
}

export interface UserModerationStateVo {
  voPolicyType: 'Mute' | 'Ban';
  voState: 'Inactive' | 'Active';
  voIsEffective: boolean;
  voEffectiveUntil?: string | null;
  voVersion: number;
}

export interface ContentModerationCaseDetailVo {
  voCase: ContentModerationCaseQueueItemVo;
  voReports: ContentModerationCaseReportVo[];
  voEvidence: ContentModerationEvidenceVo[];
  voEvents: ContentModerationCaseEventVo[];
  voUserStates: UserModerationStateVo[];
  voPublicResultCode?: string | null;
  voInternalRemark?: string | null;
  voResolvedAt?: string | null;
}

export interface CaptureContentModerationEvidenceRequest {
  casePublicId: string;
  expectedVersion: number;
  evidenceType: 2 | 3;
  snapshotTitle?: string;
  snapshotSummary?: string;
}

export interface ContentModerationCaseUserActionRequest {
  actionType: 1 | 2 | 3 | 4;
  expectedStateVersion: number;
  durationHours?: number | null;
  reason: string;
}

export interface ReviewContentModerationCaseRequest {
  casePublicId: string;
  expectedVersion: number;
  decision: 1 | 2 | 3;
  targetDisposition: 1 | 2 | 3;
  expectedTargetVersion?: number | null;
  publicResultCode: string;
  internalRemark?: string;
  userAction?: ContentModerationCaseUserActionRequest | null;
  operationKey: string;
}

export interface ContentModerationCaseReviewResultVo {
  voCasePublicId: string;
  voStatus: ContentModerationCaseStatus;
  voDecision: ContentModerationDecision;
  voTargetDisposition: ContentModerationTargetDisposition;
  voVersion: number;
  voUserActionId?: ModerationLongId | null;
  voUserStateVersion?: number | null;
  voIsIdempotentReplay: boolean;
}

export interface ApplyContentModerationCorrectiveActionRequest {
  casePublicId: string;
  expectedVersion: number;
  userAction: ContentModerationCaseUserActionRequest;
  operationKey: string;
  remark: string;
}

export type ContentModerationAppealStatus =
  | 'Submitted'
  | 'Reviewing'
  | 'ReliefPending'
  | 'ReliefFailed'
  | 'Resolved'
  | 'Withdrawn';
export type ContentModerationAppealOutcome = 'None' | 'Upheld' | 'PartiallyGranted' | 'Granted';
export type ContentModerationReliefScope = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7;

export interface ContentModerationDecisionNoticeVo {
  voCasePublicId: string;
  voTargetType: ContentModerationTargetType;
  voTargetContentId: ModerationLongId;
  voPublicResultCode?: string | null;
  voEligibleScope: ContentModerationReliefScope;
  voResolvedAt: string;
  voEligibleUntilUtc: string;
  voAppealPublicId?: string | null;
  voAppealStatus?: ContentModerationAppealStatus | null;
}

export interface ContentModerationAppealEventVo {
  voSequence: number;
  voEventType: string;
  voResultCode?: string | null;
  voRemark?: string | null;
  voActorUserId: ModerationLongId;
  voActorName: string;
  voCreateTime: string;
}

export interface ContentModerationTargetActionVo {
  voActionType: 'Restrict' | 'Restore';
  voStatus: 'Pending' | 'Succeeded' | 'Failed' | 'Superseded' | 'NoEffect';
  voResultCode?: string | null;
  voChangedTargetState: boolean;
  voRequestedAt: string;
  voCompletedAt?: string | null;
}

export interface ContentModerationAppealVo {
  voAppealPublicId: string;
  voCasePublicId: string;
  voStatus: ContentModerationAppealStatus;
  voOutcome: ContentModerationAppealOutcome;
  voEligibleScope: ContentModerationReliefScope;
  voGrantedScope: ContentModerationReliefScope;
  voVersion: number;
  voStatement: string;
  voPublicResultCode?: string | null;
  voPublicResultSummary?: string | null;
  voInternalRemark?: string | null;
  voSubmittedAt: string;
  voEligibleUntilUtc: string;
  voResolvedAt?: string | null;
  voEvents: ContentModerationAppealEventVo[];
  voTargetActions: ContentModerationTargetActionVo[];
  voUserActions: unknown[];
  voIsIdempotentReplay: boolean;
}

export interface SubmitContentModerationAppealRequest {
  casePublicId: string;
  statement: string;
  operationKey: string;
}

export interface ContentModerationAppealVersionedOperationRequest {
  appealPublicId: string;
  expectedVersion: number;
  operationKey: string;
}

export interface ReviewContentModerationAppealRequest
  extends ContentModerationAppealVersionedOperationRequest {
  outcome: 1 | 2 | 3;
  grantedScope: ContentModerationReliefScope;
  publicResultSummary: string;
  internalRemark?: string | null;
}

export interface CaptureContentModerationAppealEvidenceRequest
  extends ContentModerationAppealVersionedOperationRequest {
  snapshotTitle?: string | null;
  snapshotSummary?: string | null;
}
