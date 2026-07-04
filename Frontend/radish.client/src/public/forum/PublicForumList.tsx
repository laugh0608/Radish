import { useEffect, useMemo, useRef, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  getCategoryById,
  getCurrentGodCommentsBatch,
  getPostList,
  getTopCategories,
  type Category,
  type CommentHighlight,
  type PostItem,
} from '@/api/forum';
import { PostCard } from '@/apps/forum/components/PostCard';
import { createForumCommentHighlightMap, getForumCommentHighlight } from '@/utils/forumCommentHighlights';
import { log } from '@/utils/logger';
import type {
  PublicForumListRoute,
  PublicListSort,
} from '../forumRouteState';
import { buildPublicForumPath, createDefaultSearchRoute } from '../forumRouteState';
import { usePublicReplaceRouteSync } from '../usePublicReplaceRouteSync';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import { PublicForumPagination, PublicForumRouteLink } from './PublicForumLinks';
import {
  buildActiveSectionTitle,
  buildCategoryIntro,
  buildListRouteKey,
  buildVisiblePages,
  categoryGuideDefinition,
  createForumReadingGuide,
  formatCategoryPostCount,
  getForumPostRouteIdentifier,
  listGuideDefinition,
  resolvePublicProfileUserId,
} from './publicForumUtils';
import {
  resolvePublicForumCategoryLoadState,
  resolvePublicForumReadSectionState,
} from './publicForumViewState';
import { PublicStatusCard } from './PublicStatusCard';
import styles from './PublicForumApp.module.css';

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
  onOpenCompose?: (categoryId: string | null) => void;
}

export const PublicForumList = ({
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
  onOpenLottery,
  onOpenCompose
}: PublicForumListProps) => {
  const { t } = useTranslation();
  const [categories, setCategories] = useState<Category[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | null>(routeState.categoryId);
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

  const nextListRoute = useMemo<PublicForumListRoute>(() => ({
    kind: 'list',
    categoryId: selectedCategoryId,
    sortBy,
    page: currentPage
  }), [currentPage, selectedCategoryId, sortBy]);
  usePublicReplaceRouteSync({
    currentRouteKey: buildListRouteKey(routeState),
    nextRoute: nextListRoute,
    nextRouteKey: buildListRouteKey(nextListRoute),
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
  const readingGuide = useMemo(
    () => createForumReadingGuide(t, selectedCategoryId ? categoryGuideDefinition : listGuideDefinition),
    [selectedCategoryId, t]
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
  const buildListRoute = (nextPage: number, nextCategoryId = selectedCategoryId, nextSortBy = sortBy): PublicForumListRoute => ({
    kind: 'list',
    categoryId: nextCategoryId,
    sortBy: nextSortBy,
    page: nextPage
  });
  const allPostsRoute = buildListRoute(1, null);

  const composeRoute = {
    kind: 'compose' as const,
    categoryId: selectedCategoryId
  };

  return (
    <div className={styles.forumGrid}>
      <section className={`${styles.sectionCard} ${styles.listSectionCard}`}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>{t('forum.public.guide.label')}</p>
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
            <PublicForumRouteLink
              className={styles.segmentButton}
              route={createDefaultSearchRoute()}
              onNavigate={onOpenSearch ? () => onOpenSearch() : undefined}
            >
              <Icon icon="mdi:magnify" size={16} />
              <span>{t('forum.public.searchAction')}</span>
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={`${styles.segmentButton} ${!selectedCategoryId ? styles.segmentButtonActive : ''}`}
              route={allPostsRoute}
              onNavigate={() => {
                setSelectedCategoryId(null);
                setCurrentPage(1);
              }}
            >
              {t('forum.allPosts')}
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={`${styles.segmentButton} ${sortBy === 'newest' ? styles.segmentButtonActive : ''}`}
              route={buildListRoute(1, selectedCategoryId, 'newest')}
              onNavigate={() => {
                setSortBy('newest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.newest')}
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={`${styles.segmentButton} ${sortBy === 'hottest' ? styles.segmentButtonActive : ''}`}
              route={buildListRoute(1, selectedCategoryId, 'hottest')}
              onNavigate={() => {
                setSortBy('hottest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.hottest')}
            </PublicForumRouteLink>
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
          categories.map((category) => {
            const nextCategoryId = selectedCategoryId === category.voId ? null : category.voId;

            return (
              <PublicForumRouteLink
                key={category.voId}
                className={`${styles.categoryChip} ${selectedCategoryId === category.voId ? styles.categoryChipActive : ''}`}
                route={buildListRoute(1, nextCategoryId)}
                onNavigate={() => {
                  setSelectedCategoryId(nextCategoryId);
                  setCurrentPage(1);
                }}
              >
                {category.voName}
              </PublicForumRouteLink>
            );
          })
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
              href: buildPublicForumPath(allPostsRoute),
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
              href: buildPublicForumPath(allPostsRoute),
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

      {categoryState.kind !== 'notFound' && totalPages > 1 && (
        <PublicForumPagination
          currentPage={currentPage}
          totalPages={totalPages}
          visiblePages={visiblePages}
          buildRoute={(page) => buildListRoute(page)}
          onPageChange={setCurrentPage}
        />
      )}

      </section>

      <aside className={styles.forumSideRail} aria-label={t('forum.public.listRailLabel')}>
        <PublicReadingGuide
          className={`${styles.readingGuide} ${styles.sideReadingGuide}`}
          label={readingGuide.label}
          title={readingGuide.title}
          description={readingGuide.description}
          items={readingGuide.items}
        />

        <section className={styles.sidePanel}>
          <p className={styles.sidePanelKicker}>{t('forum.public.listRailRulesTitle')}</p>
          <ul className={styles.sideRuleList}>
            <li>{t('forum.public.listRailRuleGodSummary')}</li>
            <li>{t('forum.public.listRailRuleNoListReaction')}</li>
            <li>{t('forum.public.listRailRuleStats')}</li>
            <li>{t('forum.public.listRailRuleCommentEntry')}</li>
          </ul>
        </section>

        <section className={styles.sidePanel}>
          <p className={styles.sidePanelKicker}>{t('forum.public.listRailFeedsTitle')}</p>
          <p className={styles.sidePanelText}>{t('forum.public.listRailFeedsDescription')}</p>
          <div className={styles.railChipList}>
            <PublicForumRouteLink
              className={styles.railChip}
              route={allPostsRoute}
              onNavigate={() => {
                setSelectedCategoryId(null);
                setCurrentPage(1);
              }}
            >
              {t('forum.allPosts')}
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={styles.railChip}
              route={buildListRoute(1, selectedCategoryId, 'newest')}
              onNavigate={() => {
                setSortBy('newest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.newest')}
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={styles.railChip}
              route={buildListRoute(1, selectedCategoryId, 'hottest')}
              onNavigate={() => {
                setSortBy('hottest');
                setCurrentPage(1);
              }}
            >
              {t('forum.sort.hottest')}
            </PublicForumRouteLink>
            <PublicForumRouteLink
              className={styles.railChip}
              route={createDefaultSearchRoute()}
              onNavigate={onOpenSearch ? () => onOpenSearch() : undefined}
            >
              {t('forum.public.searchAction')}
            </PublicForumRouteLink>
          </div>
        </section>

        <section className={styles.sidePanel}>
          <p className={styles.sidePanelKicker}>{t('forum.public.listRailComposeTitle')}</p>
          <p className={styles.sidePanelText}>{t('forum.public.listRailComposeDescription')}</p>
          <PublicForumRouteLink
            className={`${styles.workspaceActionButton} ${styles.workspaceActionButtonPrimary} ${styles.sidePanelAction}`}
            route={composeRoute}
            onNavigate={() => onOpenCompose?.(selectedCategoryId)}
          >
            <Icon icon="mdi:pencil-plus-outline" size={18} />
            <span>{t('forum.public.listRailComposeAction')}</span>
          </PublicForumRouteLink>
        </section>
      </aside>
    </div>
  );
};
