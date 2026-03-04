import { useCallback, useMemo, useState } from 'react';
import {
  batchGetReactionSummary,
  getReactionSummary,
  toggleReaction,
  type ReactionSummaryVo,
} from '@/api/forum';

interface ReactionTogglePayload {
  emojiType: 'unicode' | 'sticker';
  emojiValue: string;
}

interface LoadCommentOptions {
  replace?: boolean;
}

interface UseReactionsOptions {
  onError?: (message: string) => void;
}

const normalizeItems = (items: ReactionSummaryVo[]): ReactionSummaryVo[] =>
  (items || []).filter((item) => item.voCount > 0);

const splitBatches = (ids: number[], batchSize: number): number[][] => {
  const batches: number[][] = [];
  for (let index = 0; index < ids.length; index += batchSize) {
    batches.push(ids.slice(index, index + batchSize));
  }
  return batches;
};

const applyOptimisticToggle = (
  items: ReactionSummaryVo[],
  payload: ReactionTogglePayload
): ReactionSummaryVo[] => {
  const next = [...items];
  const targetIndex = next.findIndex(
    (item) => item.voEmojiType === payload.emojiType && item.voEmojiValue === payload.emojiValue
  );

  if (targetIndex < 0) {
    return [
      ...next,
      {
        voEmojiType: payload.emojiType,
        voEmojiValue: payload.emojiValue,
        voCount: 1,
        voIsReacted: true,
      },
    ];
  }

  const target = next[targetIndex];
  if (target.voIsReacted) {
    const nextCount = (target.voCount || 0) - 1;
    if (nextCount <= 0) {
      next.splice(targetIndex, 1);
      return next;
    }

    next[targetIndex] = {
      ...target,
      voCount: nextCount,
      voIsReacted: false,
    };
    return next;
  }

  next[targetIndex] = {
    ...target,
    voCount: (target.voCount || 0) + 1,
    voIsReacted: true,
  };
  return next;
};

export const useReactions = (options: UseReactionsOptions = {}) => {
  const { onError } = options;
  const [postItems, setPostItems] = useState<ReactionSummaryVo[]>([]);
  const [commentItemsMap, setCommentItemsMap] = useState<Record<number, ReactionSummaryVo[]>>({});
  const [loadingPost, setLoadingPost] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [pendingTargets, setPendingTargets] = useState<Record<string, boolean>>({});

  const reportError = useCallback(
    (error: unknown, fallback: string) => {
      const message = error instanceof Error ? error.message : fallback;
      onError?.(message);
    },
    [onError]
  );

  const loadPostReactions = useCallback(
    async (postId: number) => {
      if (!postId) {
        setPostItems([]);
        return;
      }

      setLoadingPost(true);
      try {
        const summary = await getReactionSummary('Post', postId);
        setPostItems(normalizeItems(summary));
      } catch (error) {
        setPostItems([]);
        reportError(error, '加载帖子回应失败');
      } finally {
        setLoadingPost(false);
      }
    },
    [reportError]
  );

  const loadCommentReactions = useCallback(
    async (commentIds: number[], options?: LoadCommentOptions) => {
      const uniqueIds = Array.from(new Set((commentIds || []).filter((id) => id > 0)));
      if (uniqueIds.length === 0) {
        if (options?.replace) {
          setCommentItemsMap({});
        }
        return;
      }

      setLoadingComments(true);
      try {
        const mergedSummaryMap: Record<string, ReactionSummaryVo[]> = {};
        const batches = splitBatches(uniqueIds, 100);
        const responses = await Promise.all(
          batches.map((batchIds) =>
            batchGetReactionSummary({
              targetType: 'Comment',
              targetIds: batchIds,
            })
          )
        );

        for (const responseMap of responses) {
          for (const [targetId, summary] of Object.entries(responseMap)) {
            mergedSummaryMap[targetId] = summary;
          }
        }

        setCommentItemsMap((prev) => {
          const next: Record<number, ReactionSummaryVo[]> = options?.replace ? {} : { ...prev };
          for (const commentId of uniqueIds) {
            next[commentId] = normalizeItems(mergedSummaryMap[String(commentId)] || []);
          }
          return next;
        });
      } catch (error) {
        reportError(error, '批量加载评论回应失败');
      } finally {
        setLoadingComments(false);
      }
    },
    [reportError]
  );

  const togglePostReaction = useCallback(
    async (postId: number, payload: ReactionTogglePayload) => {
      const pendingKey = `Post:${postId}`;
      if (pendingTargets[pendingKey]) {
        return;
      }

      const snapshot = postItems;
      setPendingTargets((prev) => ({ ...prev, [pendingKey]: true }));
      setPostItems((current) => applyOptimisticToggle(current, payload));

      try {
        const summary = await toggleReaction({
          targetType: 'Post',
          targetId: postId,
          emojiType: payload.emojiType,
          emojiValue: payload.emojiValue,
        });

        setPostItems(normalizeItems(summary));
      } catch (error) {
        setPostItems(snapshot);
        reportError(error, '帖子回应失败');
        throw error;
      } finally {
        setPendingTargets((prev) => {
          const next = { ...prev };
          delete next[pendingKey];
          return next;
        });
      }
    },
    [pendingTargets, postItems, reportError]
  );

  const toggleCommentReaction = useCallback(
    async (commentId: number, payload: ReactionTogglePayload) => {
      const pendingKey = `Comment:${commentId}`;
      if (pendingTargets[pendingKey]) {
        return;
      }

      const snapshot = commentItemsMap[commentId] || [];
      setPendingTargets((prev) => ({ ...prev, [pendingKey]: true }));
      setCommentItemsMap((prev) => ({
        ...prev,
        [commentId]: applyOptimisticToggle(prev[commentId] || [], payload),
      }));

      try {
        const summary = await toggleReaction({
          targetType: 'Comment',
          targetId: commentId,
          emojiType: payload.emojiType,
          emojiValue: payload.emojiValue,
        });

        setCommentItemsMap((prev) => ({
          ...prev,
          [commentId]: normalizeItems(summary),
        }));
      } catch (error) {
        setCommentItemsMap((prev) => ({
          ...prev,
          [commentId]: snapshot,
        }));
        reportError(error, '评论回应失败');
        throw error;
      } finally {
        setPendingTargets((prev) => {
          const next = { ...prev };
          delete next[pendingKey];
          return next;
        });
      }
    },
    [commentItemsMap, pendingTargets, reportError]
  );

  const isPending = useCallback(
    (targetType: 'Post' | 'Comment', targetId: number): boolean => {
      return Boolean(pendingTargets[`${targetType}:${targetId}`]);
    },
    [pendingTargets]
  );

  const commentReactionCount = useMemo(
    () => Object.keys(commentItemsMap).length,
    [commentItemsMap]
  );

  return {
    postItems,
    commentItemsMap,
    loadingPost,
    loadingComments,
    commentReactionCount,
    loadPostReactions,
    loadCommentReactions,
    togglePostReaction,
    toggleCommentReaction,
    isPending,
  };
};
