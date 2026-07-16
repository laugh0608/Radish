import { useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import {
  getCurrentGodCommentsBatch,
  getPostList,
  type CommentHighlight,
  type PostItem,
} from '@/api/forum';
import { log } from '@/utils/logger';
import { createForumCommentHighlightMap, getForumCommentHighlight } from '@/utils/forumCommentHighlights';
import { Icon } from '@radish/ui/icon';
import { PostCard } from '@/apps/forum/components/PostCard';
import type {
  PublicForumListRoute,
  PublicForumRouteSort,
  PublicForumSearchRoute,
  PublicForumTypeRoute,
} from '../forumRouteState';
import { buildPublicForumPath, createDefaultListRoute, createDefaultSearchRoute } from '../forumRouteState';
import { usePublicReplaceRouteSync } from '../usePublicReplaceRouteSync';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import { PublicStatusCard } from './PublicStatusCard';
import { PublicForumPagination, PublicForumRouteLink } from './PublicForumLinks';
import {
  buildTypeRouteKey,
  buildVisiblePages,
  createForumReadingGuide,
  getForumPostRouteIdentifier,
  lotteryGuideDefinition,
  pollGuideDefinition,
  questionGuideDefinition,
  resolvePublicProfileUserId,
} from './publicForumUtils';
import {
  resolvePublicForumReadSectionState,
} from './publicForumViewState';
import styles from './PublicForumApp.module.css';

interface PublicForumTypeFeedProps {
  routeState: PublicForumTypeRoute;
  displayTimeZone: string;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  onScrollRestored: () => void;
  onRouteStateChange: (route: PublicForumTypeRoute, options?: { replace?: boolean }) => void;
  onOpenPost: (postId: string) => void;
  onBackToList: () => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenSearch?: (keyword?: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

function getTypeRouteHeader(
  kind: PublicForumTypeRoute['kind'],
  t: TFunction
): {
  title: string;
  intro: string;
  emptyTitle: string;
  emptyDescription: string;
  countLabel: (count: number) => string;
  sortOptions: Array<{ value: PublicForumRouteSort; label: string }>;
} {
  if (kind === 'question') {
    return {
      title: t('forum.public.questionTitle'),
      intro: t('forum.public.questionIntro'),
      emptyTitle: t('forum.public.questionEmptyTitle'),
      emptyDescription: t('forum.public.questionEmptyDescription'),
      countLabel: (count) => t('forum.public.questionCount', { count }),
      sortOptions: [
        { value: 'newest', label: t('forum.sort.newest') },
        { value: 'pending', label: t('forum.filter.pending') },
        { value: 'answers', label: t('forum.postCard.stat.answer') }
      ]
    };
  }

  if (kind === 'poll') {
    return {
      title: t('forum.public.pollTitle'),
      intro: t('forum.public.pollIntro'),
      emptyTitle: t('forum.public.pollEmptyTitle'),
      emptyDescription: t('forum.public.pollEmptyDescription'),
      countLabel: (count) => t('forum.public.pollCount', { count }),
      sortOptions: [
        { value: 'newest', label: t('forum.sort.newest') },
        { value: 'hottest', label: t('forum.sort.hottest') },
        { value: 'votes', label: t('forum.postCard.stat.vote') },
        { value: 'deadline', label: t('forum.public.pollDeadlineSortLabel') }
      ]
    };
  }

  return {
    title: t('forum.public.lotteryTitle'),
    intro: t('forum.public.lotteryIntro'),
    emptyTitle: t('forum.public.lotteryEmptyTitle'),
    emptyDescription: t('forum.public.lotteryEmptyDescription'),
    countLabel: (count) => t('forum.public.lotteryCount', { count }),
    sortOptions: [
      { value: 'newest', label: t('forum.sort.newest') },
      { value: 'hottest', label: t('forum.sort.hottest') }
    ]
  };
}

export const PublicForumTypeFeed = ({
  routeState,
  displayTimeZone,
  scrollContainerRef,
  restoreScrollTop,
  onScrollRestored,
  onRouteStateChange,
  onOpenPost,
  onBackToList,
  onOpenAuthorProfile,
  onOpenSearch,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumTypeFeedProps) => {
  const { t } = useTranslation();
  const [sortBy, setSortBy] = useState<PublicForumRouteSort>(routeState.sortBy);
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
  const header = useMemo(() => getTypeRouteHeader(routeState.kind, t), [routeState.kind, t]);

  useEffect(() => {
    setSortBy(routeState.sortBy);
    setCurrentPage(routeState.page);
  }, [routeState.kind, routeState.page, routeState.sortBy]);

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

  const nextTypeRoute = useMemo<PublicForumTypeRoute>(() => ({
    kind: routeState.kind,
    sortBy,
    page: currentPage
  }), [currentPage, routeState.kind, sortBy]);
  usePublicReplaceRouteSync({
    currentRouteKey: buildTypeRouteKey(routeState),
    nextRoute: nextTypeRoute,
    nextRouteKey: buildTypeRouteKey(nextTypeRoute),
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
    const requestId = ++postsRequestIdRef.current;

    const loadPosts = async () => {
      setLoadingPosts(true);
      setPostError(null);
      try {
        const pageModel = await getPostList(
          null,
          t,
          currentPage,
          20,
          sortBy,
          '',
          undefined,
          undefined,
          routeState.kind,
          'all',
          'all'
        );

        if (requestId !== postsRequestIdRef.current) {
          return;
        }

        if (pageModel.pageCount > 0 && currentPage > pageModel.pageCount) {
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

          log.warn(`公开论坛 ${routeState.kind} 列表神评预览补查失败，已降级使用帖子主数据:`, highlightError);
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
  }, [currentPage, reloadToken, routeState.kind, sortBy, t]);

  const visiblePages = useMemo(() => {
    return buildVisiblePages(currentPage, totalPages, isCompactViewport ? 5 : 7);
  }, [currentPage, isCompactViewport, totalPages]);
  const readingGuide = useMemo(() => {
    const guideDefinition = routeState.kind === 'question'
      ? questionGuideDefinition
      : routeState.kind === 'poll'
        ? pollGuideDefinition
        : lotteryGuideDefinition;

    return createForumReadingGuide(t, guideDefinition);
  }, [routeState.kind, t]);

  const listState = resolvePublicForumReadSectionState({
    loading: loadingPosts,
    error: postError,
    itemCount: posts.length,
    totalCount: totalPosts
  });
  const backToListRoute: PublicForumListRoute = createDefaultListRoute();
  const searchRoute: PublicForumSearchRoute = createDefaultSearchRoute();
  const buildTypeRoute = (nextPage: number, nextSortBy = sortBy): PublicForumTypeRoute => ({
    kind: routeState.kind,
    sortBy: nextSortBy,
    page: nextPage
  });

  return (
    <section className={`${styles.sectionCard} ${styles.listSectionCard}`}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>{t('forum.public.guide.label')}</p>
          <div className={styles.searchTopbar}>
            <PublicForumRouteLink
              className={styles.backButton}
              route={backToListRoute}
              onNavigate={onBackToList}
            >
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('forum.backToList')}</span>
            </PublicForumRouteLink>
            <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
          </div>
          <h1 className={styles.pageTitle}>{header.title}</h1>
          <p className={styles.pageIntro}>{header.intro}</p>
          {totalPosts > 0 && (
            <div className={styles.searchSummaryRail}>
              <span className={styles.detailMetaChip}>{header.countLabel(totalPosts)}</span>
            </div>
          )}
        </div>

        <div className={styles.toolbar}>
          <div className={styles.segmented}>
            <PublicForumRouteLink
              className={styles.segmentButton}
              route={searchRoute}
              onNavigate={onOpenSearch ? () => onOpenSearch() : undefined}
            >
              <Icon icon="mdi:magnify" size={16} />
              <span>{t('forum.public.searchAction')}</span>
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={styles.segmentButton}
              route={backToListRoute}
              onNavigate={onBackToList}
            >
              {t('forum.allPosts')}
            </PublicForumRouteLink>
            {header.sortOptions.map((option) => (
              <PublicForumRouteLink
                key={`${routeState.kind}-${option.value}`}
                className={`${styles.segmentButton} ${sortBy === option.value ? styles.segmentButtonActive : ''}`}
                route={buildTypeRoute(1, option.value)}
                onNavigate={() => {
                  setSortBy(option.value);
                  setCurrentPage(1);
                }}
              >
                {option.label}
              </PublicForumRouteLink>
            ))}
          </div>
        </div>

      </div>

      <div className={styles.postList}>
        {listState === 'loading' ? (
          <PublicStatusCard
            tone="loading"
            title={t('forum.public.loadingTitle')}
            description={t('forum.public.loadingDescription')}
          />
        ) : listState === 'error' ? (
          <PublicStatusCard
            tone="error"
            title={t('forum.public.listErrorTitle')}
            description={postError || t('forum.public.loadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
          />
        ) : listState === 'empty' ? (
          <PublicStatusCard
            tone="empty"
            title={header.emptyTitle}
            description={header.emptyDescription}
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
                href={buildPublicForumPath({ kind: 'detail', postId: getForumPostRouteIdentifier(post) })}
                variant="publicCompact"
                resolveAuthorProfileId={resolvePublicProfileUserId}
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
        <PublicForumPagination
          currentPage={currentPage}
          totalPages={totalPages}
          visiblePages={visiblePages}
          buildRoute={(page) => buildTypeRoute(page)}
          onPageChange={setCurrentPage}
        />
      )}

      <PublicReadingGuide
        className={styles.readingGuide}
        label={readingGuide.label}
        title={readingGuide.title}
        description={readingGuide.description}
        items={readingGuide.items}
      />
    </section>
  );
};
