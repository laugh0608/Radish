import { lazy, Suspense, useCallback, useEffect, useMemo, useState, type MouseEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { ExperienceBar } from '@radish/ui/experience-bar';
import { Icon } from '@radish/ui/icon';
import type { MyAttachmentItem } from '@/api/attachment';
import { coinApi, type CoinTransaction, type UserBalance } from '@/api/coin';
import { experienceApi, type ExperienceData, type ExpTransactionData } from '@/api/experience';
import type { UserPostQuickReply } from '@/api/forum';
import { getMyPet, type PetProfile } from '@/api/pet';
import {
  getMyBrowseHistory,
  getPublicProfile,
  type LongId,
  type PublicUserProfile,
  type UserComment,
  type UserBrowseHistoryItem,
  type UserPost,
} from '@/api/user';
import { getApiBaseUrl } from '@/config/env';
import { WebStateSlot } from '@/components/web-shell';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { buildPublicForumPath } from '@/public/forumRouteState';
import { resolvePublicUserRouteIdentifier } from '@/public/publicId';
import { buildPublicProfilePath, type PublicProfileRoute } from '@/public/profileRouteState';
import {
  createPublicRouteSourceState,
  rememberPublicRouteSourceTransfer,
  type PublicRouteDescriptor,
  type PublicRouteSourceState,
} from '@/public/publicRouteNavigation';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import {
  buildMeAttachmentsReturnPath,
  buildMeAssetTransactionsReturnPath,
  buildMeAssetsReturnPath,
  buildMeContentReturnPath,
  buildMeExperienceReturnPath,
  buildMeHistoryReturnPath,
  buildMeReturnPath,
} from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import { log } from '@/utils/logger';
import { resolveMediaUrl } from '@/utils/media';
import { MeAssetsPage } from './MeAssetsPage';
import { buildMePath, createDefaultMeRoute, parseMeRoute, type MeContentTab, type MeRoute } from './meRouteState';
import styles from './MeApp.module.css';

const UserPostList = lazy(() =>
  import('@/apps/profile/components/UserPostList').then((module) => ({ default: module.UserPostList }))
);
const UserCommentList = lazy(() =>
  import('@/apps/profile/components/UserCommentList').then((module) => ({ default: module.UserCommentList }))
);
const UserQuickReplyList = lazy(() =>
  import('@/apps/profile/components/UserQuickReplyList').then((module) => ({ default: module.UserQuickReplyList }))
);
const UserBrowseHistoryList = lazy(() =>
  import('@/apps/profile/components/UserBrowseHistoryList').then((module) => ({ default: module.UserBrowseHistoryList }))
);
const UserAttachmentList = lazy(() =>
  import('@/apps/profile/components/UserAttachmentList').then((module) => ({ default: module.UserAttachmentList }))
);
const ExperienceDetailApp = lazy(() =>
  import('@/apps/experience-detail/ExperienceDetailApp').then((module) => ({ default: module.ExperienceDetailApp }))
);

type MeDataErrorKey = 'profile' | 'experience' | 'assets' | 'browse' | 'pet';

interface MeDashboardData {
  publicProfile: PublicUserProfile | null;
  pet: PetProfile | null;
  experience: ExperienceData | null;
  expTransactions: ExpTransactionData[];
  balance: UserBalance | null;
  coinTransactions: CoinTransaction[];
  browseHistory: UserBrowseHistoryItem[];
  errors: Partial<Record<MeDataErrorKey, string>>;
  loadedAt: string | null;
}

interface MeSubPageMetric {
  icon: string;
  label: string;
  value: string;
  description?: string;
}

interface MeSubPagePreview {
  icon: string;
  badge: string;
  title: string;
  description: string;
  meta: string;
  href?: string | null;
  external?: boolean;
  rememberSource?: boolean;
  actionLabel?: string;
  stats?: string[];
}

interface MeSubPageContext {
  count: number;
  preview: MeSubPagePreview | null;
}

interface MeSubPageRailItem {
  icon: string;
  label: string;
  value: string;
}

interface MeExperienceContext extends MeSubPageContext {
  levelLabel: string | null;
  totalPages: number;
}

const initialDashboardData: MeDashboardData = {
  publicProfile: null,
  pet: null,
  experience: null,
  expTransactions: [],
  balance: null,
  coinTransactions: [],
  browseHistory: [],
  errors: {},
  loadedAt: null,
};

const meRouteDescriptor: PublicRouteDescriptor = {
  app: 'me',
  route: { kind: 'index' },
};

const contentTabs: Array<{ tab: MeContentTab; labelKey: string }> = [
  { tab: 'posts', labelKey: 'me.content.tab.posts' },
  { tab: 'comments', labelKey: 'me.content.tab.comments' },
  { tab: 'quick-replies', labelKey: 'me.content.tab.quickReplies' },
];

const initialSubPageContext: MeSubPageContext = {
  count: 0,
  preview: null,
};

const initialExperienceContext: MeExperienceContext = {
  count: 0,
  preview: null,
  levelLabel: null,
  totalPages: 1,
};

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim() ? error.message : fallback;
}

function formatNumber(value: number | null | undefined): string {
  return Number(value ?? 0).toLocaleString();
}

function formatSignedNumber(value: number): string {
  return `${value > 0 ? '+' : ''}${formatNumber(value)}`;
}

function truncatePreviewText(value: string | null | undefined, fallback: string, maxLength = 96): string {
  const normalized = value?.replace(/\s+/g, ' ').trim();
  if (!normalized) {
    return fallback;
  }

  return normalized.length > maxLength ? `${normalized.slice(0, maxLength)}...` : normalized;
}

function resolveProfileIdentifier(profile: PublicUserProfile | null, fallbackUserId: string): string | null {
  return resolvePublicUserRouteIdentifier(profile, fallbackUserId);
}

function buildSelfProfileRoute(profile: PublicUserProfile | null, fallbackUserId: string): PublicProfileRoute | null {
  const userId = resolveProfileIdentifier(profile, fallbackUserId);
  if (!userId) {
    return null;
  }

  return {
    kind: 'detail',
    userId,
    tab: 'posts',
    page: 1,
  };
}

function normalizeInternalPath(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  if (!trimmed || !trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.includes('\\')) {
    return null;
  }

  try {
    const url = new URL(trimmed, 'https://radish.local');
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return null;
  }
}

function normalizePositiveId(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  return trimmed && /^[1-9]\d*$/.test(trimmed) ? trimmed : null;
}

function buildBrowseHistoryHref(item: UserBrowseHistoryItem): string | null {
  const routePath = normalizeInternalPath(item.voRoutePath);
  if (routePath) {
    const pathname = new URL(routePath, 'https://radish.local').pathname;
    if (
      pathname.startsWith('/forum/post/')
      || pathname.startsWith('/docs/')
      || pathname.startsWith('/shop/product/')
      || pathname.startsWith('/u/')
    ) {
      return routePath;
    }
  }

  const targetId = normalizePositiveId(item.voTargetId);
  if (item.voTargetType === 'Post' && targetId) {
    return `/forum/post/${targetId}`;
  }

  if (item.voTargetType === 'Product' && targetId) {
    return `/shop/product/${targetId}`;
  }

  if (item.voTargetType === 'Wiki') {
    const slug = item.voTargetSlug?.trim();
    if (slug) {
      return `/docs/${encodeURIComponent(slug)}`;
    }

    if (targetId) {
      return `/docs/${targetId}`;
    }
  }

  return null;
}

function buildForumDetailHref(postId: LongId, postPublicId?: string | null, commentId?: LongId): string {
  const normalizedPublicId = postPublicId?.trim();
  const normalizedPostId = String(postId);
  return buildPublicForumPath({
    kind: 'detail',
    postId: normalizedPublicId || normalizedPostId,
    ...(normalizedPublicId ? { postPublicId: normalizedPublicId } : {}),
    ...(commentId ? { commentId: String(commentId) } : {})
  });
}

function isPublicDocsDetailPath(pathname: string): boolean {
  const normalizedPathname = pathname.endsWith('/') ? pathname.slice(0, -1) : pathname;
  const docsPathPrefix = '/docs/';
  if (!normalizedPathname.startsWith(docsPathPrefix) || normalizedPathname === '/docs/search') {
    return false;
  }

  const slugSegment = normalizedPathname.slice(docsPathPrefix.length);
  return slugSegment.length > 0 && !slugSegment.includes('/');
}

function buildSourceStateForHref(href: string): PublicRouteSourceState | null {
  const normalizedPath = normalizeInternalPath(href);
  if (!normalizedPath) {
    return null;
  }

  const pathname = new URL(normalizedPath, 'https://radish.local').pathname;
  if (pathname.startsWith('/forum/post/')) {
    return { forumDetailSourceRoute: meRouteDescriptor };
  }

  if (isPublicDocsDetailPath(pathname)) {
    return { docsDetailSourceRoute: meRouteDescriptor };
  }

  if (pathname.startsWith('/shop/product/')) {
    return { shopDetailSourceRoute: meRouteDescriptor };
  }

  if (pathname.startsWith('/u/')) {
    return { profileSourceRoute: meRouteDescriptor };
  }

  return null;
}

function shouldHandlePlainLinkClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function resolveInitialMeRoute(): MeRoute {
  if (typeof window === 'undefined') {
    return createDefaultMeRoute();
  }

  return parseMeRoute(window.location.pathname, window.location.search) ?? createDefaultMeRoute();
}

function buildMeRouteReturnPath(route: MeRoute): string {
  if (route.kind === 'assets') {
    return buildMeAssetsReturnPath();
  }

  if (route.kind === 'assets-transactions') {
    return buildMeAssetTransactionsReturnPath();
  }

  if (route.kind === 'content') {
    return buildMeContentReturnPath({ tab: route.tab, page: route.page }) ?? buildMeReturnPath();
  }

  if (route.kind === 'history') {
    return buildMeHistoryReturnPath({ page: route.page }) ?? buildMeReturnPath();
  }

  if (route.kind === 'attachments') {
    return buildMeAttachmentsReturnPath({
      businessType: route.businessType,
      keyword: route.keyword,
      page: route.page
    }) ?? buildMeReturnPath();
  }

  if (route.kind === 'experience') {
    return buildMeExperienceReturnPath({ page: route.page }) ?? buildMeReturnPath();
  }

  return buildMeReturnPath();
}

export const MeApp = () => {
  const { t } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  const userId = useUserStore(state => state.userId);
  const storeDisplayName = useUserStore(state => state.displayName);
  const userName = useUserStore(state => state.userName);
  const displayHandle = useUserStore(state => state.displayHandle);
  const nickname = useUserStore(state => state.nickname);
  const avatarUrl = useUserStore(state => state.avatarUrl);
  const avatarThumbnailUrl = useUserStore(state => state.avatarThumbnailUrl);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [route, setRoute] = useState<MeRoute>(() => resolveInitialMeRoute());
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [dashboardData, setDashboardData] = useState<MeDashboardData>(initialDashboardData);
  const [loadingData, setLoadingData] = useState(false);
  const [contentContext, setContentContext] = useState<MeSubPageContext>(initialSubPageContext);
  const [historyContext, setHistoryContext] = useState<MeSubPageContext>(initialSubPageContext);
  const [attachmentContext, setAttachmentContext] = useState<MeSubPageContext>(initialSubPageContext);
  const [experienceContext, setExperienceContext] = useState<MeExperienceContext>(initialExperienceContext);

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('MeApp', '我的状态登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    const title = route.kind === 'assets'
      ? t('me.assets.overviewTitle')
      : route.kind === 'assets-transactions'
        ? t('me.assets.transactionsTitle')
        : route.kind === 'content'
          ? t('me.content.title')
          : route.kind === 'history'
            ? t('me.history.title')
            : route.kind === 'attachments'
              ? t('me.attachments.title')
              : route.kind === 'experience'
                ? t('me.experience.detailTitle')
                : t('me.title');
    document.title = `${title} · Radish`;
  }, [route.kind, t]);

  useEffect(() => {
    const handlePopState = () => {
      setRoute(resolveInitialMeRoute());
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  useEffect(() => {
    const canonicalPath = buildMePath(route);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, [route]);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({
      returnPath: buildMeRouteReturnPath(route)
    });
  }, [authReady, loggedIn, redirecting, route]);

  const loadDashboardData = useCallback(async () => {
    if (!userId.trim()) {
      return;
    }

    setLoadingData(true);

    const [
      profileResult,
      experienceResult,
      expTransactionsResult,
      balanceResult,
      coinTransactionsResult,
      browseHistoryResult,
      petResult,
    ] = await Promise.allSettled([
      getPublicProfile(userId),
      experienceApi.getMyExperience(),
      experienceApi.getTransactions({ pageIndex: 1, pageSize: 5 }),
      coinApi.getBalance(),
      coinApi.getTransactions(1, 5),
      getMyBrowseHistory(1, 5),
      getMyPet(),
    ]);

    const errors: MeDashboardData['errors'] = {};

    if (profileResult.status === 'rejected') {
      errors.profile = getErrorMessage(profileResult.reason, t('me.error.profile'));
      log.warn('MeApp', '加载公开资料失败', profileResult.reason);
    }
    if (experienceResult.status === 'rejected' || expTransactionsResult.status === 'rejected') {
      const reason = experienceResult.status === 'rejected'
        ? experienceResult.reason
        : expTransactionsResult.status === 'rejected'
          ? expTransactionsResult.reason
          : null;
      errors.experience = getErrorMessage(reason, t('me.error.experience'));
      log.warn('MeApp', '加载经验状态失败', reason);
    }
    if (balanceResult.status === 'rejected' || coinTransactionsResult.status === 'rejected') {
      const reason = balanceResult.status === 'rejected'
        ? balanceResult.reason
        : coinTransactionsResult.status === 'rejected'
          ? coinTransactionsResult.reason
          : null;
      errors.assets = getErrorMessage(reason, t('me.error.assets'));
      log.warn('MeApp', '加载资产状态失败', reason);
    }
    if (browseHistoryResult.status === 'rejected') {
      errors.browse = getErrorMessage(browseHistoryResult.reason, t('me.error.browse'));
      log.warn('MeApp', '加载最近访问失败', browseHistoryResult.reason);
    }
    if (petResult.status === 'rejected') {
      errors.pet = getErrorMessage(petResult.reason, t('me.error.pet'));
      log.warn('MeApp', '加载电子宠物摘要失败', petResult.reason);
    }

    setDashboardData({
      publicProfile: profileResult.status === 'fulfilled' ? profileResult.value : null,
      pet: petResult.status === 'fulfilled' ? petResult.value : null,
      experience: experienceResult.status === 'fulfilled' ? experienceResult.value : null,
      expTransactions: expTransactionsResult.status === 'fulfilled' ? expTransactionsResult.value?.data ?? [] : [],
      balance: balanceResult.status === 'fulfilled' ? balanceResult.value : null,
      coinTransactions: coinTransactionsResult.status === 'fulfilled' ? coinTransactionsResult.value.data : [],
      browseHistory: browseHistoryResult.status === 'fulfilled' ? browseHistoryResult.value.voItems : [],
      errors,
      loadedAt: new Date().toISOString(),
    });
    setLoadingData(false);
  }, [t, userId]);

  useEffect(() => {
    if (route.kind === 'dashboard' && authReady && loggedIn) {
      void loadDashboardData();
    }
  }, [authReady, loadDashboardData, loggedIn, route.kind]);

  useEffect(() => {
    if (route.kind === 'content') {
      setContentContext(initialSubPageContext);
      return;
    }

    if (route.kind === 'history') {
      setHistoryContext(initialSubPageContext);
      return;
    }

    if (route.kind === 'attachments') {
      setAttachmentContext(initialSubPageContext);
      return;
    }

    if (route.kind === 'experience') {
      setExperienceContext(initialExperienceContext);
    }
  }, [route]);

  const navigateToMeRoute = useCallback((nextRoute: MeRoute) => {
    const nextPath = buildMePath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== nextPath) {
      window.history.pushState(window.history.state, '', nextPath);
    }
    setRoute(nextRoute);
  }, []);

  const selfProfileRoute = useMemo(
    () => buildSelfProfileRoute(dashboardData.publicProfile, userId),
    [dashboardData.publicProfile, userId]
  );
  const selfProfilePath = selfProfileRoute ? buildPublicProfilePath(selfProfileRoute) : null;
  const resolvedAvatarUrl = resolveMediaUrl(
    dashboardData.publicProfile?.voAvatarThumbnailUrl
      || dashboardData.publicProfile?.voAvatarUrl
      || avatarThumbnailUrl
      || avatarUrl
  );
  const displayName = dashboardData.publicProfile?.voDisplayName?.trim()
    || nickname?.trim()
    || storeDisplayName?.trim()
    || userName?.trim()
    || t('me.userFallback');
  const accountName = dashboardData.publicProfile?.voDisplayHandle?.trim()
    || displayHandle?.trim()
    || displayName
    || userId;
  const experience = dashboardData.experience;
  const balance = dashboardData.balance;
  const pet = dashboardData.pet;
  const loadedAtLabel = dashboardData.loadedAt
    ? formatDateTimeByTimeZone(dashboardData.loadedAt, displayTimeZone)
    : null;

  const rememberSelfPublicProfileSource = useCallback((event: MouseEvent<HTMLAnchorElement>) => {
    if (!selfProfileRoute || !selfProfilePath || !shouldHandlePlainLinkClick(event)) {
      return;
    }

    const sourceState = createPublicRouteSourceState(
      {},
      meRouteDescriptor,
      { app: 'profile', route: selfProfileRoute }
    );
    rememberPublicRouteSourceTransfer(selfProfilePath, sourceState);
  }, [selfProfilePath, selfProfileRoute]);

  const rememberSourceForPublicLink = useCallback((event: MouseEvent<HTMLAnchorElement>, href: string) => {
    if (!shouldHandlePlainLinkClick(event)) {
      return;
    }

    const sourceState = buildSourceStateForHref(href);
    if (sourceState) {
      rememberPublicRouteSourceTransfer(href, sourceState);
    }
  }, []);

  const renderStatusPanel = (title: string, description: string, icon = 'mdi:account-circle-outline') => (
    <WebStateSlot tone={icon === 'mdi:progress-clock' ? 'loading' : 'auth'} title={title} description={description} icon={icon} />
  );

  const formatAttachmentBusinessType = (businessType: string | null | undefined): string => {
    if (!businessType || businessType === 'All') {
      return t('common.all');
    }

    const keyMap: Record<string, string> = {
      General: 'profile.attachments.business.general',
      Post: 'profile.attachments.business.post',
      Comment: 'profile.attachments.business.comment',
      Avatar: 'profile.attachments.business.avatar',
      Document: 'profile.attachments.business.document',
    };

    return t(keyMap[businessType] ?? 'profile.attachments.business.general');
  };

  const renderSubPageNav = () => {
    const navItems: Array<{ key: string; icon: string; label: string; route: MeRoute; active: boolean }> = [
      {
        key: 'content',
        icon: 'mdi:file-document-edit-outline',
        label: t('me.content.title'),
        route: route.kind === 'content' ? route : { kind: 'content', tab: 'posts', page: 1 },
        active: route.kind === 'content',
      },
      {
        key: 'history',
        icon: 'mdi:history',
        label: t('me.history.title'),
        route: route.kind === 'history' ? route : { kind: 'history', page: 1 },
        active: route.kind === 'history',
      },
      {
        key: 'attachments',
        icon: 'mdi:paperclip',
        label: t('me.attachments.title'),
        route: route.kind === 'attachments'
          ? route
          : { kind: 'attachments', businessType: 'All', keyword: '', page: 1 },
        active: route.kind === 'attachments',
      },
      {
        key: 'experience',
        icon: 'mdi:star-circle-outline',
        label: t('me.experience.detailTitle'),
        route: route.kind === 'experience' ? route : { kind: 'experience', page: 1 },
        active: route.kind === 'experience',
      },
    ];

    return (
      <nav className={styles.subPageRouteTabs} aria-label={t('me.subPage.routeTabsLabel')}>
        {navItems.map((item) => {
          const href = buildMePath(item.route);
          return (
            <a
              key={item.key}
              className={item.active ? styles.subPageRouteTabActive : styles.subPageRouteTab}
              href={href}
              aria-current={item.active ? 'page' : undefined}
              onClick={(event) => {
                if (!shouldHandlePlainLinkClick(event)) {
                  return;
                }

                event.preventDefault();
                navigateToMeRoute(item.route);
              }}
            >
              <Icon icon={item.icon} size={17} />
              <span>{item.label}</span>
            </a>
          );
        })}
      </nav>
    );
  };

  const renderSubPageMetrics = (metrics: MeSubPageMetric[]) => (
    <div className={styles.subPageMetrics}>
      {metrics.map((metric) => (
        <div key={`${metric.label}-${metric.value}`} className={styles.subPageMetric}>
          <span className={styles.subPageMetricIcon} aria-hidden="true">
            <Icon icon={metric.icon} size={18} />
          </span>
          <span className={styles.subPageMetricBody}>
            <span className={styles.subPageMetricLabel}>{metric.label}</span>
            <strong>{metric.value}</strong>
            {metric.description ? <span>{metric.description}</span> : null}
          </span>
        </div>
      ))}
    </div>
  );

  const renderPreviewRail = (
    title: string,
    description: string,
    preview: MeSubPagePreview | null,
    railItems: MeSubPageRailItem[] = []
  ) => (
    <aside className={styles.subPageRail} aria-label={title}>
      <div className={styles.railHeader}>
        <p className={styles.kicker}>{t('me.subPage.contextKicker')}</p>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>

      {preview ? (
        <article className={styles.previewCard}>
          <div className={styles.previewBadge}>
            <Icon icon={preview.icon} size={18} />
            <span>{preview.badge}</span>
          </div>
          <h3>{preview.title}</h3>
          <p>{preview.description}</p>
          <div className={styles.previewMeta}>{preview.meta}</div>
          {preview.stats && preview.stats.length > 0 ? (
            <div className={styles.previewStats}>
              {preview.stats.map((item) => <span key={item}>{item}</span>)}
            </div>
          ) : null}
          {preview.href ? (
            <a
              className={styles.previewAction}
              href={preview.href}
              target={preview.external ? '_blank' : undefined}
              rel={preview.external ? 'noreferrer' : undefined}
              onClick={(event) => {
                if (preview.rememberSource && preview.href) {
                  rememberSourceForPublicLink(event, preview.href);
                }
              }}
            >
              <span>{preview.actionLabel ?? t('me.preview.open')}</span>
              <Icon icon={preview.external ? 'mdi:open-in-new' : 'mdi:arrow-right'} size={16} />
            </a>
          ) : null}
        </article>
      ) : (
        <div className={styles.previewEmpty}>
          <Icon icon="mdi:tray-search" size={26} />
          <span>{t('me.preview.empty')}</span>
        </div>
      )}

      {railItems.length > 0 ? (
        <div className={styles.railItemList}>
          {railItems.map((item) => (
            <div key={`${item.label}-${item.value}`} className={styles.railItem}>
              <span className={styles.railItemIcon} aria-hidden="true">
                <Icon icon={item.icon} size={16} />
              </span>
              <span>
                <strong>{item.label}</strong>
                <small>{item.value}</small>
              </span>
            </div>
          ))}
        </div>
      ) : null}
    </aside>
  );

  const renderSubPageShell = ({
    title,
    description,
    content,
    metrics,
    railTitle,
    railDescription,
    preview,
    railItems,
    actions,
  }: {
    title: string;
    description: string;
    content: ReactNode;
    metrics: MeSubPageMetric[];
    railTitle: string;
    railDescription: string;
    preview: MeSubPagePreview | null;
    railItems?: MeSubPageRailItem[];
    actions?: ReactNode;
  }) => (
    <section className={styles.subPagePanel}>
      <div className={styles.subPageHero}>
        <div className={styles.subPageHeader}>
          <div>
            <p className={styles.kicker}>{t('me.privateCenterKicker')}</p>
            <h1>{title}</h1>
            <p>{description}</p>
          </div>
          <div className={styles.subPageActions}>
            {actions}
            <a
              className={styles.secondaryButton}
              href={buildMePath({ kind: 'dashboard' })}
              onClick={(event) => {
                if (!shouldHandlePlainLinkClick(event)) {
                  return;
                }

                event.preventDefault();
                navigateToMeRoute({ kind: 'dashboard' });
              }}
            >
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('me.assets.backToStatus')}</span>
            </a>
          </div>
        </div>
        {renderSubPageNav()}
        {renderSubPageMetrics(metrics)}
      </div>
      <div className={styles.subPageBodyGrid}>
        <div className={styles.subPagePrimary}>
          <Suspense fallback={<div className={styles.loadingBlock}>{t('common.loading')}</div>}>
            {content}
          </Suspense>
        </div>
        {renderPreviewRail(railTitle, railDescription, preview, railItems)}
      </div>
    </section>
  );

  const renderContentPage = () => {
    if (route.kind !== 'content') {
      return null;
    }

    const activeContentTab = contentTabs.find((item) => item.tab === route.tab) ?? contentTabs[0];
    const activeContentTabLabel = t(activeContentTab.labelKey);

    const tabActions = (
      <div className={styles.segmentedTabs} role="tablist" aria-label={t('me.content.tabsLabel')}>
        {contentTabs.map((item) => {
          const href = buildMePath({ kind: 'content', tab: item.tab, page: 1 });
          return (
            <a
              key={item.tab}
              href={href}
              className={route.tab === item.tab ? styles.segmentedTabActive : styles.segmentedTab}
              role="tab"
              aria-selected={route.tab === item.tab}
              onClick={(event) => {
                if (!shouldHandlePlainLinkClick(event)) {
                  return;
                }

                event.preventDefault();
                navigateToMeRoute({ kind: 'content', tab: item.tab, page: 1 });
              }}
            >
              {t(item.labelKey)}
            </a>
          );
        })}
      </div>
    );

    const pageContent = route.tab === 'posts' ? (
      <UserPostList
        userId={userId}
        apiBaseUrl={apiBaseUrl}
        displayTimeZone={displayTimeZone}
        page={route.page}
        onPageChange={(page) => navigateToMeRoute({ ...route, page })}
        getPostHref={(postId, postPublicId) => buildForumDetailHref(postId, postPublicId)}
        onPostLinkClick={(event, href) => rememberSourceForPublicLink(event, href)}
        onItemsLoaded={(items: UserPost[]) => {
          const firstItem = items[0];
          const preview: MeSubPagePreview | null = firstItem ? {
            icon: 'mdi:file-document-edit-outline',
            badge: activeContentTabLabel,
            title: firstItem.voTitle,
            description: truncatePreviewText(firstItem.voContent, t('me.preview.noSummary')),
            meta: formatDateTimeByTimeZone(firstItem.voCreateTime, displayTimeZone),
            href: buildForumDetailHref(firstItem.voId, firstItem.voPublicId),
            rememberSource: true,
            actionLabel: t('me.preview.openPublicDetail'),
            stats: [
              t('me.preview.views', { count: firstItem.voViewCount }),
              t('me.preview.likes', { count: firstItem.voLikeCount }),
              t('me.preview.comments', { count: firstItem.voCommentCount }),
            ],
          } : null;
          setContentContext({ count: items.length, preview });
        }}
      />
    ) : route.tab === 'comments' ? (
      <UserCommentList
        userId={userId}
        apiBaseUrl={apiBaseUrl}
        displayTimeZone={displayTimeZone}
        page={route.page}
        onPageChange={(page) => navigateToMeRoute({ ...route, page })}
        getCommentHref={(postId, commentId, postPublicId) => buildForumDetailHref(postId, postPublicId, commentId)}
        onCommentLinkClick={(event, href) => rememberSourceForPublicLink(event, href)}
        onItemsLoaded={(items: UserComment[]) => {
          const firstItem = items[0];
          const preview: MeSubPagePreview | null = firstItem ? {
            icon: 'mdi:comment-text-outline',
            badge: activeContentTabLabel,
            title: t('me.preview.commentTitle'),
            description: truncatePreviewText(firstItem.voContent, t('me.preview.noSummary')),
            meta: formatDateTimeByTimeZone(firstItem.voCreateTime, displayTimeZone),
            href: buildForumDetailHref(firstItem.voPostId, firstItem.voPostPublicId, firstItem.voId),
            rememberSource: true,
            actionLabel: t('me.preview.openPublicDetail'),
            stats: [
              t('me.preview.likes', { count: firstItem.voLikeCount }),
              t('me.preview.anchorReady'),
            ],
          } : null;
          setContentContext({ count: items.length, preview });
        }}
      />
    ) : (
      <UserQuickReplyList
        displayTimeZone={displayTimeZone}
        page={route.page}
        onPageChange={(page) => navigateToMeRoute({ ...route, page })}
        getItemHref={(postId, postPublicId) => buildForumDetailHref(postId, postPublicId)}
        onItemLinkClick={(event, href) => rememberSourceForPublicLink(event, href)}
        onItemsLoaded={(items: UserPostQuickReply[]) => {
          const firstItem = items[0];
          const preview: MeSubPagePreview | null = firstItem ? {
            icon: 'mdi:comment-quote-outline',
            badge: activeContentTabLabel,
            title: firstItem.voPostTitle,
            description: truncatePreviewText(firstItem.voContent, t('me.preview.noSummary')),
            meta: formatDateTimeByTimeZone(firstItem.voCreateTime, displayTimeZone),
            href: buildForumDetailHref(firstItem.voPostId, firstItem.voPostPublicId),
            rememberSource: true,
            actionLabel: t('me.preview.openPublicDetail'),
            stats: [t('me.preview.quickReplySource')],
          } : null;
          setContentContext({ count: items.length, preview });
        }}
      />
    );

    return renderSubPageShell({
      title: t('me.content.title'),
      description: t('me.content.description'),
      content: (
        <>
          {tabActions}
          {pageContent}
        </>
      ),
      metrics: [
        {
          icon: 'mdi:tab',
          label: t('me.task.currentTab'),
          value: activeContentTabLabel,
          description: t('me.task.contentTabDescription'),
        },
        {
          icon: 'mdi:format-list-numbered',
          label: t('me.task.recordsThisPage'),
          value: t('me.task.recordCount', { count: contentContext.count }),
          description: t('me.task.pageValue', { page: route.page }),
        },
        {
          icon: 'mdi:arrow-u-left-top',
          label: t('me.task.returnContract'),
          value: t('me.task.publicDetail'),
          description: t('me.task.sourceReturnReady'),
        },
      ],
      railTitle: t('me.content.railTitle'),
      railDescription: t('me.content.railDescription'),
      preview: contentContext.preview,
      railItems: [
        {
          icon: 'mdi:link-variant',
          label: t('me.content.rail.publicReturn'),
          value: t('me.content.rail.publicReturnValue'),
        },
        {
          icon: 'mdi:timeline-check-outline',
          label: t('me.content.rail.tabScope'),
          value: activeContentTabLabel,
        },
        {
          icon: 'mdi:shield-account-outline',
          label: t('me.content.rail.contract'),
          value: t('me.content.rail.contractValue'),
        },
      ],
    });
  };

  const renderHistoryPage = () => {
    if (route.kind !== 'history') {
      return null;
    }

    return renderSubPageShell({
      title: t('me.history.title'),
      description: t('me.history.description'),
      content: (
        <UserBrowseHistoryList
          displayTimeZone={displayTimeZone}
          page={route.page}
          onPageChange={(page) => navigateToMeRoute({ kind: 'history', page })}
          getItemHref={buildBrowseHistoryHref}
          onItemLinkClick={(event, href) => rememberSourceForPublicLink(event, href)}
          onItemsLoaded={(items: UserBrowseHistoryItem[]) => {
            const firstItem = items[0];
            const href = firstItem ? buildBrowseHistoryHref(firstItem) : null;
            const preview: MeSubPagePreview | null = firstItem ? {
              icon: 'mdi:history',
              badge: firstItem.voTargetTypeDisplay,
              title: firstItem.voTitle,
              description: truncatePreviewText(firstItem.voSummary, t('profile.browse.noSummary')),
              meta: formatDateTimeByTimeZone(firstItem.voLastViewTime, displayTimeZone),
              href,
              rememberSource: Boolean(href),
              actionLabel: t('me.preview.openPublicRoute'),
              stats: [
                t('me.viewCount', { count: firstItem.voViewCount }),
                firstItem.voRoutePath?.trim() || t('profile.browse.internalRoute'),
              ],
            } : null;
            setHistoryContext({ count: items.length, preview });
          }}
        />
      ),
      metrics: [
        {
          icon: 'mdi:format-list-numbered',
          label: t('me.task.recordsThisPage'),
          value: t('me.task.recordCount', { count: historyContext.count }),
          description: t('me.task.pageValue', { page: route.page }),
        },
        {
          icon: 'mdi:routes',
          label: t('me.task.returnContract'),
          value: t('me.task.sourceReturn'),
          description: t('me.task.publicRouteReady'),
        },
        {
          icon: 'mdi:compass-outline',
          label: t('me.task.historyScope'),
          value: t('me.task.publicReadable'),
          description: t('me.task.noDesktopReturn'),
        },
      ],
      railTitle: t('me.history.railTitle'),
      railDescription: t('me.history.railDescription'),
      preview: historyContext.preview,
      railItems: [
        {
          icon: 'mdi:forum-outline',
          label: t('me.history.rail.forum'),
          value: t('me.history.rail.publicDetail'),
        },
        {
          icon: 'mdi:file-document-outline',
          label: t('me.history.rail.docs'),
          value: t('me.history.rail.publicDetail'),
        },
        {
          icon: 'mdi:shopping-outline',
          label: t('me.history.rail.shop'),
          value: t('me.history.rail.publicDetail'),
        },
      ],
    });
  };

  const renderAttachmentsPage = () => {
    if (route.kind !== 'attachments') {
      return null;
    }

    return renderSubPageShell({
      title: t('me.attachments.title'),
      description: t('me.attachments.description'),
      content: (
        <UserAttachmentList
          apiBaseUrl={apiBaseUrl}
          displayTimeZone={displayTimeZone}
          initialPage={route.page}
          initialBusinessType={route.businessType}
          initialKeyword={route.keyword}
          onItemsLoaded={(items: MyAttachmentItem[]) => {
            const firstItem = items[0];
            const href = firstItem ? resolveMediaUrl(firstItem.voUrl, apiBaseUrl) : null;
            const preview: MeSubPagePreview | null = firstItem ? {
              icon: firstItem.voThumbnailUrl ? 'mdi:image-outline' : 'mdi:file-document-outline',
              badge: formatAttachmentBusinessType(firstItem.voBusinessType),
              title: firstItem.voOriginalName,
              description: truncatePreviewText(
                firstItem.voMimeType || firstItem.voExtension,
                t('me.attachments.previewDescription')
              ),
              meta: firstItem.voCreateTime ? formatDateTimeByTimeZone(firstItem.voCreateTime, displayTimeZone) : t('me.preview.noTime'),
              href,
              external: Boolean(href),
              actionLabel: t('profile.attachments.download'),
              stats: [
                firstItem.voFileSizeFormatted || `${firstItem.voFileSize} B`,
                formatAttachmentBusinessType(firstItem.voBusinessType),
              ],
            } : null;
            setAttachmentContext({ count: items.length, preview });
          }}
          onStateChange={(state) => navigateToMeRoute({
            kind: 'attachments',
            businessType: state.businessType,
            keyword: state.keyword,
            page: state.page
          })}
        />
      ),
      metrics: [
        {
          icon: 'mdi:filter-variant',
          label: t('me.task.filter'),
          value: formatAttachmentBusinessType(route.businessType),
          description: t('me.task.pageValue', { page: route.page }),
        },
        {
          icon: 'mdi:magnify',
          label: t('me.task.keyword'),
          value: route.keyword || t('me.task.noKeyword'),
          description: t('me.task.recordsThisPage'),
        },
        {
          icon: 'mdi:paperclip',
          label: t('me.task.recordsThisPage'),
          value: t('me.task.recordCount', { count: attachmentContext.count }),
          description: t('me.task.attachmentOwnership'),
        },
      ],
      railTitle: t('me.attachments.railTitle'),
      railDescription: t('me.attachments.railDescription'),
      preview: attachmentContext.preview,
      railItems: [
        {
          icon: 'mdi:file-tree-outline',
          label: t('me.attachments.rail.businessType'),
          value: formatAttachmentBusinessType(route.businessType),
        },
        {
          icon: 'mdi:download-outline',
          label: t('me.attachments.rail.download'),
          value: t('me.attachments.rail.downloadValue'),
        },
        {
          icon: 'mdi:delete-outline',
          label: t('me.attachments.rail.manage'),
          value: t('me.attachments.rail.manageValue'),
        },
      ],
    });
  };

  const renderExperiencePage = () => {
    if (route.kind !== 'experience') {
      return null;
    }

    return renderSubPageShell({
      title: t('me.experience.detailTitle'),
      description: t('me.experience.detailDescription'),
      content: (
        <ExperienceDetailApp
          pageIndex={route.page}
          onPageIndexChange={(page) => navigateToMeRoute({ kind: 'experience', page })}
          onDataLoaded={(snapshot) => {
            const firstTransaction = snapshot.transactions[0];
            const levelLabel = snapshot.experience
              ? `Lv.${snapshot.experience.voCurrentLevel} ${snapshot.experience.voCurrentLevelName}`
              : null;
            const preview: MeSubPagePreview | null = firstTransaction
              ? {
                icon: firstTransaction.voExpAmount >= 0 ? 'mdi:plus-circle-outline' : 'mdi:minus-circle-outline',
                badge: firstTransaction.voExpTypeDisplay || firstTransaction.voExpType,
                title: firstTransaction.voIsLevelUp ? t('me.experience.previewLevelUp') : t('me.experience.previewTransaction'),
                description: truncatePreviewText(
                  firstTransaction.voRemark,
                  t('me.experience.previewLevelChange', {
                    before: firstTransaction.voLevelBefore,
                    after: firstTransaction.voLevelAfter,
                  })
                ),
                meta: formatDateTimeByTimeZone(firstTransaction.voCreateTime, displayTimeZone),
                stats: [
                  formatSignedNumber(firstTransaction.voExpAmount),
                  t('me.experience.previewBalance', { value: formatNumber(firstTransaction.voExpAfter) }),
                ],
              }
              : snapshot.experience
                ? {
                  icon: 'mdi:star-circle-outline',
                  badge: t('me.experienceTitle'),
                  title: levelLabel ?? t('me.experience.detailTitle'),
                  description: t('me.experience.previewNoTransactions'),
                  meta: t('me.experience.previewTotalExp', { value: formatNumber(snapshot.experience.voTotalExp) }),
                  stats: [
                    t('me.nextLevel'),
                    formatNumber(snapshot.experience.voExpToNextLevel),
                  ],
                }
                : null;

            setExperienceContext({
              count: snapshot.transactions.length,
              preview,
              levelLabel,
              totalPages: snapshot.totalPages,
            });
          }}
        />
      ),
      metrics: [
        {
          icon: 'mdi:star-circle-outline',
          label: t('me.task.level'),
          value: experienceContext.levelLabel ?? t('me.task.pending'),
          description: t('me.task.expProgress'),
        },
        {
          icon: 'mdi:format-list-numbered',
          label: t('me.task.ledgerPage'),
          value: t('me.assets.pageInfo', { current: route.page, total: experienceContext.totalPages }),
          description: t('me.task.recordCount', { count: experienceContext.count }),
        },
        {
          icon: 'mdi:timeline-clock-outline',
          label: t('me.task.returnContract'),
          value: t('me.task.webLedger'),
          description: t('me.task.noDesktopReturn'),
        },
      ],
      railTitle: t('me.experience.railTitle'),
      railDescription: t('me.experience.railDescription'),
      preview: experienceContext.preview,
      railItems: [
        {
          icon: 'mdi:chart-line',
          label: t('me.experience.rail.progress'),
          value: t('me.experience.rail.progressValue'),
        },
        {
          icon: 'mdi:receipt-text-clock-outline',
          label: t('me.experience.rail.ledger'),
          value: t('me.experience.rail.ledgerValue'),
        },
        {
          icon: 'mdi:shield-check-outline',
          label: t('me.experience.rail.contract'),
          value: t('me.experience.rail.contractValue'),
        },
      ],
    });
  };

  const renderContent = () => {
    if (!authReady || !loggedIn) {
      return renderStatusPanel(
        t('me.title'),
        t(redirecting ? 'me.auth.redirecting' : 'me.auth.loading'),
        'mdi:account-clock-outline'
      );
    }

    if (loadingData && !dashboardData.loadedAt) {
      return renderStatusPanel(t('me.loadingTitle'), t('me.loadingDescription'), 'mdi:progress-clock');
    }

    if (route.kind === 'assets' || route.kind === 'assets-transactions') {
      return (
        <MeAssetsPage
          mode={route.kind === 'assets-transactions' ? 'transactions' : 'overview'}
          onNavigate={navigateToMeRoute}
        />
      );
    }

    if (route.kind === 'content') {
      return renderContentPage();
    }

    if (route.kind === 'history') {
      return renderHistoryPage();
    }

    if (route.kind === 'attachments') {
      return renderAttachmentsPage();
    }

    if (route.kind === 'experience') {
      return renderExperiencePage();
    }

    return (
      <>
        <section className={styles.identityPanel}>
          <div className={styles.avatar} aria-hidden="true">
            {resolvedAvatarUrl ? (
              <img src={resolvedAvatarUrl} alt="" className={styles.avatarImage} />
            ) : (
              <span>{displayName.charAt(0).toUpperCase()}</span>
            )}
          </div>
          <div className={styles.identityBody}>
            <p className={styles.kicker}>{t('me.identityKicker')}</p>
            <h1 className={styles.title}>{displayName}</h1>
            <div className={styles.identityMeta}>
              <span>@{accountName}</span>
              {loadedAtLabel ? <span>{t('me.refreshedAt', { time: loadedAtLabel })}</span> : null}
            </div>
          </div>
          <div className={styles.identityActions} aria-label={t('me.actionsLabel')}>
            <div className={styles.primaryActionSlot}>
              {selfProfilePath ? (
                <a
                  className={styles.primaryButton}
                  href={selfProfilePath}
                  onClick={rememberSelfPublicProfileSource}
                >
                  <Icon icon="mdi:account-arrow-right-outline" size={18} />
                  <span>{t('me.openPublicProfile')}</span>
                </a>
              ) : (
                <button
                  type="button"
                  className={styles.primaryButton}
                  disabled
                >
                  <Icon icon="mdi:account-arrow-right-outline" size={18} />
                  <span>{t('me.openPublicProfile')}</span>
                </button>
              )}
            </div>
            <div className={styles.secondaryActionGroup}>
              <a
                className={styles.secondaryButton}
                href={buildMePath({ kind: 'content', tab: 'posts', page: 1 })}
                onClick={(event) => {
                  if (!shouldHandlePlainLinkClick(event)) {
                    return;
                  }

                  event.preventDefault();
                  navigateToMeRoute({ kind: 'content', tab: 'posts', page: 1 });
                }}
              >
                <Icon icon="mdi:file-document-edit-outline" size={18} />
                <span>{t('me.openContent')}</span>
              </a>
              <a className={styles.secondaryButton} href="/circle">
                <Icon icon="mdi:account-group-outline" size={18} />
                <span>{t('me.openCircle')}</span>
              </a>
              <a
                className={styles.secondaryButton}
                href={buildMePath({ kind: 'attachments', businessType: 'All', keyword: '', page: 1 })}
                onClick={(event) => {
                  if (!shouldHandlePlainLinkClick(event)) {
                    return;
                  }

                  event.preventDefault();
                  navigateToMeRoute({ kind: 'attachments', businessType: 'All', keyword: '', page: 1 });
                }}
              >
                <Icon icon="mdi:paperclip" size={18} />
                <span>{t('me.openAttachments')}</span>
              </a>
              <a className={styles.secondaryButton} href="/notifications">
                <Icon icon="mdi:bell-outline" size={18} />
                <span>{t('me.openNotifications')}</span>
              </a>
              <a className={styles.secondaryButton} href="/pet">
                <Icon icon="mdi:leaf" size={18} />
                <span>{t('me.openPet')}</span>
              </a>
            </div>
          </div>
        </section>

        <div className={styles.toolbar}>
          <div>
            <h2>{t('me.overviewTitle')}</h2>
            <p>{t('me.overviewDescription')}</p>
          </div>
          <button type="button" className={styles.refreshButton} onClick={() => void loadDashboardData()} disabled={loadingData}>
            <Icon icon={loadingData ? 'mdi:loading' : 'mdi:refresh'} size={18} className={loadingData ? styles.spin : undefined} />
            <span>{loadingData ? t('me.refreshing') : t('me.refresh')}</span>
          </button>
        </div>

        <section className={styles.summaryGrid}>
          <article className={styles.summaryCard}>
            <div className={styles.cardHeader}>
              <Icon icon="mdi:star-circle-outline" size={22} />
              <h3>{t('me.experienceTitle')}</h3>
            </div>
            {dashboardData.errors.experience ? (
              <p className={styles.errorText}>{dashboardData.errors.experience}</p>
            ) : experience ? (
              <>
                <ExperienceBar
                  data={experience}
                  size="medium"
                  showLevel={true}
                  showProgress={true}
                  showTooltip={true}
                  animated={true}
                />
                <div className={styles.metricRow}>
                  <span>{t('me.totalExp')}</span>
                  <strong>{formatNumber(experience.voTotalExp)}</strong>
                </div>
                <div className={styles.metricRow}>
                  <span>{t('me.nextLevel')}</span>
                  <strong>{formatNumber(experience.voExpToNextLevel)}</strong>
                </div>
              </>
            ) : (
              <p className={styles.emptyText}>{t('me.experienceEmpty')}</p>
            )}
          </article>

          <article className={styles.summaryCard}>
            <div className={styles.cardHeader}>
              <Icon icon="mdi:wallet-outline" size={22} />
              <h3>{t('me.assetTitle')}</h3>
            </div>
            {dashboardData.errors.assets ? (
              <p className={styles.errorText}>{dashboardData.errors.assets}</p>
            ) : balance ? (
              <>
                <div className={styles.balanceValue}>{balance.voBalanceDisplay || `${formatNumber(balance.voBalance)} ${t('me.carrotUnit')}`}</div>
                <div className={styles.metricRow}>
                  <span>{t('me.frozenBalance')}</span>
                  <strong>{balance.voFrozenBalanceDisplay || `${formatNumber(balance.voFrozenBalance)} ${t('me.carrotUnit')}`}</strong>
                </div>
                <div className={styles.metricRow}>
                  <span>{t('me.totalEarned')}</span>
                  <strong>{formatNumber(balance.voTotalEarned)}</strong>
                </div>
              </>
            ) : (
              <p className={styles.emptyText}>{t('me.assetEmpty')}</p>
            )}
          </article>

          <article className={styles.summaryCard}>
            <div className={styles.cardHeader}>
              <Icon icon="mdi:history" size={22} />
              <h3>{t('me.revisitTitle')}</h3>
            </div>
            {dashboardData.errors.browse ? (
              <p className={styles.errorText}>{dashboardData.errors.browse}</p>
            ) : dashboardData.browseHistory.length > 0 ? (
              <>
                <div className={styles.balanceValue}>{dashboardData.browseHistory.length}</div>
                <p className={styles.emptyText}>{t('me.revisitDescription')}</p>
              </>
            ) : (
              <p className={styles.emptyText}>{t('me.revisitEmpty')}</p>
            )}
          </article>

          <article className={styles.summaryCard}>
            <div className={styles.cardHeader}>
              <Icon icon="mdi:leaf" size={22} />
              <h3>{t('me.petTitle')}</h3>
            </div>
            {dashboardData.errors.pet ? (
              <p className={styles.errorText}>{dashboardData.errors.pet}</p>
            ) : pet ? (
              <>
                <div className={styles.balanceValue}>{pet.voName}</div>
                <div className={styles.metricRow}>
                  <span>{t('me.petMood')}</span>
                  <strong>{pet.voMoodDisplay}</strong>
                </div>
                <div className={styles.metricRow}>
                  <span>{t('me.petStage')}</span>
                  <strong>{pet.voGrowthStageName}</strong>
                </div>
              </>
            ) : (
              <p className={styles.emptyText}>{t('me.petEmpty')}</p>
            )}
          </article>
        </section>

        <section className={styles.detailGrid}>
          <article className={styles.detailPanel}>
            <div className={styles.panelHeader}>
              <h3>{t('me.recentExperience')}</h3>
              <a
                href={buildMePath({ kind: 'experience', page: 1 })}
                onClick={(event) => {
                  if (!shouldHandlePlainLinkClick(event)) {
                    return;
                  }

                  event.preventDefault();
                  navigateToMeRoute({ kind: 'experience', page: 1 });
                }}
              >
                {t('me.openExperienceDetail')}
              </a>
            </div>
            {dashboardData.expTransactions.length > 0 ? (
              <div className={styles.itemList}>
                {dashboardData.expTransactions.map((transaction) => (
                  <div key={transaction.voId} className={styles.listItem}>
                    <div className={styles.itemIcon} data-tone={transaction.voExpAmount >= 0 ? 'positive' : 'negative'}>
                      <Icon icon={transaction.voExpAmount >= 0 ? 'mdi:plus' : 'mdi:minus'} size={16} />
                    </div>
                    <div className={styles.itemBody}>
                      <strong>{transaction.voExpTypeDisplay || transaction.voExpType}</strong>
                      <span>{formatDateTimeByTimeZone(transaction.voCreateTime, displayTimeZone)}</span>
                    </div>
                    <div className={styles.itemAmount}>{formatSignedNumber(transaction.voExpAmount)}</div>
                  </div>
                ))}
              </div>
            ) : (
              <p className={styles.emptyText}>{t('me.recentExperienceEmpty')}</p>
            )}
          </article>

          <article className={styles.detailPanel}>
            <div className={styles.panelHeader}>
              <h3>{t('me.recentAssets')}</h3>
              <a
                href={buildMePath({ kind: 'assets-transactions' })}
                onClick={(event) => {
                  if (!shouldHandlePlainLinkClick(event)) {
                    return;
                  }

                  event.preventDefault();
                  navigateToMeRoute({ kind: 'assets-transactions' });
                }}
              >
                {t('me.openFullWallet')}
              </a>
            </div>
            {dashboardData.coinTransactions.length > 0 ? (
              <div className={styles.itemList}>
                {dashboardData.coinTransactions.map((transaction) => (
                  <div key={transaction.voId} className={styles.listItem}>
                    <div className={styles.itemIcon} data-tone={transaction.voAmount >= 0 ? 'positive' : 'negative'}>
                      <Icon icon={transaction.voAmount >= 0 ? 'mdi:arrow-up' : 'mdi:arrow-down'} size={16} />
                    </div>
                    <div className={styles.itemBody}>
                      <strong>{transaction.voTransactionTypeDisplay || transaction.voTransactionType}</strong>
                      <span>{formatDateTimeByTimeZone(transaction.voCreateTime, displayTimeZone)}</span>
                    </div>
                    <div className={styles.itemAmount}>{transaction.voAmountDisplay || formatSignedNumber(transaction.voAmount)}</div>
                  </div>
                ))}
              </div>
            ) : (
              <p className={styles.emptyText}>{t('me.recentAssetsEmpty')}</p>
            )}
          </article>

          <article className={styles.detailPanel}>
            <div className={styles.panelHeader}>
              <h3>{t('me.recentBrowse')}</h3>
              <a
                href={buildMePath({ kind: 'history', page: 1 })}
                onClick={(event) => {
                  if (!shouldHandlePlainLinkClick(event)) {
                    return;
                  }

                  event.preventDefault();
                  navigateToMeRoute({ kind: 'history', page: 1 });
                }}
              >
                {t('me.openHistory')}
              </a>
            </div>
            {dashboardData.browseHistory.length > 0 ? (
              <div className={styles.itemList}>
                {dashboardData.browseHistory.map((item) => {
                  const href = buildBrowseHistoryHref(item);
                  return (
                    <div key={item.voId} className={styles.browseItem}>
                      <div className={styles.itemBody}>
                        {href ? (
                          <a href={href} onClick={(event) => rememberSourceForPublicLink(event, href)}>
                            {item.voTitle}
                          </a>
                        ) : (
                          <strong>{item.voTitle}</strong>
                        )}
                        <span>
                          {item.voTargetTypeDisplay} · {formatDateTimeByTimeZone(item.voLastViewTime, displayTimeZone)}
                        </span>
                      </div>
                      <span className={styles.viewCount}>{t('me.viewCount', { count: item.voViewCount })}</span>
                    </div>
                  );
                })}
              </div>
            ) : (
              <p className={styles.emptyText}>{t('me.recentBrowseEmpty')}</p>
            )}
          </article>
        </section>
      </>
    );
  };

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey={route.kind === 'assets' || route.kind === 'assets-transactions' ? 'assets' : 'me'}
        brandMark="我"
        brandName={t('me.title')}
        brandSubline={t('me.shellSubline')}
        discoverLabel={t('public.shell.discoverAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
        onBrandClick={() => {
          navigateToMeRoute({ kind: 'dashboard' });
        }}
        onNavigateToDiscover={() => {
          window.location.href = '/discover';
        }}
      />

      <main className={styles.main}>
        {renderContent()}
      </main>
    </div>
  );
};
