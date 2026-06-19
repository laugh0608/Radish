import type { CommentNode } from '@/api/forum';
import type { LongId } from '@/api/user';
import type { CommentHighlightRealtimeEvent } from '@/services/commentHub';

type RootCommentSort = 'newest' | 'hottest' | null;

export function isSameCommentId(left: LongId | null | undefined, right: LongId | null | undefined): boolean {
  if (left == null || right == null) {
    return false;
  }

  return String(left) === String(right);
}

export function hasCommentInTree(comments: CommentNode[], commentId: LongId): boolean {
  return comments.some((comment) => {
    if (isSameCommentId(comment.voId, commentId)) {
      return true;
    }

    return hasCommentInTree(comment.voChildren ?? [], commentId);
  });
}

export function upsertCommentInTree(
  comments: CommentNode[],
  incomingComment: CommentNode,
  sortBy: RootCommentSort
): CommentNode[] {
  const parentId = incomingComment.voParentId ?? null;

  if (!parentId) {
    const updated = comments.map((comment) => (
      isSameCommentId(comment.voId, incomingComment.voId)
        ? { ...incomingComment, voChildren: comment.voChildren ?? incomingComment.voChildren ?? [] }
        : comment
    ));

    if (updated.some((comment) => isSameCommentId(comment.voId, incomingComment.voId))) {
      return sortRootComments(updated, sortBy);
    }

    const next = sortBy === 'newest'
      ? [incomingComment, ...comments]
      : [...comments, incomingComment];
    return sortRootComments(next, sortBy);
  }

  let handled = false;
  const next = comments.map((root) => {
    if (!isSameCommentId(root.voId, parentId)) {
      const children = root.voChildren ?? [];
      if (!children.some((child) => isSameCommentId(child.voId, incomingComment.voId))) {
        return root;
      }

      handled = true;
      return {
        ...root,
        voChildren: children.map((child) => (
          isSameCommentId(child.voId, incomingComment.voId)
            ? { ...incomingComment, voChildren: child.voChildren ?? incomingComment.voChildren ?? [] }
            : child
        ))
      };
    }

    const children = root.voChildren ?? [];
    const childExists = children.some((child) => isSameCommentId(child.voId, incomingComment.voId));
    handled = true;

    return {
      ...root,
      voChildren: childExists
        ? children.map((child) => (
            isSameCommentId(child.voId, incomingComment.voId)
              ? { ...incomingComment, voChildren: child.voChildren ?? incomingComment.voChildren ?? [] }
              : child
          ))
        : [...children, incomingComment],
      voChildrenTotal: childExists
        ? root.voChildrenTotal
        : Math.max(children.length + 1, (root.voChildrenTotal ?? children.length) + 1)
    };
  });

  return handled ? next : comments;
}

export function removeCommentFromTree(comments: CommentNode[], commentId: LongId): CommentNode[] {
  return removeCommentFromTreeWithCount(comments, commentId).comments;
}

function removeCommentFromTreeWithCount(
  comments: CommentNode[],
  commentId: LongId
): { comments: CommentNode[]; removedDirectCount: number; changed: boolean } {
  let removedDirectCount = 0;
  let changed = false;
  const nextComments: CommentNode[] = [];

  for (const comment of comments) {
    if (isSameCommentId(comment.voId, commentId)) {
      removedDirectCount += 1;
      changed = true;
      continue;
    }

    const children = comment.voChildren ?? [];
    if (children.length === 0) {
      nextComments.push(comment);
      continue;
    }

    const childResult = removeCommentFromTreeWithCount(children, commentId);
    if (!childResult.changed) {
      nextComments.push(comment);
      continue;
    }

    changed = true;
    nextComments.push({
      ...comment,
      voChildren: childResult.comments,
      voChildrenTotal: Math.max(0, (comment.voChildrenTotal ?? children.length) - childResult.removedDirectCount)
    });
  }

  return {
    comments: changed ? nextComments : comments,
    removedDirectCount,
    changed
  };
}

export function updateCommentLikeCount(
  comments: CommentNode[],
  commentId: LongId,
  likeCount: number
): CommentNode[] {
  return comments.map((comment) => {
    if (isSameCommentId(comment.voId, commentId)) {
      return {
        ...comment,
        voLikeCount: likeCount
      };
    }

    const children = comment.voChildren ?? [];
    if (children.length === 0) {
      return comment;
    }

    return {
      ...comment,
      voChildren: updateCommentLikeCount(children, commentId, likeCount)
    };
  });
}

export function applyCommentHighlightEvent(
  comments: CommentNode[],
  event: CommentHighlightRealtimeEvent
): CommentNode[] {
  const currentIds = new Set((event.voCurrentCommentIds ?? []).map(String));
  const isGodComment = event.voHighlightType === 1;
  const parentCommentId = event.voParentCommentId ?? null;

  return comments.map((comment) => {
    if (isGodComment) {
      return {
        ...comment,
        voIsGodComment: currentIds.has(String(comment.voId)),
        voHighlightRank: currentIds.has(String(comment.voId)) ? comment.voHighlightRank ?? 1 : undefined
      };
    }

    if (!isSameCommentId(comment.voId, parentCommentId)) {
      return comment;
    }

    return {
      ...comment,
      voChildren: (comment.voChildren ?? []).map((child) => ({
        ...child,
        voIsSofa: currentIds.has(String(child.voId)),
        voHighlightRank: currentIds.has(String(child.voId)) ? child.voHighlightRank ?? 1 : undefined
      }))
    };
  });
}

function sortRootComments(comments: CommentNode[], sortBy: RootCommentSort): CommentNode[] {
  return [...comments].sort((left, right) => {
    const topDiff = Number(right.voIsTop ?? false) - Number(left.voIsTop ?? false);
    if (topDiff !== 0) {
      return topDiff;
    }

    const leftTime = new Date(left.voCreateTime || 0).getTime();
    const rightTime = new Date(right.voCreateTime || 0).getTime();

    if (sortBy === 'hottest') {
      const likeDiff = (right.voLikeCount || 0) - (left.voLikeCount || 0);
      if (likeDiff !== 0) {
        return likeDiff;
      }
      return rightTime - leftTime;
    }

    if (sortBy === 'newest') {
      return rightTime - leftTime;
    }

    return leftTime - rightTime;
  });
}
