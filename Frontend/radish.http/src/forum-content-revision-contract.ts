export type ForumContentLongId = string | number;

export type ForumContentRevisionSourceType = 'Baseline' | 'Edit' | 'Restore';

export type ForumContentRevisionIntegrityStatus = 'Complete' | 'LegacyIncomplete' | 'Redacted';

export const ForumContentRevisionErrorCode = {
  NotFound: 'Forum.RevisionNotFound',
  AccessDenied: 'Forum.RevisionAccessDenied',
  Incomplete: 'Forum.RevisionIncomplete',
  Conflict: 'Forum.RevisionConflict',
  CategoryUnavailable: 'Forum.RevisionCategoryUnavailable',
  TagUnavailable: 'Forum.RevisionTagUnavailable',
  AttachmentUnavailable: 'Forum.RevisionAttachmentUnavailable',
  ContentRejected: 'Forum.RevisionContentRejected',
  EditLimitReached: 'Forum.RevisionEditLimitReached',
  CommentWindowExpired: 'Forum.CommentRevisionWindowExpired',
  RestoreKeyConflict: 'Forum.RevisionRestoreKeyConflict',
} as const;

export type ForumContentRevisionErrorCodeValue =
  typeof ForumContentRevisionErrorCode[keyof typeof ForumContentRevisionErrorCode];

export interface ForumContentRevisionTagVo {
  voTagId: ForumContentLongId;
  voTagName: string;
  voSortOrder: number;
}

export interface ForumContentRevisionSummaryVo {
  voRevisionId: ForumContentLongId;
  voRevisionNumber: number;
  voSourceType: ForumContentRevisionSourceType;
  voIntegrityStatus: ForumContentRevisionIntegrityStatus;
  voRestoredFromRevisionId?: ForumContentLongId | null;
  voRestoredFromRevisionNumber?: number | null;
  voEditorId: ForumContentLongId;
  voEditorName: string;
  voCreateTime: string;
  voIsCurrent: boolean;
  voCanViewSnapshot: boolean;
  voCanRestore: boolean;
  voUnavailableReasonCode?: string | null;
}

export interface ForumContentRevisionListVo<TSummary extends ForumContentRevisionSummaryVo> {
  voIsEdited: boolean;
  voEditCount: number;
  voCurrentContentRevision: number;
  voLastEditedAt?: string | null;
  voCanViewDetails: boolean;
  voItems: TSummary[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export type PostContentRevisionSummaryVo = ForumContentRevisionSummaryVo;

export type CommentContentRevisionSummaryVo = ForumContentRevisionSummaryVo;

export type PostContentRevisionListVo = ForumContentRevisionListVo<PostContentRevisionSummaryVo>;

export type CommentContentRevisionListVo = ForumContentRevisionListVo<CommentContentRevisionSummaryVo>;

export interface PostContentRevisionDetailVo {
  voSummary: PostContentRevisionSummaryVo;
  voPostId: ForumContentLongId;
  voTitle: string;
  voContent: string;
  voContentType: string;
  voCategoryId: ForumContentLongId;
  voCategoryName: string;
  voCoverAttachmentId?: ForumContentLongId | null;
  voTags: ForumContentRevisionTagVo[];
  voAttachmentIds: ForumContentLongId[];
  voExpectedContentRevision: number;
}

export interface CommentContentRevisionDetailVo {
  voSummary: CommentContentRevisionSummaryVo;
  voCommentId: ForumContentLongId;
  voPostId: ForumContentLongId;
  voContent: string;
  voAttachmentIds: ForumContentLongId[];
  voExpectedContentRevision: number;
}

export interface RestoreForumContentRevisionRequest {
  targetId: ForumContentLongId;
  revisionId: ForumContentLongId;
  expectedContentRevision: number;
  clientSubmissionId: string;
}

export interface ForumContentRevisionWriteResult {
  voTargetId: ForumContentLongId;
  voRevisionId: ForumContentLongId;
  voContentRevision: number;
}
