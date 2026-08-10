import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ApiResponseError,
  ProductReviewErrorCode,
  createApiResponseError,
  type ProductReviewSummaryVo,
} from '@radish/http';
import { useTranslation } from 'react-i18next';
import {
  deleteProductReview,
  getMyProductReview,
  getProductReviews,
  upsertProductReview,
  type MyProductReviewVo,
  type ProductReviewPageVo,
  type ProductReviewVo,
} from '@/api/shop';
import type { LongId } from '@/api/user';
import { log } from '@/utils/logger';

const REVIEW_PAGE_SIZE = 5;

interface ProductReviewDraft {
  rating: number;
  comment: string;
}

const emptyDraft: ProductReviewDraft = {
  rating: 0,
  comment: '',
};

export interface PublicProductReviewController {
  summary: ProductReviewSummaryVo | null;
  reviews: ProductReviewVo[];
  total: number;
  pageIndex: number;
  totalPages: number;
  reviewsLoading: boolean;
  reviewsError: string | null;
  reviewsStale: boolean;
  myReviewState: MyProductReviewVo | null;
  myReviewLoading: boolean;
  myReviewError: string | null;
  myReviewUnavailableMessage: string | null;
  editorOpen: boolean;
  draft: ProductReviewDraft;
  dirty: boolean;
  mutationBusy: boolean;
  mutationError: string | null;
  versionConflict: boolean;
  openEditor: () => void;
  closeEditor: () => void;
  setDraftRating: (rating: number) => void;
  setDraftComment: (comment: string) => void;
  submitReview: () => Promise<void>;
  removeReview: () => Promise<void>;
  retryReviews: () => void;
  retryMyReview: () => void;
  refreshAfterConflict: () => void;
  setPageIndex: (page: number) => void;
}

interface UsePublicProductReviewsOptions {
  productId: LongId;
  loggedIn: boolean;
  authReady: boolean;
  onDirtyChange?: (dirty: boolean) => void;
}

function createDraft(review?: ProductReviewVo | null): ProductReviewDraft {
  if (!review) {
    return emptyDraft;
  }

  return {
    rating: review.voRating,
    comment: review.voComment ?? '',
  };
}

function draftsEqual(left: ProductReviewDraft, right: ProductReviewDraft): boolean {
  return left.rating === right.rating && left.comment === right.comment;
}

export function usePublicProductReviews({
  productId,
  loggedIn,
  authReady,
  onDirtyChange,
}: UsePublicProductReviewsOptions): PublicProductReviewController {
  const { t } = useTranslation();
  const publicRequestIdRef = useRef(0);
  const myRequestIdRef = useRef(0);
  const reviewPageRef = useRef<ProductReviewPageVo | null>(null);
  const [reviewPage, setReviewPage] = useState<ProductReviewPageVo | null>(null);
  const [pageIndex, setPageIndexState] = useState(1);
  const [reviewsLoading, setReviewsLoading] = useState(true);
  const [reviewsError, setReviewsError] = useState<string | null>(null);
  const [reviewsStale, setReviewsStale] = useState(false);
  const [reviewsReloadToken, setReviewsReloadToken] = useState(0);
  const [myReviewState, setMyReviewState] = useState<MyProductReviewVo | null>(null);
  const [myReviewLoading, setMyReviewLoading] = useState(false);
  const [myReviewError, setMyReviewError] = useState<string | null>(null);
  const [myReviewReloadToken, setMyReviewReloadToken] = useState(0);
  const [editorOpen, setEditorOpen] = useState(false);
  const [draft, setDraft] = useState<ProductReviewDraft>(emptyDraft);
  const [baselineDraft, setBaselineDraft] = useState<ProductReviewDraft>(emptyDraft);
  const [mutationBusy, setMutationBusy] = useState(false);
  const [mutationError, setMutationError] = useState<string | null>(null);
  const [versionConflict, setVersionConflict] = useState(false);
  const dirty = editorOpen && !draftsEqual(draft, baselineDraft);
  const myReviewUnavailableMessage = useMemo(() => {
    const unavailableReason = myReviewState?.voUnavailableReason?.trim();
    if (!unavailableReason) {
      return null;
    }

    if (unavailableReason === '该评价已被治理限制') {
      return t('shop.review.moderated');
    }

    if (unavailableReason === '仅已完成订单的购买者可以评价') {
      return t('shop.review.purchaseRequired');
    }

    return t('shop.review.eligibilityLoadFailed');
  }, [myReviewState?.voUnavailableReason, t]);

  reviewPageRef.current = reviewPage;

  useEffect(() => {
    publicRequestIdRef.current += 1;
    myRequestIdRef.current += 1;
    setReviewPage(null);
    setPageIndexState(1);
    setReviewsError(null);
    setReviewsStale(false);
    setMyReviewState(null);
    setMyReviewError(null);
    setEditorOpen(false);
    setDraft(emptyDraft);
    setBaselineDraft(emptyDraft);
    setMutationError(null);
    setVersionConflict(false);
  }, [productId]);

  useEffect(() => {
    onDirtyChange?.(dirty);
    return () => onDirtyChange?.(false);
  }, [dirty, onDirtyChange]);

  useEffect(() => {
    const requestId = ++publicRequestIdRef.current;

    const loadReviews = async () => {
      setReviewsLoading(true);
      setReviewsError(null);
      setReviewsStale(false);
      if (reviewPageRef.current?.voPageIndex !== pageIndex) {
        setReviewPage(null);
      }

      try {
        const response = await getProductReviews(productId, t, pageIndex, REVIEW_PAGE_SIZE);
        if (requestId !== publicRequestIdRef.current) {
          return;
        }

        if (!response.ok || !response.data) {
          throw createApiResponseError(response, t('shop.review.loadFailed'));
        }

        setReviewPage(response.data);
      } catch (error) {
        if (requestId !== publicRequestIdRef.current) {
          return;
        }

        const currentPage = reviewPageRef.current;
        const canKeepCurrentPage = currentPage?.voPageIndex === pageIndex;
        if (!canKeepCurrentPage) {
          setReviewPage(null);
        }
        setReviewsStale(canKeepCurrentPage);
        setReviewsError(error instanceof Error ? error.message : t('shop.review.loadFailed'));
        log.warn('usePublicProductReviews', '加载公开商品评价失败', error);
      } finally {
        if (requestId === publicRequestIdRef.current) {
          setReviewsLoading(false);
        }
      }
    };

    void loadReviews();
  }, [pageIndex, productId, reviewsReloadToken, t]);

  useEffect(() => {
    const requestId = ++myRequestIdRef.current;

    if (!authReady || !loggedIn) {
      setMyReviewState(null);
      setMyReviewError(null);
      setMyReviewLoading(false);
      return;
    }

    const loadMyReview = async () => {
      setMyReviewLoading(true);
      setMyReviewError(null);

      try {
        const response = await getMyProductReview(productId, t);
        if (requestId !== myRequestIdRef.current) {
          return;
        }

        if (!response.ok || !response.data) {
          throw createApiResponseError(response, t('shop.review.eligibilityLoadFailed'));
        }

        setMyReviewState(response.data);
      } catch (error) {
        if (requestId !== myRequestIdRef.current) {
          return;
        }

        setMyReviewState(null);
        setMyReviewError(error instanceof Error ? error.message : t('shop.review.eligibilityLoadFailed'));
        log.warn('usePublicProductReviews', '加载本人商品评价资格失败', error);
      } finally {
        if (requestId === myRequestIdRef.current) {
          setMyReviewLoading(false);
        }
      }
    };

    void loadMyReview();
  }, [authReady, loggedIn, myReviewReloadToken, productId, t]);

  const openEditor = useCallback(() => {
    if (!myReviewState?.voCanReview) {
      return;
    }

    const nextDraft = createDraft(myReviewState.voReview);
    setDraft(nextDraft);
    setBaselineDraft(nextDraft);
    setMutationError(null);
    setVersionConflict(false);
    setEditorOpen(true);
  }, [myReviewState]);

  const closeEditor = useCallback(() => {
    if (dirty && !window.confirm(t('shop.review.leaveConfirm'))) {
      return;
    }

    setEditorOpen(false);
    setMutationError(null);
    setVersionConflict(false);
  }, [dirty, t]);

  const submitReview = useCallback(async () => {
    if (mutationBusy || myReviewLoading) {
      return;
    }

    if (!myReviewState?.voCanReview) {
      setMutationError(
        myReviewUnavailableMessage || t('shop.review.eligibilityLoadFailed'),
      );
      return;
    }

    if (!Number.isInteger(draft.rating) || draft.rating < 1 || draft.rating > 5) {
      setMutationError(t('shop.review.ratingRequired'));
      return;
    }

    const normalizedComment = draft.comment.trim();
    if (normalizedComment.length > 500) {
      setMutationError(t('shop.review.commentTooLong'));
      return;
    }

    setMutationBusy(true);
    setMutationError(null);
    setVersionConflict(false);

    try {
      const response = await upsertProductReview(productId, {
        rating: draft.rating,
        comment: normalizedComment.length > 0 ? normalizedComment : null,
        expectedVersion: myReviewState.voExpectedVersion,
      }, t);
      if (!response.ok || !response.data) {
        throw createApiResponseError(response, t('shop.review.saveFailed'));
      }

      const savedDraft = createDraft(response.data);
      setDraft(savedDraft);
      setBaselineDraft(savedDraft);
      setEditorOpen(false);
      setPageIndexState(1);
      setReviewsReloadToken((current) => current + 1);
      setMyReviewReloadToken((current) => current + 1);
    } catch (error) {
      const conflict = error instanceof ApiResponseError
        && error.code === ProductReviewErrorCode.VersionConflict;
      setVersionConflict(conflict);
      setMutationError(
        conflict
          ? null
          : error instanceof ApiResponseError && error.code === ProductReviewErrorCode.PurchaseRequired
            ? t('shop.review.purchaseRequired')
            : error instanceof ApiResponseError && error.code === ProductReviewErrorCode.Moderated
              ? t('shop.review.moderated')
              : t('shop.review.saveFailed'),
      );
      log.warn('usePublicProductReviews', '保存商品评价失败', error);
    } finally {
      setMutationBusy(false);
    }
  }, [draft, mutationBusy, myReviewLoading, myReviewState, myReviewUnavailableMessage, productId, t]);

  const removeReview = useCallback(async () => {
    const review = myReviewState?.voReview;
    if (!review || mutationBusy || myReviewLoading || !window.confirm(t('shop.review.deleteConfirm'))) {
      return;
    }

    setMutationBusy(true);
    setMutationError(null);
    setVersionConflict(false);

    try {
      const response = await deleteProductReview(
        review.voId,
        myReviewState.voExpectedVersion,
        t,
      );
      if (!response.ok || !response.data) {
        throw createApiResponseError(response, t('shop.review.deleteFailed'));
      }

      setEditorOpen(false);
      setDraft(emptyDraft);
      setBaselineDraft(emptyDraft);
      setPageIndexState(1);
      setReviewsReloadToken((current) => current + 1);
      setMyReviewReloadToken((current) => current + 1);
    } catch (error) {
      const conflict = error instanceof ApiResponseError
        && error.code === ProductReviewErrorCode.VersionConflict;
      setVersionConflict(conflict);
      setMutationError(
        conflict
          ? null
          : error instanceof ApiResponseError && error.code === ProductReviewErrorCode.Moderated
            ? t('shop.review.moderated')
            : t('shop.review.deleteFailed'),
      );
      log.warn('usePublicProductReviews', '删除商品评价失败', error);
    } finally {
      setMutationBusy(false);
    }
  }, [mutationBusy, myReviewLoading, myReviewState, t]);

  const totalPages = useMemo(
    () => Math.max(1, Math.ceil((reviewPage?.voTotal ?? 0) / REVIEW_PAGE_SIZE)),
    [reviewPage?.voTotal],
  );

  const setPageIndex = useCallback((page: number) => {
    setPageIndexState(Math.min(Math.max(1, page), totalPages));
  }, [totalPages]);

  const refreshAfterConflict = useCallback(() => {
    setVersionConflict(false);
    setMutationError(null);
    setMyReviewReloadToken((current) => current + 1);
  }, []);

  return {
    summary: reviewPage?.voSummary ?? null,
    reviews: reviewPage?.voItems ?? [],
    total: reviewPage?.voTotal ?? 0,
    pageIndex,
    totalPages,
    reviewsLoading,
    reviewsError,
    reviewsStale,
    myReviewState,
    myReviewLoading,
    myReviewError,
    myReviewUnavailableMessage,
    editorOpen,
    draft,
    dirty,
    mutationBusy,
    mutationError,
    versionConflict,
    openEditor,
    closeEditor,
    setDraftRating: (rating) => setDraft((current) => ({ ...current, rating })),
    setDraftComment: (comment) => setDraft((current) => ({ ...current, comment })),
    submitReview,
    removeReview,
    retryReviews: () => setReviewsReloadToken((current) => current + 1),
    retryMyReview: () => setMyReviewReloadToken((current) => current + 1),
    refreshAfterConflict,
    setPageIndex,
  };
}
