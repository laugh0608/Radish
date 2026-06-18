import { useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  getCurrentGodCommentsBatch,
  getPostList,
  getTagBySlug,
  type CommentHighlight,
  type PostItem,
  type Tag,
} from '@/api/forum';
import { PostCard } from '@/apps/forum/components/PostCard';
import { createForumCommentHighlightMap, getForumCommentHighlight } from '@/utils/forumCommentHighlights';
import { log } from '@/utils/logger';
import type {
  PublicForumTagRoute,
  PublicListSort,
} from '../forumRouteState';
import { buildPublicForumPath } from '../forumRouteState';
import { usePublicReplaceRouteSync } from '../usePublicReplaceRouteSync';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import {
  buildTagRouteKey,
  buildVisiblePages,
  createForumReadingGuide,
  formatTagPostCount,
  getForumPostRouteIdentifier,
  resolvePublicProfileUserId,
  tagGuideDefinition,
} from './publicForumUtils';
import {
  resolvePublicForumReadSectionState,
  resolvePublicForumTagLoadState,
} from './publicForumViewState';
import { PublicStatusCard } from './PublicStatusCard';
import styles from './PublicForumApp.module.css';

interface PublicForumTagProps {
  routeState: PublicForumTagRoute;
  displayTimeZone: string;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  onScrollRestored: () => void;
  onRouteStateChange: (route: PublicForumTagRoute, options?: { replace?: boolean }) => void;
  onOpenPost: (postId: string) => void;
  onBackToList: () => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenSearch?: (keyword?: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

export const PublicForumTag = ({
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
}: PublicForumTagProps) => {
  const { t } = useTranslation();
  const [selectedTag, setSelectedTag] = useState<Tag | null>(null);
  const [sortBy, setSortBy] = useState<PublicListSort>(routeState.sortBy);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<string, CommentHighlight>>(new Map());
  const [currentPage, setCurrentPage] = useState(routeState.page);
  const [totalPosts, setTotalPosts] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loadingTag, setLoadingTag] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [tagError, setTagError] = useState<string | null>(null);
  const [postError, setPostError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [isCompactViewport, setIsCompactViewport] = useState(() =>
    typeof window !== 'undefined' ? window.innerWidth <= 720 : false
  );
  const tagRequestIdRef = useRef(0);
  const postsRequestIdRef = useRef(0);

  useEffect(() => {
    setSortBy(routeState.sortBy);
    setCurrentPage(routeState.page);
  }, [routeState.page, routeState.sortBy, routeState.tagSlug]);

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

  const nextTagRoute = useMemo<PublicForumTagRoute>(() => ({
    kind: 'tag',
    tagSlug: routeState.tagSlug,
    sortBy,
    page: currentPage
  }), [currentPage, routeState.tagSlug, sortBy]);
  usePublicReplaceRouteSync({
    currentRouteKey: buildTagRouteKey(routeState),
    nextRoute: nextTagRoute,
    nextRouteKey: buildTagRouteKey(nextTagRoute),
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
    const requestId = ++tagRequestIdRef.current;

    const loadTag = async () => {
      setLoadingTag(true);
      setTagError(null);
      try {
        const tag = await getTagBySlug(routeState.tagSlug, t);
        if (requestId !== tagRequestIdRef.current) {
          return;
        }

        setSelectedTag(tag);
      } catch (err) {
        if (requestId !== tagRequestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setSelectedTag(null);
        setTagError(message);
      } finally {
        if (requestId === tagRequestIdRef.current) {
          setLoadingTag(false);
        }
      }
    };

    void loadTag();
  }, [reloadToken, routeState.tagSlug, t]);

  const canonicalTagRoute = useMemo<PublicForumTagRoute>(() => ({
    kind: 'tag',
    tagSlug: selectedTag?.voSlug ?? routeState.tagSlug,
    sortBy,
    page: currentPage
  }), [currentPage, routeState.tagSlug, selectedTag?.voSlug, sortBy]);
  usePublicReplaceRouteSync({
    currentRouteKey: buildTagRouteKey(routeState),
    nextRoute: canonicalTagRoute,
    nextRouteKey: buildTagRouteKey(canonicalTagRoute),
    onRouteStateChange
  });

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
          'all',
          'all',
          'all',
          { tagSlug: routeState.tagSlug }
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

          log.warn('公开论坛标签页神评预览补查失败，已降级使用帖子主数据:', highlightError);
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
  }, [currentPage, reloadToken, routeState.tagSlug, sortBy, t]);

  const tagState = resolvePublicForumTagLoadState({
    loadingTag,
    hasTag: !!selectedTag,
    tagError
  });
  const pageTitle = selectedTag?.voName
    || (tagState.kind === 'notFound' ? t('forum.public.tagUnavailableTitle') : t('forum.public.tagTitle'));
  const pageIntro = selectedTag?.voDescription?.trim()
    || (tagState.kind === 'notFound'
      ? t('forum.public.tagUnavailableDescription')
      : t('forum.public.tagDescriptionFallback'));
  const tagPostCount = useMemo(() => formatTagPostCount(selectedTag, t), [selectedTag, t]);
  const readingGuide = useMemo(
    () => createForumReadingGuide(t, tagGuideDefinition),
    [t]
  );

  useEffect(() => {
    document.title = `${t('desktop.apps.forum.name')} · ${pageTitle}`;
  }, [pageTitle, t]);

  const visiblePages = useMemo(() => {
    return buildVisiblePages(currentPage, totalPages, isCompactViewport ? 5 : 7);
  }, [currentPage, isCompactViewport, totalPages]);
  const listState = resolvePublicForumReadSectionState({
    loading: loadingPosts,
    error: postError,
    itemCount: posts.length,
    totalCount: totalPosts
  });

  return (
    <section className={`${styles.sectionCard} ${styles.listSectionCard}`}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>{t('forum.public.guide.label')}</p>
          <div className={styles.searchTopbar}>
            <button type="button" className={styles.backButton} onClick={onBackToList}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('forum.backToList')}</span>
            </button>
            <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
          </div>
          <h1 className={styles.pageTitle}>{pageTitle}</h1>
          <p className={styles.pageIntro}>{pageIntro}</p>
          {selectedTag && (
            <div className={styles.categorySpotlight}>
              <div className={styles.categoryMetaRail}>
                <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
                {tagPostCount && (
                  <span className={styles.detailMetaChip}>{tagPostCount}</span>
                )}
                {selectedTag.voSlug && (
                  <span className={styles.detailMetaChip}>#{selectedTag.voSlug}</span>
                )}
                {selectedTag.voIsFixed && (
                  <span className={styles.detailMetaChip}>{t('forum.public.tagFixedBadge')}</span>
                )}
              </div>
            </div>
          )}
        </div>

        <div className={styles.toolbar}>
          <div className={styles.segmented}>
            <button
              type="button"
              className={styles.segmentButton}
              onClick={() => onOpenSearch?.(selectedTag?.voName ?? '')}
            >
              <Icon icon="mdi:magnify" size={16} />
              <span>{t('forum.public.searchAction')}</span>
            </button>
            <button
              type="button"
              className={styles.segmentButton}
              onClick={onBackToList}
            >
              {t('forum.allPosts')}
            </button>
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
        </div>

        <PublicReadingGuide
          className={styles.readingGuide}
          label={readingGuide.label}
          title={readingGuide.title}
          description={readingGuide.description}
          items={readingGuide.items}
        />
      </div>

      {tagState.kind === 'error' && selectedTag && (
        <div className={styles.inlineNotice} data-tone="warning">
          <span className={styles.inlineNoticeText}>{t('forum.public.tagContextFallback')}</span>
          <button
            type="button"
            className={styles.inlineTextButton}
            onClick={() => setReloadToken((current) => current + 1)}
          >
            {t('common.retry')}
          </button>
        </div>
      )}

      <div className={styles.postList}>
        {tagState.kind === 'loading' && !posts.length ? (
          <PublicStatusCard
            tone="loading"
            title={t('forum.public.tagLoadingTitle')}
            description={t('forum.public.tagLoadingDescription')}
          />
        ) : tagState.kind === 'notFound' ? (
          <PublicStatusCard
            tone="notFound"
            title={t('forum.public.tagUnavailableTitle')}
            description={t('forum.public.tagUnavailableDescription')}
            primaryAction={{
              label: t('forum.viewAllPosts'),
              onClick: onBackToList
            }}
          />
        ) : tagState.kind === 'error' && !selectedTag ? (
          <PublicStatusCard
            tone="error"
            title={t('forum.public.tagErrorTitle')}
            description={tagError || t('forum.public.tagLoadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
            secondaryAction={{
              label: t('forum.viewAllPosts'),
              onClick: onBackToList
            }}
          />
        ) : listState === 'loading' ? (
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
            title={t('forum.public.emptyTitle')}
            description={t('forum.public.tagEmptyDescription')}
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

      {tagState.kind !== 'notFound' && totalPages > 1 && (
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
