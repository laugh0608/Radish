import type { CommentNode } from '@/api/forum';
import type { LongId } from '@/api/user';

export function findForumCommentById(
  comments: CommentNode[],
  commentId: LongId | null
): CommentNode | null {
  if (commentId == null) {
    return null;
  }

  const stack = [...comments];
  while (stack.length > 0) {
    const comment = stack.pop();
    if (!comment) {
      continue;
    }
    if (String(comment.voId) === String(commentId)) {
      return comment;
    }
    if (comment.voChildren?.length) {
      stack.push(...comment.voChildren);
    }
  }

  return null;
}
