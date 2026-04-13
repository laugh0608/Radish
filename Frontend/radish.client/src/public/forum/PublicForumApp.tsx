import { useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import {
  getCategoryById,
  getChildComments,
  getCurrentGodCommentsBatch,
  getPostById,
  getPostList,
  getPostQuickReplyWall,
  getTagBySlug,
  getRootCommentsPage,
  getTopCategories,
  type Category,
  type CommentHighlight,
  type CommentNode,
  type PostDetail,
  type PostItem,
  type PostQuickReply,
  type Tag,
} from '@/api/forum';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import { createForumCommentHighlightMap, getForumCommentHighlight } from '@/utils/forumCommentHighlights';
import { Icon } from '@radish/ui/icon';
import { PostCard } from '@/apps/forum/components/PostCard';
import { PostDetail as ForumPostDetail } from '@/apps/forum/components/PostDetail';
import { PostQuickReplyWall } from '@/apps/forum/components/PostQuickReplyWall';
import { CommentTree } from '@/apps/forum/components/CommentTree';
import type {
  PublicForumBrowseRoute,
  PublicForumListRoute,
  PublicForumRoute,
  PublicForumSearchRoute,
  PublicForumTagRoute,
  PublicForumTypeRoute,
  PublicListSort,
  PublicForumRouteSort,
  PublicSearchTimeRange,
} from '../forumRouteState';
import type { PublicDetailBackMode } from '../publicRouteNavigation';
import { createDefaultSearchRoute } from '../forumRouteState';
import {
  resolvePublicForumCategoryLoadState,
  resolvePublicForumDetailLoadState,
  resolvePublicForumReadSectionState,
  resolvePublicForumTagLoadState,
} from './publicForumViewState';
import styles from './PublicForumApp.module.css';

interface PublicForumAppProps {
  route: PublicForumRoute;
  fallbackBrowseRoute: PublicForumBrowseRoute;
  detailBackAction?: {
    mode: PublicDetailBackMode;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
  onNavigateToDiscover?: () => void;
  onNavigateToProfile?: (userId: string) => void;
  onNavigateToSearch?: (keyword?: string) => void;
  onNavigateToTag?: (tagSlug: string) => void;
  onNavigateToQuestion?: () => void;
  onNavigateToPoll?: () => void;
  onNavigateToLottery?: () => void;
}

type RootCommentSort = 'newest' | 'hottest' | null;

function buildActiveSectionTitle(categories: Category[], selectedCategoryId: number | null, fallback: string): string {
  if (!selectedCategoryId) {
    return fallback;
  }

  return categories.find((item) => item.voId === selectedCategoryId)?.voName || fallback;
}

function buildCategoryIntro(category: Category | null, fallback: string): string {
  if (!category) {
    return fallback;
  }

  const description = category.voDescription?.trim();
  if (description) {
    return description;
  }

  return fallback;
}

function formatCategoryPostCount(category: Category | null, t: ReturnType<typeof useTranslation>['t']): string | null {
  if (!category || typeof category.voPostCount !== 'number') {
    return null;
  }

  return t('forum.public.categoryPostCount', { count: category.voPostCount });
}

function formatTagPostCount(tag: Tag | null, t: ReturnType<typeof useTranslation>['t']): string | null {
  if (!tag || typeof tag.voPostCount !== 'number') {
    return null;
  }

  return t('forum.public.tagPostCount', { count: tag.voPostCount });
}

function buildVisiblePages(currentPage: number, totalPages: number, maxVisible: number): number[] {
  if (totalPages <= maxVisible) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }

  const half = Math.floor(maxVisible / 2);
  if (currentPage <= half + 1) {
    return Array.from({ length: maxVisible }, (_, index) => index + 1);
  }

  if (currentPage >= totalPages - half) {
    return Array.from({ length: maxVisible }, (_, index) => totalPages - maxVisible + 1 + index);
  }

  return Array.from({ length: maxVisible }, (_, index) => currentPage - half + index);
}

function buildListRouteKey(route: PublicForumListRoute): string {
  return `${route.categoryId ?? 'all'}:${route.sortBy}:${route.page}`;
}

function buildTagRouteKey(route: PublicForumTagRoute): string {
  return `${route.tagSlug}:${route.sortBy}:${route.page}`;
}

function buildSearchRouteKey(route: PublicForumSearchRoute): string {
  return `${route.keyword}:${route.sortBy}:${route.timeRange}:${route.startDate ?? ''}:${route.endDate ?? ''}:${route.page}`;
}

function buildTypeRouteKey(route: PublicForumTypeRoute): string {
  return `${route.kind}:${route.sortBy}:${route.page}`;
}

function buildBrowseRouteKey(route: PublicForumBrowseRoute): string {
  return route.kind === 'list'
    ? buildListRouteKey(route)
    : route.kind === 'tag'
      ? buildTagRouteKey(route)
      : route.kind === 'search'
        ? buildSearchRouteKey(route)
        : buildTypeRouteKey(route);
}

function buildSearchTimeRange(route: PublicForumSearchRoute): { startTime?: string; endTime?: string } {
  const now = new Date();

  switch (route.timeRange) {
    case '24h':
      return {
        startTime: new Date(now.getTime() - 24 * 60 * 60 * 1000).toISOString(),
        endTime: now.toISOString()
      };
    case '7d':
      return {
        startTime: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        endTime: now.toISOString()
      };
    case '30d':
      return {
        startTime: new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000).toISOString(),
        endTime: now.toISOString()
      };
    case 'custom': {
      if (!route.startDate && !route.endDate) {
        return {};
      }

      const parsedStart = route.startDate ? new Date(`${route.startDate}T00:00:00`) : null;
      const parsedEnd = route.endDate ? new Date(`${route.endDate}T23:59:59.999`) : null;

      if ((parsedStart && Number.isNaN(parsedStart.getTime())) || (parsedEnd && Number.isNaN(parsedEnd.getTime()))) {
        return {};
      }

      if (parsedStart && parsedEnd && parsedStart > parsedEnd) {
        return {
          startTime: parsedEnd.toISOString(),
          endTime: parsedStart.toISOString()
        };
      }

      return {
        startTime: parsedStart ? parsedStart.toISOString() : undefined,
        endTime: parsedEnd ? parsedEnd.toISOString() : undefined
      };
    }
    case 'all':
    default:
      return {};
  }
}

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound' | 'info';

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  compact?: boolean;
  primaryAction?: {
    label: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
    onClick: () => void;
  };
}

function PublicStatusCard({
  tone,
  title,
  description,
  compact = false,
  primaryAction,
  secondaryAction
}: PublicStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : tone === 'notFound'
        ? 'mdi:file-search-outline'
        : tone === 'info'
          ? 'mdi:information-outline'
          : 'mdi:alert-circle-outline';

  return (
    <div
      className={`${styles.statusCard} ${compact ? styles.statusCardCompact : ''}`}
      data-tone={tone}
    >
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={compact ? 18 : 22} />
      </div>
      <div className={styles.statusBody}>
        <h2 className={styles.statusTitle}>{title}</h2>
        <p className={styles.statusDescription}>{description}</p>
        {(primaryAction || secondaryAction) && (
          <div className={styles.statusActions}>
            {primaryAction && (
              <button type="button" className={styles.retryButton} onClick={primaryAction.onClick}>
                {primaryAction.label}
              </button>
            )}
            {secondaryAction && (
              <button type="button" className={styles.secondaryButton} onClick={secondaryAction.onClick}>
                {secondaryAction.label}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export const PublicForumApp = ({
  route,
  fallbackBrowseRoute,
  detailBackAction,
  onNavigate,
  onNavigateToDiscover,
  onNavigateToProfile,
  onNavigateToSearch,
  onNavigateToTag,
  onNavigateToQuestion,
  onNavigateToPoll,
  onNavigateToLottery
}: PublicForumAppProps) => {
  const { t } = useTranslation();
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));
  const pageRef = useRef<HTMLDivElement>(null);
  const previousRouteRef = useRef<PublicForumRoute>(route);
  const browseScrollSnapshotRef = useRef<{ routeKey: string; scrollTop: number } | null>(null);
  const [pendingRestoreScrollTop, setPendingRestoreScrollTop] = useState<number | null>(null);
  const detailBackLabel = detailBackAction?.mode === 'discover'
    ? t('public.shell.backToDiscover')
    : detailBackAction
      ? t('public.shell.backToSource')
      : t('forum.backToList');
  const handleForumDetailBack = detailBackAction?.onBack ?? (() => onNavigate(fallbackBrowseRoute));

  useEffect(() => {
    const titleKey = route.kind === 'detail'
      ? 'forum.postDetail.title'
      : route.kind === 'search'
        ? 'forum.public.searchTitle'
        : route.kind === 'tag'
          ? 'forum.public.tagTitle'
          : route.kind === 'question'
            ? 'forum.public.questionTitle'
            : route.kind === 'poll'
              ? 'forum.public.pollTitle'
              : route.kind === 'lottery'
                ? 'forum.public.lotteryTitle'
                : 'forum.allPosts';
    const nextTitle = `${t('desktop.apps.forum.name')} · ${t(titleKey)}`;

    document.title = nextTitle;
  }, [route.kind, t]);

  useEffect(() => {
    const page = pageRef.current;
    const previousRoute = previousRouteRef.current;

    if (!page) {
      previousRouteRef.current = route;
      return;
    }

    if (previousRoute.kind !== 'detail' && route.kind === 'detail') {
      browseScrollSnapshotRef.current = {
        routeKey: buildBrowseRouteKey(previousRoute),
        scrollTop: page.scrollTop
      };
      setPendingRestoreScrollTop(null);
      page.scrollTo({ top: 0, behavior: 'auto' });
    } else if (route.kind !== 'detail') {
      const nextRouteKey = buildBrowseRouteKey(route);
      if (browseScrollSnapshotRef.current?.routeKey === nextRouteKey) {
        setPendingRestoreScrollTop(browseScrollSnapshotRef.current.scrollTop);
      } else {
        setPendingRestoreScrollTop(null);
        page.scrollTo({ top: 0, behavior: 'auto' });
      }
    } else {
      setPendingRestoreScrollTop(null);
      page.scrollTo({ top: 0, behavior: 'auto' });
    }

    previousRouteRef.current = route;
  }, [
    route.kind,
    route.kind === 'detail'
      ? route.postId
      : route.kind === 'list'
        ? route.categoryId
        : route.kind === 'tag'
          ? route.tagSlug
          : route.kind === 'question' || route.kind === 'poll' || route.kind === 'lottery'
            ? route.kind
          : null,
    route.kind !== 'detail' ? route.sortBy : null,
    route.kind === 'search' ? route.keyword : null,
    route.kind === 'search' ? route.timeRange : null,
    route.kind === 'search' ? route.startDate : null,
    route.kind === 'search' ? route.endDate : null,
    route.kind !== 'detail' ? route.page : null
  ]);

  return (
    <div className={styles.page} ref={pageRef}>
      <header className={styles.hero}>
        <div className={styles.heroInner}>
          <button
            type="button"
            className={styles.brand}
            onClick={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
          >
            <span className={styles.brandMark}>R</span>
            <span className={styles.brandText}>
              <span className={styles.brandName}>{t('desktop.apps.forum.name')}</span>
              <span className={styles.brandSubline}>Public Content Shell</span>
            </span>
          </button>
          <div className={styles.heroActions}>
            <button type="button" className={styles.discoverLink} onClick={onNavigateToDiscover}>
              <Icon icon="mdi:compass-outline" size={18} />
              <span>{t('public.shell.discoverAction')}</span>
            </button>
            <a className={styles.desktopLink} href="/">
              <Icon icon="mdi:view-dashboard-outline" size={18} />
              <span>WebOS</span>
            </a>
          </div>
        </div>
      </header>

      <main className={styles.main}>
        {route.kind === 'detail' ? (
          <PublicForumDetail
            key={`detail-${route.postId}`}
            postId={route.postId}
            displayTimeZone={displayTimeZone}
            backLabel={detailBackLabel}
            onBack={handleForumDetailBack}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'search' ? (
          <PublicForumSearch
            key="search"
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onBackToList={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'tag' ? (
          <PublicForumTag
            key={`tag-${route.tagSlug}`}
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onBackToList={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenSearch={onNavigateToSearch}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'question' || route.kind === 'poll' || route.kind === 'lottery' ? (
          <PublicForumTypeFeed
            key={`${route.kind}-${route.sortBy}-${route.page}`}
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onBackToList={() => onNavigate({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenSearch={onNavigateToSearch}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : route.kind === 'list' ? (
          <PublicForumList
            key="list"
            routeState={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onRouteStateChange={onNavigate}
            onOpenPost={(postId) => onNavigate({ kind: 'detail', postId })}
            onOpenAuthorProfile={onNavigateToProfile}
            onOpenSearch={onNavigateToSearch}
            onOpenTag={onNavigateToTag}
            onOpenQuestion={onNavigateToQuestion}
            onOpenPoll={onNavigateToPoll}
            onOpenLottery={onNavigateToLottery}
          />
        ) : null}
      </main>
    </div>
  );
};

interface PublicForumListProps {
  routeState: PublicForumListRoute;
  displayTimeZone: string;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  onScrollRestored: () => void;
  onRouteStateChange: (route: PublicForumListRoute, options?: { replace?: boolean }) => void;
  onOpenPost: (postId: string) => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenSearch?: (keyword?: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

const PublicForumList = ({
  routeState,
  displayTimeZone,
  scrollContainerRef,
  restoreScrollTop,
  onScrollRestored,
  onRouteStateChange,
  onOpenPost,
  onOpenAuthorProfile,
  onOpenSearch,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumListProps) => {
  const { t } = useTranslation();
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(routeState.categoryId);
  const [selectedCategory, setSelectedCategory] = useState<Category | null>(null);
  const [sortBy, setSortBy] = useState<PublicListSort>(routeState.sortBy);
  const [posts, setPosts] = useState<PostItem[]>([]);
  const [postGodComments, setPostGodComments] = useState<Map<string, CommentHighlight>>(new Map());
  const [currentPage, setCurrentPage] = useState(routeState.page);
  const [totalPosts, setTotalPosts] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loadingCategories, setLoadingCategories] = useState(false);
  const [loadingSelectedCategory, setLoadingSelectedCategory] = useState(false);
  const [loadingPosts, setLoadingPosts] = useState(false);
  const [categoryError, setCategoryError] = useState<string | null>(null);
  const [selectedCategoryError, setSelectedCategoryError] = useState<string | null>(null);
  const [postError, setPostError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [isCompactViewport, setIsCompactViewport] = useState(() =>
    typeof window !== 'undefined' ? window.innerWidth <= 720 : false
  );
  const selectedCategoryRequestIdRef = useRef(0);
  const postsRequestIdRef = useRef(0);

  useEffect(() => {
    let cancelled = false;

    const loadCategories = async () => {
      setLoadingCategories(true);
      setCategoryError(null);
      try {
        const data = await getTopCategories(t);
        if (!cancelled) {
          setCategories(data);
        }
      } catch (err) {
        if (!cancelled) {
          const message = err instanceof Error ? err.message : String(err);
          setCategoryError(message);
        }
      } finally {
        if (!cancelled) {
          setLoadingCategories(false);
        }
      }
    };

    void loadCategories();
    return () => {
      cancelled = true;
    };
  }, [reloadToken, t]);

  useEffect(() => {
    setSelectedCategoryId(routeState.categoryId);
    setSortBy(routeState.sortBy);
    setCurrentPage(routeState.page);
  }, [routeState.categoryId, routeState.page, routeState.sortBy]);

  useEffect(() => {
    const categoryFromRail = selectedCategoryId
      ? categories.find((item) => item.voId === selectedCategoryId) ?? null
      : null;

    if (!selectedCategoryId) {
      selectedCategoryRequestIdRef.current += 1;
      setSelectedCategory(null);
      setSelectedCategoryError(null);
      setLoadingSelectedCategory(false);
      return;
    }

    if (categoryFromRail) {
      setSelectedCategory(categoryFromRail);
    } else {
      setSelectedCategory(null);
    }

    const requestId = ++selectedCategoryRequestIdRef.current;

    const loadSelectedCategory = async () => {
      setLoadingSelectedCategory(true);
      setSelectedCategoryError(null);
      try {
        const category = await getCategoryById(selectedCategoryId, t);
        if (requestId !== selectedCategoryRequestIdRef.current) {
          return;
        }

        setSelectedCategory(category);
      } catch (err) {
        if (requestId !== selectedCategoryRequestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setSelectedCategory((current) => current && current.voId === selectedCategoryId ? current : null);
        setSelectedCategoryError(message);
      } finally {
        if (requestId === selectedCategoryRequestIdRef.current) {
          setLoadingSelectedCategory(false);
        }
      }
    };

    void loadSelectedCategory();
  }, [categories, reloadToken, selectedCategoryId, t]);

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

  useEffect(() => {
    onRouteStateChange({
      kind: 'list',
      categoryId: selectedCategoryId,
      sortBy,
      page: currentPage
    }, { replace: true });
  }, [currentPage, onRouteStateChange, selectedCategoryId, sortBy]);

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
    const title = selectedCategory?.voName || buildActiveSectionTitle(categories, selectedCategoryId, t('forum.allPosts'));
    document.title = `${t('desktop.apps.forum.name')} · ${title}`;
  }, [categories, selectedCategory, selectedCategoryId, t]);

  useEffect(() => {
    const requestId = ++postsRequestIdRef.current;

    const loadPosts = async () => {
      setLoadingPosts(true);
      setPostError(null);
      try {
        const pageModel = await getPostList(
          selectedCategoryId,
          t,
          currentPage,
          20,
          sortBy
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

          log.warn('公开论坛神评预览补查失败，已降级使用帖子主数据:', highlightError);
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
  }, [currentPage, reloadToken, selectedCategoryId, sortBy, t]);

  const categoryState = resolvePublicForumCategoryLoadState({
    categoryId: selectedCategoryId,
    loadingCategory: loadingSelectedCategory,
    hasCategory: !!selectedCategory,
    categoryError: selectedCategoryError
  });
  const activeCategory = selectedCategory
    || (selectedCategoryId ? categories.find((item) => item.voId === selectedCategoryId) ?? null : null);
  const activeTitle = useMemo(() => {
    if (activeCategory?.voName) {
      return activeCategory.voName;
    }

    if (categoryState.kind === 'notFound') {
      return t('forum.public.categoryUnavailableTitle');
    }

    return buildActiveSectionTitle(categories, selectedCategoryId, t('forum.allPosts'));
  }, [activeCategory, categories, categoryState.kind, selectedCategoryId, t]);
  const activeIntro = useMemo(() => {
    if (activeCategory) {
      return buildCategoryIntro(activeCategory, t('forum.public.categoryDescriptionFallback'));
    }

    if (selectedCategoryId && categoryState.kind === 'notFound') {
      return t('forum.public.categoryUnavailableDescription');
    }

    return t('forum.public.listIntro');
  }, [activeCategory, categoryState.kind, selectedCategoryId, t]);
  const activeCategoryPostCount = useMemo(
    () => formatCategoryPostCount(activeCategory, t),
    [activeCategory, t]
  );

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
          <p className={styles.kicker}>Phase 2-2</p>
          <h1 className={styles.pageTitle}>{activeTitle}</h1>
          <p className={styles.pageIntro}>{activeIntro}</p>
          {activeCategory && (
            <div className={styles.categorySpotlight}>
              <div className={styles.categoryMetaRail}>
                <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
                {activeCategoryPostCount && (
                  <span className={styles.detailMetaChip}>{activeCategoryPostCount}</span>
                )}
                {activeCategory.voSlug && (
                  <span className={styles.detailMetaChip}>/{activeCategory.voSlug}</span>
                )}
              </div>
              {activeCategory.voCoverImage && (
                <div className={styles.categoryCoverWrap}>
                  <img
                    className={styles.categoryCover}
                    src={activeCategory.voCoverImage}
                    alt={t('forum.public.categoryCoverAlt', { name: activeCategory.voName })}
                  />
                </div>
              )}
            </div>
          )}
        </div>

        <div className={styles.toolbar}>
          <div className={styles.segmented}>
            <button
              type="button"
              className={styles.segmentButton}
              onClick={() => onOpenSearch?.()}
            >
              <Icon icon="mdi:magnify" size={16} />
              <span>{t('forum.public.searchAction')}</span>
            </button>
            <button
              type="button"
              className={`${styles.segmentButton} ${!selectedCategoryId ? styles.segmentButtonActive : ''}`}
              onClick={() => {
                setSelectedCategoryId(null);
                setCurrentPage(1);
              }}
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
      </div>

      <div className={styles.categoryRail}>
        {loadingCategories ? (
          <span className={styles.categoryHint}>{t('forum.category.loading')}</span>
        ) : categoryError ? (
          <div className={styles.inlineNotice} data-tone="warning">
            <span className={styles.inlineNoticeText}>{t('forum.public.categoryFallback')}</span>
            <button
              type="button"
              className={styles.inlineTextButton}
              onClick={() => setReloadToken((current) => current + 1)}
            >
              {t('common.retry')}
            </button>
          </div>
        ) : categories.length === 0 ? (
          <span className={styles.categoryHint}>{t('forum.category.empty')}</span>
        ) : (
          categories.map((category) => (
            <button
              key={category.voId}
              type="button"
              className={`${styles.categoryChip} ${selectedCategoryId === category.voId ? styles.categoryChipActive : ''}`}
              onClick={() => {
                setSelectedCategoryId((current) => (current === category.voId ? null : category.voId));
                setCurrentPage(1);
              }}
            >
              {category.voName}
            </button>
          ))
        )}
      </div>

      {categoryState.kind === 'error' && activeCategory && (
        <div className={styles.inlineNotice} data-tone="warning">
          <span className={styles.inlineNoticeText}>{t('forum.public.categoryContextFallback')}</span>
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
        {categoryState.kind === 'loading' && !posts.length ? (
          <PublicStatusCard
            tone="loading"
            title={t('forum.public.categoryLoadingTitle')}
            description={t('forum.public.categoryLoadingDescription')}
          />
        ) : categoryState.kind === 'notFound' ? (
          <PublicStatusCard
            tone="notFound"
            title={t('forum.public.categoryUnavailableTitle')}
            description={t('forum.public.categoryUnavailableDescription')}
            primaryAction={{
              label: t('forum.viewAllPosts'),
              onClick: () => {
                setSelectedCategoryId(null);
                setCurrentPage(1);
              }
            }}
          />
        ) : categoryState.kind === 'error' && !activeCategory ? (
          <PublicStatusCard
            tone="error"
            title={t('forum.public.categoryErrorTitle')}
            description={selectedCategoryError || t('forum.public.categoryLoadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
            secondaryAction={{
              label: t('forum.viewAllPosts'),
              onClick: () => {
                setSelectedCategoryId(null);
                setCurrentPage(1);
              }
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
            description={selectedCategoryId
              ? t('forum.public.categoryEmptyDescription')
              : t('forum.public.emptyDescription')}
          />
        ) : (
          posts.map((post) => {
            const godComment = getForumCommentHighlight(postGodComments, post.voId);
            return (
              <PostCard
                key={post.voId}
                post={post}
                displayTimeZone={displayTimeZone}
                onClick={() => onOpenPost(String(post.voId))}
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

      {categoryState.kind !== 'notFound' && totalPages > 1 && (
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

const PublicForumTag = ({
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

  useEffect(() => {
    onRouteStateChange({
      kind: 'tag',
      tagSlug: routeState.tagSlug,
      sortBy,
      page: currentPage
    }, { replace: true });
  }, [currentPage, onRouteStateChange, routeState.tagSlug, sortBy]);

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

  useEffect(() => {
    if (!selectedTag?.voSlug) {
      return;
    }

    if (selectedTag.voSlug === routeState.tagSlug) {
      return;
    }

    onRouteStateChange({
      kind: 'tag',
      tagSlug: selectedTag.voSlug,
      sortBy,
      page: currentPage
    }, { replace: true });
  }, [currentPage, onRouteStateChange, routeState.tagSlug, selectedTag?.voSlug, sortBy]);

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
          <p className={styles.kicker}>Phase 2-2</p>
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
                onClick={() => onOpenPost(String(post.voId))}
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
  t: ReturnType<typeof useTranslation>['t']
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

const PublicForumTypeFeed = ({
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

  useEffect(() => {
    onRouteStateChange({
      kind: routeState.kind,
      sortBy,
      page: currentPage
    }, { replace: true });
  }, [currentPage, onRouteStateChange, routeState.kind, sortBy]);

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

  useEffect(() => {
    document.title = `${t('desktop.apps.forum.name')} · ${header.title}`;
  }, [header.title, t]);

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
          <p className={styles.kicker}>Phase 2-2</p>
          <div className={styles.searchTopbar}>
            <button type="button" className={styles.backButton} onClick={onBackToList}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('forum.backToList')}</span>
            </button>
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
            <button
              type="button"
              className={styles.segmentButton}
              onClick={() => onOpenSearch?.()}
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
            {header.sortOptions.map((option) => (
              <button
                key={`${routeState.kind}-${option.value}`}
                type="button"
                className={`${styles.segmentButton} ${sortBy === option.value ? styles.segmentButtonActive : ''}`}
                onClick={() => {
                  setSortBy(option.value);
                  setCurrentPage(1);
                }}
              >
                {option.label}
              </button>
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
                onClick={() => onOpenPost(String(post.voId))}
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

const PublicForumSearch = ({
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

  useEffect(() => {
    onRouteStateChange({
      kind: 'search',
      keyword,
      sortBy,
      timeRange,
      startDate: timeRange === 'custom' ? appliedStartDate || undefined : undefined,
      endDate: timeRange === 'custom' ? appliedEndDate || undefined : undefined,
      page: currentPage
    }, { replace: true });
  }, [appliedEndDate, appliedStartDate, currentPage, keyword, onRouteStateChange, sortBy, timeRange]);

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
                onClick={() => onOpenPost(String(post.voId))}
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

interface PublicForumDetailProps {
  postId: string;
  displayTimeZone: string;
  backLabel: string;
  onBack: () => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}

const PublicForumDetail = ({
  postId,
  displayTimeZone,
  backLabel,
  onBack,
  onOpenAuthorProfile,
  onOpenTag,
  onOpenQuestion,
  onOpenPoll,
  onOpenLottery
}: PublicForumDetailProps) => {
  const { t } = useTranslation();
  const [post, setPost] = useState<PostDetail | null>(null);
  const [comments, setComments] = useState<CommentNode[]>([]);
  const [quickReplies, setQuickReplies] = useState<PostQuickReply[]>([]);
  const [quickReplyTotal, setQuickReplyTotal] = useState(0);
  const [commentTotal, setCommentTotal] = useState(0);
  const [loadedCommentPages, setLoadedCommentPages] = useState(0);
  const [commentSortBy, setCommentSortBy] = useState<RootCommentSort>(null);
  const [loadingPost, setLoadingPost] = useState(false);
  const [loadingComments, setLoadingComments] = useState(false);
  const [loadingQuickReplies, setLoadingQuickReplies] = useState(false);
  const [loadingMoreComments, setLoadingMoreComments] = useState(false);
  const [postError, setPostError] = useState<string | null>(null);
  const [commentError, setCommentError] = useState<string | null>(null);
  const [quickReplyError, setQuickReplyError] = useState<string | null>(null);
  const [commentPagingError, setCommentPagingError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const requestIdRef = useRef(0);
  const commentPageSize = 20;

  useEffect(() => {
    const requestId = ++requestIdRef.current;

    const loadDetail = async () => {
      setLoadingPost(true);
      setLoadingComments(true);
      setLoadingQuickReplies(true);
      setPostError(null);
      setCommentError(null);
      setQuickReplyError(null);
      setCommentPagingError(null);

      try {
        const postDetail = await getPostById(postId, t);
        if (requestId !== requestIdRef.current) {
          return;
        }

        setPost(postDetail);
      } catch (err) {
        if (requestId !== requestIdRef.current) {
          return;
        }

        const message = err instanceof Error ? err.message : String(err);
        setPost(null);
        setComments([]);
        setQuickReplies([]);
        setQuickReplyTotal(0);
        setCommentTotal(0);
        setLoadedCommentPages(0);
        setPostError(message);
        return;
      } finally {
        if (requestId === requestIdRef.current) {
          setLoadingPost(false);
        }
      }

      try {
        const [rootCommentsResult, replyWallResult] = await Promise.allSettled([
          getRootCommentsPage(postId, 1, commentPageSize, commentSortBy || 'default', t),
          getPostQuickReplyWall(postId, t)
        ]);

        if (requestId !== requestIdRef.current) {
          return;
        }

        if (rootCommentsResult.status === 'fulfilled') {
          const rootComments = rootCommentsResult.value;
          setComments(rootComments.voItems ?? []);
          setCommentTotal(rootComments.voTotal ?? 0);
          setLoadedCommentPages((rootComments.voItems?.length ?? 0) > 0 ? 1 : 0);
          setCommentError(null);
        } else {
          setComments([]);
          setCommentTotal(0);
          setLoadedCommentPages(0);
          const message = rootCommentsResult.reason instanceof Error
            ? rootCommentsResult.reason.message
            : String(rootCommentsResult.reason);
          setCommentError(message);
        }

        if (replyWallResult.status === 'fulfilled') {
          const replyWall = replyWallResult.value;
          setQuickReplies(replyWall.voItems ?? []);
          setQuickReplyTotal(replyWall.voTotal ?? 0);
          setQuickReplyError(null);
        } else {
          setQuickReplies([]);
          setQuickReplyTotal(0);
          const message = replyWallResult.reason instanceof Error
            ? replyWallResult.reason.message
            : String(replyWallResult.reason);
          setQuickReplyError(message);
        }
      } finally {
        if (requestId === requestIdRef.current) {
          setLoadingComments(false);
          setLoadingQuickReplies(false);
        }
      }
    };

    void loadDetail();
  }, [commentSortBy, postId, reloadToken, t]);

  useEffect(() => {
    if (!post?.voTitle) {
      return;
    }

    document.title = `${post.voTitle} · ${t('desktop.apps.forum.name')}`;
  }, [post?.voTitle, t]);

  const handleLoadMoreComments = async () => {
    if (loadingMoreComments || loadingComments || comments.length >= commentTotal) {
      return;
    }

    setLoadingMoreComments(true);
    setCommentPagingError(null);
    try {
      const nextPage = loadedCommentPages + 1;
      const pageData = await getRootCommentsPage(postId, nextPage, commentPageSize, commentSortBy || 'default', t);
      const nextItems = pageData.voItems ?? [];

      setComments((current) => {
        const existingIds = new Set(current.map((item) => item.voId));
        const appended = nextItems.filter((item) => !existingIds.has(item.voId));
        return [...current, ...appended];
      });
      setCommentTotal((current) => pageData.voTotal ?? current);
      if (nextItems.length > 0) {
        setLoadedCommentPages(nextPage);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setCommentPagingError(message);
    } finally {
      setLoadingMoreComments(false);
    }
  };

  const handleLoadMoreChildren = async (
    parentId: number,
    pageIndex: number,
    pageSize: number
  ): Promise<CommentNode[]> => {
    try {
      setCommentPagingError(null);
      const result = await getChildComments(parentId, pageIndex, pageSize, t);
      return result.voItems ?? [];
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setCommentPagingError(message);
      return [];
    }
  };
  const detailState = resolvePublicForumDetailLoadState({
    loadingPost,
    hasPost: !!post,
    postError
  });
  const quickReplySectionState = resolvePublicForumReadSectionState({
    loading: loadingQuickReplies,
    error: quickReplyError,
    itemCount: quickReplies.length,
    totalCount: quickReplyTotal
  });
  const commentSectionState = resolvePublicForumReadSectionState({
    loading: loadingComments,
    error: commentError,
    itemCount: comments.length,
    totalCount: commentTotal
  });
  const commentSortLabel = commentSortBy === 'newest'
    ? t('forum.sort.newest')
    : commentSortBy === 'hottest'
      ? t('forum.sort.hottest')
      : t('forum.public.commentSortDefault');

  return (
    <section className={`${styles.sectionCard} ${styles.detailSectionCard}`}>
      <div className={styles.detailTopbar}>
        <button type="button" className={styles.backButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={18} />
          <span>{backLabel}</span>
        </button>
      </div>

      <div className={styles.detailStack}>
        {detailState.kind === 'loading' && (
          <PublicStatusCard
            tone="loading"
            title={t('forum.public.loadingTitle')}
            description={t('forum.public.loadingDescription')}
          />
        )}

        {detailState.kind === 'notFound' && (
          <PublicStatusCard
            tone="notFound"
            title={t('forum.public.postNotFoundTitle')}
            description={t('forum.public.postNotFoundDescription')}
            secondaryAction={{
              label: backLabel,
              onClick: onBack
            }}
          />
        )}

        {detailState.kind === 'error' && (
          <PublicStatusCard
            tone="error"
            title={t('forum.public.postErrorTitle')}
            description={detailState.message}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
            secondaryAction={{
              label: backLabel,
              onClick: onBack
            }}
          />
        )}

        {detailState.kind === 'ready' && post && (
          <div className={styles.detailMetaRail}>
            <span className={styles.readOnlyBadge}>{t('forum.public.readOnlyBadge')}</span>
            <span className={styles.detailMetaChip}>{t('forum.postDetail.views', { count: post.voViewCount ?? 0 })}</span>
            <span className={styles.detailMetaChip}>{t('forum.quickReply.total', { count: quickReplyTotal })}</span>
            <span className={styles.detailMetaChip}>{t('forum.postDetail.commentCount', { count: commentTotal })}</span>
          </div>
        )}

        {detailState.kind === 'ready' && (
          <>
            <div className={styles.detailContext}>
              <p className={styles.detailContextText}>{t('forum.public.readOnlyDescription')}</p>
            </div>

            <ForumPostDetail
              post={post}
              loading={false}
              displayTimeZone={displayTimeZone}
              mode="readOnly"
              isAuthenticated={false}
              showSectionTitle={false}
              onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
              onTagClick={(_, tagSlug) => onOpenTag?.(tagSlug)}
              onQuestionClick={onOpenQuestion}
              onPollClick={onOpenPoll}
              onLotteryClick={onOpenLottery}
            />

            {quickReplySectionState === 'error' ? (
              <section className={styles.sectionShell}>
                <div className={styles.sectionShellHeader}>
                  <h2 className={styles.sectionShellTitle}>{t('forum.quickReply.title')}</h2>
                  <span className={styles.detailMetaChip}>{t('forum.quickReply.total', { count: quickReplyTotal })}</span>
                </div>
                <PublicStatusCard
                  tone="error"
                  compact={true}
                  title={t('forum.public.partialErrorTitle')}
                  description={quickReplyError || t('forum.public.quickRepliesErrorDescription')}
                  primaryAction={{
                    label: t('common.retry'),
                    onClick: () => setReloadToken((current) => current + 1)
                  }}
                />
              </section>
            ) : (
              <>
                {quickReplyError && (
                  <div className={styles.inlineNotice} data-tone="warning">
                    <span className={styles.inlineNoticeText}>{t('forum.public.quickRepliesErrorDescription')}</span>
                  </div>
                )}
                <PostQuickReplyWall
                  replies={quickReplies}
                  total={quickReplyTotal}
                  loading={loadingQuickReplies}
                  isAuthenticated={false}
                  currentUserId={0}
                  mode="readOnly"
                />
              </>
            )}

            <section className={styles.commentSection}>
              <div className={styles.commentHeading}>
                <div>
                  <h2 className={styles.commentTitle}>{t('forum.commentTree.title')}</h2>
                  <p className={styles.commentIntro}>{t('forum.quickReply.discussionSubtitle')}</p>
                </div>
                <div className={styles.commentSummary}>
                  <span className={styles.commentSummaryChip}>
                    {t('forum.public.loadedComments', { loaded: comments.length, total: commentTotal })}
                  </span>
                  <span className={styles.commentSummaryChip}>
                    {t('forum.public.commentOrder', { label: commentSortLabel })}
                  </span>
                </div>
              </div>

              {commentPagingError && (
                <div className={styles.inlineNotice} data-tone="warning">
                  <span className={styles.inlineNoticeText}>
                    {t('forum.public.commentPagingErrorDescription')}
                  </span>
                </div>
              )}

              {commentSectionState === 'error' ? (
                <PublicStatusCard
                  tone="error"
                  compact={true}
                  title={t('forum.public.partialErrorTitle')}
                  description={commentError || t('forum.public.commentsErrorDescription')}
                  primaryAction={{
                    label: t('common.retry'),
                    onClick: () => setReloadToken((current) => current + 1)
                  }}
                />
              ) : (
                <>
                  {commentError && (
                    <div className={styles.inlineNotice} data-tone="warning">
                      <span className={styles.inlineNoticeText}>{t('forum.public.commentsErrorDescription')}</span>
                    </div>
                  )}
                  <CommentTree
                    comments={comments}
                    loading={loadingComments}
                    loadingMoreRootComments={loadingMoreComments}
                    hasPost={true}
                    displayTimeZone={displayTimeZone}
                    currentUserId={0}
                    rootCommentTotal={commentTotal}
                    loadedRootCommentCount={comments.length}
                    rootCommentPageSize={commentPageSize}
                    sortBy={commentSortBy}
                    onSortChange={setCommentSortBy}
                    onLoadMoreChildren={handleLoadMoreChildren}
                    onLoadMoreRootComments={handleLoadMoreComments}
                    showTitle={false}
                    onAuthorClick={(userId) => onOpenAuthorProfile?.(String(userId))}
                  />
                </>
              )}
            </section>
          </>
        )}
      </div>
    </section>
  );
};
