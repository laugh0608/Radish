import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { PostCard } from '@/apps/forum/components/PostCard';
import type { WikiDocumentVo } from '@/apps/wiki/types/wiki';
import {
  getHotTags,
  getPostList,
  type PostItem,
  type Tag,
} from '@/api/forum';
import { getProducts, type ProductListItem } from '@/api/shop';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
import { getPublicWikiList } from '../docs/publicDocsApi';
import type { PublicDocsRoute } from '../docsRouteState';
import { createDefaultPublicLeaderboardRoute, type PublicLeaderboardRoute } from '../leaderboardRouteState';
import type { PublicForumRoute } from '../forumRouteState';
import type { PublicShopRoute } from '../shopRouteState';
import { createDefaultPublicShopProductsRoute } from '../shopRouteState';
import { PublicShellHeader } from '../components/PublicShellHeader';
import styles from './PublicDiscoverApp.module.css';

interface PublicDiscoverAppProps {
  onNavigateToForum: (route: PublicForumRoute, options?: { replace?: boolean }) => void;
  onNavigateToDocs: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
  onNavigateToLeaderboard: (route: PublicLeaderboardRoute, options?: { replace?: boolean }) => void;
  onNavigateToShop: (route: PublicShopRoute, options?: { replace?: boolean }) => void;
}

interface SectionStatusCardProps {
  tone: 'loading' | 'error' | 'empty';
  title: string;
  description: string;
  actionLabel?: string;
  onAction?: () => void;
}

interface DiscoverGuideItemDefinition {
  icon: string;
  titleKey: string;
  descriptionKey: string;
}

interface DiscoverRouteGuideDefinition {
  key: 'forum' | 'docs' | 'leaderboard' | 'shop';
  icon: string;
  titleKey: string;
  descriptionKey: string;
  valueLabelKey: string;
  valueTextKey: string;
  destinationLabelKey: string;
  destinationTextKey: string;
  boundaryLabelKey: string;
  boundaryTextKey: string;
}

const featuredLeaderboardConfigs = [
  {
    key: 'experience',
    icon: 'mdi:star-circle',
    nameKey: 'leaderboard.public.types.experience.name',
    descriptionKey: 'leaderboard.public.types.experience.description',
  },
  {
    key: 'post-count',
    icon: 'mdi:file-document-outline',
    nameKey: 'leaderboard.public.types.postCount.name',
    descriptionKey: 'leaderboard.public.types.postCount.description',
  },
  {
    key: 'hot-product',
    icon: 'mdi:gift-outline',
    nameKey: 'leaderboard.public.types.hotProduct.name',
    descriptionKey: 'leaderboard.public.types.hotProduct.description',
  },
] as const;

const discoverGuideItems: DiscoverGuideItemDefinition[] = [
  {
    icon: 'mdi:compass-outline',
    titleKey: 'discover.public.heroGuideValueTitle',
    descriptionKey: 'discover.public.heroGuideValueDescription',
  },
  {
    icon: 'mdi:source-branch',
    titleKey: 'discover.public.heroGuideReturnTitle',
    descriptionKey: 'discover.public.heroGuideReturnDescription',
  },
  {
    icon: 'mdi:shield-half-full',
    titleKey: 'discover.public.heroGuideBoundaryTitle',
    descriptionKey: 'discover.public.heroGuideBoundaryDescription',
  },
] as const;

const discoverRouteGuideDefinitions: DiscoverRouteGuideDefinition[] = [
  {
    key: 'forum',
    icon: 'mdi:forum-outline',
    titleKey: 'discover.public.routeForumTitle',
    descriptionKey: 'discover.public.routeForumDescription',
    valueLabelKey: 'discover.public.routeValueLabel',
    valueTextKey: 'discover.public.routeForumValue',
    destinationLabelKey: 'discover.public.routeDestinationLabel',
    destinationTextKey: 'discover.public.routeForumDestination',
    boundaryLabelKey: 'discover.public.routeBoundaryLabel',
    boundaryTextKey: 'discover.public.routeForumBoundary',
  },
  {
    key: 'docs',
    icon: 'mdi:file-document-outline',
    titleKey: 'discover.public.routeDocsTitle',
    descriptionKey: 'discover.public.routeDocsDescription',
    valueLabelKey: 'discover.public.routeValueLabel',
    valueTextKey: 'discover.public.routeDocsValue',
    destinationLabelKey: 'discover.public.routeDestinationLabel',
    destinationTextKey: 'discover.public.routeDocsDestination',
    boundaryLabelKey: 'discover.public.routeBoundaryLabel',
    boundaryTextKey: 'discover.public.routeDocsBoundary',
  },
  {
    key: 'leaderboard',
    icon: 'mdi:trophy-outline',
    titleKey: 'discover.public.routeLeaderboardTitle',
    descriptionKey: 'discover.public.routeLeaderboardDescription',
    valueLabelKey: 'discover.public.routeValueLabel',
    valueTextKey: 'discover.public.routeLeaderboardValue',
    destinationLabelKey: 'discover.public.routeDestinationLabel',
    destinationTextKey: 'discover.public.routeLeaderboardDestination',
    boundaryLabelKey: 'discover.public.routeBoundaryLabel',
    boundaryTextKey: 'discover.public.routeLeaderboardBoundary',
  },
  {
    key: 'shop',
    icon: 'mdi:storefront-outline',
    titleKey: 'discover.public.routeShopTitle',
    descriptionKey: 'discover.public.routeShopDescription',
    valueLabelKey: 'discover.public.routeValueLabel',
    valueTextKey: 'discover.public.routeShopValue',
    destinationLabelKey: 'discover.public.routeDestinationLabel',
    destinationTextKey: 'discover.public.routeShopDestination',
    boundaryLabelKey: 'discover.public.routeBoundaryLabel',
    boundaryTextKey: 'discover.public.routeShopBoundary',
  },
] as const;

function SectionStatusCard({ tone, title, description, actionLabel, onAction }: SectionStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : 'mdi:alert-circle-outline';

  return (
    <div className={styles.statusCard} data-tone={tone}>
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={20} />
      </div>
      <div>
        <h3 className={styles.statusTitle}>{title}</h3>
        <p className={styles.statusDescription}>{description}</p>
        {actionLabel && onAction && (
          <button type="button" className={styles.secondaryButton} onClick={onAction}>
            {actionLabel}
          </button>
        )}
      </div>
    </div>
  );
}

function formatDocumentMeta(document: WikiDocumentVo, locale: string, fallback: string): string {
  const source = document.voPublishedAt || document.voModifyTime || document.voCreateTime;
  if (!source) {
    return fallback;
  }

  const parsed = new Date(source);
  if (Number.isNaN(parsed.getTime())) {
    return fallback;
  }

  return parsed.toLocaleDateString(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  });
}

export const PublicDiscoverApp = ({
  onNavigateToForum,
  onNavigateToDocs,
  onNavigateToLeaderboard,
  onNavigateToShop
}: PublicDiscoverAppProps) => {
  const { t, i18n } = useTranslation();
  const [forumPosts, setForumPosts] = useState<PostItem[]>([]);
  const [hotTags, setHotTags] = useState<Tag[]>([]);
  const [docs, setDocs] = useState<WikiDocumentVo[]>([]);
  const [products, setProducts] = useState<ProductListItem[]>([]);
  const [loadingForum, setLoadingForum] = useState(true);
  const [loadingDocs, setLoadingDocs] = useState(true);
  const [loadingShop, setLoadingShop] = useState(true);
  const [forumError, setForumError] = useState<string | null>(null);
  const [docsError, setDocsError] = useState<string | null>(null);
  const [shopError, setShopError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);

  useEffect(() => {
    document.title = `${t('discover.public.pageTitle')} · ${t('discover.public.shellLabel')}`;
  }, [t]);

  useEffect(() => {
    let cancelled = false;

    const loadForum = async () => {
      setLoadingForum(true);
      setForumError(null);

      try {
        const [postPage, tagList] = await Promise.all([
          getPostList(null, t, 1, 4, 'newest'),
          getHotTags(t, 6)
        ]);

        if (cancelled) {
          return;
        }

        setForumPosts(postPage.data ?? []);
        setHotTags(tagList ?? []);
      } catch (error) {
        if (cancelled) {
          return;
        }

        setForumPosts([]);
        setHotTags([]);
        setForumError(error instanceof Error ? error.message : String(error));
      } finally {
        if (!cancelled) {
          setLoadingForum(false);
        }
      }
    };

    const loadDocs = async () => {
      setLoadingDocs(true);
      setDocsError(null);

      try {
        const result = await getPublicWikiList({ pageIndex: 1, pageSize: 4 });
        if (cancelled) {
          return;
        }

        setDocs(result.data ?? []);
      } catch (error) {
        if (cancelled) {
          return;
        }

        setDocs([]);
        setDocsError(error instanceof Error ? error.message : String(error));
      } finally {
        if (!cancelled) {
          setLoadingDocs(false);
        }
      }
    };

    const loadShop = async () => {
      setLoadingShop(true);
      setShopError(null);

      try {
        const result = await getProducts(t, undefined, undefined, undefined, 1, 4);
        if (cancelled) {
          return;
        }

        if (!result.ok || !result.data) {
          throw new Error(result.message || t('discover.public.shopLoadFailedDescription'));
        }

        setProducts(result.data.data ?? []);
      } catch (error) {
        if (cancelled) {
          return;
        }

        setProducts([]);
        setShopError(error instanceof Error ? error.message : String(error));
      } finally {
        if (!cancelled) {
          setLoadingShop(false);
        }
      }
    };

    void Promise.all([loadForum(), loadDocs(), loadShop()]);
    return () => {
      cancelled = true;
    };
  }, [reloadToken, t]);

  const summaryCards = useMemo(() => ([
    {
      label: t('discover.public.summaryForumLabel'),
      value: String(forumPosts.length)
    },
    {
      label: t('discover.public.summaryDocsLabel'),
      value: String(docs.length)
    },
    {
      label: t('discover.public.summaryTagsLabel'),
      value: String(hotTags.length)
    },
    {
      label: t('discover.public.summaryShopLabel'),
      value: String(products.length)
    }
  ]), [docs.length, forumPosts.length, hotTags.length, products.length, t]);

  const routeGuideCards = useMemo(() => (
    discoverRouteGuideDefinitions.map((item) => ({
      ...item,
      onClick: () => {
        switch (item.key) {
          case 'forum':
            onNavigateToForum({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 });
            return;
          case 'docs':
            onNavigateToDocs({ kind: 'list' });
            return;
          case 'leaderboard':
            onNavigateToLeaderboard(createDefaultPublicLeaderboardRoute());
            return;
          case 'shop':
            onNavigateToShop({ kind: 'home' });
            return;
        }
      }
    }))
  ), [onNavigateToDocs, onNavigateToForum, onNavigateToLeaderboard, onNavigateToShop]);

  return (
    <div className={styles.page}>
      <PublicShellHeader
        brandMark="览"
        brandName={t('discover.public.pageTitle')}
        brandSubline={t('discover.public.shellLabel')}
        onBrandClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
      />

      <main className={styles.main}>
        <section className={styles.heroCard}>
          <div className={styles.heroTitleRow}>
            <p className={styles.kicker}>Phase 2-2</p>
            <span className={styles.readOnlyBadge}>{t('discover.public.readOnlyBadge')}</span>
          </div>
          <h1 className={styles.pageTitle}>{t('discover.public.pageTitle')}</h1>
          <p className={styles.pageIntro}>{t('discover.public.pageIntro')}</p>

          <div className={styles.heroGuideGrid}>
            {discoverGuideItems.map((item) => (
              <article key={item.titleKey} className={styles.heroGuideCard}>
                <span className={styles.heroGuideIcon} aria-hidden="true">
                  <Icon icon={item.icon} size={18} />
                </span>
                <div className={styles.heroGuideBody}>
                  <h2 className={styles.heroGuideTitle}>{t(item.titleKey)}</h2>
                  <p className={styles.heroGuideDescription}>{t(item.descriptionKey)}</p>
                </div>
              </article>
            ))}
          </div>

          <div className={styles.heroActions}>
            <button
              type="button"
              className={styles.primaryButton}
              onClick={() => onNavigateToForum({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
            >
              <Icon icon="mdi:forum-outline" size={18} />
              <span>{t('discover.public.openForum')}</span>
            </button>
            <button
              type="button"
              className={styles.secondaryButton}
              onClick={() => onNavigateToDocs({ kind: 'search', keyword: '', page: 1 })}
            >
              <Icon icon="mdi:file-search-outline" size={18} />
              <span>{t('discover.public.searchDocs')}</span>
            </button>
            <button
              type="button"
              className={styles.secondaryButton}
              onClick={() => onNavigateToShop(createDefaultPublicShopProductsRoute())}
            >
              <Icon icon="mdi:store-search-outline" size={18} />
              <span>{t('discover.public.browseShop')}</span>
            </button>
          </div>

          <div className={styles.summaryGrid}>
            {summaryCards.map((item) => (
              <div key={item.label} className={styles.summaryCard}>
                <span className={styles.summaryLabel}>{item.label}</span>
                <span className={styles.summaryValue}>{item.value}</span>
              </div>
            ))}
          </div>
        </section>

        <div className={styles.sectionGrid}>
          <section className={styles.sectionCard}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.routeSectionTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.routeSectionDescription')}</p>
              </div>
            </div>

            <div className={styles.routeGrid}>
              {routeGuideCards.map((item) => (
                <button
                  key={item.key}
                  type="button"
                  className={styles.routeButton}
                  onClick={item.onClick}
                >
                  <div className={styles.routeTop}>
                    <span className={styles.routeIcon}>
                      <Icon icon={item.icon} size={20} />
                    </span>
                    <div className={styles.routeHeading}>
                      <span className={styles.routeLabel}>{t(item.titleKey)}</span>
                      <span className={styles.routeDescription}>{t(item.descriptionKey)}</span>
                    </div>
                  </div>

                  <div className={styles.routeMetaList}>
                    <div className={styles.routeMetaRow}>
                      <span className={styles.routeMetaLabel}>{t(item.valueLabelKey)}</span>
                      <span className={styles.routeMetaText}>{t(item.valueTextKey)}</span>
                    </div>
                    <div className={styles.routeMetaRow}>
                      <span className={styles.routeMetaLabel}>{t(item.destinationLabelKey)}</span>
                      <span className={styles.routeMetaText}>{t(item.destinationTextKey)}</span>
                    </div>
                    <div className={styles.routeMetaRow}>
                      <span className={styles.routeMetaLabel}>{t(item.boundaryLabelKey)}</span>
                      <span className={styles.routeMetaText}>{t(item.boundaryTextKey)}</span>
                    </div>
                  </div>

                  <span className={styles.routeActionHint}>
                    {t('discover.public.routeActionHint')}
                    <Icon icon="mdi:arrow-right" size={16} />
                  </span>
                </button>
              ))}
            </div>
          </section>

          <section className={styles.sectionCard}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.forumTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.forumDescription')}</p>
              </div>
              <button
                type="button"
                className={styles.sectionLink}
                onClick={() => onNavigateToForum({ kind: 'list', categoryId: null, sortBy: 'newest', page: 1 })}
              >
                <span>{t('discover.public.viewAllForum')}</span>
                <Icon icon="mdi:arrow-right" size={16} />
              </button>
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
                actionLabel={t('common.retry')}
                onAction={() => setReloadToken((current) => current + 1)}
              />
            ) : forumPosts.length === 0 ? (
              <SectionStatusCard
                tone="empty"
                title={t('discover.public.forumEmptyTitle')}
                description={t('discover.public.forumEmptyDescription')}
              />
            ) : (
              <>
                <div className={styles.forumList}>
                  {forumPosts.map((post) => (
                    <PostCard
                      key={post.voId}
                      post={post}
                      displayTimeZone={displayTimeZone}
                      onClick={() => onNavigateToForum({ kind: 'detail', postId: String(post.voId) })}
                      variant="publicCompact"
                      onTagClick={(_, tagSlug) => onNavigateToForum({ kind: 'tag', tagSlug, sortBy: 'newest', page: 1 })}
                      onQuestionClick={() => onNavigateToForum({ kind: 'question', sortBy: 'newest', page: 1 })}
                      onPollClick={() => onNavigateToForum({ kind: 'poll', sortBy: 'newest', page: 1 })}
                      onLotteryClick={() => onNavigateToForum({ kind: 'lottery', sortBy: 'newest', page: 1 })}
                    />
                  ))}
                </div>
                {hotTags.length > 0 && (
                  <div className={styles.tagRail}>
                    {hotTags.map((tag) => (
                      <button
                        key={tag.voId}
                        type="button"
                        className={styles.tagChip}
                        onClick={() => onNavigateToForum({ kind: 'tag', tagSlug: tag.voSlug, sortBy: 'newest', page: 1 })}
                      >
                        #{tag.voName}
                      </button>
                    ))}
                  </div>
                )}
              </>
            )}
          </section>
        </div>

        <div className={styles.sectionGrid}>
          <section className={styles.sectionCard}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.docsTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.docsDescription')}</p>
              </div>
              <button type="button" className={styles.sectionLink} onClick={() => onNavigateToDocs({ kind: 'list' })}>
                <span>{t('discover.public.viewAllDocs')}</span>
                <Icon icon="mdi:arrow-right" size={16} />
              </button>
            </div>

            {loadingDocs ? (
              <SectionStatusCard
                tone="loading"
                title={t('discover.public.docsLoadingTitle')}
                description={t('discover.public.docsLoadingDescription')}
              />
            ) : docsError ? (
              <SectionStatusCard
                tone="error"
                title={t('discover.public.docsLoadFailedTitle')}
                description={docsError}
                actionLabel={t('common.retry')}
                onAction={() => setReloadToken((current) => current + 1)}
              />
            ) : docs.length === 0 ? (
              <SectionStatusCard
                tone="empty"
                title={t('discover.public.docsEmptyTitle')}
                description={t('discover.public.docsEmptyDescription')}
              />
            ) : (
              <div className={styles.docsList}>
                {docs.map((document) => (
                  <article key={document.voId} className={styles.docsItem}>
                    <button
                      type="button"
                      className={styles.docsButton}
                      onClick={() => onNavigateToDocs({ kind: 'detail', slug: document.voSlug })}
                    >
                      <h3 className={styles.docsTitle}>{document.voTitle}</h3>
                      <p className={styles.docsSummary}>
                        {document.voSummary?.trim() || t('discover.public.documentFallbackSummary')}
                      </p>
                      <span className={styles.docsMeta}>
                        <Icon icon="mdi:calendar-blank-outline" size={16} />
                        <span>{formatDocumentMeta(document, i18n.language, t('discover.public.documentMetaFallback'))}</span>
                      </span>
                    </button>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className={styles.sectionCard}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.leaderboardTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.leaderboardDescription')}</p>
              </div>
              <button
                type="button"
                className={styles.sectionLink}
                onClick={() => onNavigateToLeaderboard(createDefaultPublicLeaderboardRoute())}
              >
                <span>{t('discover.public.viewAllLeaderboard')}</span>
                <Icon icon="mdi:arrow-right" size={16} />
              </button>
            </div>

            <div className={styles.leaderboardList}>
              {featuredLeaderboardConfigs.map((item) => (
                <article key={item.key} className={styles.leaderboardItem}>
                  <div className={styles.leaderboardTop}>
                    <span className={styles.leaderboardIcon}>
                      <Icon icon={item.icon} size={20} />
                    </span>
                    <h3 className={styles.leaderboardTitle}>{t(item.nameKey)}</h3>
                  </div>
                  <p className={styles.leaderboardSummary}>{t(item.descriptionKey)}</p>
                  <button
                    type="button"
                    className={`${styles.secondaryButton} ${styles.leaderboardButton}`}
                    onClick={() => {
                      const defaultRoute = createDefaultPublicLeaderboardRoute();
                      onNavigateToLeaderboard({
                        ...defaultRoute,
                        typeSlug: item.key
                      });
                    }}
                  >
                    {t('discover.public.openLeaderboardType')}
                  </button>
                </article>
              ))}
            </div>
          </section>
        </div>

        <section className={styles.sectionCard}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionHeading}>
              <h2 className={styles.sectionTitle}>{t('discover.public.shopTitle')}</h2>
              <p className={styles.sectionDescription}>{t('discover.public.shopDescription')}</p>
            </div>
            <button
              type="button"
              className={styles.sectionLink}
              onClick={() => onNavigateToShop(createDefaultPublicShopProductsRoute())}
            >
              <span>{t('discover.public.viewAllShop')}</span>
              <Icon icon="mdi:arrow-right" size={16} />
            </button>
          </div>

          {loadingShop ? (
            <SectionStatusCard
              tone="loading"
              title={t('discover.public.shopLoadingTitle')}
              description={t('discover.public.shopLoadingDescription')}
            />
          ) : shopError ? (
            <SectionStatusCard
              tone="error"
              title={t('discover.public.shopLoadFailedTitle')}
              description={shopError}
              actionLabel={t('common.retry')}
              onAction={() => setReloadToken((current) => current + 1)}
            />
          ) : products.length === 0 ? (
            <SectionStatusCard
              tone="empty"
              title={t('discover.public.shopEmptyTitle')}
              description={t('discover.public.shopEmptyDescription')}
            />
          ) : (
            <div className={styles.productList}>
              {products.map((product) => {
                const imageUrl = resolveMediaUrl(product.voCoverImage || product.voIcon);
                return (
                  <article key={product.voId} className={styles.productItem}>
                    <button
                      type="button"
                      className={styles.productButton}
                      onClick={() => onNavigateToShop({ kind: 'detail', productId: String(product.voId) })}
                    >
                      <div className={styles.productCardHead}>
                        {imageUrl ? (
                          <img className={styles.productImage} src={imageUrl} alt={product.voName} />
                        ) : (
                          <span className={styles.productImageFallback}>
                            <Icon icon="mdi:gift-outline" size={24} />
                          </span>
                        )}
                        <div>
                          <h3 className={styles.productTitle}>{product.voName}</h3>
                          <p className={styles.productSummary}>{product.voDurationDisplay || t('discover.public.productFallbackSummary')}</p>
                        </div>
                      </div>
                      <span className={styles.productPrice}>
                        <Icon icon="mdi:carrot" size={16} />
                        <span>{product.voPrice.toLocaleString()} {t('shop.currency.carrot')}</span>
                      </span>
                    </button>
                  </article>
                );
              })}
            </div>
          )}
        </section>
      </main>
    </div>
  );
};
