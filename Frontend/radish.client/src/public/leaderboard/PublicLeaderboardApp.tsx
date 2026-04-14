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
                  <button
                    key={`${item.voLeaderboardType}-${String(item.voUserId)}-${item.voRank}`}
                    type="button"
                    className={`${styles.listItem} ${item.voIsCurrentUser ? styles.listItemCurrentUser : ''}`}
                    onClick={() => handleOpenUserProfile(item)}
                    disabled={!item.voUserId}
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
                    <div className={styles.itemBody}>
                      <div className={styles.itemPrimary}>
                        <div className={styles.itemTitleRow}>
                          <span className={styles.itemTitle}>
                            {item.voUserName?.trim() || t('common.userFallback', { id: item.voUserId || '?' })}
                          </span>
                          {item.voIsCurrentUser && (
                            <span className={styles.currentUserBadge}>{t('leaderboard.public.currentUser')}</span>
                          )}
                        </div>
                        <p
                          className={styles.itemMeta}
                          style={item.voThemeColor ? { color: item.voThemeColor } : undefined}
                        >
                          {t('leaderboard.public.levelLabel', {
                            level: item.voCurrentLevel ?? 0,
                            levelName: item.voCurrentLevelName?.trim() || t('leaderboard.public.levelFallback'),
                          })}
                        </p>
                      </div>
                      {item.voSecondaryValue !== undefined && item.voSecondaryLabel && (
                        <div className={styles.itemSideInfo}>
                          <span className={styles.sideInfoLabel}>{item.voSecondaryLabel}</span>
                          <span className={styles.sideInfoValue}>{Number(item.voSecondaryValue).toLocaleString()}</span>
                        </div>
                      )}
                    </div>
                    <div className={styles.itemMetric}>
                      <span className={styles.metricValue}>{Number(item.voPrimaryValue || 0).toLocaleString()}</span>
                      <span className={styles.metricLabel}>{item.voPrimaryLabel || activeTypeConfig.voPrimaryLabel}</span>
                    </div>
                  </button>
                ) : (() => {
                  const productIconUrl = resolveMediaUrl(item.voProductIcon);
                  return (
                    <article
                      key={`${item.voLeaderboardType}-${String(item.voProductId)}-${item.voRank}`}
                      className={styles.listItem}
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
