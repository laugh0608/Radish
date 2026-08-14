import { useCallback, useEffect, useRef, useState } from 'react';
import type {
  ContentRewardLongId,
  ContentRewardTargetRequest,
  ContentRewardTargetStateVo,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getContentRewardTargetStates } from '@/api/contentReward';
import type { CommentNode } from '@/api/forum';
import { log } from '@/utils/logger';
import {
  buildContentRewardTargetKey,
  collectCommentRewardTargets,
  mapContentRewardTargetStates,
} from '../utils/contentRewardState';

interface UseContentRewardStatesOptions {
  postId: ContentRewardLongId | null;
  comments: CommentNode[];
  viewerKey: string;
  t: TFunction;
  logSource: string;
}

export const useContentRewardStates = ({
  postId,
  comments,
  viewerKey,
  t,
  logSource,
}: UseContentRewardStatesOptions) => {
  const [stateMap, setStateMap] = useState<Record<string, ContentRewardTargetStateVo>>({});
  const sessionRef = useRef(0);

  useEffect(() => {
    sessionRef.current += 1;
    setStateMap({});
  }, [postId, viewerKey]);

  const loadStates = useCallback(async (targets: ContentRewardTargetRequest[]) => {
    if (targets.length === 0) {
      return;
    }

    const session = sessionRef.current;
    try {
      const states = await getContentRewardTargetStates(targets, t);
      if (session !== sessionRef.current) {
        return;
      }
      setStateMap((current) => ({
        ...current,
        ...mapContentRewardTargetStates(states),
      }));
    } catch (error) {
      if (session === sessionRef.current) {
        log.error(logSource, '加载内容赞赏批量状态失败', error);
      }
    }
  }, [logSource, t]);

  useEffect(() => {
    if (postId == null || String(postId) === '0') {
      return;
    }

    void loadStates([
      { targetType: 'Post', targetId: postId },
      ...collectCommentRewardTargets(comments),
    ]);
  }, [comments, loadStates, postId, viewerKey]);

  const handleStateChange = useCallback((state: ContentRewardTargetStateVo) => {
    setStateMap((current) => ({
      ...current,
      [buildContentRewardTargetKey(state.voTargetType, state.voTargetId)]: state,
    }));
  }, []);

  const handleTargetsVisible = useCallback((targets: ContentRewardTargetRequest[]) => {
    const missingTargets = targets.filter((target) => !stateMap[
      buildContentRewardTargetKey(target.targetType, target.targetId)
    ]);
    if (missingTargets.length > 0) {
      void loadStates(missingTargets);
    }
  }, [loadStates, stateMap]);

  return {
    stateMap,
    handleStateChange,
    handleTargetsVisible,
  };
};
