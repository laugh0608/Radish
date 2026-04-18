import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import {
  leaderboardApi,
  LeaderboardCategory,
  type LeaderboardTypeData,
  type UnifiedLeaderboardItemData,
} from '@/api/leaderboard';
import { useUserStore } from '@/stores/userStore';
import {
  createDefaultPublicLeaderboardRoute,
  getPublicLeaderboardRouteDefinitionBySlug,
  getPublicLeaderboardRouteDefinitionByType,
  publicLeaderboardTypeRouteDefinitions,
  type PublicLeaderboardRoute,
  type PublicLeaderboardTypeSlug,
} from '../leaderboardRouteState';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { resolveMediaUrl } from '@/utils/media';
import styles from './PublicLeaderboardApp.module.css';

interface PublicLeaderboardAppProps {
  route: PublicLeaderboardRoute;
  onNavigate: (route: PublicLeaderboardRoute, options?: { replace?: boolean }) => void;
  onNavigateToDiscover?: () => void;
  onNavigateToProfile?: (userId: string) => void;
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

interface ExperienceGuideItemDefinition {
  icon: string;
  titleKey: string;
  descriptionKey: string;
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

const publicLeaderboardFallbackTypes: Record<PublicLeaderboardTypeSlug, PublicLeaderboardFallbackTypeDefinition> = {
  experience: {
    icon: 'mdi:star-circle',
    nameKey: 'leaderboard.public.types.experience.name',
    descriptionKey: 'leaderboard.public.types.experience.description',
    primaryLabelKey: 'leaderboard.public.types.experience.primaryLabel',
  },
  balance: {
    icon: 'mdi:carrot',
    nameKey: 'leaderboard.public.types.balance.name',
    descriptionKey: 'leaderboard.public.types.balance.description',
    primaryLabelKey: 'leaderboard.public.types.balance.primaryLabel',
  },
  'total-spent': {
    icon: 'mdi:cash-multiple',
    nameKey: 'leaderboard.public.types.totalSpent.name',
    descriptionKey: 'leaderboard.public.types.totalSpent.description',
    primaryLabelKey: 'leaderboard.public.types.totalSpent.primaryLabel',
  },
  'purchase-count': {
    icon: 'mdi:shopping',
    nameKey: 'leaderboard.public.types.purchaseCount.name',
    descriptionKey: 'leaderboard.public.types.purchaseCount.description',
    primaryLabelKey: 'leaderboard.public.types.purchaseCount.primaryLabel',
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

const experienceGuideItems: ExperienceGuideItemDefinition[] = [
  {
    icon: 'mdi:trophy-outline',
    titleKey: 'leaderboard.public.experienceGuide.rankingTitle',
    descriptionKey: 'leaderboard.public.experienceGuide.rankingDescription',
  },
  {
    icon: 'mdi:star-circle-outline',
    titleKey: 'leaderboard.public.experienceGuide.levelTitle',
    descriptionKey: 'leaderboard.public.experienceGuide.levelDescription',
  },
  {
    icon: 'mdi:shield-half-full',
    titleKey: 'leaderboard.public.experienceGuide.boundaryTitle',
    descriptionKey: 'leaderboard.public.experienceGuide.boundaryDescription',
  },
];

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

const experienceGuideBoundaryItems = [
  'leaderboard.public.experienceGuide.boundaryItemDetail',
  'leaderboard.public.experienceGuide.boundaryItemHistory',
  'leaderboard.public.experienceGuide.boundaryItemWorkspace',
] as const;

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

function PublicStatusCard({ tone, title, description, primaryAction }: PublicStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:trophy-outline'
      : 'mdi:alert-circle-outline';

  return (
    <div className={styles.statusCard} data-tone={tone}>
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={22} />
      </div>
      <div className={styles.statusBody}>
        <h2 className={styles.statusTitle}>{title}</h2>
        <p className={styles.statusDescription}>{description}</p>
        {primaryAction && (
          <div className={styles.statusActions}>
            <button type="button" className={styles.primaryButton} onClick={primaryAction.onClick}>
              {primaryAction.label}
            </button>
          </div>
        )}
      </div>
    </div>
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

export const PublicLeaderboardApp = ({
  route,
  onNavigate,
  onNavigateToDiscover,
  onNavigateToProfile,
}: PublicLeaderboardAppProps) => {
  const { t } = useTranslation();
  const pageRef = useRef<HTMLDivElement>(null);
  const requestIdRef = useRef(0);
  const userId = useUserStore((state) => state.userId);
  const isLoggedIn = userId > 0;
  const [items, setItems] = useState<UnifiedLeaderboardItemData[]>([]);
  const [types, setTypes] = useState<LeaderboardTypeData[]>(() => createFallbackLeaderboardTypes(t));
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);
  const [loadingTypes, setLoadingTypes] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [typesError, setTypesError] = useState<string | null>(null);
  const [myRank, setMyRank] = useState<number | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [isCompactViewport, setIsCompactViewport] = useState(() =>
    typeof window !== 'undefined' ? window.innerWidth <= 720 : false
  );

  const fallbackTypes = useMemo(() => createFallbackLeaderboardTypes(t), [t]);
  const activeRouteDefinition = useMemo(
    () => getPublicLeaderboardRouteDefinitionBySlug(route.typeSlug),
    [route.typeSlug]
  );
  const activeTypeConfig = useMemo(() => {
    return types.find((item) => item.voType === activeRouteDefinition.type)
      ?? fallbackTypes.find((item) => item.voType === activeRouteDefinition.type)
      ?? fallbackTypes[0];
  }, [activeRouteDefinition.type, fallbackTypes, types]);

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
    document.title = `${activeTypeConfig.voName} · ${t('desktop.apps.leaderboard.name')}`;
  }, [activeTypeConfig.voName, t]);

  useEffect(() => {
    let cancelled = false;

    const loadTypes = async () => {
      setLoadingTypes(true);
      setTypesError(null);

      try {
        const result = await leaderboardApi.getTypes();
        if (!cancelled && result?.length) {
          setTypes(result);
        }
      } catch (loadTypesError) {
        if (!cancelled) {
          setTypes(fallbackTypes);
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
  }, [fallbackTypes, reloadToken]);

  useEffect(() => {
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
  }, [activeRouteDefinition.type, onNavigate, reloadToken, route.page, route.typeSlug, t]);

  useEffect(() => {
    let cancelled = false;

    const loadMyRank = async () => {
      if (!isLoggedIn || activeTypeConfig.voCategory !== LeaderboardCategory.User) {
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
  }, [activeRouteDefinition.type, activeTypeConfig.voCategory, isLoggedIn]);

  const visiblePages = useMemo(
    () => buildVisiblePages(route.page, totalPages, isCompactViewport ? 5 : 7),
    [isCompactViewport, route.page, totalPages]
  );
  const showExperienceGuide = route.typeSlug === 'experience';
  const lightweightGuide = useMemo<LightweightGuideDefinition | null>(() => {
    if (showExperienceGuide) {
      return null;
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
  }, [activeTypeConfig.voCategory, showExperienceGuide]);

  const handleTypeChange = (typeSlug: PublicLeaderboardTypeSlug) => {
    onNavigate({
      ...createDefaultPublicLeaderboardRoute(),
      typeSlug,
    });
  };

  const handleOpenUserProfile = (item: UnifiedLeaderboardItemData) => {
    if (!item.voUserId) {
      return;
    }

    onNavigateToProfile?.(String(item.voUserId));
  };

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="榜"
        brandName={t('desktop.apps.leaderboard.name')}
        brandSubline={t('leaderboard.public.shellLabel')}
        onBrandClick={() => onNavigate(createDefaultPublicLeaderboardRoute())}
        onNavigateToDiscover={onNavigateToDiscover}
        discoverLabel={t('public.shell.discoverAction')}
      />

      <main className={styles.main}>
        <section className={styles.sectionCard}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionHeading}>
              <div className={styles.sectionTitleRow}>
                <p className={styles.kicker}>Phase 2-2</p>
                <span className={styles.readOnlyBadge}>{t('leaderboard.public.readOnlyBadge')}</span>
              </div>
              <h1 className={styles.pageTitle}>{activeTypeConfig.voName}</h1>
              <p className={styles.pageIntro}>{t('leaderboard.public.pageIntro')}</p>
            </div>

            <div className={styles.sectionStats}>
              {isLoggedIn && myRank !== null && activeTypeConfig.voCategory === LeaderboardCategory.User && (
                <span className={styles.statChip}>{t('leaderboard.public.myRank', { rank: myRank })}</span>
              )}
              <span className={styles.statChip}>{activeTypeConfig.voPrimaryLabel}</span>
            </div>
          </div>

          <div className={styles.toolbar}>
            <div className={styles.tabRail}>
              {types.map((type) => {
                const typeSlug = getPublicLeaderboardRouteDefinitionByType(type.voType).slug;
                return (
                  <button
                    key={type.voType}
                    type="button"
                    className={`${styles.tabButton} ${route.typeSlug === typeSlug ? styles.tabButtonActive : ''}`}
                    onClick={() => handleTypeChange(typeSlug)}
                  >
                    <Icon icon={type.voIcon} size={18} />
                    <span>{type.voName}</span>
                  </button>
                );
              })}
            </div>
            <p className={styles.toolbarHint}>{activeTypeConfig.voDescription}</p>
          </div>

          {showExperienceGuide && (
            <section className={styles.experienceGuideSection} aria-label={t('leaderboard.public.experienceGuide.title')}>
              <div className={styles.experienceGuideHeader}>
                <div className={styles.experienceGuideHeading}>
                  <p className={styles.experienceGuideKicker}>{t('leaderboard.public.experienceGuide.kicker')}</p>
                  <h2 className={styles.experienceGuideTitle}>{t('leaderboard.public.experienceGuide.title')}</h2>
                </div>
                <p className={styles.experienceGuideIntro}>{t('leaderboard.public.experienceGuide.intro')}</p>
              </div>

              <div className={styles.experienceGuideSummary}>
                <div className={styles.experienceGuideSummaryCard}>
                  <div className={styles.experienceGuideSummaryHeading}>
                    <span className={styles.experienceGuideSummaryLabel}>
                      {t('leaderboard.public.experienceGuide.summaryLabel')}
                    </span>
                    <h3 className={styles.experienceGuideSummaryTitle}>
                      {t('leaderboard.public.experienceGuide.summaryTitle')}
                    </h3>
                  </div>
                  <p className={styles.experienceGuideSummaryDescription}>
                    {t('leaderboard.public.experienceGuide.summaryDescription')}
                  </p>

                  <div className={styles.experienceGuideFocusRow}>
                    {experienceGuideFocusItems.map((item) => (
                      <article key={item.labelKey} className={styles.experienceGuideFocusChip}>
                        <span className={styles.experienceGuideFocusLabel}>{t(item.labelKey)}</span>
                        <span className={styles.experienceGuideFocusValue}>{t(item.valueKey)}</span>
                      </article>
                    ))}
                  </div>
                </div>

                <aside className={styles.experienceGuideBoundaryPanel}>
                  <span className={styles.experienceGuideBoundaryLabel}>
                    {t('leaderboard.public.experienceGuide.boundaryPanelLabel')}
                  </span>
                  <h3 className={styles.experienceGuideBoundaryTitle}>
                    {t('leaderboard.public.experienceGuide.boundaryPanelTitle')}
                  </h3>
                  <p className={styles.experienceGuideBoundaryDescription}>
                    {t('leaderboard.public.experienceGuide.boundaryPanelDescription')}
                  </p>
                  <ul className={styles.experienceGuideBoundaryList}>
                    {experienceGuideBoundaryItems.map((itemKey) => (
                      <li key={itemKey} className={styles.experienceGuideBoundaryItem}>
                        {t(itemKey)}
                      </li>
                    ))}
                  </ul>
                </aside>
              </div>

              <div className={styles.experienceGuideGrid}>
                {experienceGuideItems.map((item) => (
                  <article key={item.titleKey} className={styles.experienceGuideCard}>
                    <span className={styles.experienceGuideIcon} aria-hidden="true">
                      <Icon icon={item.icon} size={18} />
                    </span>
                    <div className={styles.experienceGuideBody}>
                      <h3 className={styles.experienceGuideCardTitle}>{t(item.titleKey)}</h3>
                      <p className={styles.experienceGuideCardDescription}>{t(item.descriptionKey)}</p>
                    </div>
                  </article>
                ))}
              </div>
            </section>
          )}

          {lightweightGuide && (
            <section className={styles.lightweightGuideSection} aria-label={t(lightweightGuide.titleKey)}>
              <div className={styles.lightweightGuideHeader}>
                <div className={styles.lightweightGuideHeading}>
                  <span className={styles.lightweightGuideLabel}>{t('leaderboard.public.lightweightGuide.label')}</span>
                  <h2 className={styles.lightweightGuideTitle}>{t(lightweightGuide.titleKey)}</h2>
                </div>
                <p className={styles.lightweightGuideDescription}>{t(lightweightGuide.descriptionKey)}</p>
              </div>

              <div className={styles.lightweightGuideFocusRow}>
                {lightweightGuide.focusItems.map((item) => (
                  <article key={item.labelKey} className={styles.lightweightGuideFocusChip}>
                    <span className={styles.lightweightGuideFocusLabel}>{t(item.labelKey)}</span>
                    <span className={styles.lightweightGuideFocusValue}>{t(item.valueKey)}</span>
                  </article>
                ))}
              </div>
            </section>
          )}

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
                    const userName = item.voUserName?.trim() || t('common.userFallback', { id: item.voUserId || '?' });
                    const avatarUrl = resolveMediaUrl(item.voAvatarUrl);
                    return (
                      <button
                        key={`${item.voLeaderboardType}-${String(item.voUserId)}-${item.voRank}`}
                        type="button"
                        className={`${styles.listItem} ${styles.userListItem} ${item.voIsCurrentUser ? styles.listItemCurrentUser : ''}`}
                        onClick={() => handleOpenUserProfile(item)}
                        disabled={!item.voUserId}
                      >
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
                            {Number(item.voPrimaryValue || 0).toLocaleString()} {item.voPrimaryLabel || activeTypeConfig.voPrimaryLabel}
                          </span>
                        </div>
                      </button>
                    );
                  })()
                ) : (() => {
                  const productIconUrl = resolveMediaUrl(item.voProductIcon);
                  return (
                    <article
                      key={`${item.voLeaderboardType}-${String(item.voProductId)}-${item.voRank}`}
                      className={`${styles.listItem} ${styles.productListItem}`}
                    >
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
                            <span className={styles.productReadonly}>{t('leaderboard.public.productReadOnly')}</span>
                          </div>
                          <p className={styles.itemMeta}>{t('leaderboard.public.productReadOnly')}</p>
                        </div>
                        <div className={styles.itemSideInfo}>
                          <span className={styles.sideInfoLabel}>{t('shop.meta.price')}</span>
                          <span className={styles.sideInfoValue}>
                            {t('leaderboard.public.productPrice', {
                              price: Number(item.voProductPrice || 0).toLocaleString(),
                            })}
                          </span>
                        </div>
                      </div>
                      <div className={styles.itemMetric}>
                        <span className={styles.metricValue}>{Number(item.voPrimaryValue || 0).toLocaleString()}</span>
                        <span className={styles.metricLabel}>{item.voPrimaryLabel || activeTypeConfig.voPrimaryLabel}</span>
                      </div>
                    </article>
                  );
                })())}
              </div>
            )}
          </div>

          {totalPages > 1 && !loading && !error && (
            <div className={styles.pagination}>
              <button
                type="button"
                className={styles.paginationButton}
                onClick={() => onNavigate({ kind: 'list', typeSlug: route.typeSlug, page: Math.max(1, route.page - 1) })}
                disabled={route.page === 1}
              >
                {t('common.previousPage')}
              </button>
              <div className={styles.pageNumbers}>
                {visiblePages.map((page) => (
                  <button
                    key={page}
                    type="button"
                    className={`${styles.pageNumberButton} ${page === route.page ? styles.pageNumberButtonActive : ''}`}
                    onClick={() => onNavigate({ kind: 'list', typeSlug: route.typeSlug, page })}
                  >
                    {page}
                  </button>
                ))}
              </div>
              <button
                type="button"
                className={styles.paginationButton}
                onClick={() => onNavigate({ kind: 'list', typeSlug: route.typeSlug, page: Math.min(totalPages, route.page + 1) })}
                disabled={route.page >= totalPages}
              >
                {t('common.nextPage')}
              </button>
            </div>
          )}
        </section>
      </main>
    </div>
  );
};
