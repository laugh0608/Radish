export type WikiLongId = string;

export const WikiDraftReviewState = {
  Editing: 0,
  Submitted: 1,
  ChangesRequested: 2,
  Applied: 3,
  Rejected: 4,
  Withdrawn: 5,
} as const;

export type WikiDraftReviewStateValue = typeof WikiDraftReviewState[keyof typeof WikiDraftReviewState];

export const WikiCollaboratorState = {
  Pending: 0,
  Accepted: 1,
  Declined: 2,
  Revoked: 3,
} as const;

export type WikiCollaboratorStateValue = typeof WikiCollaboratorState[keyof typeof WikiCollaboratorState];

export const WikiReviewAction = {
  RequestChanges: 'RequestChanges',
  Reject: 'Reject',
  Apply: 'Apply',
} as const;

export type WikiReviewActionValue = typeof WikiReviewAction[keyof typeof WikiReviewAction];

export interface WikiAuthorDocumentVo {
  voDocumentId: WikiLongId;
  voDraftId?: WikiLongId | null;
  voTitle: string;
  voSlug: string;
  voSummary?: string | null;
  voDocumentVersion: number;
  voDraftVersion?: number | null;
  voReviewState?: WikiDraftReviewStateValue | null;
  voStatus: number;
  voAuthorRole: string;
  voCanEdit: boolean;
  voCanSubmit: boolean;
  voCanManageCollaborators: boolean;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface WikiDocumentCollaboratorVo {
  voId: WikiLongId;
  voDocumentId: WikiLongId;
  voUserPublicId: string;
  voUserName: string;
  voRole: number;
  voInviteState: WikiCollaboratorStateValue;
  voInvitedAt: string;
  voRespondedAt?: string | null;
}

export interface WikiDocumentReviewEventVo {
  voId: WikiLongId;
  voAction: string;
  voActorName: string;
  voComment?: string | null;
  voDocumentVersion: number;
  voDraftVersion: number;
  voCreateTime: string;
}

export interface WikiAuthorDraftDetailVo {
  voDocumentId: WikiLongId;
  voDraftId: WikiLongId;
  voOwnerUserId?: WikiLongId | null;
  voOwnerUserPublicId: string;
  voOwnerUserName: string;
  voTitle: string;
  voSlug: string;
  voSummary?: string | null;
  voMarkdownContent: string;
  voCoverAttachmentId?: WikiLongId | null;
  voProposedParentId?: WikiLongId | null;
  voDocumentVersion: number;
  voBaseDocumentVersion: number;
  voDraftVersion: number;
  voReviewState: WikiDraftReviewStateValue;
  voDocumentStatus: number;
  voAuthorRole: string;
  voCanEdit: boolean;
  voCanSubmit: boolean;
  voCanManageCollaborators: boolean;
  voReadOnlyReason?: string | null;
  voChangeSummary?: string | null;
  voReviewComment?: string | null;
  voSubmittedAt?: string | null;
  voCollaborators: WikiDocumentCollaboratorVo[];
  voReviewEvents: WikiDocumentReviewEventVo[];
}

export interface WikiReviewQueueItemVo {
  voDocumentId: WikiLongId;
  voDraftId: WikiLongId;
  voOwnerUserId?: WikiLongId | null;
  voOwnerUserPublicId: string;
  voOwnerUserName: string;
  voTitle: string;
  voSlug: string;
  voDocumentVersion: number;
  voBaseDocumentVersion: number;
  voDraftVersion: number;
  voReviewState: WikiDraftReviewStateValue;
  voChangeSummary?: string | null;
  voSubmittedAt?: string | null;
}

export interface CreateWikiAuthorDraftRequest {
  title: string;
  slug?: string;
  summary?: string;
  markdownContent: string;
  coverAttachmentId?: WikiLongId | null;
  proposedParentId?: WikiLongId | null;
  changeSummary?: string;
}

export interface SaveWikiAuthorDraftRequest extends CreateWikiAuthorDraftRequest {
  expectedDraftVersion: number;
}

export interface SubmitWikiDraftRequest {
  expectedDraftVersion: number;
  changeSummary?: string;
}

export interface ReviewWikiDraftRequest {
  action: WikiReviewActionValue;
  expectedDraftVersion: number;
  expectedDocumentVersion: number;
  comment?: string;
  finalParentId?: WikiLongId | null;
}
