import type { ForumContentLongId, ForumContentRevisionIntegrityStatus, ForumContentRevisionSourceType } from './forum-content-revision-contract';

export const ForumQuestionErrorCode = {
  NotFound: 'Forum.QuestionNotFound',
  AnswerNotFound: 'Forum.AnswerNotFound',
  AccessDenied: 'Forum.AnswerAccessDenied',
  Conflict: 'Forum.AnswerRevisionConflict',
  AcceptanceConflict: 'Forum.QuestionAcceptanceConflict',
  AcceptedAnswerLocked: 'Forum.AcceptedAnswerLocked',
  AttachmentUnavailable: 'Forum.AnswerAttachmentUnavailable',
  RevisionIncomplete: 'Forum.AnswerRevisionIncomplete',
  InteractionUnavailable: 'UserBlock.InteractionUnavailable',
} as const;

export type ForumQuestionErrorCodeValue =
  typeof ForumQuestionErrorCode[keyof typeof ForumQuestionErrorCode];

export type PostAnswerSort = 'default' | 'latest';

export interface PostAnswerVo {
  voAnswerId: ForumContentLongId;
  voPublicId: string;
  voPostId: ForumContentLongId;
  voAuthorId: ForumContentLongId;
  voAuthorPublicId?: string | null;
  voAuthorName: string;
  voAuthorAvatarUrl?: string | null;
  voContent: string;
  voIsAccepted: boolean;
  voContentRevision: number;
  voEditCount: number;
  voIsEnabled: boolean;
  voCanEdit: boolean;
  voCanDelete: boolean;
  voCanReport: boolean;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface GetPostAnswerPageRequest {
  postIdentifier: string;
  pageIndex?: number;
  pageSize?: number;
  sort?: PostAnswerSort;
}

export interface CreatePostAnswerRequest {
  postIdentifier: string;
  content: string;
  clientSubmissionId: string;
}

export interface PostAnswerPageVo {
  voPostPublicId: string;
  voIsSolved: boolean;
  voAcceptedAnswerPublicId?: string | null;
  voAcceptedAnswer?: PostAnswerVo | null;
  voAcceptanceRevision: number;
  voTotal: number;
  voOtherTotal: number;
  voPageIndex: number;
  voPageSize: number;
  voItems: PostAnswerVo[];
}

export interface UpdatePostAnswerRequest {
  answerPublicId: string;
  content: string;
  expectedContentRevision: number;
  clientSubmissionId: string;
}

export interface DeletePostAnswerRequest {
  answerPublicId: string;
  expectedContentRevision: number;
  clientSubmissionId: string;
}

export interface RestorePostAnswerRevisionRequest {
  answerPublicId: string;
  revisionNumber: number;
  expectedContentRevision: number;
  clientSubmissionId: string;
}

export interface ChangePostAnswerAcceptanceRequest {
  postIdentifier: string;
  answerPublicId: string;
  expectedAcceptanceRevision: number;
  clientSubmissionId: string;
}

export interface RevokePostAnswerAcceptanceRequest {
  postIdentifier: string;
  expectedAcceptanceRevision: number;
  clientSubmissionId: string;
}

export interface PostAnswerMutationVo {
  voPostPublicId: string;
  voAnswer: PostAnswerVo;
  voAnswerCount: number;
}

export interface PostAnswerAcceptanceMutationVo {
  voPostPublicId: string;
  voAcceptedAnswerPublicId?: string | null;
  voAcceptanceRevision: number;
  voIsSolved: boolean;
}

export interface PostAnswerRevisionSummaryVo {
  voRevisionNumber: number;
  voSourceType: ForumContentRevisionSourceType;
  voIntegrityStatus: ForumContentRevisionIntegrityStatus;
  voIsCurrent: boolean;
  voCanRestore: boolean;
  voRestoredFromRevisionNumber?: number | null;
  voCreateTime: string;
  voEditorName: string;
}

export interface PostAnswerRevisionListVo {
  voAnswerPublicId: string;
  voCurrentContentRevision: number;
  voItems: PostAnswerRevisionSummaryVo[];
}

export interface PostAnswerRevisionDetailVo {
  voAnswerPublicId: string;
  voRevisionNumber: number;
  voSourceType: ForumContentRevisionSourceType;
  voIntegrityStatus: ForumContentRevisionIntegrityStatus;
  voContent: string;
  voExpectedContentRevision: number;
  voCreateTime: string;
  voEditorName: string;
}
