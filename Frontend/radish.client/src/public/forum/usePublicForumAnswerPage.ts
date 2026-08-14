import { useEffect, useRef, useState } from 'react';
import {
  createApiResponseError,
  getPostAnswerPage,
  type PostAnswerSort,
  type PostAnswerPageVo,
} from '@radish/http';
import type { TFunction } from 'i18next';

const ANSWER_PAGE_SIZE = 10;

interface UsePublicForumAnswerPageOptions {
  enabled: boolean;
  postIdentifier: string;
  pageIndex: number;
  sort: PostAnswerSort;
  targetAnswerPublicId?: string | null;
  reloadToken: number;
  viewerKey: string;
  t: TFunction;
  onStateChange: (pageIndex: number, sort: PostAnswerSort, replace?: boolean) => void;
}

export const usePublicForumAnswerPage = ({
  enabled,
  postIdentifier,
  pageIndex,
  sort,
  targetAnswerPublicId,
  reloadToken,
  viewerKey,
  t,
  onStateChange,
}: UsePublicForumAnswerPageOptions) => {
  const [answerPage, setAnswerPage] = useState<PostAnswerPageVo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [targetUnavailable, setTargetUnavailable] = useState(false);
  const requestIdRef = useRef(0);
  const handledTargetRef = useRef<string | null>(null);

  useEffect(() => {
    handledTargetRef.current = null;
    setTargetUnavailable(false);
  }, [targetAnswerPublicId]);

  useEffect(() => {
    if (!enabled) {
      setAnswerPage(null);
      setError(null);
      setLoading(false);
      setTargetUnavailable(false);
      return;
    }

    const requestId = ++requestIdRef.current;
    const safeRequestedPage = Math.max(1, pageIndex);

    const requestPage = async (requestedPage: number): Promise<PostAnswerPageVo> => {
      const response = await getPostAnswerPage({
        postIdentifier,
        pageIndex: requestedPage,
        pageSize: ANSWER_PAGE_SIZE,
        sort,
      });
      if (!response.ok || !response.data) {
        throw createApiResponseError(response, t('forum.answerLifecycle.loadFailed'));
      }
      return response.data;
    };

    const loadAnswers = async () => {
      setLoading(true);
      setError(null);
      try {
        const shouldLocateTarget = Boolean(targetAnswerPublicId)
          && handledTargetRef.current !== targetAnswerPublicId;

        if (shouldLocateTarget && targetAnswerPublicId) {
          let candidatePageIndex = 1;
          let totalPages = 1;
          do {
            const candidate = await requestPage(candidatePageIndex);
            if (requestId !== requestIdRef.current) {
              return;
            }
            totalPages = Math.max(
              1,
              Math.ceil(candidate.voOtherTotal / Math.max(1, candidate.voPageSize)),
            );
            const found = candidate.voAcceptedAnswer?.voPublicId === targetAnswerPublicId
              || candidate.voItems.some((answer) => answer.voPublicId === targetAnswerPublicId);
            if (found) {
              handledTargetRef.current = targetAnswerPublicId;
              setTargetUnavailable(false);
              setAnswerPage(candidate);
              if (candidatePageIndex !== safeRequestedPage) {
                onStateChange(candidatePageIndex, sort, true);
              }
              return;
            }
            candidatePageIndex += 1;
          } while (candidatePageIndex <= totalPages);

          handledTargetRef.current = targetAnswerPublicId;
          setTargetUnavailable(true);
        }

        let page = await requestPage(safeRequestedPage);
        if (requestId !== requestIdRef.current) {
          return;
        }
        const totalPages = Math.max(
          1,
          Math.ceil(page.voOtherTotal / Math.max(1, page.voPageSize)),
        );
        if (safeRequestedPage > totalPages) {
          page = await requestPage(totalPages);
          if (requestId !== requestIdRef.current) {
            return;
          }
          onStateChange(totalPages, sort, true);
        }
        setAnswerPage(page);
      } catch (loadError) {
        if (requestId !== requestIdRef.current) {
          return;
        }
        setAnswerPage(null);
        setError(loadError instanceof Error ? loadError.message : t('forum.answerLifecycle.loadFailed'));
      } finally {
        if (requestId === requestIdRef.current) {
          setLoading(false);
        }
      }
    };

    void loadAnswers();
  }, [
    enabled,
    onStateChange,
    pageIndex,
    postIdentifier,
    reloadToken,
    sort,
    t,
    targetAnswerPublicId,
    viewerKey,
  ]);

  return {
    answerPage,
    loading,
    error,
    targetUnavailable,
  };
};
