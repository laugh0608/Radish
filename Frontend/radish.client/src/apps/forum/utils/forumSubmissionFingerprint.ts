import type {
  CommentReplyTarget,
  CreateLotteryRequest,
  CreatePollRequest,
} from '@/api/forum';
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

export function buildPostEditSubmissionFingerprint(
  postId: LongId,
  title: string,
  content: string,
  categoryId: LongId,
  tagNames: string[]
): string {
  return JSON.stringify({
    postId: String(postId),
    title: title.trim(),
    content: content.trim(),
    categoryId: String(categoryId),
    tagNames: [...tagNames].map(tag => tag.trim()).sort()
  });
}

export function buildCommentEditSubmissionFingerprint(commentId: LongId, content: string): string {
  return JSON.stringify({
    commentId: String(commentId),
    content: content.trim()
  });
}
