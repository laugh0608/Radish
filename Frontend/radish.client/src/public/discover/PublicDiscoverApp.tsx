import {
  Fragment,
  useCallback,
  useEffect,
  useMemo,
  useState,
  type MouseEvent,
} from 'react';
import { useTranslation } from 'react-i18next';
import {
  createApiResponseError,
  getPublicDiscoverFeed,
  PublicDiscoverItemKinds,
  PublicDiscoverMetricKinds,
  PublicDiscoverTargetKinds,
  type PublicDiscoverFeedVo,
  type PublicDiscoverItemKind,
  type PublicDiscoverItemVo,
} from '@radish/http';
import {
  formatLocalizedNumber,
  formatLocalizedRelativeTime,
  resolveIntlLocale,
} from '@radish/ui';
import { Icon } from '@radish/ui/icon';
import { buildMessagesPath } from '@/messages/messagesRouteState';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import { buildPublicDiscoverPath, type PublicDiscoverRoute } from '../discoverRouteState';
import { buildPublicDocsPath, type PublicDocsRoute } from '../docsRouteState';
import {
  buildPublicLeaderboardPath,
  createDefaultPublicLeaderboardRoute,
  type PublicLeaderboardRoute,
} from '../leaderboardRouteState';
import { buildPublicForumPath, type PublicForumRoute } from '../forumRouteState';
import { buildPublicProfilePath } from '../profileRouteState';
import {
  buildPublicShopPath,
  createDefaultPublicShopProductsRoute,
  type PublicShopRoute,
} from '../shopRouteState';
import { buildPublicShareUrl } from '../publicHead';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import styles from './PublicDiscoverApp.module.css';

const DISCOVER_PAGE_SIZE = 10;

interface PublicDiscoverAppProps {
  route: PublicDiscoverRoute;
  onNavigate: (route?: PublicDiscoverRoute, options?: { replace?: boolean }) => void;
  onNavigateToForum: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
  onNavigateToDocs: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
  onNavigateToLeaderboard: (route: PublicLeaderboardRoute, options?: { replace?: boolean }) => void;
  onNavigateToShop: (route: PublicShopRoute, options?: { replace?: boolean }) => void;
  onNavigateToMessages: (channelId: string) => void;
  onNavigateToProfile: (userPublicId: string) => void;
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

interface ContributorSummary {
  publicId: string;
  displayName: string;
  avatarUrl?: string | null;
  contributionCount: number;
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

function getItemKindLabelKey(kind: PublicDiscoverItemKind): string {
  switch (kind) {
    case PublicDiscoverItemKinds.ChannelSummary:
      return 'discover.public.feedKindChannel';
    case PublicDiscoverItemKinds.MemberActivity:
      return 'discover.public.feedKindMemberActivity';
    case PublicDiscoverItemKinds.HighlightedComment:
      return 'discover.public.feedKindHighlightedComment';
    case PublicDiscoverItemKinds.Question:
      return 'discover.public.feedKindQuestion';
    default:
      return 'discover.public.feedKindPost';
  }
}

function getItemKindIcon(kind: PublicDiscoverItemKind): string {
  switch (kind) {
    case PublicDiscoverItemKinds.ChannelSummary:
      return 'mdi:forum-outline';
    case PublicDiscoverItemKinds.MemberActivity:
      return 'mdi:book-open-page-variant-outline';
    case PublicDiscoverItemKinds.HighlightedComment:
      return 'mdi:format-quote-close';
    case PublicDiscoverItemKinds.Question:
      return 'mdi:help-circle-outline';
    default:
      return 'mdi:file-document-outline';
  }
}

function getMetricLabelKey(item: PublicDiscoverItemVo): string | null {
  switch (item.voPrimaryMetric?.voKind) {
    case PublicDiscoverMetricKinds.RecentReplies:
      return 'discover.public.feedMetricRecentReplies';
    case PublicDiscoverMetricKinds.Likes:
      return 'discover.public.feedMetricLikes';
    case PublicDiscoverMetricKinds.Comments:
      return 'discover.public.feedMetricComments';
    case PublicDiscoverMetricKinds.Answers:
      return 'discover.public.feedMetricAnswers';
    default:
      return null;
  }
}

function formatPublicDiscoverCount(
  value: string,
  language: string | null | undefined,
): string {
  const normalized = value.trim();
  if (!/^\d+$/.test(normalized)) {
    return value;
  }

  return new Intl.NumberFormat(resolveIntlLocale(language)).format(BigInt(normalized));
}

function getPublicDiscoverPluralCount(value: string): number {
  return value.trim() === '1' ? 1 : 2;
}

function getContributorInitial(displayName: string): string {
  return Array.from(displayName.trim())[0]?.toUpperCase() ?? 'R';
}

function collectContributors(items: PublicDiscoverItemVo[]): ContributorSummary[] {
  const contributorMap = new Map<string, ContributorSummary>();
  for (const item of items) {
    const actor = item.voActor;
    if (!actor?.voPublicId) {
      continue;
    }

    const current = contributorMap.get(actor.voPublicId);
    if (current) {
      current.contributionCount += 1;
      continue;
    }

    contributorMap.set(actor.voPublicId, {
      publicId: actor.voPublicId,
      displayName: actor.voDisplayName,
      avatarUrl: actor.voAvatarThumbnailUrl,
      contributionCount: 1,
    });
  }

  return Array.from(contributorMap.values())
    .sort((left, right) => right.contributionCount - left.contributionCount)
    .slice(0, 3);
}

export const PublicDiscoverApp = ({
  route,
  onNavigate,
  onNavigateToForum,
  onNavigateToDocs,
  onNavigateToLeaderboard,
  onNavigateToShop,
  onNavigateToMessages,
  onNavigateToProfile,
}: PublicDiscoverAppProps) => {
  const { t, i18n } = useTranslation();
  const [feed, setFeed] = useState<PublicDiscoverFeedVo | null>(null);
  const [items, setItems] = useState<PublicDiscoverItemVo[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loadMoreError, setLoadMoreError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const buildDiscoverShareUrl = useCallback(
    () => buildPublicShareUrl(buildPublicDiscoverPath(route)),
    [route],
  );
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildDiscoverShareUrl,
  });

  useEffect(() => {
    let cancelled = false;

    const loadInitialFeed = async () => {
      setLoading(true);
      setLoadError(null);
      setLoadMoreError(null);
      try {
        const response = await getPublicDiscoverFeed({ pageSize: DISCOVER_PAGE_SIZE });
        if (!response.ok || !response.data) {
          throw createApiResponseError(response, t('discover.public.feedLoadFailedDescription'));
        }

        if (!cancelled) {
          setFeed(response.data);
          setItems(response.data.voItems);
        }
      } catch (error) {
        if (!cancelled) {
          setFeed(null);
          setItems([]);
          setLoadError(error instanceof Error ? error.message : t('discover.public.feedLoadFailedDescription'));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadInitialFeed();
    return () => {
      cancelled = true;
    };
  }, [reloadToken, t]);

  const loadMore = useCallback(async () => {
    if (!feed?.voHasMore || !feed.voNextCursor || loadingMore) {
      return;
    }

    setLoadingMore(true);
    setLoadMoreError(null);
    try {
      const response = await getPublicDiscoverFeed({
        cursor: feed.voNextCursor,
        pageSize: DISCOVER_PAGE_SIZE,
      });
      if (!response.ok || !response.data) {
        throw createApiResponseError(response, t('discover.public.feedLoadMoreFailed'));
      }

      const knownKeys = new Set(items.map((item) => item.voKey));
      const nextItems = response.data.voItems.filter((item) => !knownKeys.has(item.voKey));
      setItems((current) => [...current, ...nextItems]);
      setFeed(response.data);
    } catch (error) {
      setLoadMoreError(error instanceof Error ? error.message : t('discover.public.feedLoadMoreFailed'));
    } finally {
      setLoadingMore(false);
    }
  }, [feed, items, loadingMore, t]);

  const handlePublicDiscoverLinkClick = useCallback((
    event: MouseEvent<HTMLAnchorElement>,
    action: () => void,
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
  const contributors = useMemo(() => collectContributors(items), [items]);

  const getItemHref = useCallback((item: PublicDiscoverItemVo): string => {
    if (item.voTarget.voKind === PublicDiscoverTargetKinds.Messages && item.voTarget.voChannelId) {
      return buildMessagesPath({ channelId: item.voTarget.voChannelId });
    }

    if (item.voTarget.voKind === PublicDiscoverTargetKinds.Docs && item.voTarget.voDocumentSlug) {
      return buildPublicDocsPath({ kind: 'detail', slug: item.voTarget.voDocumentSlug });
    }

    if (item.voTarget.voPostPublicId) {
      return buildPublicForumPath({
        kind: 'detail',
        postId: item.voTarget.voPostPublicId,
        ...(item.voTarget.voCommentId ? { commentId: item.voTarget.voCommentId } : {}),
      });
    }

    return discoverHomeHref;
  }, [discoverHomeHref]);

  const navigateToItem = useCallback((item: PublicDiscoverItemVo) => {
    if (item.voTarget.voKind === PublicDiscoverTargetKinds.Messages && item.voTarget.voChannelId) {
      onNavigateToMessages(item.voTarget.voChannelId);
      return;
    }

    if (item.voTarget.voKind === PublicDiscoverTargetKinds.Docs && item.voTarget.voDocumentSlug) {
      onNavigateToDocs({ kind: 'detail', slug: item.voTarget.voDocumentSlug });
      return;
    }

    if (item.voTarget.voPostPublicId) {
      onNavigateToForum({
        kind: 'detail',
        postId: item.voTarget.voPostPublicId,
        ...(item.voTarget.voCommentId ? { commentId: item.voTarget.voCommentId } : {}),
      });
    }
  }, [onNavigateToDocs, onNavigateToForum, onNavigateToMessages]);

  const renderContributorStrip = (className: string) => {
    if (contributors.length === 0) {
      return null;
    }

    return (
      <section className={className} aria-label={t('discover.public.contributorsTitle')}>
        <div className={styles.contributorHeading}>
          <h2>{t('discover.public.contributorsTitle')}</h2>
          <span>{t('discover.public.contributorsWindow')}</span>
        </div>
        <div className={styles.contributorList}>
          {contributors.map((contributor) => {
            const profileHref = buildPublicProfilePath({
              kind: 'detail',
              userId: contributor.publicId,
              tab: 'posts',
              page: 1,
            });
            return (
              <a
                key={contributor.publicId}
                className={styles.contributorItem}
                href={profileHref}
                onClick={(event) => handlePublicDiscoverLinkClick(
                  event,
                  () => onNavigateToProfile(contributor.publicId),
                )}
              >
                {contributor.avatarUrl ? (
                  <img src={contributor.avatarUrl} alt="" className={styles.contributorAvatar} />
                ) : (
                  <span className={styles.contributorAvatarFallback} aria-hidden="true">
                    {getContributorInitial(contributor.displayName)}
                  </span>
                )}
                  <span className={styles.contributorCopy}>
                    <strong>{contributor.displayName}</strong>
                  <small>{t('discover.public.contributorCount', {
                    count: contributor.contributionCount,
                    formattedCount: formatLocalizedNumber(contributor.contributionCount, i18n.language),
                  })}</small>
                  </span>
              </a>
            );
          })}
        </div>
      </section>
    );
  };

  const renderItemMeta = (item: PublicDiscoverItemVo, isFocus = false) => {
    const metricKey = getMetricLabelKey(item);
    const metricValue = item.voPrimaryMetric?.voValue;
    return (
      <div className={isFocus ? styles.focusMetaLine : styles.itemMetaLine}>
        {item.voActor && <span>{item.voActor.voDisplayName}</span>}
        {metricKey && metricValue !== undefined && (
          <span>
            {t(metricKey, {
              count: getPublicDiscoverPluralCount(metricValue),
              formattedCount: formatPublicDiscoverCount(metricValue, i18n.language),
            })}
          </span>
        )}
        {item.voTarget.voRequiresAuthentication && (
          <span>{t('discover.public.feedRequiresSignIn')}</span>
        )}
      </div>
    );
  };

  const renderFeedItem = (item: PublicDiscoverItemVo, index: number) => {
    const isFocus = index === 0;
    const relativeTime = formatLocalizedRelativeTime(item.voOccurredAtUtc, i18n.language);
    const href = getItemHref(item);
    if (isFocus) {
      return (
        <a
          className={styles.focusEvent}
          href={href}
          onClick={(event) => handlePublicDiscoverLinkClick(event, () => navigateToItem(item))}
        >
          <div className={styles.focusTopline}>
            <span className={styles.focusKind}>
              <Icon icon={getItemKindIcon(item.voKind)} size={17} />
              {t(getItemKindLabelKey(item.voKind))}
            </span>
            <time dateTime={item.voOccurredAtUtc}>{relativeTime}</time>
          </div>
          <h2>{item.voTitle}</h2>
          {item.voSummary && <p>{item.voSummary}</p>}
          {renderItemMeta(item, true)}
        </a>
      );
    }

    return (
      <a
        className={styles.timelineEvent}
        href={href}
        data-kind={item.voKind}
        onClick={(event) => handlePublicDiscoverLinkClick(event, () => navigateToItem(item))}
      >
        <span className={styles.timelineNumber}>{String(index + 1).padStart(2, '0')}</span>
        <span className={styles.timelineNode} aria-hidden="true" />
        <span className={styles.timelineContent}>
          <span className={styles.itemTopline}>
            <span>{t(getItemKindLabelKey(item.voKind))}</span>
            <time dateTime={item.voOccurredAtUtc}>{relativeTime}</time>
          </span>
          <strong className={styles.timelineTitle}>{item.voTitle}</strong>
          {item.voSummary && <span className={styles.timelineSummary}>{item.voSummary}</span>}
          {renderItemMeta(item)}
        </span>
      </a>
    );
  };

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
            <Icon icon="mdi:arrow-right" size={17} />
          </a>
          <div className={styles.mobileFilterRow} aria-label={t('discover.public.mobileFilterLabel')}>
            <a
              className={`${styles.mobileFilterChip} ${styles.mobileFilterChipActive}`}
              href={discoverHomeHref}
              aria-current="page"
              onClick={(event) => handlePublicDiscoverLinkClick(
                event,
                () => onNavigate(discoverHomeRoute, { replace: true }),
              )}
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
              href={docsListHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToDocs(docsListRoute))}
            >
              {t('discover.public.mobileFilterKnowledge')}
            </a>
          </div>
        </section>

        <div className={styles.workspace}>
          <section className={styles.flowPanel} aria-labelledby="public-discover-flow-title">
            <header className={styles.flowHeader}>
              <div className={styles.flowTitleGroup}>
                <h1 id="public-discover-flow-title">{t('discover.public.flowTitle')}</h1>
                {!loading && <span className={styles.flowCount}>{items.length}</span>}
              </div>
              <div className={styles.flowTools}>
                <span className={styles.latestMode}>
                  <Icon icon="mdi:clock-outline" size={16} />
                  {t('discover.public.latestPublic')}
                </span>
                <button
                  type="button"
                  className={styles.iconButton}
                  onClick={() => void copyShareLink()}
                  disabled={shareBusy}
                  aria-label={shareBusy ? t('discover.public.shareSubmitting') : t('discover.public.shareAction')}
                >
                  <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
                </button>
              </div>
            </header>

            {shareState !== 'idle' && (
              <p className={styles.shareFeedback} data-state={shareState}>
                {shareState === 'success' ? t('discover.public.shareSuccess') : t('discover.public.shareFailed')}
              </p>
            )}

            <div className={styles.flowBody}>
              {loading ? (
                <SectionStatusCard
                  tone="loading"
                  title={t('discover.public.feedLoadingTitle')}
                  description={t('discover.public.feedLoadingDescription')}
                />
              ) : loadError ? (
                <SectionStatusCard
                  tone="error"
                  title={t('discover.public.feedLoadFailedTitle')}
                  description={loadError}
                  secondaryAction={{
                    label: t('common.retry'),
                    onClick: () => setReloadToken((current) => current + 1),
                  }}
                />
              ) : items.length === 0 ? (
                <SectionStatusCard
                  tone="empty"
                  title={t('discover.public.feedEmptyTitle')}
                  description={t('discover.public.feedEmptyDescription')}
                  primaryAction={{
                    label: t('discover.public.openForum'),
                    href: forumListHref,
                    onClick: () => onNavigateToForum(forumListRoute),
                  }}
                />
              ) : (
                <div className={styles.timeline}>
                  {items.map((item, index) => (
                    <Fragment key={item.voKey}>
                      {renderFeedItem(item, index)}
                      {index === 2 && renderContributorStrip(styles.mobileContributorNode)}
                    </Fragment>
                  ))}
                </div>
              )}
            </div>

            {!loading && !loadError && items.length > 0 && (
              <footer className={styles.flowFooter}>
                {loadMoreError && <p className={styles.loadMoreError}>{loadMoreError}</p>}
                {feed?.voHasMore ? (
                  <button
                    type="button"
                    className={styles.loadMoreButton}
                    onClick={() => void loadMore()}
                    disabled={loadingMore}
                  >
                    <span>{loadingMore ? t('discover.public.feedLoadingMore') : t('discover.public.feedLoadMore')}</span>
                    <Icon icon={loadingMore ? 'mdi:progress-clock' : 'mdi:arrow-down'} size={17} />
                  </button>
                ) : (
                  <a
                    className={styles.allCommunityLink}
                    href={forumListHref}
                    onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumListRoute))}
                  >
                    <span>{t('discover.public.viewAllCommunity')}</span>
                    <Icon icon="mdi:arrow-right" size={18} />
                  </a>
                )}
              </footer>
            )}
          </section>

          <aside className={styles.insightRail}>
            {feed && (
              <section className={styles.pulseCard} aria-labelledby="public-discover-pulse-title">
                <div className={styles.railHeading}>
                  <div>
                    <span>{t('discover.public.pulseEyebrow')}</span>
                    <h2 id="public-discover-pulse-title">{t('discover.public.pulseCardTitle')}</h2>
                  </div>
                  <Icon icon="mdi:pulse" size={20} />
                </div>
                <div className={styles.pulseMetrics}>
                  <div>
                    <strong>{formatPublicDiscoverCount(feed.voPulse.voDiscoverableChannelCount, i18n.language)}</strong>
                    <span>{t('discover.public.pulseChannels')}</span>
                  </div>
                  <div>
                    <strong>{formatPublicDiscoverCount(feed.voPulse.voEligibleItemCount, i18n.language)}</strong>
                    <span>{t('discover.public.pulseItems')}</span>
                  </div>
                  <div>
                    <strong>{formatPublicDiscoverCount(feed.voPulse.voKnowledgeContributionCount, i18n.language)}</strong>
                    <span>{t('discover.public.pulseKnowledge')}</span>
                  </div>
                </div>
              </section>
            )}

            {renderContributorStrip(styles.contributorCard)}

            <section className={styles.contextCard} aria-labelledby="public-discover-context-title">
              <div className={styles.contextHeader}>
                <h2 id="public-discover-context-title">{t('discover.public.contextTitle')}</h2>
                <span>{t('discover.public.contextHint')}</span>
              </div>
              <div className={styles.contextLinks}>
                <a
                  href={docsListHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToDocs(docsListRoute))}
                >
                  <span className={styles.contextIcon}><Icon icon="mdi:bookshelf" size={19} /></span>
                  <span><strong>{t('discover.public.contextDocsTitle')}</strong><small>{t('discover.public.contextDocsDescription')}</small></span>
                </a>
                <a
                  href={forumQuestionHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumQuestionRoute))}
                >
                  <span className={styles.contextIcon}><Icon icon="mdi:help-circle-outline" size={19} /></span>
                  <span><strong>{t('discover.public.contextQuestionsTitle')}</strong><small>{t('discover.public.contextQuestionsDescription')}</small></span>
                </a>
                <a
                  href={leaderboardHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToLeaderboard(leaderboardRoute))}
                >
                  <span className={styles.contextIcon}><Icon icon="mdi:chart-box-outline" size={19} /></span>
                  <span><strong>{t('discover.public.contextLeaderboardTitle')}</strong><small>{t('discover.public.contextLeaderboardDescription')}</small></span>
                </a>
                <a
                  href={shopProductsHref}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToShop(shopProductsRoute))}
                >
                  <span className={styles.contextIcon}><Icon icon="mdi:shopping-outline" size={19} /></span>
                  <span><strong>{t('discover.public.contextShopTitle')}</strong><small>{t('discover.public.contextShopDescription')}</small></span>
                </a>
              </div>
            </section>

            <section className={styles.participationCard}>
              <span className={styles.participationIcon}><Icon icon="mdi:sprout-outline" size={20} /></span>
              <div>
                <h2>{t('discover.public.participationTitle')}</h2>
                <p>{t('discover.public.participationDescription')}</p>
              </div>
              <a
                href={forumHotHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => onNavigateToForum(forumHotRoute))}
              >
                {t('discover.public.participationAction')}
                <Icon icon="mdi:arrow-right" size={17} />
              </a>
            </section>
          </aside>
        </div>
      </main>
    </div>
  );
};
