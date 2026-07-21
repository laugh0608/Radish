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
  voTargetSnapshotTitle?: string | null;
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
