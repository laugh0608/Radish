import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { formatLocalizedNumber } from '@radish/ui';
import {
  leaderboardApi,
  LeaderboardCategory,
  type LeaderboardTypeData,
  type UnifiedLeaderboardItemData,
} from '@/api/leaderboard';
import { useUserStore } from '@/stores/userStore';
import {
  createDefaultPublicLeaderboardRoute,
  buildPublicLeaderboardPath,
  filterPublicLeaderboardTypes,
  getPublicLeaderboardRouteDefinitionBySlug,
  getPublicLeaderboardRouteDefinitionByType,
  publicLeaderboardTypeRouteDefinitions,
  type PublicLeaderboardRoute,
  type PublicLeaderboardTypeSlug,
} from '../leaderboardRouteState';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { buildLocalizedPublicRouteHead, buildPublicShareUrl } from '../publicHead';
import { usePublicHeadSnapshot } from '../publicHeadLifecycleContext';
import { resolvePublicUserRouteIdentifier } from '../publicId';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { buildPublicProfilePath } from '../profileRouteState';
import { buildPublicShopPath } from '../shopRouteState';
import { resolveMediaUrl } from '@/utils/media';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import styles from './PublicLeaderboardApp.module.css';

interface PublicLeaderboardAppProps {
  route: PublicLeaderboardRoute;
  onNavigate: (route: PublicLeaderboardRoute, options?: { replace?: boolean }) => void;
  onNavigateToProfile?: (userId: string) => void;
  onNavigateToShopProduct?: (productId: string) => void;
}

type PublicStatusTone = 'loading' | 'empty' | 'error';

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  primaryAction?: {
    label: string;
    onClick: () => void;
  };
}

interface PublicLeaderboardFallbackTypeDefinition {
  icon: string;
  nameKey: string;
  descriptionKey: string;
  primaryLabelKey: string;
}

interface ExperienceGuideFocusDefinition {
  labelKey: string;
  valueKey: string;
}

interface LightweightGuideDefinition {
  titleKey: string;
  descriptionKey: string;
  focusItems: ExperienceGuideFocusDefinition[];
}

interface PublicLeaderboardRailProps {
  activeTypeConfig: LeaderboardTypeData;
  guide: LightweightGuideDefinition;
}

const publicLeaderboardFallbackTypes: Record<PublicLeaderboardTypeSlug, PublicLeaderboardFallbackTypeDefinition> = {
  experience: {
    icon: 'mdi:star-circle',
    nameKey: 'leaderboard.public.types.experience.name',
    descriptionKey: 'leaderboard.public.types.experience.description',
    primaryLabelKey: 'leaderboard.public.types.experience.primaryLabel',
  },
  'hot-product': {
    icon: 'mdi:gift-outline',
    nameKey: 'leaderboard.public.types.hotProduct.name',
    descriptionKey: 'leaderboard.public.types.hotProduct.description',
    primaryLabelKey: 'leaderboard.public.types.hotProduct.primaryLabel',
  },
  'post-count': {
    icon: 'mdi:file-document-outline',
    nameKey: 'leaderboard.public.types.postCount.name',
    descriptionKey: 'leaderboard.public.types.postCount.description',
    primaryLabelKey: 'leaderboard.public.types.postCount.primaryLabel',
  },
  'comment-count': {
    icon: 'mdi:comment-multiple-outline',
    nameKey: 'leaderboard.public.types.commentCount.name',
    descriptionKey: 'leaderboard.public.types.commentCount.description',
    primaryLabelKey: 'leaderboard.public.types.commentCount.primaryLabel',
  },
  popularity: {
    icon: 'mdi:fire-circle',
    nameKey: 'leaderboard.public.types.popularity.name',
    descriptionKey: 'leaderboard.public.types.popularity.description',
    primaryLabelKey: 'leaderboard.public.types.popularity.primaryLabel',
  },
};

const experienceGuideFocusItems: ExperienceGuideFocusDefinition[] = [
  {
    labelKey: 'leaderboard.public.experienceGuide.focusRankingLabel',
    valueKey: 'leaderboard.public.experienceGuide.focusRankingValue',
  },
  {
    labelKey: 'leaderboard.public.experienceGuide.focusLevelLabel',
    valueKey: 'leaderboard.public.experienceGuide.focusLevelValue',
  },
  {
    labelKey: 'leaderboard.public.experienceGuide.focusBoundaryLabel',
    valueKey: 'leaderboard.public.experienceGuide.focusBoundaryValue',
  },
];

const userLeaderboardGuideFocusItems: ExperienceGuideFocusDefinition[] = [
  {
    labelKey: 'leaderboard.public.userGuide.focusCompareLabel',
    valueKey: 'leaderboard.public.userGuide.focusCompareValue',
  },
  {
    labelKey: 'leaderboard.public.userGuide.focusProfileLabel',
    valueKey: 'leaderboard.public.userGuide.focusProfileValue',
  },
  {
    labelKey: 'leaderboard.public.userGuide.focusBoundaryLabel',
    valueKey: 'leaderboard.public.userGuide.focusBoundaryValue',
  },
];

const productLeaderboardGuideFocusItems: ExperienceGuideFocusDefinition[] = [
  {
    labelKey: 'leaderboard.public.productGuide.focusDisplayLabel',
    valueKey: 'leaderboard.public.productGuide.focusDisplayValue',
  },
  {
    labelKey: 'leaderboard.public.productGuide.focusCompareLabel',
    valueKey: 'leaderboard.public.productGuide.focusCompareValue',
  },
  {
    labelKey: 'leaderboard.public.productGuide.focusBoundaryLabel',
    valueKey: 'leaderboard.public.productGuide.focusBoundaryValue',
  },
];

const experienceLeaderboardGuide: LightweightGuideDefinition = {
  titleKey: 'leaderboard.public.experienceGuide.title',
  descriptionKey: 'leaderboard.public.experienceGuide.summaryDescription',
  focusItems: experienceGuideFocusItems,
};

function PublicStatusCard({ tone, title, description, primaryAction }: PublicStatusCardProps) {
  const resolvedIcon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:trophy-outline'
      : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = primaryAction
    ? [{
      label: primaryAction.label,
      kind: 'primary',
      onClick: () => primaryAction.onClick(),
    }]
    : [];

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={resolvedIcon}
      actions={actions}
    />
  );
}

function createFallbackLeaderboardTypes(t: (key: string) => string): LeaderboardTypeData[] {
  return publicLeaderboardTypeRouteDefinitions.map((definition, index) => {
    const fallback = publicLeaderboardFallbackTypes[definition.slug];
    return {
      voType: definition.type,
      voCategory: definition.category,
      voName: t(fallback.nameKey),
      voDescription: t(fallback.descriptionKey),
      voIcon: fallback.icon,
      voPrimaryLabel: t(fallback.primaryLabelKey),
      voSortOrder: index + 1,
    };
  });
}

function localizePublicLeaderboardType(
  type: LeaderboardTypeData,
  fallbackTypes: LeaderboardTypeData[],
): LeaderboardTypeData {
  const localizedType = fallbackTypes.find((fallbackType) => fallbackType.voType === type.voType);
  if (!localizedType) {
    return type;
  }

  return {
    ...type,
    voName: localizedType.voName,
    voDescription: localizedType.voDescription,
    voPrimaryLabel: localizedType.voPrimaryLabel,
    voIcon: type.voIcon?.trim() || localizedType.voIcon,
  };
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

function buildAvatarText(name: string | undefined, fallback: string): string {
  const source = name?.trim() || fallback.trim();
  if (!source) {
    return '?';
  }

  return source.charAt(0).toUpperCase();
}

function resolveLeaderboardUserProfileIdentifier(item: UnifiedLeaderboardItemData): string {
  return resolvePublicUserRouteIdentifier({
    voPublicId: item.voUserPublicId,
    voUserId: item.voUserId,
  }) ?? '';
}

function shouldHandlePublicLeaderboardLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handlePublicLeaderboardLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandlePublicLeaderboardLink(event)) {
    return;
  }

  event.preventDefault();
  action();
}

function PublicLeaderboardRail({
  activeTypeConfig,
  guide,
}: PublicLeaderboardRailProps) {
  const { t } = useTranslation();
  const activeRouteDefinition = getPublicLeaderboardRouteDefinitionByType(activeTypeConfig.voType);

  return (
    <aside className={styles.leaderboardRail} aria-label={t('leaderboard.public.railLabel')}>
      <section className={styles.railPanel}>
        <h2 className={styles.railTitle}>{t('leaderboard.public.railStateTitle')}</h2>
        <div className={styles.railStatGrid}>
          <span className={styles.railStat}>
            <strong>{activeTypeConfig.voName}</strong>
            <span>{activeTypeConfig.voPrimaryLabel}</span>
          </span>
          <span className={styles.railStat}>
            <strong>{activeRouteDefinition.category === LeaderboardCategory.User ? t('leaderboard.public.railUserType') : t('leaderboard.public.railProductType')}</strong>
            <span>{t('leaderboard.public.railTypeLabel')}</span>
          </span>
        </div>
      </section>

      <PublicReadingGuide
        label={t('leaderboard.public.lightweightGuide.label')}
        title={t(guide.titleKey)}
        description={t(guide.descriptionKey)}
        items={guide.focusItems.map((item) => ({
          label: t(item.labelKey),
          value: t(item.valueKey),
        }))}
      />
    </aside>
  );
}

export const PublicLeaderboardApp = ({
  route,
  onNavigate,
  onNavigateToProfile,
  onNavigateToShopProduct,
}: PublicLeaderboardAppProps) => {
  const { t, i18n } = useTranslation();
  const pageRef = useRef<HTMLDivElement>(null);
  const requestIdRef = useRef(0);
  const userId = useUserStore((state) => state.userId);
  const isLoggedIn = userId !== '';
  const [items, setItems] = useState<UnifiedLeaderboardItemData[]>([]);
  const [types, setTypes] = useState<LeaderboardTypeData[]>(() => createFallbackLeaderboardTypes(t));
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);
  const [loadingTypes, setLoadingTypes] = useState(true);
  const [hasAuthoritativeTypes, setHasAuthoritativeTypes] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [typesError, setTypesError] = useState<string | null>(null);
  const [myRank, setMyRank] = useState<number | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [isCompactViewport, setIsCompactViewport] = useState(() =>
    typeof window !== 'undefined' ? window.innerWidth <= 720 : false
  );
  const buildLeaderboardShareUrl = useCallback(() => (
    buildPublicShareUrl(buildPublicLeaderboardPath(route))
  ), [route]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildLeaderboardShareUrl,
  });

  const fallbackTypes = useMemo(() => createFallbackLeaderboardTypes(t), [t]);
  const activeRouteDefinition = useMemo(
    () => getPublicLeaderboardRouteDefinitionBySlug(route.typeSlug),
    [route.typeSlug]
  );
  const activeTypeConfig = useMemo(() => {
    const activeType = types.find((item) => item.voType === activeRouteDefinition.type)
      ?? fallbackTypes.find((item) => item.voType === activeRouteDefinition.type)
      ?? fallbackTypes[0];

    return localizePublicLeaderboardType(activeType, fallbackTypes);
  }, [activeRouteDefinition.type, fallbackTypes, types]);
  const publicHeadSnapshot = useMemo(() => {
    const routeHead = buildLocalizedPublicRouteHead({ app: 'leaderboard', route }, t);
    return {
      head: {
        ...routeHead,
        title: `${activeTypeConfig.voName} · ${t('desktop.apps.leaderboard.name')}`,
        description: activeTypeConfig.voDescription?.trim() || routeHead.description,
      },
    };
  }, [activeTypeConfig.voDescription, activeTypeConfig.voName, route, t]);
  usePublicHeadSnapshot(publicHeadSnapshot);

  useEffect(() => {
    pageRef.current?.scrollTo({ top: 0, behavior: 'auto' });
  }, [route.page, route.typeSlug]);

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
    let cancelled = false;

    const loadTypes = async () => {
      setLoadingTypes(true);
      setTypesError(null);

      try {
        const result = await leaderboardApi.getTypes();
        if (cancelled) {
          return;
        }

        const publicTypes = filterPublicLeaderboardTypes(result ?? []);
        setTypes(publicTypes);
        setHasAuthoritativeTypes(true);
      } catch (loadTypesError) {
        if (!cancelled) {
          setTypes(fallbackTypes);
          setHasAuthoritativeTypes(false);
          setTypesError(loadTypesError instanceof Error ? loadTypesError.message : String(loadTypesError));
        }
      } finally {
        if (!cancelled) {
          setLoadingTypes(false);
        }
      }
    };

    void loadTypes();
    return () => {
      cancelled = true;
    };
  }, [fallbackTypes, reloadToken, t]);

  useEffect(() => {
    if (loadingTypes || !hasAuthoritativeTypes) {
      return;
    }

    if (
      types.length === 0 ||
      types.some((type) => type.voType === activeRouteDefinition.type)
    ) {
      return;
    }

    const fallbackDefinition = getPublicLeaderboardRouteDefinitionByType(types[0].voType);
    onNavigate({
      ...createDefaultPublicLeaderboardRoute(),
      typeSlug: fallbackDefinition.slug,
    }, { replace: true });
  }, [
    activeRouteDefinition.type,
    hasAuthoritativeTypes,
    loadingTypes,
    onNavigate,
    types,
  ]);

  useEffect(() => {
    if (loadingTypes) {
      return;
    }

    if (hasAuthoritativeTypes && types.length === 0) {
      requestIdRef.current += 1;
      setItems([]);
      setTotalPages(1);
      setLoading(false);
      setError(t('leaderboard.public.loadFailedDescription'));
      return;
    }

    if (
      hasAuthoritativeTypes &&
      !types.some((type) => type.voType === activeRouteDefinition.type)
    ) {
      return;
    }

    const requestId = ++requestIdRef.current;

    const loadLeaderboard = async () => {
      setLoading(true);
      setError(null);

      try {
        const result = await leaderboardApi.getLeaderboard(activeRouteDefinition.type, route.page, 20);
        if (requestId !== requestIdRef.current) {
          return;
        }

        if (!result) {
          setItems([]);
          setTotalPages(1);
          setError(t('leaderboard.public.loadFailedDescription'));
          return;
        }

        const nextTotalPages = Math.max(result.pageCount || 1, 1);
        if (route.page > nextTotalPages) {
          onNavigate({ kind: 'list', typeSlug: route.typeSlug, page: nextTotalPages }, { replace: true });
          return;
        }

        setItems(result.data ?? []);
        setTotalPages(nextTotalPages);
      } catch (loadError) {
        if (requestId !== requestIdRef.current) {
          return;
        }

        setItems([]);
        setTotalPages(1);
        setError(loadError instanceof Error ? loadError.message : String(loadError));
      } finally {
        if (requestId === requestIdRef.current) {
          setLoading(false);
        }
      }
    };

    void loadLeaderboard();
  }, [
    activeRouteDefinition.type,
    hasAuthoritativeTypes,
    loadingTypes,
    onNavigate,
    reloadToken,
    route.page,
    route.typeSlug,
    t,
    types,
  ]);

  useEffect(() => {
    let cancelled = false;

    const loadMyRank = async () => {
      if (
        !isLoggedIn ||
        activeTypeConfig.voCategory !== LeaderboardCategory.User ||
        (
          hasAuthoritativeTypes &&
          !types.some((type) => type.voType === activeRouteDefinition.type)
        )
      ) {
        setMyRank(null);
        return;
      }

      try {
        const result = await leaderboardApi.getMyRank(activeRouteDefinition.type);
        if (!cancelled) {
          setMyRank(result && result > 0 ? result : null);
        }
      } catch {
        if (!cancelled) {
          setMyRank(null);
        }
      }
    };

    void loadMyRank();
    return () => {
      cancelled = true;
    };
  }, [
    activeRouteDefinition.type,
    activeTypeConfig.voCategory,
    hasAuthoritativeTypes,
    isLoggedIn,
    types,
  ]);

  const visiblePages = useMemo(
    () => buildVisiblePages(route.page, totalPages, isCompactViewport ? 5 : 7),
    [isCompactViewport, route.page, totalPages]
  );
  const lightweightGuide = useMemo<LightweightGuideDefinition | null>(() => {
    if (route.typeSlug === 'experience') {
      return experienceLeaderboardGuide;
    }

    if (activeTypeConfig.voCategory === LeaderboardCategory.User) {
      return {
        titleKey: 'leaderboard.public.userGuide.title',
        descriptionKey: 'leaderboard.public.userGuide.description',
        focusItems: userLeaderboardGuideFocusItems,
      };
    }

    if (activeTypeConfig.voCategory === LeaderboardCategory.Product) {
      return {
        titleKey: 'leaderboard.public.productGuide.title',
        descriptionKey: 'leaderboard.public.productGuide.description',
        focusItems: productLeaderboardGuideFocusItems,
      };
    }

    return null;
  }, [activeTypeConfig.voCategory, route.typeSlug]);

  const handleTypeChange = (typeSlug: PublicLeaderboardTypeSlug) => {
    onNavigate({
      ...createDefaultPublicLeaderboardRoute(),
      typeSlug,
    });
  };

  const handleUserProfileLinkClick = (event: MouseEvent<HTMLAnchorElement>, profileIdentifier: string) => {
    if (!onNavigateToProfile || !shouldHandlePublicLeaderboardLink(event)) {
      return;
    }

    event.preventDefault();
    onNavigateToProfile(profileIdentifier);
  };

  const handleProductDetailLinkClick = (event: MouseEvent<HTMLAnchorElement>, productId: string) => {
    if (!onNavigateToShopProduct || !shouldHandlePublicLeaderboardLink(event)) {
      return;
    }

    event.preventDefault();
    onNavigateToShopProduct(productId);
  };

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="榜"
        brandName={t('desktop.apps.leaderboard.name')}
        brandSubline={t('leaderboard.public.shellLabel')}
        onBrandClick={() => onNavigate(createDefaultPublicLeaderboardRoute())}
        loginLabel={t('public.shell.loginAction')}
      />

      <main className={styles.main}>
        <div className={styles.leaderboardLayout}>
          <section className={styles.sectionCard}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <div className={styles.sectionTitleRow}>
                  <p className={styles.kicker}>{t('leaderboard.public.lightweightGuide.label')}</p>
                  <span className={styles.readOnlyBadge}>{t('leaderboard.public.readOnlyBadge')}</span>
                </div>
                <h1 className={styles.pageTitle}>{t('leaderboard.public.title')}</h1>
                <p className={styles.pageIntro}>{t('leaderboard.public.pageIntro')}</p>
              </div>

              <div className={styles.sectionStats}>
                {isLoggedIn && myRank !== null && activeTypeConfig.voCategory === LeaderboardCategory.User && (
                  <span className={styles.statChip}>{t('leaderboard.public.myRank', { rank: myRank })}</span>
                )}
                <span className={styles.statChip}>{activeTypeConfig.voName}</span>
                <span className={styles.statChip}>{activeTypeConfig.voPrimaryLabel}</span>
                <button type="button" className={styles.shareButton} onClick={() => void copyShareLink()} disabled={shareBusy}>
                  <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={16} />
                  <span>{shareBusy ? t('leaderboard.public.shareSubmitting') : t('leaderboard.public.shareAction')}</span>
                </button>
                {shareState !== 'idle' && (
                  <span className={styles.shareFeedback} data-state={shareState}>
                    {shareState === 'success' ? t('leaderboard.public.shareSuccess') : t('leaderboard.public.shareFailed')}
                  </span>
                )}
              </div>
            </div>

            <div className={styles.toolbar}>
              <div className={styles.tabRail}>
                {types.map((type) => {
                  const localizedType = localizePublicLeaderboardType(type, fallbackTypes);
                  const typeSlug = getPublicLeaderboardRouteDefinitionByType(type.voType).slug;
                  const typeRoute = {
                    ...createDefaultPublicLeaderboardRoute(),
                    typeSlug,
                  };
                  return (
                    <a
                      key={type.voType}
                      className={`${styles.tabButton} ${route.typeSlug === typeSlug ? styles.tabButtonActive : ''}`}
                      href={buildPublicLeaderboardPath(typeRoute)}
                      aria-current={route.typeSlug === typeSlug ? 'page' : undefined}
                      onClick={(event) => handlePublicLeaderboardLinkClick(event, () => handleTypeChange(typeSlug))}
                    >
                      <Icon icon={localizedType.voIcon} size={18} />
                      <span>{localizedType.voName}</span>
                    </a>
                  );
                })}
              </div>
              <p className={styles.toolbarHint}>{activeTypeConfig.voDescription}</p>
            </div>

            {typesError && (
              <div className={styles.inlineNotice}>
                <span className={styles.inlineNoticeText}>{t('leaderboard.public.typesFallback')}</span>
                <button type="button" className={styles.inlineTextButton} onClick={() => setReloadToken((current) => current + 1)}>
                  {t('common.retry')}
                </button>
              </div>
            )}

            <div className={styles.contentWrap}>
              {(loading || loadingTypes) && items.length === 0 ? (
                <PublicStatusCard
                  tone="loading"
                  title={t('leaderboard.public.loadingTitle')}
                  description={t('leaderboard.public.loadingDescription')}
                />
              ) : error ? (
                <PublicStatusCard
                  tone="error"
                  title={t('leaderboard.public.loadFailedTitle')}
                  description={error}
                  primaryAction={{
                    label: t('common.retry'),
                    onClick: () => setReloadToken((current) => current + 1),
                  }}
                />
              ) : items.length === 0 ? (
                <PublicStatusCard
                  tone="empty"
                  title={t('leaderboard.public.emptyTitle')}
                  description={t('leaderboard.public.emptyDescription')}
                />
              ) : (
                <div className={styles.list}>
                  {items.map((item) => item.voCategory === LeaderboardCategory.User ? (
                  (() => {
                    const profileIdentifier = resolveLeaderboardUserProfileIdentifier(item);
                    const userName = resolveVisibleUserDisplayName({
                      voDisplayName: item.voUserDisplayName,
                      voDisplayHandle: item.voUserDisplayHandle,
                      voPublicIndex: item.voUserPublicIndex,
                      voUserName: item.voUserName,
                    }, t('common.userFallback', { id: profileIdentifier || '?' }));
                    const displayHandle = resolveVisibleUserHandle({
                      voDisplayHandle: item.voUserDisplayHandle,
                      voPublicIndex: item.voUserPublicIndex,
                    }, userName);
                    const avatarUrl = resolveMediaUrl(item.voAvatarUrl);
                    const profileHref = profileIdentifier
                      ? buildPublicProfilePath({ kind: 'detail', userId: profileIdentifier, tab: 'posts', page: 1 })
                      : null;
                    const userItemContent = (
                      <>
                        <div className={styles.userHeaderRow}>
                          <div className={styles.userIdentityCluster}>
                            <div className={styles.rankBadge} data-rank={item.voRank <= 3 ? item.voRank : undefined}>
                              {item.voRank <= 3 ? (
                                <span className={styles.rankMedal}>
                                  {item.voRank === 1 ? '🥇' : item.voRank === 2 ? '🥈' : '🥉'}
                                </span>
                              ) : (
                                <span className={styles.rankNumber}>#{item.voRank}</span>
                              )}
                            </div>
                            <div className={styles.userMedia}>
                              {avatarUrl ? (
                                <img className={styles.userAvatar} src={avatarUrl} alt={userName} />
                              ) : (
                                <span className={styles.userAvatarFallback} aria-hidden="true">
                                  {buildAvatarText(item.voUserName, userName)}
                                </span>
                              )}
                            </div>
                          </div>
                          <div className={styles.userTitleGroup}>
                            <span className={`${styles.itemTitle} ${styles.userName}`}>{userName}</span>
                            {item.voIsCurrentUser && (
                              <span className={styles.currentUserBadge}>{t('leaderboard.public.currentUser')}</span>
                            )}
                            {displayHandle && (
                              <span className={styles.itemDescription}>{displayHandle}</span>
                            )}
                          </div>
                        </div>
                        <div className={styles.userStatsRow}>
                          <span className={styles.userStatChip}>Lv.{item.voCurrentLevel ?? 0}</span>
                          <span
                            className={`${styles.userStatChip} ${styles.userLevelNameChip}`}
                            style={item.voThemeColor ? { color: item.voThemeColor } : undefined}
                          >
                            {item.voCurrentLevelName?.trim() || t('leaderboard.public.levelFallback')}
                          </span>
                          <span className={`${styles.userStatChip} ${styles.userMetricChip}`}>
                            {formatLocalizedNumber(Number(item.voPrimaryValue || 0), i18n.resolvedLanguage ?? i18n.language)} {activeTypeConfig.voPrimaryLabel}
                          </span>
                          <span className={styles.itemOpenAction}>{t('leaderboard.public.openTarget')}</span>
                        </div>
                      </>
                    );

                    return profileHref ? (
                      <a
                        key={`${item.voLeaderboardType}-${profileIdentifier}-${item.voRank}`}
                        className={`${styles.listItem} ${styles.userListItem} ${item.voIsCurrentUser ? styles.listItemCurrentUser : ''}`}
                        href={profileHref}
                        onClick={(event) => handleUserProfileLinkClick(event, profileIdentifier)}
                      >
                        {userItemContent}
                      </a>
                    ) : (
                      <button
                        key={`${item.voLeaderboardType}-${profileIdentifier || 'unknown'}-${item.voRank}`}
                        type="button"
                        className={`${styles.listItem} ${styles.userListItem} ${item.voIsCurrentUser ? styles.listItemCurrentUser : ''}`}
                        disabled
                      >
                        {userItemContent}
                      </button>
                    );
                  })()
                  ) : (() => {
                  const productIconUrl = resolveMediaUrl(item.voProductIcon);
                  const productId = item.voProductId ? String(item.voProductId) : '';
                  const productHref = productId ? buildPublicShopPath({ kind: 'detail', productId }) : null;
                  const productItemContent = (
                    <>
                      <div className={styles.rankBadge} data-rank={item.voRank <= 3 ? item.voRank : undefined}>
                        {item.voRank <= 3 ? (
                          <span className={styles.rankMedal}>
                            {item.voRank === 1 ? '🥇' : item.voRank === 2 ? '🥈' : '🥉'}
                          </span>
                        ) : (
                          <span className={styles.rankNumber}>#{item.voRank}</span>
                        )}
                      </div>
                      <div className={styles.productMedia}>
                        {productIconUrl ? (
                          <img className={styles.productImage} src={productIconUrl} alt={item.voProductName || t('leaderboard.public.productFallback')} />
                        ) : (
                          <span className={styles.productImageFallback}>
                            <Icon icon="mdi:gift-outline" size={24} />
                          </span>
                        )}
                      </div>
                      <div className={styles.itemBody}>
                        <div className={styles.itemPrimary}>
                          <div className={styles.itemTitleRow}>
                            <span className={styles.itemTitle}>{item.voProductName || t('leaderboard.public.productFallback')}</span>
                            <span className={styles.productReadonly}>{t('leaderboard.public.productDetailAction')}</span>
                          </div>
                          <p className={styles.itemMeta}>{t('leaderboard.public.productDetailMeta')}</p>
                        </div>
                        <div className={styles.itemSideInfo}>
                          <span className={styles.sideInfoLabel}>{t('shop.meta.price')}</span>
                          <span className={styles.sideInfoValue}>
                            {t('leaderboard.public.productPrice', {
                              price: formatLocalizedNumber(Number(item.voProductPrice || 0), i18n.resolvedLanguage ?? i18n.language),
                            })}
                          </span>
                        </div>
                      </div>
                      <div className={styles.itemMetric}>
                        <span className={styles.metricValue}>{formatLocalizedNumber(Number(item.voPrimaryValue || 0), i18n.resolvedLanguage ?? i18n.language)}</span>
                        <span className={styles.metricLabel}>{activeTypeConfig.voPrimaryLabel}</span>
                        <span className={styles.itemOpenAction}>{t('leaderboard.public.openTarget')}</span>
                      </div>
                    </>
                  );

                  return productHref ? (
                    <a
                      key={`${item.voLeaderboardType}-${productId}-${item.voRank}`}
                      className={`${styles.listItem} ${styles.productListItem}`}
                      href={productHref}
                      onClick={(event) => handleProductDetailLinkClick(event, productId)}
                    >
                      {productItemContent}
                    </a>
                  ) : (
                    <button
                      key={`${item.voLeaderboardType}-${String(item.voProductId)}-${item.voRank}`}
                      type="button"
                      className={`${styles.listItem} ${styles.productListItem}`}
                      disabled
                    >
                      {productItemContent}
                    </button>
                  );
                  })())}
                </div>
              )}
            </div>

            {totalPages > 1 && !loading && !error && (
              <div className={styles.pagination}>
                {route.page === 1 ? (
                  <button type="button" className={styles.paginationButton} disabled>
                    {t('common.previousPage')}
                  </button>
                ) : (
                  <a
                    className={styles.paginationButton}
                    href={buildPublicLeaderboardPath({ kind: 'list', typeSlug: route.typeSlug, page: route.page - 1 })}
                    onClick={(event) => handlePublicLeaderboardLinkClick(event, () => onNavigate({ kind: 'list', typeSlug: route.typeSlug, page: route.page - 1 }))}
                  >
                    {t('common.previousPage')}
                  </a>
                )}
                <div className={styles.pageNumbers}>
                  {visiblePages.map((page) => (
                    <a
                      key={page}
                      className={`${styles.pageNumberButton} ${page === route.page ? styles.pageNumberButtonActive : ''}`}
                      href={buildPublicLeaderboardPath({ kind: 'list', typeSlug: route.typeSlug, page })}
                      aria-current={page === route.page ? 'page' : undefined}
                      onClick={(event) => handlePublicLeaderboardLinkClick(event, () => onNavigate({ kind: 'list', typeSlug: route.typeSlug, page }))}
                    >
                      {page}
                    </a>
                  ))}
                </div>
                {route.page >= totalPages ? (
                  <button type="button" className={styles.paginationButton} disabled>
                    {t('common.nextPage')}
                  </button>
                ) : (
                  <a
                    className={styles.paginationButton}
                    href={buildPublicLeaderboardPath({ kind: 'list', typeSlug: route.typeSlug, page: route.page + 1 })}
                    onClick={(event) => handlePublicLeaderboardLinkClick(event, () => onNavigate({ kind: 'list', typeSlug: route.typeSlug, page: route.page + 1 }))}
                  >
                    {t('common.nextPage')}
                  </a>
                )}
              </div>
            )}
          </section>

          {lightweightGuide && (
            <PublicLeaderboardRail
              activeTypeConfig={activeTypeConfig}
              guide={lightweightGuide}
            />
          )}
        </div>
      </main>
    </div>
  );
};
