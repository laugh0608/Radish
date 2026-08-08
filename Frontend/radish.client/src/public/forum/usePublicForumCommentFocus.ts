import { useCallback, useEffect, useRef, useState } from 'react';
import type { CommentNode } from '@/api/forum';
import type { LongId } from '@/api/user';
import type { PublicForumCommentNavigationTarget } from './publicForumDetailTypes';

interface UsePublicForumCommentFocusOptions {
  navigationTarget: PublicForumCommentNavigationTarget | null;
  navigationNotice: string | null;
  comments: CommentNode[];
}

export const usePublicForumCommentFocus = ({
  navigationTarget,
  navigationNotice,
  comments,
}: UsePublicForumCommentFocusOptions) => {
  const [highlightedCommentId, setHighlightedCommentId] = useState<LongId | null>(null);
  const anchorMapRef = useRef(new Map<string, HTMLDivElement>());
  const handledNavigationRef = useRef<string | null>(null);
  const highlightTimeoutRef = useRef<number | null>(null);
  const noticeRef = useRef<HTMLDivElement | null>(null);

  const registerCommentAnchor = (commentId: LongId, element: HTMLDivElement | null) => {
    const commentKey = String(commentId);
    if (element) {
      anchorMapRef.current.set(commentKey, element);
    } else {
      anchorMapRef.current.delete(commentKey);
    }
  };
  const resetHighlight = useCallback(() => setHighlightedCommentId(null), []);

  useEffect(() => () => {
    if (highlightTimeoutRef.current !== null) {
      window.clearTimeout(highlightTimeoutRef.current);
    }
  }, []);

  useEffect(() => {
    if (!navigationTarget?.commentId) {
      return;
    }
    const signature = `${navigationTarget.navigationKey}:${navigationTarget.commentId}`;
    if (handledNavigationRef.current === signature) {
      return;
    }
    const targetElement = anchorMapRef.current.get(String(navigationTarget.commentId));
    if (!targetElement) {
      return;
    }

    handledNavigationRef.current = signature;
    targetElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
    setHighlightedCommentId(navigationTarget.commentId);
    if (highlightTimeoutRef.current !== null) {
      window.clearTimeout(highlightTimeoutRef.current);
    }
    highlightTimeoutRef.current = window.setTimeout(() => {
      setHighlightedCommentId((current) => current === navigationTarget.commentId ? null : current);
    }, 3200);
  }, [comments, navigationTarget]);

  useEffect(() => {
    if (!navigationNotice) {
      return;
    }
    const frameId = window.requestAnimationFrame(() => {
      noticeRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
    return () => window.cancelAnimationFrame(frameId);
  }, [navigationNotice]);

  return {
    highlightedCommentId,
    noticeRef,
    registerCommentAnchor,
    resetHighlight,
  };
};
