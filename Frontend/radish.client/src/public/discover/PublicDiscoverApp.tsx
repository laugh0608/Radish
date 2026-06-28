import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
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
import { getProductTypeDisplay, getProducts, type ProductListItem } from '@/api/shop';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { resolveMediaUrl } from '@/utils/media';
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
import type { PublicShopRoute } from '../shopRouteState';
import { buildPublicShopPath, createDefaultPublicShopProductsRoute } from '../shopRouteState';
import { buildPublicShareUrl } from '../publicHead';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import {
  buildDocumentSummary,
  buildProductSummary,
  formatDocumentMeta,
  isRecentDocument,
} from './publicDiscoverFeedUtils';
import { PublicDiscoverFeed } from './PublicDiscoverFeed';
import styles from './PublicDiscoverApp.module.css';

type DiscoverRouteKey = 'forum' | 'docs' | 'leaderboard' | 'shop';

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
  guideLabel?: string;
  guideText?: string;
  destinationLabel?: string;
  destinationText?: string;
  boundaryLabel?: string;
  boundaryText?: string;
  primaryAction?: SectionStatusAction;
  secondaryAction?: SectionStatusAction;
}

interface DiscoverGuideItemDefinition {
  icon: string;
  titleKey: string;
  descriptionKey: string;
}

interface DiscoverRouteGuideDefinition {
  key: DiscoverRouteKey;
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

interface DiscoverSectionCueDefinition {
  key: string;
  labelKey: string;
}

interface DiscoverSummaryCardDefinition {
  key: DiscoverRouteKey;
  icon: string;
  label: string;
  value: string;
  state: 'ready' | 'loading' | 'error' | 'empty';
  statusLabel: string;
  meaning: string;
  destination: string;
  href: string;
  onPreview: () => void;
  onOpen: () => void;
}

interface DiscoverLeaderboardCardDefinition {
  key: 'experience' | 'post-count' | 'hot-product';
  icon: string;
  nameKey: string;
  descriptionKey: string;
  cueLabelKeys: readonly string[];
  focusLabelKey: string;
  focusTextKey: string;
  boundaryLabelKey: string;
  boundaryTextKey: string;
}

const featuredLeaderboardConfigs: DiscoverLeaderboardCardDefinition[] = [
  {
    key: 'experience',
    icon: 'mdi:star-circle',
    nameKey: 'leaderboard.public.types.experience.name',
    descriptionKey: 'leaderboard.public.types.experience.description',
    cueLabelKeys: [
      'discover.public.leaderboardItemExperienceCueRanking',
      'discover.public.leaderboardItemExperienceCueLevel',
      'discover.public.leaderboardItemExperienceCueBoundary',
    ],
    focusLabelKey: 'discover.public.leaderboardItemFocusLabel',
    focusTextKey: 'discover.public.leaderboardItemExperienceFocus',
    boundaryLabelKey: 'discover.public.leaderboardItemBoundaryLabel',
    boundaryTextKey: 'discover.public.leaderboardItemExperienceBoundary',
  },
  {
    key: 'post-count',
    icon: 'mdi:file-document-outline',
    nameKey: 'leaderboard.public.types.postCount.name',
    descriptionKey: 'leaderboard.public.types.postCount.description',
    cueLabelKeys: [
      'discover.public.leaderboardItemPostCueAuthors',
      'discover.public.leaderboardItemPostCueProfiles',
      'discover.public.leaderboardItemPostCueReadOnly',
    ],
    focusLabelKey: 'discover.public.leaderboardItemFocusLabel',
    focusTextKey: 'discover.public.leaderboardItemPostFocus',
    boundaryLabelKey: 'discover.public.leaderboardItemBoundaryLabel',
    boundaryTextKey: 'discover.public.leaderboardItemPostBoundary',
  },
  {
    key: 'hot-product',
    icon: 'mdi:gift-outline',
    nameKey: 'leaderboard.public.types.hotProduct.name',
    descriptionKey: 'leaderboard.public.types.hotProduct.description',
    cueLabelKeys: [
      'discover.public.leaderboardItemProductCuePopularity',
      'discover.public.leaderboardItemProductCueReadOnly',
      'discover.public.leaderboardItemProductCueShopBoundary',
    ],
    focusLabelKey: 'discover.public.leaderboardItemFocusLabel',
    focusTextKey: 'discover.public.leaderboardItemProductFocus',
    boundaryLabelKey: 'discover.public.leaderboardItemBoundaryLabel',
    boundaryTextKey: 'discover.public.leaderboardItemProductBoundary',
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

const forumSectionCueDefinitions: DiscoverSectionCueDefinition[] = [
  { key: 'latest', labelKey: 'discover.public.forumCueLatest' },
  { key: 'tags', labelKey: 'discover.public.forumCueTags' },
  { key: 'return', labelKey: 'discover.public.forumCueReturn' },
] as const;

const docsSectionCueDefinitions: DiscoverSectionCueDefinition[] = [
  { key: 'directory', labelKey: 'discover.public.docsCueDirectory' },
  { key: 'search', labelKey: 'discover.public.docsCueSearch' },
  { key: 'detail', labelKey: 'discover.public.docsCueDetail' },
] as const;

const shopSectionCueDefinitions: DiscoverSectionCueDefinition[] = [
  { key: 'browse', labelKey: 'discover.public.shopCueBrowse' },
  { key: 'detail', labelKey: 'discover.public.shopCueDetail' },
  { key: 'workspace', labelKey: 'discover.public.shopCueWorkspace' },
] as const;

const leaderboardSectionCueDefinitions: DiscoverSectionCueDefinition[] = [
  { key: 'ranking', labelKey: 'discover.public.leaderboardCueRanking' },
  { key: 'profile', labelKey: 'discover.public.leaderboardCueProfile' },
  { key: 'boundary', labelKey: 'discover.public.leaderboardCueBoundary' },
] as const;

const PUBLIC_SECTION_SCROLL_OFFSET = 88;

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
  guideLabel,
  guideText,
  destinationLabel,
  destinationText,
  boundaryLabel,
  boundaryText,
  primaryAction,
  secondaryAction
}: SectionStatusCardProps) {
  const resolvedIcon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = [];
  const meta = (guideText || destinationText || boundaryText) ? (
    <div className={styles.statusMetaList}>
      {guideText && guideLabel && (
        <div className={styles.statusMetaRow}>
          <span className={styles.statusMetaLabel}>{guideLabel}</span>
          <span className={styles.statusMetaText}>{guideText}</span>
        </div>
      )}
      {destinationText && destinationLabel && (
        <div className={styles.statusMetaRow}>
          <span className={styles.statusMetaLabel}>{destinationLabel}</span>
          <span className={styles.statusMetaText}>{destinationText}</span>
        </div>
      )}
      {boundaryText && boundaryLabel && (
        <div className={styles.statusMetaRow}>
          <span className={styles.statusMetaLabel}>{boundaryLabel}</span>
          <span className={styles.statusMetaText}>{boundaryText}</span>
        </div>
      )}
    </div>
  ) : undefined;

  if (primaryAction) {
    actions.push({
      label: primaryAction.label,
      href: primaryAction.href,
      kind: 'primary',
      onClick: primaryAction.href
        ? (event) => {
          if (!shouldHandlePublicDiscoverLink(event as MouseEvent<HTMLAnchorElement>)) {
            return;
          }

          event.preventDefault();
          primaryAction.onClick();
        }
        : () => primaryAction.onClick(),
    });
  }

  if (secondaryAction) {
    actions.push({
      label: secondaryAction.label,
      href: secondaryAction.href,
      kind: 'secondary',
      onClick: secondaryAction.href
        ? (event) => {
          if (!shouldHandlePublicDiscoverLink(event as MouseEvent<HTMLAnchorElement>)) {
            return;
          }

          event.preventDefault();
          secondaryAction.onClick();
        }
        : () => secondaryAction.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={resolvedIcon}
      compact={true}
      meta={meta}
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
  onNavigateToShop
}: PublicDiscoverAppProps) => {
  const { t, i18n } = useTranslation();
  const routeScrollBehaviorRef = useRef<ScrollBehavior>('auto');
  const sectionRefs = useState(() => ({
    forum: { current: null as HTMLElement | null },
    docs: { current: null as HTMLElement | null },
    leaderboard: { current: null as HTMLElement | null },
    shop: { current: null as HTMLElement | null }
  }))[0];
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
  const buildDiscoverShareUrl = useCallback(() => (
    buildPublicShareUrl(buildPublicDiscoverPath(route))
  ), [route]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildDiscoverShareUrl,
  });

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
          getPostList(null, t, 1, 8, 'newest'),
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
        const result = await getPublicWikiList({ pageIndex: 1, pageSize: 5 });
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

  const scrollToSection = useCallback((key: DiscoverRouteKey, behavior: ScrollBehavior) => {
    const element = sectionRefs[key].current;
    if (!element) {
      return;
    }

    const nextTop = window.scrollY + element.getBoundingClientRect().top - PUBLIC_SECTION_SCROLL_OFFSET;
    window.scrollTo({
      top: Math.max(nextTop, 0),
      behavior
    });
  }, [sectionRefs]);

  useEffect(() => {
    const section = route.section;
    if (!section) {
      return;
    }

    const behavior = routeScrollBehaviorRef.current;
    routeScrollBehaviorRef.current = 'auto';
    const frameId = window.requestAnimationFrame(() => {
      scrollToSection(section, behavior);
    });

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [route.section, scrollToSection]);

  const previewSection = useCallback((key: DiscoverRouteKey) => {
    if (route.section === key) {
      scrollToSection(key, 'smooth');
      return;
    }

    routeScrollBehaviorRef.current = 'smooth';
    onNavigate({ kind: 'home', section: key }, { replace: true });
  }, [onNavigate, route.section, scrollToSection]);

  const rememberSection = useCallback((key: DiscoverRouteKey) => {
    if (route.section === key) {
      return;
    }

    onNavigate({ kind: 'home', section: key }, { replace: true });
  }, [onNavigate, route.section]);

  const runFromSection = useCallback((key: DiscoverRouteKey, action: () => void) => {
    rememberSection(key);
    action();
  }, [rememberSection]);

  const handlePublicDiscoverLinkClick = useCallback((event: MouseEvent<HTMLAnchorElement>, action: () => void) => {
    if (!shouldHandlePublicDiscoverLink(event)) {
      return;
    }

    event.preventDefault();
    action();
  }, []);

  const forumListRoute = useMemo<PublicForumRoute>(() => (
    { kind: 'list', categoryId: null, sortBy: 'newest', page: 1 }
  ), []);
  const docsListRoute = useMemo<PublicDocsRoute>(() => ({ kind: 'list' }), []);
  const docsSearchRoute = useMemo<PublicDocsRoute>(() => ({ kind: 'search', keyword: '', page: 1 }), []);
  const leaderboardDefaultRoute = useMemo<PublicLeaderboardRoute>(() => createDefaultPublicLeaderboardRoute(), []);
  const shopHomeRoute = useMemo<PublicShopRoute>(() => ({ kind: 'home' }), []);
  const shopProductsRoute = useMemo<PublicShopRoute>(() => createDefaultPublicShopProductsRoute(), []);
  const forumListHref = buildPublicForumPath(forumListRoute);
  const docsListHref = buildPublicDocsPath(docsListRoute);
  const docsSearchHref = buildPublicDocsPath(docsSearchRoute);
  const leaderboardDefaultHref = buildPublicLeaderboardPath(leaderboardDefaultRoute);
  const shopHomeHref = buildPublicShopPath(shopHomeRoute);
  const shopProductsHref = buildPublicShopPath(shopProductsRoute);

  const summaryCards = useMemo<DiscoverSummaryCardDefinition[]>(() => ([
    {
      key: 'forum',
      icon: 'mdi:forum-outline',
      label: t('discover.public.summaryForumLabel'),
      value: loadingForum ? '...' : forumError ? '--' : String(forumPosts.length),
      state: loadingForum ? 'loading' : forumError ? 'error' : forumPosts.length === 0 ? 'empty' : 'ready',
      statusLabel: loadingForum
        ? t('discover.public.summaryStateLoading')
        : forumError
          ? t('discover.public.summaryStateError')
          : forumPosts.length === 0
            ? t('discover.public.summaryStateEmpty')
            : t('discover.public.summaryStateReady'),
      meaning: loadingForum
        ? t('discover.public.summaryForumMeaningLoading')
        : forumError
          ? t('discover.public.summaryForumMeaningError')
          : forumPosts.length === 0
            ? t('discover.public.summaryForumMeaningEmpty')
            : t('discover.public.summaryForumMeaning', { count: forumPosts.length }),
      destination: t('discover.public.summaryForumDestination'),
      href: forumListHref,
      onPreview: () => previewSection('forum'),
      onOpen: () => runFromSection('forum', () => onNavigateToForum(forumListRoute))
    },
    {
      key: 'docs',
      icon: 'mdi:file-document-outline',
      label: t('discover.public.summaryDocsLabel'),
      value: loadingDocs ? '...' : docsError ? '--' : String(docs.length),
      state: loadingDocs ? 'loading' : docsError ? 'error' : docs.length === 0 ? 'empty' : 'ready',
      statusLabel: loadingDocs
        ? t('discover.public.summaryStateLoading')
        : docsError
          ? t('discover.public.summaryStateError')
          : docs.length === 0
            ? t('discover.public.summaryStateEmpty')
            : t('discover.public.summaryStateReady'),
      meaning: loadingDocs
        ? t('discover.public.summaryDocsMeaningLoading')
        : docsError
          ? t('discover.public.summaryDocsMeaningError')
          : docs.length === 0
            ? t('discover.public.summaryDocsMeaningEmpty')
            : t('discover.public.summaryDocsMeaning', { count: docs.length }),
      destination: t('discover.public.summaryDocsDestination'),
      href: docsListHref,
      onPreview: () => previewSection('docs'),
      onOpen: () => runFromSection('docs', () => onNavigateToDocs(docsListRoute))
    },
    {
      key: 'leaderboard',
      icon: 'mdi:trophy-outline',
      label: t('discover.public.summaryLeaderboardLabel'),
      value: String(featuredLeaderboardConfigs.length),
      state: 'ready',
      statusLabel: t('discover.public.summaryStateReady'),
      meaning: t('discover.public.summaryLeaderboardMeaning', { count: featuredLeaderboardConfigs.length }),
      destination: t('discover.public.summaryLeaderboardDestination'),
      href: leaderboardDefaultHref,
      onPreview: () => previewSection('leaderboard'),
      onOpen: () => runFromSection('leaderboard', () => onNavigateToLeaderboard(leaderboardDefaultRoute))
    },
    {
      key: 'shop',
      icon: 'mdi:storefront-outline',
      label: t('discover.public.summaryShopLabel'),
      value: loadingShop ? '...' : shopError ? '--' : String(products.length),
      state: loadingShop ? 'loading' : shopError ? 'error' : products.length === 0 ? 'empty' : 'ready',
      statusLabel: loadingShop
        ? t('discover.public.summaryStateLoading')
        : shopError
          ? t('discover.public.summaryStateError')
          : products.length === 0
            ? t('discover.public.summaryStateEmpty')
            : t('discover.public.summaryStateReady'),
      meaning: loadingShop
        ? t('discover.public.summaryShopMeaningLoading')
        : shopError
          ? t('discover.public.summaryShopMeaningError')
          : products.length === 0
            ? t('discover.public.summaryShopMeaningEmpty')
            : t('discover.public.summaryShopMeaning', { count: products.length }),
      destination: t('discover.public.summaryShopDestination'),
      href: shopHomeHref,
      onPreview: () => previewSection('shop'),
      onOpen: () => runFromSection('shop', () => onNavigateToShop(shopHomeRoute))
    }
  ]), [
    docs.length,
    docsError,
    docsListHref,
    docsListRoute,
    forumError,
    forumListHref,
    forumListRoute,
    forumPosts.length,
    leaderboardDefaultHref,
    leaderboardDefaultRoute,
    loadingDocs,
    loadingForum,
    loadingShop,
    onNavigateToDocs,
    onNavigateToForum,
    onNavigateToLeaderboard,
    onNavigateToShop,
    previewSection,
    products.length,
    runFromSection,
    shopError,
    shopHomeHref,
    shopHomeRoute,
    t
  ]);

  const routeGuideCards = useMemo(() => (
    discoverRouteGuideDefinitions.map((item) => ({
      ...item,
      href: item.key === 'forum'
        ? forumListHref
        : item.key === 'docs'
          ? docsListHref
          : item.key === 'leaderboard'
            ? leaderboardDefaultHref
            : shopHomeHref,
      onOpen: () => {
        switch (item.key) {
          case 'forum':
            runFromSection('forum', () => onNavigateToForum(forumListRoute));
            return;
          case 'docs':
            runFromSection('docs', () => onNavigateToDocs(docsListRoute));
            return;
          case 'leaderboard':
            runFromSection('leaderboard', () => onNavigateToLeaderboard(leaderboardDefaultRoute));
            return;
          case 'shop':
            runFromSection('shop', () => onNavigateToShop(shopHomeRoute));
            return;
        }
      }
    }))
  ), [
    docsListHref,
    docsListRoute,
    forumListHref,
    forumListRoute,
    leaderboardDefaultHref,
    leaderboardDefaultRoute,
    onNavigateToDocs,
    onNavigateToForum,
    onNavigateToLeaderboard,
    onNavigateToShop,
    runFromSection,
    shopHomeHref,
    shopHomeRoute
  ]);

  const routeGuideMap = useMemo(() => ({
    forum: routeGuideCards.find((item) => item.key === 'forum') ?? routeGuideCards[0],
    docs: routeGuideCards.find((item) => item.key === 'docs') ?? routeGuideCards[0],
    leaderboard: routeGuideCards.find((item) => item.key === 'leaderboard') ?? routeGuideCards[0],
    shop: routeGuideCards.find((item) => item.key === 'shop') ?? routeGuideCards[0]
  }), [routeGuideCards]);

  return (
    <div className={styles.page}>
      <PublicShellHeader
        brandMark="览"
        brandName={t('discover.public.pageTitle')}
        brandSubline={t('discover.public.shellLabel')}
        onBrandClick={() => {
          onNavigate({ kind: 'home' }, { replace: true });
          window.scrollTo({ top: 0, behavior: 'smooth' });
        }}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
      />

      <main className={styles.main}>
        <section className={styles.heroCard}>
          <div className={styles.heroTitleRow}>
            <p className={styles.kicker}>P3-10</p>
            <span className={styles.readOnlyBadge}>{t('discover.public.readOnlyBadge')}</span>
          </div>
          <h1 className={styles.pageTitle}>{t('discover.public.pageTitle')}</h1>
          <p className={styles.pageIntro}>{t('discover.public.pageIntro')}</p>

          <div className={styles.heroActions}>
            <a
              className={styles.primaryButton}
              href={forumListHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('forum', () => onNavigateToForum(forumListRoute)))}
            >
              <Icon icon="mdi:forum-outline" size={18} />
              <span>{t('discover.public.openForum')}</span>
            </a>
            <a
              className={styles.secondaryButton}
              href={docsSearchHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('docs', () => onNavigateToDocs(docsSearchRoute)))}
            >
              <Icon icon="mdi:file-search-outline" size={18} />
              <span>{t('discover.public.searchDocs')}</span>
            </a>
            <a
              className={styles.secondaryButton}
              href={shopProductsHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('shop', () => onNavigateToShop(shopProductsRoute)))}
            >
              <Icon icon="mdi:store-search-outline" size={18} />
              <span>{t('discover.public.browseShop')}</span>
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

          <div className={styles.summaryGrid}>
            {summaryCards.map((item) => (
              <article
                key={item.key}
                className={`${styles.summaryCard} ${styles.summaryButton}`}
                data-state={item.state}
              >
                <button type="button" className={styles.summaryPreviewButton} onClick={item.onPreview}>
                  <div className={styles.summaryTop}>
                    <span className={styles.summaryIcon}>
                      <Icon icon={item.icon} size={18} />
                    </span>
                    <div className={styles.summaryHeading}>
                      <span className={styles.summaryLabel}>{item.label}</span>
                      <span className={styles.summaryStatus} data-state={item.state}>{item.statusLabel}</span>
                    </div>
                  </div>
                  <span className={styles.summaryValue}>{item.value}</span>
                  <p className={styles.summaryMeaning}>{item.meaning}</p>
                  <span className={styles.summaryDestination}>
                    <span className={styles.summaryDestinationLabel}>{t('discover.public.summaryDestinationLabel')}</span>
                    <span className={styles.summaryDestinationText}>
                      {item.destination}
                      <Icon icon="mdi:arrow-down" size={14} />
                    </span>
                  </span>
                </button>
                <div className={styles.summaryActionRow}>
                  <span className={styles.summaryActionHint}>
                    <Icon icon="mdi:arrow-down-circle-outline" size={16} />
                    <span>{t('discover.public.summaryPreviewAction')}</span>
                  </span>
                  <a
                    className={styles.summaryActionButton}
                    href={item.href}
                    onClick={(event) => handlePublicDiscoverLinkClick(event, item.onOpen)}
                  >
                    <Icon icon="mdi:arrow-right-circle-outline" size={16} />
                    <span>{t('discover.public.summaryOpenAction')}</span>
                  </a>
                </div>
              </article>
            ))}
          </div>
        </section>

        <PublicDiscoverFeed
          forumPosts={forumPosts}
          documents={docs}
          products={products}
          loadingForum={loadingForum}
          loadingDocs={loadingDocs}
          loadingShop={loadingShop}
          forumError={forumError}
          docsError={docsError}
          shopError={shopError}
          displayTimeZone={displayTimeZone}
          locale={i18n.language}
          onReload={() => setReloadToken((current) => current + 1)}
          onOpenPost={(post) => runFromSection('forum', () => onNavigateToForum({ kind: 'detail', postId: getForumPostRouteIdentifier(post) }))}
          onOpenTag={(tagSlug) => runFromSection('forum', () => onNavigateToForum({ kind: 'tag', tagSlug, sortBy: 'newest', page: 1 }))}
          onOpenQuestion={() => runFromSection('forum', () => onNavigateToForum({ kind: 'question', sortBy: 'newest', page: 1 }))}
          onOpenPoll={() => runFromSection('forum', () => onNavigateToForum({ kind: 'poll', sortBy: 'newest', page: 1 }))}
          onOpenLottery={() => runFromSection('forum', () => onNavigateToForum({ kind: 'lottery', sortBy: 'newest', page: 1 }))}
          onOpenDocument={(document) => runFromSection('docs', () => onNavigateToDocs({ kind: 'detail', slug: document.voSlug }))}
          onOpenProduct={(product) => runFromSection('shop', () => onNavigateToShop({ kind: 'detail', productId: String(product.voId) }))}
          onOpenForum={() => runFromSection('forum', () => onNavigateToForum(forumListRoute))}
          onOpenDocs={() => runFromSection('docs', () => onNavigateToDocs(docsListRoute))}
          onOpenShop={() => runFromSection('shop', () => onNavigateToShop(shopProductsRoute))}
        />

        <section className={`${styles.sectionCard} ${styles.guideSection}`}>
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
                <a
                  key={item.key}
                  className={styles.routeButton}
                  href={item.href}
                  onClick={(event) => handlePublicDiscoverLinkClick(event, item.onOpen)}
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
                </a>
              ))}
            </div>
          </section>

          <section className={styles.sectionCard} ref={(element) => { sectionRefs.forum.current = element; }}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.forumTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.forumDescription')}</p>
                <div className={styles.sectionCueRow}>
                  {forumSectionCueDefinitions.map((item) => (
                    <span key={item.key} className={styles.sectionCueChip}>{t(item.labelKey)}</span>
                  ))}
                </div>
              </div>
              <a
                className={styles.sectionLink}
                href={forumListHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('forum', () => onNavigateToForum(forumListRoute)))}
              >
                <span>{t('discover.public.viewAllForum')}</span>
                <Icon icon="mdi:arrow-right" size={16} />
              </a>
            </div>

            {loadingForum ? (
              <SectionStatusCard
                tone="loading"
                title={t('discover.public.forumLoadingTitle')}
                description={t('discover.public.forumLoadingDescription')}
                guideLabel={t(routeGuideMap.forum.valueLabelKey)}
                guideText={t(routeGuideMap.forum.valueTextKey)}
                destinationLabel={t(routeGuideMap.forum.destinationLabelKey)}
                destinationText={t(routeGuideMap.forum.destinationTextKey)}
                boundaryLabel={t(routeGuideMap.forum.boundaryLabelKey)}
                boundaryText={t(routeGuideMap.forum.boundaryTextKey)}
                primaryAction={{
                  label: t('discover.public.viewAllForum'),
                  href: forumListHref,
                  onClick: () => runFromSection('forum', () => onNavigateToForum(forumListRoute))
                }}
              />
            ) : forumError ? (
              <SectionStatusCard
                tone="error"
                title={t('discover.public.forumLoadFailedTitle')}
                description={forumError}
                guideLabel={t(routeGuideMap.forum.valueLabelKey)}
                guideText={t(routeGuideMap.forum.valueTextKey)}
                destinationLabel={t(routeGuideMap.forum.destinationLabelKey)}
                destinationText={t(routeGuideMap.forum.destinationTextKey)}
                boundaryLabel={t(routeGuideMap.forum.boundaryLabelKey)}
                boundaryText={t(routeGuideMap.forum.boundaryTextKey)}
                primaryAction={{
                  label: t('discover.public.viewAllForum'),
                  href: forumListHref,
                  onClick: () => runFromSection('forum', () => onNavigateToForum(forumListRoute))
                }}
                secondaryAction={{
                  label: t('common.retry'),
                  onClick: () => setReloadToken((current) => current + 1)
                }}
              />
            ) : forumPosts.length === 0 ? (
              <SectionStatusCard
                tone="empty"
                title={t('discover.public.forumEmptyTitle')}
                description={t('discover.public.forumEmptyDescription')}
                guideLabel={t(routeGuideMap.forum.valueLabelKey)}
                guideText={t(routeGuideMap.forum.valueTextKey)}
                destinationLabel={t(routeGuideMap.forum.destinationLabelKey)}
                destinationText={t(routeGuideMap.forum.destinationTextKey)}
                boundaryLabel={t(routeGuideMap.forum.boundaryLabelKey)}
                boundaryText={t(routeGuideMap.forum.boundaryTextKey)}
                primaryAction={{
                  label: t('discover.public.viewAllForum'),
                  href: forumListHref,
                  onClick: () => runFromSection('forum', () => onNavigateToForum(forumListRoute))
                }}
              />
            ) : (
              <>
                <div className={styles.forumList}>
                  {forumPosts.map((post) => (
                    <PostCard
                      key={post.voId}
                      post={post}
                      displayTimeZone={displayTimeZone}
                      onClick={() => runFromSection('forum', () => onNavigateToForum({ kind: 'detail', postId: getForumPostRouteIdentifier(post) }))}
                      href={buildPublicForumPath({ kind: 'detail', postId: getForumPostRouteIdentifier(post) })}
                      variant="publicCompact"
                      resolveAuthorProfileId={resolvePublicProfileUserId}
                      onTagClick={(_, tagSlug) => runFromSection('forum', () => onNavigateToForum({ kind: 'tag', tagSlug, sortBy: 'newest', page: 1 }))}
                      onQuestionClick={() => runFromSection('forum', () => onNavigateToForum({ kind: 'question', sortBy: 'newest', page: 1 }))}
                      onPollClick={() => runFromSection('forum', () => onNavigateToForum({ kind: 'poll', sortBy: 'newest', page: 1 }))}
                      onLotteryClick={() => runFromSection('forum', () => onNavigateToForum({ kind: 'lottery', sortBy: 'newest', page: 1 }))}
                    />
                  ))}
                </div>
                {hotTags.length > 0 && (
                  <div className={styles.tagRail}>
                    {hotTags.map((tag) => {
                      const tagRoute: PublicForumRoute = { kind: 'tag', tagSlug: tag.voSlug, sortBy: 'newest', page: 1 };

                      return (
                        <a
                          key={tag.voId}
                          className={styles.tagChip}
                          href={buildPublicForumPath(tagRoute)}
                          onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('forum', () => onNavigateToForum(tagRoute)))}
                        >
                          #{tag.voName}
                        </a>
                      );
                    })}
                  </div>
                )}
              </>
            )}
          </section>
        </div>

        <div className={styles.sectionGrid}>
          <section className={styles.sectionCard} ref={(element) => { sectionRefs.docs.current = element; }}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.docsTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.docsDescription')}</p>
                <div className={styles.sectionCueRow}>
                  {docsSectionCueDefinitions.map((item) => (
                    <span key={item.key} className={styles.sectionCueChip}>{t(item.labelKey)}</span>
                  ))}
                </div>
              </div>
              <a
                className={styles.sectionLink}
                href={docsListHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('docs', () => onNavigateToDocs(docsListRoute)))}
              >
                <span>{t('discover.public.viewAllDocs')}</span>
                <Icon icon="mdi:arrow-right" size={16} />
              </a>
            </div>

            {loadingDocs ? (
              <SectionStatusCard
                tone="loading"
                title={t('discover.public.docsLoadingTitle')}
                description={t('discover.public.docsLoadingDescription')}
                guideLabel={t(routeGuideMap.docs.valueLabelKey)}
                guideText={t(routeGuideMap.docs.valueTextKey)}
                destinationLabel={t(routeGuideMap.docs.destinationLabelKey)}
                destinationText={t(routeGuideMap.docs.destinationTextKey)}
                boundaryLabel={t(routeGuideMap.docs.boundaryLabelKey)}
                boundaryText={t(routeGuideMap.docs.boundaryTextKey)}
                primaryAction={{
                  label: t('discover.public.viewAllDocs'),
                  href: docsListHref,
                  onClick: () => runFromSection('docs', () => onNavigateToDocs(docsListRoute))
                }}
              />
            ) : docsError ? (
              <SectionStatusCard
                tone="error"
                title={t('discover.public.docsLoadFailedTitle')}
                description={docsError}
                guideLabel={t(routeGuideMap.docs.valueLabelKey)}
                guideText={t(routeGuideMap.docs.valueTextKey)}
                destinationLabel={t(routeGuideMap.docs.destinationLabelKey)}
                destinationText={t(routeGuideMap.docs.destinationTextKey)}
                boundaryLabel={t(routeGuideMap.docs.boundaryLabelKey)}
                boundaryText={t(routeGuideMap.docs.boundaryTextKey)}
                primaryAction={{
                  label: t('discover.public.viewAllDocs'),
                  href: docsListHref,
                  onClick: () => runFromSection('docs', () => onNavigateToDocs(docsListRoute))
                }}
                secondaryAction={{
                  label: t('common.retry'),
                  onClick: () => setReloadToken((current) => current + 1)
                }}
              />
            ) : docs.length === 0 ? (
              <SectionStatusCard
                tone="empty"
                title={t('discover.public.docsEmptyTitle')}
                description={t('discover.public.docsEmptyDescription')}
                guideLabel={t(routeGuideMap.docs.valueLabelKey)}
                guideText={t(routeGuideMap.docs.valueTextKey)}
                destinationLabel={t(routeGuideMap.docs.destinationLabelKey)}
                destinationText={t(routeGuideMap.docs.destinationTextKey)}
                boundaryLabel={t(routeGuideMap.docs.boundaryLabelKey)}
                boundaryText={t(routeGuideMap.docs.boundaryTextKey)}
                primaryAction={{
                  label: t('discover.public.viewAllDocs'),
                  href: docsListHref,
                  onClick: () => runFromSection('docs', () => onNavigateToDocs(docsListRoute))
                }}
              />
            ) : (
              <div className={styles.docsList}>
                {docs.map((document) => (
                  <article key={document.voId} className={styles.docsItem}>
                    <a
                      className={styles.docsButton}
                      href={buildPublicDocsPath({ kind: 'detail', slug: document.voSlug })}
                      onClick={(event) => handlePublicDiscoverLinkClick(event, () => (
                        runFromSection('docs', () => onNavigateToDocs({ kind: 'detail', slug: document.voSlug }))
                      ))}
                    >
                      <div className={styles.itemChipRow}>
                        <span className={styles.itemChip}>{t('discover.public.docsItemReadable')}</span>
                        <span className={styles.itemChip}>
                          {isRecentDocument(document)
                            ? t('discover.public.docsItemRecent')
                            : t('discover.public.docsItemSearchable')}
                        </span>
                      </div>
                      <h3 className={styles.docsTitle}>{document.voTitle}</h3>
                      <p className={styles.docsSummary}>
                        {buildDocumentSummary(document, i18n.language, t)}
                      </p>
                      <span className={styles.docsMeta}>
                        <Icon icon="mdi:calendar-blank-outline" size={16} />
                        <span>{formatDocumentMeta(document, i18n.language, t('discover.public.documentMetaFallback'))}</span>
                      </span>
                    </a>
                  </article>
                ))}
              </div>
            )}
          </section>

          <section className={styles.sectionCard} ref={(element) => { sectionRefs.leaderboard.current = element; }}>
            <div className={styles.sectionHeader}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>{t('discover.public.leaderboardTitle')}</h2>
                <p className={styles.sectionDescription}>{t('discover.public.leaderboardDescription')}</p>
                <div className={styles.sectionCueRow}>
                  {leaderboardSectionCueDefinitions.map((item) => (
                    <span key={item.key} className={styles.sectionCueChip}>{t(item.labelKey)}</span>
                  ))}
                </div>
              </div>
              <a
                className={styles.sectionLink}
                href={leaderboardDefaultHref}
                onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('leaderboard', () => onNavigateToLeaderboard(leaderboardDefaultRoute)))}
              >
                <span>{t('discover.public.viewAllLeaderboard')}</span>
                <Icon icon="mdi:arrow-right" size={16} />
              </a>
            </div>

            <div className={styles.leaderboardList}>
              {featuredLeaderboardConfigs.map((item) => {
                const leaderboardRoute: PublicLeaderboardRoute = {
                  ...createDefaultPublicLeaderboardRoute(),
                  typeSlug: item.key
                };
                const leaderboardHref = buildPublicLeaderboardPath(leaderboardRoute);

                return (
                  <article key={item.key} className={styles.leaderboardItem}>
                    <div className={styles.itemChipRow}>
                      {item.cueLabelKeys.map((labelKey) => (
                        <span key={labelKey} className={styles.itemChip}>{t(labelKey)}</span>
                      ))}
                    </div>
                    <div className={styles.leaderboardTop}>
                      <span className={styles.leaderboardIcon}>
                        <Icon icon={item.icon} size={20} />
                      </span>
                      <h3 className={styles.leaderboardTitle}>{t(item.nameKey)}</h3>
                    </div>
                    <p className={styles.leaderboardSummary}>{t(item.descriptionKey)}</p>
                    <div className={styles.leaderboardMetaList}>
                      <div className={styles.leaderboardMetaRow}>
                        <span className={styles.leaderboardMetaLabel}>{t(item.focusLabelKey)}</span>
                        <span className={styles.leaderboardMetaText}>{t(item.focusTextKey)}</span>
                      </div>
                      <div className={styles.leaderboardMetaRow}>
                        <span className={styles.leaderboardMetaLabel}>{t(item.boundaryLabelKey)}</span>
                        <span className={styles.leaderboardMetaText}>{t(item.boundaryTextKey)}</span>
                      </div>
                    </div>
                    <a
                      className={`${styles.secondaryButton} ${styles.leaderboardButton}`}
                      href={leaderboardHref}
                      onClick={(event) => handlePublicDiscoverLinkClick(event, () => (
                        runFromSection('leaderboard', () => onNavigateToLeaderboard(leaderboardRoute))
                      ))}
                    >
                      {t('discover.public.openLeaderboardType')}
                    </a>
                  </article>
                );
              })}
            </div>
          </section>
        </div>

        <section className={styles.sectionCard} ref={(element) => { sectionRefs.shop.current = element; }}>
          <div className={styles.sectionHeader}>
            <div className={styles.sectionHeading}>
              <h2 className={styles.sectionTitle}>{t('discover.public.shopTitle')}</h2>
              <p className={styles.sectionDescription}>{t('discover.public.shopDescription')}</p>
              <div className={styles.sectionCueRow}>
                {shopSectionCueDefinitions.map((item) => (
                  <span key={item.key} className={styles.sectionCueChip}>{t(item.labelKey)}</span>
                ))}
              </div>
            </div>
            <a
              className={styles.sectionLink}
              href={shopProductsHref}
              onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('shop', () => onNavigateToShop(shopProductsRoute)))}
            >
              <span>{t('discover.public.viewAllShop')}</span>
              <Icon icon="mdi:arrow-right" size={16} />
            </a>
          </div>

          {loadingShop ? (
            <SectionStatusCard
              tone="loading"
              title={t('discover.public.shopLoadingTitle')}
              description={t('discover.public.shopLoadingDescription')}
              guideLabel={t(routeGuideMap.shop.valueLabelKey)}
              guideText={t(routeGuideMap.shop.valueTextKey)}
              destinationLabel={t(routeGuideMap.shop.destinationLabelKey)}
              destinationText={t(routeGuideMap.shop.destinationTextKey)}
              boundaryLabel={t(routeGuideMap.shop.boundaryLabelKey)}
              boundaryText={t(routeGuideMap.shop.boundaryTextKey)}
              primaryAction={{
                label: t('discover.public.viewAllShop'),
                href: shopProductsHref,
                onClick: () => runFromSection('shop', () => onNavigateToShop(shopProductsRoute))
              }}
            />
          ) : shopError ? (
            <SectionStatusCard
              tone="error"
              title={t('discover.public.shopLoadFailedTitle')}
              description={shopError}
              guideLabel={t(routeGuideMap.shop.valueLabelKey)}
              guideText={t(routeGuideMap.shop.valueTextKey)}
              destinationLabel={t(routeGuideMap.shop.destinationLabelKey)}
              destinationText={t(routeGuideMap.shop.destinationTextKey)}
              boundaryLabel={t(routeGuideMap.shop.boundaryLabelKey)}
              boundaryText={t(routeGuideMap.shop.boundaryTextKey)}
              primaryAction={{
                label: t('discover.public.viewAllShop'),
                href: shopProductsHref,
                onClick: () => runFromSection('shop', () => onNavigateToShop(shopProductsRoute))
              }}
              secondaryAction={{
                label: t('common.retry'),
                onClick: () => setReloadToken((current) => current + 1)
              }}
            />
          ) : products.length === 0 ? (
            <SectionStatusCard
              tone="empty"
              title={t('discover.public.shopEmptyTitle')}
              description={t('discover.public.shopEmptyDescription')}
              guideLabel={t(routeGuideMap.shop.valueLabelKey)}
              guideText={t(routeGuideMap.shop.valueTextKey)}
              destinationLabel={t(routeGuideMap.shop.destinationLabelKey)}
              destinationText={t(routeGuideMap.shop.destinationTextKey)}
              boundaryLabel={t(routeGuideMap.shop.boundaryLabelKey)}
              boundaryText={t(routeGuideMap.shop.boundaryTextKey)}
              primaryAction={{
                label: t('discover.public.viewAllShop'),
                href: shopProductsHref,
                onClick: () => runFromSection('shop', () => onNavigateToShop(shopProductsRoute))
              }}
            />
          ) : (
            <div className={styles.productList}>
              {products.map((product) => {
                const imageUrl = resolveMediaUrl(product.voCoverImage || product.voIcon);
                const productRoute: PublicShopRoute = { kind: 'detail', productId: String(product.voId) };
                const productHref = buildPublicShopPath(productRoute);
                return (
                  <article key={product.voId} className={styles.productItem}>
                    <a
                      className={styles.productButton}
                      href={productHref}
                      onClick={(event) => handlePublicDiscoverLinkClick(event, () => runFromSection('shop', () => onNavigateToShop(productRoute)))}
                    >
                      <div className={styles.productCardHead}>
                        {imageUrl ? (
                          <img className={styles.productImage} src={imageUrl} alt={product.voName} />
                        ) : (
                          <span className={styles.productImageFallback}>
                            <Icon icon="mdi:gift-outline" size={24} />
                          </span>
                        )}
                        <div className={styles.productContent}>
                          <div className={styles.itemChipRow}>
                            <span className={styles.itemChip}>{getProductTypeDisplay(product.voProductType)}</span>
                            {product.voDurationDisplay?.trim() ? (
                              <span className={styles.itemChip}>{product.voDurationDisplay.trim()}</span>
                            ) : product.voHasDiscount ? (
                              <span className={`${styles.itemChip} ${styles.itemChipAccent}`}>
                                {t('discover.public.shopItemDiscount')}
                              </span>
                            ) : null}
                            {(product.voSoldCount ?? 0) > 0 && (
                              <span className={styles.itemChip}>
                                {t('shop.soldCount', { count: product.voSoldCount ?? 0 })}
                              </span>
                            )}
                          </div>
                          <h3 className={styles.productTitle}>{product.voName}</h3>
                          <p className={styles.productSummary}>{buildProductSummary(product, t)}</p>
                        </div>
                      </div>
                      <span className={styles.productPrice}>
                        <Icon icon="mdi:carrot" size={16} />
                        <span>{product.voPrice.toLocaleString()} {t('shop.currency.carrot')}</span>
                      </span>
                    </a>
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
