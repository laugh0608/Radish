import { useCallback, useEffect, useMemo, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { PostCard } from '@/apps/forum/components/PostCard';
import type { WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { getHotTags, getPostList, type PostItem, type Tag } from '@/api/forum';
import { getProducts, type ProductListItem } from '@/api/shop';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { getPublicWikiList } from '../docs/publicDocsApi';
import { getForumPostRouteIdentifier, resolvePublicProfileUserId } from '../forum/publicForumUtils';
import { buildPublicDiscoverPath, type PublicDiscoverRoute } from '../discoverRouteState';
import { buildPublicDocsPath, type PublicDocsRoute } from '../docsRouteState';
import {
  buildPublicLeaderboardPath,
  createDefaultPublicLeaderboardRoute,
  type PublicLeaderboardRoute,
} from '../leaderboardRouteState';
import { buildPublicForumPath, type PublicForumRoute } from '../forumRouteState';
import { buildPublicShopPath, createDefaultPublicShopProductsRoute, type PublicShopRoute } from '../shopRouteState';
import { buildPublicShareUrl } from '../publicHead';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import styles from './PublicDiscoverApp.module.css';

interface PublicDiscoverAppProps {
  route: PublicDiscoverRoute;
  onNavigate: (route?: PublicDiscoverRoute, options?: { replace?: boolean }) => void;
  onNavigateToForum: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
  onNavigateToDocs: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
  onNavigateToLeaderboard: (route: PublicLeaderboardRoute, options?: { replace?: boolean }) => void;
  onNavigateToShop: (route: PublicShopRoute, options?: { replace?: boolean }) => void;
}

interface SectionStatusAction {
  label: string;
  href?: string;
  onClick: () => void;
}

interface SectionStatusCardProps {
  tone: 'loading' | 'error' | 'empty';
  title: string;
  description: string;
  primaryAction?: SectionStatusAction;
  secondaryAction?: SectionStatusAction;
}

function shouldHandlePublicDiscoverLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function SectionStatusCard({
  tone,
  title,
  description,
  primaryAction,
  secondaryAction,
}: SectionStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = [];

  for (const [action, kind] of [[primaryAction, 'primary'], [secondaryAction, 'secondary']] as const) {
    if (!action) {
      continue;
    }

    actions.push({
      label: action.label,
      href: action.href,
      kind,
      onClick: action.href
        ? (event) => {
          if (!shouldHandlePublicDiscoverLink(event as MouseEvent<HTMLAnchorElement>)) {
            return;
          }

          event.preventDefault();
          action.onClick();
        }
        : () => action.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={icon}
      compact={true}
      actions={actions}
    />
  );
}

export const PublicDiscoverApp = ({
  route,
  onNavigate,
  onNavigateToForum,
  onNavigateToDocs,
  onNavigateToLeaderboard,
  onNavigateToShop,
}: PublicDiscoverAppProps) => {
  const { t } = useTranslation();
  const [forumPosts, setForumPosts] = useState<PostItem[]>([]);
  const [hotTags, setHotTags] = useState<Tag[]>([]);
  const [featuredDocument, setFeaturedDocument] = useState<WikiDocumentVo | null>(null);
  const [featuredProduct, setFeaturedProduct] = useState<ProductListItem | null>(null);
  const [loadingForum, setLoadingForum] = useState(true);
  const [forumError, setForumError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const buildDiscoverShareUrl = useCallback(
    () => buildPublicShareUrl(buildPublicDiscoverPath(route)),
    [route]
  );
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildDiscoverShareUrl,
  });

  useEffect(() => {
    let cancelled = false;

    const loadCommunity = async () => {
      setLoadingForum(true);
      setForumError(null);

      try {
        const [postPage, tagList] = await Promise.all([
          getPostList(null, t, 1, 8, 'newest'),
          getHotTags(t, 6),
        ]);
        if (!cancelled) {
          setForumPosts(postPage.data ?? []);
          setHotTags(tagList ?? []);
        }
      } catch (error) {
        if (!cancelled) {
          setForumPosts([]);
          setHotTags([]);
          setForumError(error instanceof Error ? error.message : String(error));
        }
      } finally {
        if (!cancelled) {
          setLoadingForum(false);
        }
      }
    };

    const loadRelatedContent = async () => {
      const [documentResult, productResult] = await Promise.allSettled([
        getPublicWikiList({ pageIndex: 1, pageSize: 1 }),
        getProducts(t, undefined, undefined, undefined, 1, 1),
      ]);
      if (cancelled) {
        return;
      }

      setFeaturedDocument(
        documentResult.status === 'fulfilled' ? documentResult.value.data?.[0] ?? null : null
      );
      setFeaturedProduct(
        productResult.status === 'fulfilled' && productResult.value.ok
          ? productResult.value.data?.data?.[0] ?? null
          : null
      );
    };

    void Promise.all([loadCommunity(), loadRelatedContent()]);
    return () => {
      cancelled = true;
    };
  }, [reloadToken, t]);

  const handlePublicDiscoverLinkClick = useCallback((
    event: MouseEvent<HTMLAnchorElement>,
    action: () => void
  ) => {
    if (!shouldHandlePublicDiscoverLink(event)) {
      return;
    }

    event.preventDefault();
    action();
  }, []);

  const discoverHomeRoute = useMemo<PublicDiscoverRoute>(() => ({ kind: 'home' }), []);
  const forumListRoute = useMemo<PublicForumRoute>(() => (
    { kind: 'list', categoryId: null, sortBy: 'newest', page: 1 }
  ), []);
  const forumHotRoute = useMemo<PublicForumRoute>(() => (
    { kind: 'list', categoryId: null, sortBy: 'hottest', page: 1 }
  ), []);
  const forumQuestionRoute = useMemo<PublicForumRoute>(() => (
    { kind: 'question', sortBy: 'newest', page: 1 }
  ), []);
  const forumSearchRoute = useMemo<PublicForumRoute>(() => (
    { kind: 'search', keyword: '', sortBy: 'newest', timeRange: 'all', page: 1 }
  ), []);
  const docsListRoute = useMemo<PublicDocsRoute>(() => ({ kind: 'list' }), []);
  const leaderboardRoute = useMemo<PublicLeaderboardRoute>(() => createDefaultPublicLeaderboardRoute(), []);
  const shopProductsRoute = useMemo<PublicShopRoute>(() => createDefaultPublicShopProductsRoute(), []);

  const discoverHomeHref = buildPublicDiscoverPath(discoverHomeRoute);
  const forumListHref = buildPublicForumPath(forumListRoute);
  const forumHotHref = buildPublicForumPath(forumHotRoute);
  const forumQuestionHref = buildPublicForumPath(forumQuestionRoute);
  const forumSearchHref = buildPublicForumPath(forumSearchRoute);
  const docsListHref = buildPublicDocsPath(docsListRoute);
  const leaderboardHref = buildPublicLeaderboardPath(leaderboardRoute);
  const shopProductsHref = buildPublicShopPath(shopProductsRoute);
  const spotlightPosts = forumPosts.slice(0, 5);
  const spotlightTags = hotTags.slice(0, 6);

  return (
    <div className={styles.page}>
      <PublicShellHeader
        brandMark="R"
        brandName={t('discover.public.homeBrandName')}
        brandSubline={t('discover.public.homeBrandSubline')}
        onBrandClick={() => {
          onNavigate(discoverHomeRoute, { replace: true });
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
        loginLabel={t('public.shell.loginAction')}
      />

      <main className={styles.main}>
        <section className={styles.mobileTaskPanel} aria-label={t('discover.public.mobileTaskLabel')}>
          <a
            className={styles.mobileSearchLink}
            href={forumSearchHref}
            onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumSearchRoute))}
          >
            <Icon icon="mdi:magnify" size={18} />
            <span>{t('discover.public.mobileSearchPlaceholder')}</span>
            <Icon icon="mdi:chevron-right" size={18} />
          </a>
          <div className={styles.mobileFilterRow} aria-label={t('discover.public.mobileFilterLabel')}>
            <a
              className={`${styles.mobileFilterChip} ${styles.mobileFilterChipActive}`}
              href={discoverHomeHref}
              aria-current="page"
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigate(discoverHomeRoute, { replace: true }))}
            >
              {t('discover.public.discussionAll')}
            </a>
            <a
              className={styles.mobileFilterChip}
              href={forumListHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumListRoute))}
            >
              {t('discover.public.mobileFilterLatest')}
            </a>
            <a
              className={styles.mobileFilterChip}
              href={forumQuestionHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumQuestionRoute))}
            >
              {t('discover.public.mobileFilterQuestion')}
            </a>
            <a
              className={styles.mobileFilterChip}
              href={forumHotHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumHotRoute))}
            >
              {t('discover.public.mobileFilterHot')}
            </a>
          </div>
        </section>

        <section className={styles.pulseHome}>
          <div className={styles.pulseIntroCard}>
            <h1 className={styles.pageTitle}>{t('discover.public.pulseTitle')}</h1>
            <p className={styles.pageIntro}>{t('discover.public.pulseIntro')}</p>
            <div className={styles.heroActions}>
              <a
                className={styles.primaryButton}
                href={forumListHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumListRoute))}
              >
                <Icon icon="mdi:forum-outline" size={18} />
                <span>{t('discover.public.openForum')}</span>
              </a>
              <a
                className={styles.secondaryButton}
                href={forumSearchHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumSearchRoute))}
              >
                <Icon icon="mdi:magnify" size={18} />
                <span>{t('discover.public.searchCommunity')}</span>
              </a>
              <button type="button" className={styles.secondaryButton} onClick={() => void copyShareLink()} disabled={shareBusy}>
                <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
                <span>{shareBusy ? t('discover.public.shareSubmitting') : t('discover.public.shareAction')}</span>
              </button>
            </div>
            {shareState !== 'idle' && (
              <p className={styles.shareFeedback} data-state={shareState}>
                {shareState === 'success' ? t('discover.public.shareSuccess') : t('discover.public.shareFailed')}
              </p>
            )}
          </div>
        </section>

        <section className={styles.communityBoard}>
          <div className={styles.discussionPanel}>
            <div className={styles.discussionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.discussionTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.discussionDescription')}</p>
              </div>
              <div className={styles.discussionTabRow} aria-label={t('discover.public.discussionTabsLabel')}>
                <span className={styles.discussionTabActive}>{t('discover.public.discussionAll')}</span>
                <a
                  className={styles.discussionTab}
                  href={forumListHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumListRoute))}
                >
                  {t('discover.public.mobileFilterLatest')}
                </a>
                <a
                  className={styles.discussionTab}
                  href={forumQuestionHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumQuestionRoute))}
                >
                  {t('discover.public.mobileFilterQuestion')}
                </a>
                <a
                  className={styles.discussionTab}
                  href={forumHotHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumHotRoute))}
                >
                  {t('discover.public.mobileFilterHot')}
                </a>
              </div>
            </div>

            {loadingForum ? (
              <SectionStatusCard
                tone="loading"
                title={t('discover.public.forumLoadingTitle')}
                description={t('discover.public.forumLoadingDescription')}
              />
            ) : forumError ? (
              <SectionStatusCard
                tone="error"
                title={t('discover.public.forumLoadFailedTitle')}
                description={forumError}
                secondaryAction={{
                  label: t('common.retry'),
                  onClick: () => setReloadToken((current) => current + 1),
                }}
              />
            ) : spotlightPosts.length === 0 ? (
              <SectionStatusCard
                tone="empty"
                title={t('discover.public.forumEmptyTitle')}
                description={t('discover.public.forumEmptyDescription')}
                primaryAction={{
                  label: t('discover.public.viewAllForum'),
                  href: forumListHref,
                  onClick: () => onNavigateToForum(forumListRoute),
                }}
              />
            ) : (
              <>
                <div className={styles.discussionList}>
                  {spotlightPosts.map((post) => (
                    <PostCard
                      key={post.voId}
                      post={post}
                      displayTimeZone={displayTimeZone}
                      onClick={() => onNavigateToForum({ kind: 'detail', postId: getForumPostRouteIdentifier(post) })}
                      href={buildPublicForumPath({ kind: 'detail', postId: getForumPostRouteIdentifier(post) })}
                      variant="publicCompact"
                      resolveAuthorProfileId={resolvePublicProfileUserId}
                      onTagClick={(_, tagSlug) => onNavigateToForum({ kind: 'tag', tagSlug, sortBy: 'newest', page: 1 })}
                      onQuestionClick={() => onNavigateToForum(forumQuestionRoute)}
                      onPollClick={() => onNavigateToForum({ kind: 'poll', sortBy: 'newest', page: 1 })}
                      onLotteryClick={() => onNavigateToForum({ kind: 'lottery', sortBy: 'newest', page: 1 })}
                    />
                  ))}
                </div>
                {spotlightTags.length > 0 && (
                  <div className={styles.tagRail}>
                    {spotlightTags.map((tag) => {
                      const tagRoute: PublicForumRoute = {
                        kind: 'tag',
                        tagSlug: tag.voSlug,
                        sortBy: 'newest',
                        page: 1,
                      };

                      return (
                        <a
                          key={tag.voId}
                          className={styles.tagChip}
                          href={buildPublicForumPath(tagRoute)}
                          onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(tagRoute))}
                        >
                          #{tag.voName}
                        </a>
                      );
                    })}
                  </div>
                )}
              </>
            )}
          </div>

          <aside className={styles.publicRail}>
            <section className={styles.railCard}>
              <div className={styles.railCardHeader}>
                <h2 className={styles.railTitle}>{t('discover.public.railHighlightsTitle')}</h2>
              </div>
              <div className={styles.railList}>
                <a
                  className={styles.railItem}
                  href={featuredDocument ? buildPublicDocsPath({ kind: 'detail', slug: featuredDocument.voSlug }) : docsListHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => (
                    featuredDocument
                      ? onNavigateToDocs({ kind: 'detail', slug: featuredDocument.voSlug })
                      : onNavigateToDocs(docsListRoute)
                  ))}
                >
                  <span className={styles.railBadge}>{t('discover.public.railDocLabel')}</span>
                  <span className={styles.railText}>{featuredDocument?.voTitle ?? t('discover.public.railDocFallback')}</span>
                </a>
                <a
                  className={styles.railItem}
                  href={featuredProduct ? buildPublicShopPath({ kind: 'detail', productId: String(featuredProduct.voId) }) : shopProductsHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => (
                    featuredProduct
                      ? onNavigateToShop({ kind: 'detail', productId: String(featuredProduct.voId) })
                      : onNavigateToShop(shopProductsRoute)
                  ))}
                >
                  <span className={styles.railBadge}>{t('discover.public.railShopLabel')}</span>
                  <span className={styles.railText}>{featuredProduct?.voName ?? t('discover.public.railShopFallback')}</span>
                </a>
                <a
                  className={styles.railItem}
                  href={leaderboardHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToLeaderboard(leaderboardRoute))}
                >
                  <span className={styles.railBadge}>{t('discover.public.railLeaderboardLabel')}</span>
                  <span className={styles.railText}>{t('discover.public.railLeaderboardText')}</span>
                </a>
              </div>
            </section>

            <section className={`${styles.railCard} ${styles.participationCard}`}>
              <h2 className={styles.railTitle}>{t('discover.public.participationTitle')}</h2>
              <p className={styles.railDescription}>{t('discover.public.participationDescription')}</p>
              <a
                className={styles.primaryButton}
                href={forumListHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumListRoute))}
              >
                <Icon icon="mdi:comment-text-outline" size={18} />
                <span>{t('discover.public.participationAction')}</span>
              </a>
            </section>
          </aside>
        </section>
      </main>
    </div>
  );
};
