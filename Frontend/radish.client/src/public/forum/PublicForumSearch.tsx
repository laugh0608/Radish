import { useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  getCurrentGodCommentsBatch,
  getPostList,
  type CommentHighlight,
  type PostItem,
} from '@/api/forum';
import { PostCard } from '@/apps/forum/components/PostCard';
import { createForumCommentHighlightMap, getForumCommentHighlight } from '@/utils/forumCommentHighlights';
import { log } from '@/utils/logger';
import type {
  PublicForumSearchRoute,
  PublicListSort,
  PublicSearchTimeRange,
} from '../forumRouteState';
import { createDefaultSearchRoute } from '../forumRouteState';
import { usePublicReplaceRouteSync } from '../usePublicReplaceRouteSync';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import {
  buildSearchRouteKey,
  buildSearchTimeRange,
  buildVisiblePages,
  createForumReadingGuide,
  getForumPostRouteIdentifier,
  searchGuideDefinition,
} from './publicForumUtils';
import { resolvePublicForumReadSectionState } from './publicForumViewState';
import { PublicStatusCard } from './PublicStatusCard';
import styles from './PublicForumApp.module.css';

interface PublicForumSearchProps {
  routeState: PublicForumSearchRoute;
  displayTimeZone: string;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  onScrollRestored: () => void;
  onRouteStateChange: (route: PublicForumSearchRoute, options?: { replace?: boolean }) => void;
  onOpenPost: (postId: string) => void;
  onBackToList: () => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

export const PublicForumSearch = ({
  routeState,
  displayTimeZone,
  scrollContainerRef,
  restoreScrollTop,
  onScrollRestored,
  onRouteStateChange,
  onOpenPost,
  onBackToList,
  onOpenAuthorProfile,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumSearchProps) => {
  const { t } = useTranslation();
  const [draftKeyword, setDraftKeyword] = useState(routeState.keyword);
  const [keyword, setKeyword] = useState(routeState.keyword);
  const [sortBy, setSortBy] = useState<PublicListSort>(routeState.sortBy);
  const [timeRange, setTimeRange] = useState<PublicSearchTimeRange>(routeState.timeRange);
  const [customStartDate, setCustomStartDate] = useState(routeState.startDate ?? '');
  const [customEndDate, setCustomEndDate] = useState(routeState.endDate ?? '');
  const [appliedStartDate, setAppliedStartDate] = useState(routeState.startDate ?? '');
  const [appliedEndDate, setAppliedEndDate] = useState(routeState.endDate ?? '');
  const [currentPage, setCurrentPage] = useState(routeState.page);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<string, CommentHighlight>>(new Map());
  const [totalPosts, setTotalPosts] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [postError, setPostError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [isCompactViewport, setIsCompactViewport] = useState(() =>
    typeof window !== 'undefined' ? window.innerWidth <= 720 : false
  );
  const postsRequestIdRef = useRef(0);
  const routeStateRef = useRef(routeState);

  useEffect(() => {
    routeStateRef.current = routeState;
    setDraftKeyword(routeState.keyword);
    setKeyword(routeState.keyword);
    setSortBy(routeState.sortBy);
    setTimeRange(routeState.timeRange);
    setCustomStartDate(routeState.startDate ?? '');
    setCustomEndDate(routeState.endDate ?? '');
    setAppliedStartDate(routeState.startDate ?? '');
    setAppliedEndDate(routeState.endDate ?? '');
    setCurrentPage(routeState.page);
  }, [routeState]);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const handleResize = () => {
      setIsCompactViewport(window.innerWidth <= 720);
    };

    handleResize();
    window.addEventListener('resize', handleResize);
    return () => {
      window.removeEventListener('resize', handleResize);
    };
  }, []);

  const nextSearchRoute = useMemo<PublicForumSearchRoute>(() => ({
    kind: 'search',
    keyword,
    sortBy,
    timeRange,
    startDate: timeRange === 'custom' ? appliedStartDate || undefined : undefined,
    endDate: timeRange === 'custom' ? appliedEndDate || undefined : undefined,
    page: currentPage
  }), [appliedEndDate, appliedStartDate, currentPage, keyword, sortBy, timeRange]);
  usePublicReplaceRouteSync({
    currentRouteKey: buildSearchRouteKey(routeState),
    nextRoute: nextSearchRoute,
    nextRouteKey: buildSearchRouteKey(nextSearchRoute),
    onRouteStateChange
  });

  useEffect(() => {
    if (restoreScrollTop == null || loadingPosts) {
      return;
    }

    const scrollTop = restoreScrollTop;
    const frameId = window.requestAnimationFrame(() => {
      scrollContainerRef.current?.scrollTo({ top: scrollTop, behavior: 'auto' });
      onScrollRestored();
    });

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [loadingPosts, onScrollRestored, restoreScrollTop, scrollContainerRef]);

  useEffect(() => {
    const routeForRequest = routeStateRef.current;
    const isPristineSearch = routeForRequest.keyword.length === 0
      && routeForRequest.sortBy === 'newest'
      && routeForRequest.timeRange === 'all'
      && !routeForRequest.startDate
      && !routeForRequest.endDate
      && routeForRequest.page === 1;

    if (isPristineSearch) {
      setLoadingPosts(false);
      setPostError(null);
      setPosts([]);
      setPostGodComments(new Map());
      setTotalPosts(0);
      setTotalPages(0);
      return;
    }

    const requestId = ++postsRequestIdRef.current;

    const loadPosts = async () => {
      setLoadingPosts(true);
      setPostError(null);

      try {
        const { startTime, endTime } = buildSearchTimeRange(routeForRequest);
        const pageModel = await getPostList(
          null,
          t,
          routeForRequest.page,
          20,
          routeForRequest.sortBy,
          routeForRequest.keyword,
          startTime,
          endTime
        );

        if (requestId !== postsRequestIdRef.current) {
          return;
        }

        if (pageModel.pageCount > 0 && routeForRequest.page > pageModel.pageCount) {
          setCurrentPage(pageModel.pageCount);
          return;
        }

        setPosts(pageModel.data);
        setTotalPosts(pageModel.dataCount ?? 0);
        setTotalPages(pageModel.pageCount);

        if (!pageModel.data.length) {
          setPostGodComments(new Map());
          return;
        }

        try {
          const highlights = await getCurrentGodCommentsBatch(pageModel.data.map((item) => item.voId), t);
          if (requestId !== postsRequestIdRef.current) {
            return;
          }

          setPostGodComments(createForumCommentHighlightMap(highlights));
        } catch (highlightError) {
          if (requestId !== postsRequestIdRef.current) {
            return;
          }

          log.warn('公开论坛搜索结果神评预览补查失败，已降级使用帖子主数据:', highlightError);
          setPostGodComments(new Map());
        }
      } catch (err) {
        if (requestId !== postsRequestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setPosts([]);
        setPostGodComments(new Map());
        setTotalPosts(0);
        setTotalPages(0);
        setPostError(message);
      } finally {
        if (requestId === postsRequestIdRef.current) {
          setLoadingPosts(false);
        }
      }
    };

    void loadPosts();
  }, [reloadToken, routeState, t]);

  useEffect(() => {
    const title = keyword
      ? t('forum.public.searchResultTitle', { keyword })
      : t('forum.public.searchTitle');
    document.title = `${t('desktop.apps.forum.name')} · ${title}`;
  }, [keyword, t]);

  const visiblePages = useMemo(() => {
    return buildVisiblePages(currentPage, totalPages, isCompactViewport ? 5 : 7);
  }, [currentPage, isCompactViewport, totalPages]);

  const listState = resolvePublicForumReadSectionState({
    loading: loadingPosts,
    error: postError,
    itemCount: posts.length,
    totalCount: totalPosts
  });

  const keywordSummary = keyword.trim()
    ? t('forum.public.searchKeywordSummary', { keyword })
    : t('forum.public.searchKeywordSummaryEmpty');
  const timeSummary = timeRange === '24h'
    ? t('forum.public.searchTimeRange24h')
    : timeRange === '7d'
      ? t('forum.public.searchTimeRange7d')
      : timeRange === '30d'
        ? t('forum.public.searchTimeRange30d')
        : timeRange === 'custom'
          ? t('forum.public.searchTimeRangeCustom', {
              start: appliedStartDate || t('forum.public.searchDateStartFallback'),
              end: appliedEndDate || t('forum.public.searchDateEndFallback')
            })
          : t('forum.public.searchTimeRangeAll');
  const isCustomRangeDirty = customStartDate !== appliedStartDate || customEndDate !== appliedEndDate;
  const hasActiveFilters = keyword.length > 0 || sortBy !== 'newest' || timeRange !== 'all';
  const resultTitle = keyword
    ? t('forum.public.searchResultTitle', { keyword })
    : t('forum.public.searchTitle');
  const resultIntro = hasActiveFilters
    ? t('forum.public.searchResultIntro')
    : t('forum.public.searchIntro');
  const readingGuide = useMemo(
    () => createForumReadingGuide(t, searchGuideDefinition),
    [t]
  );

  const submitSearch = () => {
    setKeyword(draftKeyword.trim());
    setCurrentPage(1);
  };

  const resetSearch = () => {
    const defaultRoute = createDefaultSearchRoute();
    setDraftKeyword(defaultRoute.keyword);
    setKeyword(defaultRoute.keyword);
    setSortBy(defaultRoute.sortBy);
    setTimeRange(defaultRoute.timeRange);
    setCustomStartDate('');
    setCustomEndDate('');
    setAppliedStartDate('');
    setAppliedEndDate('');
    setCurrentPage(defaultRoute.page);
  };

  return (
    <section className={`${styles.sectionCard} ${styles.listSectionCard}`}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>Phase 2-2</p>
          <div className={styles.searchTopbar}>
            <button type="button" className={styles.backButton} onClick={onBackToList}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('forum.backToList')}</span>
            </button>
            <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
          </div>
          <h1 className={styles.pageTitle}>{resultTitle}</h1>
          <p className={styles.pageIntro}>{resultIntro}</p>
          <div className={styles.searchSummaryRail}>
            <span className={styles.detailMetaChip}>{keywordSummary}</span>
            <span className={styles.detailMetaChip}>{t('forum.public.searchSortSummary', { sort: sortBy === 'hottest' ? t('forum.sort.hottest') : t('forum.sort.newest') })}</span>
            <span className={styles.detailMetaChip}>{timeSummary}</span>
            {totalPosts > 0 && (
              <span className={styles.detailMetaChip}>{t('forum.public.searchResultCount', { count: totalPosts })}</span>
            )}
          </div>
        </div>

        <PublicReadingGuide
          className={styles.readingGuide}
          label={readingGuide.label}
          title={readingGuide.title}
          description={readingGuide.description}
          items={readingGuide.items}
        />

        <div className={styles.searchPanel}>
          <div className={styles.searchForm}>
            <div className={styles.searchInputWrap}>
              <Icon icon="mdi:magnify" size={18} />
              <input
                type="text"
                className={styles.searchInput}
                value={draftKeyword}
                placeholder={t('forum.searchInputPlaceholder')}
                onChange={(event) => setDraftKeyword(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    event.preventDefault();
                    submitSearch();
                  }
                }}
              />
            </div>
            <button type="button" className={styles.retryButton} onClick={submitSearch}>
              {t('forum.searchSubmit')}
            </button>
            <button type="button" className={styles.secondaryButton} onClick={resetSearch}>
              {t('forum.public.searchReset')}
            </button>
          </div>

          <div className={styles.segmented}>
            <button
              type="button"
              className={`${styles.segmentButton} ${sortBy === 'newest' ? styles.segmentButtonActive : ''}`}
              onClick={() => {
                setSortBy('newest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.newest')}
            </button>
            <button
              type="button"
              className={`${styles.segmentButton} ${sortBy === 'hottest' ? styles.segmentButtonActive : ''}`}
              onClick={() => {
                setSortBy('hottest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.hottest')}
            </button>
          </div>

          <div className={styles.segmented}>
            {(['all', '24h', '7d', '30d', 'custom'] as PublicSearchTimeRange[]).map((value) => (
              <button
                key={value}
                type="button"
                className={`${styles.segmentButton} ${timeRange === value ? styles.segmentButtonActive : ''}`}
                onClick={() => {
                  setTimeRange(value);
                  setCurrentPage(1);
                  if (value !== 'custom') {
                    setAppliedStartDate('');
                    setAppliedEndDate('');
                    setCustomStartDate('');
                    setCustomEndDate('');
                  }
                }}
              >
                {value === 'all'
                  ? t('forum.public.searchTimeRangeAllLabel')
                  : value === '24h'
                    ? t('forum.public.searchTimeRange24hLabel')
                    : value === '7d'
                      ? t('forum.public.searchTimeRange7dLabel')
                      : value === '30d'
                        ? t('forum.public.searchTimeRange30dLabel')
                        : t('forum.public.searchTimeRangeCustomLabel')}
              </button>
            ))}
          </div>

          {timeRange === 'custom' && (
            <div className={styles.customRange}>
              <input
                type="date"
                className={styles.dateInput}
                value={customStartDate}
                onChange={(event) => setCustomStartDate(event.target.value)}
              />
              <span className={styles.dateSeparator}>{t('forum.public.searchDateSeparator')}</span>
              <input
                type="date"
                className={styles.dateInput}
                value={customEndDate}
                onChange={(event) => setCustomEndDate(event.target.value)}
              />
              <button
                type="button"
                className={styles.secondaryButton}
                onClick={() => {
                  setAppliedStartDate(customStartDate);
                  setAppliedEndDate(customEndDate);
                  setCurrentPage(1);
                }}
                disabled={!isCustomRangeDirty}
              >
                {t('forum.public.searchApplyDates')}
              </button>
            </div>
          )}
        </div>
      </div>

      <div className={styles.postList}>
        {listState === 'loading' ? (
          <PublicStatusCard
            tone="loading"
            title={t('forum.public.searchLoadingTitle')}
            description={t('forum.public.searchLoadingDescription')}
          />
        ) : listState === 'error' ? (
          <PublicStatusCard
            tone="error"
            title={t('forum.public.searchErrorTitle')}
            description={postError || t('forum.public.searchLoadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
          />
        ) : !hasActiveFilters ? (
          <PublicStatusCard
            tone="empty"
            title={t('forum.public.searchIdleTitle')}
            description={t('forum.public.searchIdleDescription')}
          />
        ) : listState === 'empty' ? (
          <PublicStatusCard
            tone="empty"
            title={t('forum.public.searchEmptyTitle')}
            description={t('forum.public.searchEmptyDescription')}
          />
        ) : (
          posts.map((post) => {
            const godComment = getForumCommentHighlight(postGodComments, post.voId);
            return (
              <PostCard
                key={post.voId}
                post={post}
                displayTimeZone={displayTimeZone}
                onClick={() => onOpenPost(getForumPostRouteIdentifier(post))}
                variant="publicCompact"
                onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
                onTagClick={(_, tagSlug) => onOpenTag?.(tagSlug)}
                onQuestionClick={onOpenQuestion}
                onPollClick={onOpenPoll}
                onLotteryClick={onOpenLottery}
                godComment={godComment ? {
                  authorName: godComment.voAuthorName,
                  content: godComment.voContentSnapshot
                } : null}
              />
            );
          })
        )}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            type="button"
            className={styles.paginationButton}
            onClick={() => setCurrentPage((page: number) => Math.max(1, page - 1))}
            disabled={currentPage === 1}
          >
            ‹
          </button>
          {visiblePages.map((page) => (
            <button
              key={page}
              type="button"
              className={`${styles.paginationButton} ${page === currentPage ? styles.paginationButtonActive : ''}`}
              onClick={() => setCurrentPage(page)}
            >
              {page}
            </button>
          ))}
          <button
            type="button"
            className={styles.paginationButton}
            onClick={() => setCurrentPage((page: number) => Math.min(totalPages, page + 1))}
            disabled={currentPage === totalPages}
          >
            ›
          </button>
        </div>
      )}
    </section>
  );
};
