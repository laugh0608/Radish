import type {
  CommentReplyTarget,
  CreateLotteryRequest,
  CreatePollRequest,
} from '@/api/forum';
import type { ForumContentLongId } from '@radish/http';
import type { LongId } from '@/api/user';

export function buildPostSubmissionFingerprint(
  title: string,
  content: string,
  categoryId: LongId,
  tagNames: string[],
  isQuestion?: boolean,
  poll?: CreatePollRequest | null,
  lottery?: CreateLotteryRequest | null
): string {
  return JSON.stringify({
    title: title.trim(),
    content: content.trim(),
    categoryId: String(categoryId),
    tagNames: [...tagNames].map(tag => tag.trim()).sort(),
    isQuestion: Boolean(isQuestion),
    poll: poll ?? null,
    lottery: lottery ?? null
  });
}

export function buildCommentSubmissionFingerprint(
  postId: LongId,
  content: string,
  replyTo: CommentReplyTarget | null
): string {
  return JSON.stringify({
    postId: String(postId),
    content: content.trim(),
    parentId: replyTo?.parentCommentId == null ? null : String(replyTo.parentCommentId),
    replyToCommentId: replyTo?.targetCommentId == null ? null : String(replyTo.targetCommentId),
    replyToCommentSnapshot: replyTo?.contentSnapshot ?? null,
    replyToUserName: replyTo?.authorName ?? null
  });
}

export function buildAnswerSubmissionFingerprint(postId: LongId, content: string): string {
  return JSON.stringify({
    postId: String(postId),
    content: content.trim()
  });
}

export function buildAnswerCreateFingerprint(postIdentifier: string, content: string): string {
  return JSON.stringify({
    postIdentifier: postIdentifier.trim().toLowerCase(),
    content: content.trim(),
  });
}

export function buildAnswerEditFingerprint(
  answerPublicId: string,
  content: string,
  expectedContentRevision: number,
): string {
  return JSON.stringify({
    answerPublicId: answerPublicId.trim().toLowerCase(),
    content: content.trim(),
    expectedContentRevision,
  });
}

export function buildAnswerDeleteFingerprint(
  answerPublicId: string,
  expectedContentRevision: number,
): string {
  return JSON.stringify({
    answerPublicId: answerPublicId.trim().toLowerCase(),
    expectedContentRevision,
  });
}

export function buildAnswerRestoreFingerprint(
  answerPublicId: string,
  revisionNumber: number,
  expectedContentRevision: number,
): string {
  return JSON.stringify({
    answerPublicId: answerPublicId.trim().toLowerCase(),
    revisionNumber,
    expectedContentRevision,
  });
}

export function buildAnswerAcceptanceFingerprint(
  postIdentifier: string,
  answerPublicId: string,
  expectedAcceptanceRevision: number,
  action: 'accept' | 'revoke',
): string {
  return JSON.stringify({
    postIdentifier: postIdentifier.trim().toLowerCase(),
    answerPublicId: answerPublicId.trim().toLowerCase(),
    expectedAcceptanceRevision,
    action,
  });
}

export function buildPostEditSubmissionFingerprint(
  postId: LongId,
  title: string,
  content: string,
  categoryId: LongId,
  tagNames: string[],
  expectedContentRevision: number
): string {
  return JSON.stringify({
    postId: String(postId),
    title: title.trim(),
    content: content.trim(),
    categoryId: String(categoryId),
    tagNames: [...tagNames].map(tag => tag.trim()).sort(),
    expectedContentRevision
  });
}

export function buildCommentEditSubmissionFingerprint(
  commentId: LongId,
  content: string,
  expectedContentRevision: number
): string {
  return JSON.stringify({
    commentId: String(commentId),
    content: content.trim(),
    expectedContentRevision
  });
}

export function buildContentRevisionRestoreFingerprint(
  targetKind: 'post' | 'comment',
  targetId: ForumContentLongId,
  revisionId: ForumContentLongId,
  expectedContentRevision: number
): string {
  return JSON.stringify({
    targetKind,
    targetId: String(targetId),
    revisionId: String(revisionId),
    expectedContentRevision
  });
}
