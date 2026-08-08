import { useCallback, useEffect, useMemo, useRef, useState, type Dispatch, type SetStateAction } from 'react';
import { useTranslation } from 'react-i18next';
import type { CommentNode } from '@/api/forum';
import type { LongId } from '@/api/user';
import { commentHub, type CommentTypingRealtimeEvent } from '@/services/commentHub';
import { resolveVisibleUserDisplayName } from '@/utils/userIdentityDisplay';
import {
  applyCommentHighlightEvent,
  removeCommentFromTree,
  updateCommentLikeCount,
  upsertCommentInTree,
} from '@/apps/forum/utils/commentRealtimeTree';
import { isSameLongId } from './publicForumUtils';

type RootCommentSort = 'newest' | 'hottest' | null;

interface UsePublicForumCommentRealtimeOptions {
  postId?: LongId | null;
  commentSortBy: RootCommentSort;
  setComments: Dispatch<SetStateAction<CommentNode[]>>;
  setCommentTotal: Dispatch<SetStateAction<number>>;
  onPostCommentCountDelta: (delta: number) => void;
}

export const usePublicForumCommentRealtime = ({
  postId,
  commentSortBy,
  setComments,
  setCommentTotal,
  onPostCommentCountDelta,
}: UsePublicForumCommentRealtimeOptions) => {
  const { t, i18n } = useTranslation();
  const [typingUserNames, setTypingUserNames] = useState<string[]>([]);
  const countedRootCommentIdsRef = useRef(new Set<string>());
  const deletedRootCommentIdsRef = useRef(new Set<string>());
  const typingUsersRef = useRef(new Map<string, string>());
  const typingTimersRef = useRef(new Map<string, number>());

  const syncCountedRootComments = useCallback((rootComments: CommentNode[]) => {
    countedRootCommentIdsRef.current = new Set(rootComments.map((comment) => String(comment.voId)));
    deletedRootCommentIdsRef.current.clear();
  }, []);

  const registerLoadedRootComments = useCallback((rootComments: CommentNode[]) => {
    for (const comment of rootComments) {
      countedRootCommentIdsRef.current.add(String(comment.voId));
    }
  }, []);

  const registerRootCommentCount = useCallback((commentId: LongId, parentCommentId?: LongId | null): boolean => {
    if (parentCommentId) {
      return false;
    }
    const commentKey = String(commentId);
    if (countedRootCommentIdsRef.current.has(commentKey)) {
      return false;
    }
    countedRootCommentIdsRef.current.add(commentKey);
    deletedRootCommentIdsRef.current.delete(commentKey);
    return true;
  }, []);

  const registerRootCommentRemoval = useCallback((commentId: LongId, parentCommentId?: LongId | null): boolean => {
    if (parentCommentId) {
      return false;
    }
    const commentKey = String(commentId);
    if (deletedRootCommentIdsRef.current.has(commentKey)) {
      return false;
    }
    deletedRootCommentIdsRef.current.add(commentKey);
    countedRootCommentIdsRef.current.delete(commentKey);
    return true;
  }, []);

  const syncTypingUsers = useCallback(() => {
    setTypingUserNames([...typingUsersRef.current.values()]);
  }, []);

  const clearTypingUsers = useCallback(() => {
    for (const timer of typingTimersRef.current.values()) {
      window.clearTimeout(timer);
    }
    typingTimersRef.current.clear();
    typingUsersRef.current.clear();
    setTypingUserNames([]);
  }, []);

  const registerTypingUser = useCallback((payload: CommentTypingRealtimeEvent) => {
    if (!postId || !isSameLongId(payload.voPostId, postId)) {
      return;
    }
    const userKey = String(payload.voUserId);
    const userName = resolveVisibleUserDisplayName({ voUserName: payload.voUserName }, t('common.unknownUser'));
    const oldTimer = typingTimersRef.current.get(userKey);
    if (oldTimer) {
      window.clearTimeout(oldTimer);
    }
    typingUsersRef.current.set(userKey, userName);
    typingTimersRef.current.set(userKey, window.setTimeout(() => {
      typingUsersRef.current.delete(userKey);
      typingTimersRef.current.delete(userKey);
      syncTypingUsers();
    }, 3200));
    syncTypingUsers();
  }, [postId, syncTypingUsers, t]);

  const typingText = useMemo(() => {
    if (typingUserNames.length === 0) {
      return null;
    }
    const separator = i18n.language.startsWith('zh') ? '、' : ', ';
    return `${typingUserNames.join(separator)}${t('forum.comment.typingSuffix')}`;
  }, [i18n.language, t, typingUserNames]);

  useEffect(() => {
    if (!postId) {
      return;
    }

    void commentHub.joinPost(postId);
    const unsubscribeCreated = commentHub.subscribe('CommentCreated', (payload) => {
      if (!isSameLongId(payload.voPostId, postId) || !payload.voComment) {
        return;
      }
      const shouldIncrementTotal = registerRootCommentCount(payload.voComment.voId, payload.voComment.voParentId);
      setComments((current) => upsertCommentInTree(current, payload.voComment!, commentSortBy));
      if (shouldIncrementTotal) {
        setCommentTotal((total) => total + 1);
        onPostCommentCountDelta(1);
      }
    });
    const unsubscribeUpdated = commentHub.subscribe('CommentUpdated', (payload) => {
      if (isSameLongId(payload.voPostId, postId) && payload.voComment) {
        setComments((current) => upsertCommentInTree(current, payload.voComment!, commentSortBy));
      }
    });
    const unsubscribeDeleted = commentHub.subscribe('CommentDeleted', (payload) => {
      if (!isSameLongId(payload.voPostId, postId)) {
        return;
      }
      const shouldDecrementTotal = registerRootCommentRemoval(payload.voCommentId, payload.voParentCommentId);
      setComments((current) => removeCommentFromTree(current, payload.voCommentId));
      if (shouldDecrementTotal) {
        setCommentTotal((total) => Math.max(0, total - 1));
        onPostCommentCountDelta(-1);
      }
    });
    const unsubscribeLikeChanged = commentHub.subscribe('CommentLikeChanged', (payload) => {
      if (isSameLongId(payload.voPostId, postId) && typeof payload.voLikeCount === 'number') {
        setComments((current) => updateCommentLikeCount(current, payload.voCommentId, payload.voLikeCount!));
      }
    });
    const unsubscribeHighlightsChanged = commentHub.subscribe('CommentHighlightsChanged', (payload) => {
      if (isSameLongId(payload.voPostId, postId)) {
        setComments((current) => applyCommentHighlightEvent(current, payload));
      }
    });
    const unsubscribeTyping = commentHub.subscribe('CommentTyping', registerTypingUser);

    return () => {
      unsubscribeCreated();
      unsubscribeUpdated();
      unsubscribeDeleted();
      unsubscribeLikeChanged();
      unsubscribeHighlightsChanged();
      unsubscribeTyping();
      clearTypingUsers();
      void commentHub.leavePost(postId);
    };
  }, [
    clearTypingUsers,
    commentSortBy,
    onPostCommentCountDelta,
    postId,
    registerRootCommentCount,
    registerRootCommentRemoval,
    registerTypingUser,
    setComments,
    setCommentTotal,
  ]);

  return {
    clearTypingUsers,
    registerLoadedRootComments,
    registerRootCommentCount,
    syncCountedRootComments,
    typingText,
  };
};
