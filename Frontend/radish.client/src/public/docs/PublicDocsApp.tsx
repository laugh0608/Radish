import { useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { Icon } from '@radish/ui/icon';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import type {
  WikiDocumentDetailVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
} from '@/apps/wiki/types/wiki';
import { WikiDocumentStatus, WikiDocumentVisibility } from '@/apps/wiki/types/wiki';
import {
  buildPublicDocsPath,
  createDefaultDocsListRoute,
  createDefaultDocsSearchRoute,
  rewritePublicDocsHref,
  resolvePublicDocsRouteFromHref,
  type PublicDocsBrowseRoute,
  type PublicDocsRoute,
  type PublicDocsSearchRoute,
} from '../docsRouteState';
import { getPublicWikiDocumentBySlug, getPublicWikiList, getPublicWikiTree } from './publicDocsApi';
import styles from './PublicDocsApp.module.css';

const PUBLIC_DOCS_SEARCH_PAGE_SIZE = 10;

interface PublicDocsAppProps {
  route: PublicDocsRoute;
  fallbackBrowseRoute: PublicDocsBrowseRoute;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
}

interface PublicDocsTreeRow {
  id: number;
  slug: string;
  title: string;
  depth: number;
  childCount: number;
}

interface PublicDocsCollectionState {
  tree: WikiDocumentTreeNodeVo[];
  documents: WikiDocumentVo[];
  totalDocuments: number;
  loadingTree: boolean;
  loadingDocuments: boolean;
  treeError: string | null;
  listError: string | null;
}

interface PublicDocsSearchState {
  documents: WikiDocumentVo[];
  totalDocuments: number;
  totalPages: number;
  loading: boolean;
  error: string | null;
}

interface PublicStatusCardProps {
  tone: 'loading' | 'empty' | 'error' | 'notFound';
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

function flattenPublicDocsTree(nodes: WikiDocumentTreeNodeVo[], depth: number = 0): PublicDocsTreeRow[] {
  return nodes.flatMap((node) => {
    const children = node.voChildren || [];
    return [
      {
        id: node.voId,
        slug: node.voSlug,
        title: node.voTitle,
        depth,
        childCount: children.length
      },
      ...flattenPublicDocsTree(children, depth + 1)
    ];
  });
}

function isPublicDocumentNotFound(message: string | null | undefined): boolean {
  if (!message) {
    return false;
  }

  return /文档不存在|无权访问|not\s+found|404/i.test(message);
}

function toVisibilityText(t: (key: string, options?: Record<string, unknown>) => string, visibility?: number): string {
  switch (visibility) {
    case WikiDocumentVisibility.Public:
      return t('wiki.visibility.public');
    case WikiDocumentVisibility.Restricted:
      return t('wiki.visibility.restricted');
    default:
      return t('wiki.visibility.authenticated');
  }
}

function toStatusText(t: (key: string, options?: Record<string, unknown>) => string, status?: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return t('wiki.status.published');
    case WikiDocumentStatus.Archived:
      return t('wiki.status.archived');
    default:
      return t('wiki.status.draft');
  }
}

function buildBrowseRouteKey(route: PublicDocsBrowseRoute): string {
  return buildPublicDocsPath(route);
}

function getCurrentOrigin(): string {
  if (typeof window !== 'undefined' && window.location.origin) {
    return window.location.origin;
  }

  return 'https://localhost:5000';
}

function getDocumentScrollElement(): HTMLElement | null {
  if (typeof document === 'undefined') {
    return null;
  }

  if (document.scrollingElement instanceof HTMLElement) {
    return document.scrollingElement;
  }

  return document.documentElement instanceof HTMLElement ? document.documentElement : null;
}

function readPublicDocsScrollTop(container: HTMLDivElement | null): number {
  const containerTop = container?.scrollTop ?? 0;
  const windowTop = typeof window !== 'undefined' ? window.scrollY : 0;
  const documentTop = getDocumentScrollElement()?.scrollTop ?? 0;
  return Math.max(containerTop, windowTop, documentTop, 0);
}

function getPublicDocsMaxScrollTop(container: HTMLDivElement | null): number {
  const containerMax = container
    ? Math.max(container.scrollHeight - container.clientHeight, 0)
    : 0;
  const documentElement = getDocumentScrollElement();
  const documentMax = typeof window !== 'undefined' && documentElement
    ? Math.max(documentElement.scrollHeight - window.innerHeight, 0)
    : 0;
  return Math.max(containerMax, documentMax, 0);
}

function writePublicDocsScrollTop(container: HTMLDivElement | null, top: number): void {
  const nextTop = Math.max(top, 0);

  container?.scrollTo({ top: nextTop, behavior: 'auto' });

  const documentElement = getDocumentScrollElement();
  if (documentElement) {
    documentElement.scrollTop = nextTop;
  }

  if (typeof window !== 'undefined') {
    window.scrollTo({ top: nextTop, behavior: 'auto' });
  }
}

function usePublicDocsScrollRestore({
  isReady,
  restoreScrollTop,
  scrollContainerRef,
  onScrollRestored
}: {
  isReady: boolean;
  restoreScrollTop: number | null;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  onScrollRestored: () => void;
}) {
  useEffect(() => {
    if (restoreScrollTop == null || !isReady) {
      return;
    }

    let frameId = 0;
    let attempt = 0;
    const maxAttempts = 12;
    const targetScrollTop = restoreScrollTop;

    const restoreScroll = () => {
      const container = scrollContainerRef.current;
      if (!container) {
        onScrollRestored();
        return;
      }

      const maxScrollTop = getPublicDocsMaxScrollTop(container);
      const nextScrollTop = Math.min(targetScrollTop, maxScrollTop);
      writePublicDocsScrollTop(container, nextScrollTop);

      const restored = Math.abs(readPublicDocsScrollTop(container) - nextScrollTop) <= 2;
      const needsMoreLayout = maxScrollTop + 2 < targetScrollTop;

      if ((!needsMoreLayout && restored) || attempt >= maxAttempts) {
        onScrollRestored();
        return;
      }

      attempt += 1;
      frameId = window.requestAnimationFrame(restoreScroll);
    };

    frameId = window.requestAnimationFrame(restoreScroll);

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [isReady, onScrollRestored, restoreScrollTop, scrollContainerRef]);
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
      ? 'mdi:file-document-outline'
      : tone === 'notFound'
        ? 'mdi:file-search-outline'
        : 'mdi:alert-circle-outline';

  return (
    <div className={`${styles.statusCard} ${compact ? styles.statusCardCompact : ''}`} data-tone={tone}>
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={compact ? 18 : 22} />
      </div>
      <div className={styles.statusBody}>
        <h2 className={styles.statusTitle}>{title}</h2>
        <p className={styles.statusDescription}>{description}</p>
        {(primaryAction || secondaryAction) && (
          <div className={styles.statusActions}>
            {primaryAction && (
              <button type="button" className={styles.primaryButton} onClick={primaryAction.onClick}>
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

export const PublicDocsApp = ({ route, fallbackBrowseRoute, onNavigate }: PublicDocsAppProps) => {
  const { t } = useTranslation();
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));
  const pageRef = useRef<HTMLDivElement>(null);
  const previousRouteRef = useRef<PublicDocsRoute>(route);
  const browseScrollSnapshotRef = useRef<{ routeKey: string; scrollTop: number } | null>(null);
  const [pendingRestoreScrollTop, setPendingRestoreScrollTop] = useState<number | null>(null);
  const [collectionReloadToken, setCollectionReloadToken] = useState(0);
  const [collectionState, setCollectionState] = useState<PublicDocsCollectionState>({
    tree: [],
    documents: [],
    totalDocuments: 0,
    loadingTree: true,
    loadingDocuments: true,
    treeError: null,
    listError: null
  });

  const captureBrowseScrollSnapshot = (browseRoute: PublicDocsBrowseRoute) => {
    browseScrollSnapshotRef.current = {
      routeKey: buildBrowseRouteKey(browseRoute),
      scrollTop: readPublicDocsScrollTop(pageRef.current)
    };
    setPendingRestoreScrollTop(null);
  };

  useEffect(() => {
    const nextTitle = route.kind === 'list'
      ? `${t('desktop.apps.document.name')} · ${t('wiki.public.pageTitle')}`
      : route.kind === 'search'
        ? `${t('desktop.apps.document.name')} · ${route.keyword ? t('wiki.public.searchResultTitle', { keyword: route.keyword }) : t('wiki.public.searchTitle')}`
        : `${t('desktop.apps.document.name')} · ${t('wiki.public.detailTitle')}`;

    document.title = nextTitle;
  }, [route, t]);

  useEffect(() => {
    const page = pageRef.current;
    const previousRoute = previousRouteRef.current;

    if (!page) {
      previousRouteRef.current = route;
      return;
    }

    if (previousRoute.kind !== 'detail' && route.kind === 'detail') {
      const previousRouteKey = buildBrowseRouteKey(previousRoute);
      if (!browseScrollSnapshotRef.current || browseScrollSnapshotRef.current.routeKey !== previousRouteKey) {
        captureBrowseScrollSnapshot(previousRoute);
      } else {
        setPendingRestoreScrollTop(null);
      }
      writePublicDocsScrollTop(page, 0);
    } else if (route.kind !== 'detail') {
      const nextRouteKey = buildBrowseRouteKey(route);
      if (previousRoute.kind === 'detail' && browseScrollSnapshotRef.current?.routeKey === nextRouteKey) {
        setPendingRestoreScrollTop(browseScrollSnapshotRef.current.scrollTop);
      } else {
        setPendingRestoreScrollTop(null);
        writePublicDocsScrollTop(page, 0);
      }
    } else {
      setPendingRestoreScrollTop(null);
      writePublicDocsScrollTop(page, 0);
    }

    previousRouteRef.current = route;
  }, [route]);

  useEffect(() => {
    let cancelled = false;

    const loadCollections = async () => {
      setCollectionState((current) => ({
        ...current,
        loadingTree: true,
        loadingDocuments: true,
        treeError: null,
        listError: null
      }));

      const [treeResult, listResult] = await Promise.allSettled([
        getPublicWikiTree(),
        getPublicWikiList({
          pageIndex: 1,
          pageSize: 100
        })
      ]);

      if (cancelled) {
        return;
      }

      setCollectionState((current) => ({
        ...current,
        tree: treeResult.status === 'fulfilled' ? treeResult.value : [],
        documents: listResult.status === 'fulfilled' ? (listResult.value.data || []) : [],
        totalDocuments: listResult.status === 'fulfilled'
          ? (listResult.value.dataCount || listResult.value.data?.length || 0)
          : 0,
        treeError: treeResult.status === 'fulfilled'
          ? null
          : treeResult.reason instanceof Error
            ? treeResult.reason.message
            : String(treeResult.reason),
        listError: listResult.status === 'fulfilled'
          ? null
          : listResult.reason instanceof Error
            ? listResult.reason.message
            : String(listResult.reason),
        loadingTree: false,
        loadingDocuments: false
      }));
    };

    void loadCollections();
    return () => {
      cancelled = true;
    };
  }, [collectionReloadToken]);

  const backLabel = fallbackBrowseRoute.kind === 'search'
    ? t('wiki.public.backToSearch')
    : t('wiki.public.backToList');

  return (
    <div className={styles.page} ref={pageRef}>
      <header className={styles.hero}>
        <div className={styles.heroInner}>
          <button
            type="button"
            className={styles.brand}
            onClick={() => onNavigate(createDefaultDocsListRoute())}
          >
            <span className={styles.brandMark}>文</span>
            <span className={styles.brandText}>
              <span className={styles.brandName}>{t('desktop.apps.document.name')}</span>
              <span className={styles.brandSubline}>{t('wiki.public.shellLabel')}</span>
            </span>
          </button>
          <a className={styles.desktopLink} href="/">
            <Icon icon="mdi:view-dashboard-outline" size={18} />
            <span>WebOS</span>
          </a>
        </div>
      </header>

      <main className={styles.main}>
        {route.kind === 'detail' ? (
          <PublicDocsDetail
            key={`docs-${route.slug}-${route.anchor ?? 'root'}`}
            route={route}
            displayTimeZone={displayTimeZone}
            backLabel={backLabel}
            onBack={() => onNavigate(fallbackBrowseRoute)}
            onNavigate={onNavigate}
          />
        ) : route.kind === 'search' ? (
          <PublicDocsSearch
            route={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onNavigate={onNavigate}
            onBrowseDirectory={() => onNavigate(createDefaultDocsListRoute())}
            onOpenDocument={(slug) => {
              captureBrowseScrollSnapshot(route);
              onNavigate({ kind: 'detail', slug });
            }}
          />
        ) : (
          <PublicDocsList
            displayTimeZone={displayTimeZone}
            collectionState={collectionState}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onReload={() => setCollectionReloadToken((current) => current + 1)}
            onOpenSearch={() => onNavigate(createDefaultDocsSearchRoute())}
            onOpenDocument={(slug) => {
              captureBrowseScrollSnapshot(route);
              onNavigate({ kind: 'detail', slug });
            }}
          />
        )}
      </main>
    </div>
  );
};

interface PublicDocsListProps {
  displayTimeZone: string;
  collectionState: PublicDocsCollectionState;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  onScrollRestored: () => void;
  onReload: () => void;
  onOpenSearch: () => void;
  onOpenDocument: (slug: string) => void;
}

const PublicDocsList = ({
  displayTimeZone,
  collectionState,
  scrollContainerRef,
  restoreScrollTop,
  onScrollRestored,
  onReload,
  onOpenSearch,
  onOpenDocument
}: PublicDocsListProps) => {
  const { t } = useTranslation();
  const {
    tree,
    documents,
    totalDocuments,
    loadingTree,
    loadingDocuments,
    treeError,
    listError
  } = collectionState;

  usePublicDocsScrollRestore({
    isReady: !loadingTree && !loadingDocuments,
    restoreScrollTop,
    scrollContainerRef,
    onScrollRestored
  });

  const treeRows = useMemo(() => flattenPublicDocsTree(tree), [tree]);
  const listCards = useMemo(() => documents.slice(0, 12), [documents]);
  const hasAnyContent = treeRows.length > 0 || listCards.length > 0;
  const isLoading = (loadingTree || loadingDocuments) && !hasAnyContent;
  const isError = !hasAnyContent && Boolean(treeError || listError);
  const isEmpty = !loadingTree && !loadingDocuments && !hasAnyContent && !treeError && !listError;

  return (
    <section className={styles.sectionCard}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>Phase 2-2</p>
          <h1 className={styles.pageTitle}>{t('wiki.public.pageTitle')}</h1>
          <p className={styles.pageIntro}>{t('wiki.public.pageIntro')}</p>
        </div>
        <div className={styles.sectionActions}>
          <button type="button" className={styles.secondaryButton} onClick={onOpenSearch}>
            <Icon icon="mdi:magnify" size={18} />
            <span>{t('wiki.public.searchAction')}</span>
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className={styles.contentWrap}>
          <PublicStatusCard
            tone="loading"
            title={t('wiki.public.loadingTitle')}
            description={t('wiki.public.loadingDescription')}
          />
        </div>
      ) : isError ? (
        <div className={styles.contentWrap}>
          <PublicStatusCard
            tone="error"
            title={t('wiki.public.listErrorTitle')}
            description={treeError || listError || t('wiki.public.loadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: onReload
            }}
          />
        </div>
      ) : isEmpty ? (
        <div className={styles.contentWrap}>
          <PublicStatusCard
            tone="empty"
            title={t('wiki.public.emptyTitle')}
            description={t('wiki.public.emptyDescription')}
          />
        </div>
      ) : (
        <div className={styles.contentWrap}>
          {(treeError || listError) && (
            <div className={styles.inlineNotice}>
              <span className={styles.inlineNoticeText}>
                {treeError && listError
                  ? t('wiki.public.partialBothFailed')
                  : treeError
                    ? t('wiki.public.partialTreeFailed')
                    : t('wiki.public.partialListFailed')}
              </span>
              <button type="button" className={styles.inlineTextButton} onClick={onReload}>
                {t('common.retry')}
              </button>
            </div>
          )}

          <div className={styles.listLayout}>
            <section className={styles.panel}>
              <div className={styles.panelHeader}>
                <div>
                  <h2 className={styles.panelTitle}>{t('wiki.public.directoryTitle')}</h2>
                  <p className={styles.panelHint}>{t('wiki.public.directoryHint')}</p>
                </div>
                <span className={styles.panelStat}>{t('wiki.public.directoryCount', { count: treeRows.length })}</span>
              </div>

              {treeRows.length === 0 ? (
                <PublicStatusCard
                  tone="empty"
                  compact={true}
                  title={t('wiki.public.directoryEmptyTitle')}
                  description={t('wiki.public.directoryEmptyDescription')}
                />
              ) : (
                <div className={styles.directoryList}>
                  {treeRows.map((row) => (
                    <button
                      key={row.id}
                      type="button"
                      className={styles.directoryItem}
                      onClick={() => onOpenDocument(row.slug)}
                    >
                      <span className={styles.directoryPrefix} style={{ marginLeft: `${row.depth * 14}px` }}>
                        {row.depth > 0 ? '└' : '•'}
                      </span>
                      <span className={styles.directoryTitle}>{row.title}</span>
                      {row.childCount > 0 && (
                        <span className={styles.directoryMeta}>{t('wiki.public.childCount', { count: row.childCount })}</span>
                      )}
                    </button>
                  ))}
                </div>
              )}
            </section>

            <section className={styles.panel}>
              <div className={styles.panelHeader}>
                <div>
                  <h2 className={styles.panelTitle}>{t('wiki.public.latestTitle')}</h2>
                  <p className={styles.panelHint}>{t('wiki.public.latestHint')}</p>
                </div>
                <span className={styles.panelStat}>{t('wiki.public.documentCount', { count: totalDocuments })}</span>
              </div>

              {listCards.length === 0 ? (
                <PublicStatusCard
                  tone="empty"
                  compact={true}
                  title={t('wiki.public.cardsEmptyTitle')}
                  description={t('wiki.public.cardsEmptyDescription')}
                />
              ) : (
                <div className={styles.cardList}>
                  {listCards.map((document) => (
                    <button
                      key={document.voId}
                      type="button"
                      className={styles.docCard}
                      onClick={() => onOpenDocument(document.voSlug)}
                    >
                      <div className={styles.docCardMeta}>
                        <span className={styles.metaChip}>{toVisibilityText(t, document.voVisibility)}</span>
                        <span className={styles.metaChip}>{toStatusText(t, document.voStatus)}</span>
                      </div>
                      <h3 className={styles.docCardTitle}>{document.voTitle}</h3>
                      <p className={styles.docCardSummary}>
                        {document.voSummary?.trim() || t('wiki.public.summaryFallback')}
                      </p>
                      <div className={styles.docCardFooter}>
                        <span>{formatDateTimeByTimeZone(document.voModifyTime || document.voCreateTime, displayTimeZone)}</span>
                        <span className={styles.docCardAction}>{t('wiki.public.openDocument')}</span>
                      </div>
                    </button>
                  ))}
                </div>
              )}
            </section>
          </div>
        </div>
      )}
    </section>
  );
};

interface PublicDocsSearchProps {
  route: PublicDocsSearchRoute;
  displayTimeZone: string;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  onScrollRestored: () => void;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
  onBrowseDirectory: () => void;
  onOpenDocument: (slug: string) => void;
}

const PublicDocsSearch = ({
  route,
  displayTimeZone,
  scrollContainerRef,
  restoreScrollTop,
  onScrollRestored,
  onNavigate,
  onBrowseDirectory,
  onOpenDocument
}: PublicDocsSearchProps) => {
  const { t } = useTranslation();
  const [draftKeyword, setDraftKeyword] = useState(route.keyword);
  const [reloadToken, setReloadToken] = useState(0);
  const [searchState, setSearchState] = useState<PublicDocsSearchState>({
    documents: [],
    totalDocuments: 0,
    totalPages: 1,
    loading: false,
    error: null
  });

  useEffect(() => {
    setDraftKeyword(route.keyword);
  }, [route.keyword]);

  useEffect(() => {
    const appliedKeyword = route.keyword.trim();
    if (!appliedKeyword) {
      setSearchState({
        documents: [],
        totalDocuments: 0,
        totalPages: 1,
        loading: false,
        error: null
      });
      return;
    }

    let cancelled = false;

    const loadSearchResults = async () => {
      setSearchState((current) => ({
        ...current,
        loading: true,
        error: null
      }));

      try {
        const result = await getPublicWikiList({
          keyword: appliedKeyword,
          pageIndex: route.page,
          pageSize: PUBLIC_DOCS_SEARCH_PAGE_SIZE
        });

        if (cancelled) {
          return;
        }

        setSearchState({
          documents: result.data || [],
          totalDocuments: result.dataCount || 0,
          totalPages: Math.max(result.pageCount || 1, 1),
          loading: false,
          error: null
        });
      } catch (err) {
        if (cancelled) {
          return;
        }

        setSearchState({
          documents: [],
          totalDocuments: 0,
          totalPages: 1,
          loading: false,
          error: err instanceof Error ? err.message : String(err)
        });
      }
    };

    void loadSearchResults();
    return () => {
      cancelled = true;
    };
  }, [reloadToken, route.keyword, route.page]);

  usePublicDocsScrollRestore({
    isReady: !searchState.loading,
    restoreScrollTop,
    scrollContainerRef,
    onScrollRestored
  });

  const appliedKeyword = route.keyword.trim();
  const hasKeyword = Boolean(appliedKeyword);
  const hasResults = searchState.documents.length > 0;
  const isLoading = hasKeyword && searchState.loading && !hasResults;
  const isError = hasKeyword && !searchState.loading && !hasResults && Boolean(searchState.error);
  const isEmpty = hasKeyword && !searchState.loading && !hasResults && !searchState.error;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onNavigate({
      kind: 'search',
      keyword: draftKeyword.trim(),
      page: 1
    });
  };

  const handleReset = () => {
    setDraftKeyword('');
    onNavigate(createDefaultDocsSearchRoute());
  };

  const handleChangePage = (page: number) => {
    if (!hasKeyword || page === route.page || page < 1 || page > searchState.totalPages) {
      return;
    }

    onNavigate({
      ...route,
      page
    });
  };

  return (
    <section className={styles.sectionCard}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>Phase 2-2</p>
          <h1 className={styles.pageTitle}>
            {hasKeyword ? t('wiki.public.searchResultTitle', { keyword: appliedKeyword }) : t('wiki.public.searchTitle')}
          </h1>
          <p className={styles.pageIntro}>
            {hasKeyword ? t('wiki.public.searchResultIntro') : t('wiki.public.searchIntro')}
          </p>
        </div>
        <div className={styles.sectionActions}>
          <button type="button" className={styles.secondaryButton} onClick={onBrowseDirectory}>
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{t('wiki.public.backToList')}</span>
          </button>
        </div>
      </div>

      <div className={styles.contentWrap}>
        <section className={styles.searchPanel}>
          <form className={styles.searchForm} onSubmit={handleSubmit}>
            <div className={styles.searchInputWrap}>
              <Icon icon="mdi:magnify" size={18} />
              <input
                type="search"
                value={draftKeyword}
                onChange={(event) => setDraftKeyword(event.target.value)}
                className={styles.searchInput}
                placeholder={t('wiki.public.searchPlaceholder')}
              />
            </div>
            <button type="submit" className={styles.primaryButton}>
              {t('wiki.public.searchSubmit')}
            </button>
            <button type="button" className={styles.secondaryButton} onClick={handleReset}>
              {t('wiki.public.searchReset')}
            </button>
          </form>

          <div className={styles.searchSummaryRail}>
            <span className={styles.metaChip}>
              {hasKeyword
                ? t('wiki.public.searchKeywordSummary', { keyword: appliedKeyword })
                : t('wiki.public.searchIdleSummary')}
            </span>
            {hasKeyword && (
              <span className={styles.metaChip}>{t('wiki.public.searchResultCount', { count: searchState.totalDocuments })}</span>
            )}
            {hasKeyword && searchState.totalPages > 1 && (
              <span className={styles.metaChip}>
                {t('common.pageInfo', { current: route.page, total: searchState.totalPages })}
              </span>
            )}
          </div>
        </section>

        {!hasKeyword ? (
          <PublicStatusCard
            tone="empty"
            title={t('wiki.public.searchIdleTitle')}
            description={t('wiki.public.searchIdleDescription')}
            secondaryAction={{
              label: t('wiki.public.backToList'),
              onClick: onBrowseDirectory
            }}
          />
        ) : isLoading ? (
          <PublicStatusCard
            tone="loading"
            title={t('wiki.public.searchLoadingTitle')}
            description={t('wiki.public.searchLoadingDescription')}
          />
        ) : isError ? (
          <PublicStatusCard
            tone="error"
            title={t('wiki.public.searchErrorTitle')}
            description={searchState.error || t('wiki.public.searchLoadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: () => setReloadToken((current) => current + 1)
            }}
            secondaryAction={{
              label: t('wiki.public.backToList'),
              onClick: onBrowseDirectory
            }}
          />
        ) : isEmpty ? (
          <PublicStatusCard
            tone="empty"
            title={t('wiki.public.searchEmptyTitle')}
            description={t('wiki.public.searchEmptyDescription')}
          />
        ) : (
          <section className={styles.searchResultsSection}>
            <div className={styles.searchResultsHeader}>
              <div>
                <h2 className={styles.panelTitle}>{t('wiki.public.searchResultsTitle')}</h2>
                <p className={styles.panelHint}>{t('wiki.public.searchResultsHint')}</p>
              </div>
              <span className={styles.panelStat}>{t('wiki.public.searchResultCount', { count: searchState.totalDocuments })}</span>
            </div>

            <div className={styles.searchResultList}>
              {searchState.documents.map((document) => (
                <button
                  key={document.voId}
                  type="button"
                  className={`${styles.docCard} ${styles.searchResultCard}`}
                  onClick={() => onOpenDocument(document.voSlug)}
                >
                  <div className={styles.docCardMeta}>
                    <span className={styles.metaChip}>{toVisibilityText(t, document.voVisibility)}</span>
                    <span className={styles.metaChip}>{toStatusText(t, document.voStatus)}</span>
                    <span className={styles.metaChip}>{t('wiki.meta.slug', { value: document.voSlug })}</span>
                  </div>
                  <h2 className={styles.searchResultTitle}>{document.voTitle}</h2>
                  <p className={styles.docCardSummary}>
                    {document.voSummary?.trim() || t('wiki.public.summaryFallback')}
                  </p>
                  <div className={styles.searchResultMeta}>
                    <span>{t('wiki.meta.source', { value: document.voSourceType })}</span>
                    <span>{formatDateTimeByTimeZone(document.voModifyTime || document.voCreateTime, displayTimeZone)}</span>
                  </div>
                  <div className={styles.docCardFooter}>
                    <span>{t('wiki.public.searchOpenHint')}</span>
                    <span className={styles.docCardAction}>{t('wiki.public.openDocument')}</span>
                  </div>
                </button>
              ))}
            </div>

            {searchState.totalPages > 1 && (
              <div className={styles.paginationBar}>
                <button
                  type="button"
                  className={styles.secondaryButton}
                  onClick={() => handleChangePage(route.page - 1)}
                  disabled={route.page <= 1}
                >
                  {t('common.previousPage')}
                </button>
                <span className={styles.paginationInfo}>
                  {t('common.pageInfo', { current: route.page, total: searchState.totalPages })}
                </span>
                <button
                  type="button"
                  className={styles.secondaryButton}
                  onClick={() => handleChangePage(route.page + 1)}
                  disabled={route.page >= searchState.totalPages}
                >
                  {t('common.nextPage')}
                </button>
              </div>
            )}
          </section>
        )}
      </div>
    </section>
  );
};

interface PublicDocsDetailProps {
  route: PublicDocsRoute & { kind: 'detail' };
  displayTimeZone: string;
  backLabel: string;
  onBack: () => void;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
}

const PublicDocsDetail = ({ route, displayTimeZone, backLabel, onBack, onNavigate }: PublicDocsDetailProps) => {
  const { t } = useTranslation();
  const [documentDetail, setDocumentDetail] = useState<WikiDocumentDetailVo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const articleBodyRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let cancelled = false;

    const loadDetail = async () => {
      setLoading(true);
      setError(null);

      try {
        const detail = await getPublicWikiDocumentBySlug(route.slug);
        if (!cancelled) {
          setDocumentDetail(detail);
        }
      } catch (err) {
        if (!cancelled) {
          setDocumentDetail(null);
          setError(err instanceof Error ? err.message : String(err));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadDetail();
    return () => {
      cancelled = true;
    };
  }, [reloadToken, route.slug]);

  useEffect(() => {
    if (!documentDetail?.voTitle) {
      return;
    }

    globalThis.document.title = `${documentDetail.voTitle} · ${t('desktop.apps.document.name')}`;
  }, [documentDetail?.voTitle, t]);

  useEffect(() => {
    const container = articleBodyRef.current;
    if (!container) {
      return;
    }

    const currentOrigin = getCurrentOrigin();
    container.querySelectorAll<HTMLAnchorElement>('a[href]').forEach((anchor) => {
      const href = anchor.getAttribute('href') ?? anchor.href;
      if (!href) {
        return;
      }

      const rewrittenHref = rewritePublicDocsHref(href, currentOrigin);
      if (rewrittenHref) {
        anchor.setAttribute('href', rewrittenHref);
      }
    });
  }, [documentDetail?.voId, documentDetail?.voMarkdownContent]);

  const handleMarkdownLinkClick = (event: MouseEvent<HTMLDivElement>) => {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const anchor = target.closest<HTMLAnchorElement>('a[href]');
    if (!anchor || (anchor.target && anchor.target !== '_self')) {
      return;
    }

    const href = anchor.getAttribute('href') ?? anchor.href;
    if (!href) {
      return;
    }

    const nextRoute = resolvePublicDocsRouteFromHref(href, getCurrentOrigin());
    if (!nextRoute) {
      return;
    }

    event.preventDefault();
    onNavigate(nextRoute);
  };

  const detailState = loading
    ? 'loading'
    : documentDetail
      ? 'ready'
      : isPublicDocumentNotFound(error)
        ? 'notFound'
        : error
          ? 'error'
          : 'loading';

  return (
    <section className={styles.sectionCard}>
      <div className={styles.detailTopbar}>
        <button type="button" className={styles.secondaryButton} onClick={onBack}>
          <Icon icon="mdi:arrow-left" size={18} />
          <span>{backLabel}</span>
        </button>
      </div>

      <div className={styles.contentWrap}>
        {detailState === 'loading' && (
          <PublicStatusCard
            tone="loading"
            title={t('wiki.public.detailLoadingTitle')}
            description={t('wiki.public.detailLoadingDescription')}
          />
        )}

        {detailState === 'notFound' && (
          <PublicStatusCard
            tone="notFound"
            title={t('wiki.public.notFoundTitle')}
            description={t('wiki.public.notFoundDescription')}
            secondaryAction={{
              label: backLabel,
              onClick: onBack
            }}
          />
        )}

        {detailState === 'error' && (
          <PublicStatusCard
            tone="error"
            title={t('wiki.public.detailErrorTitle')}
            description={error || t('wiki.public.detailLoadingDescription')}
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

        {detailState === 'ready' && documentDetail && (
          <article className={styles.articleCard}>
            <div className={styles.articleHeader}>
              <div className={styles.articleHeaderMain}>
                <p className={styles.kicker}>{t('wiki.public.readingKicker')}</p>
                <h1 className={styles.articleTitle}>{documentDetail.voTitle}</h1>
                {documentDetail.voSummary?.trim() ? (
                  <p className={styles.articleSummary}>{documentDetail.voSummary}</p>
                ) : null}
              </div>
              <div className={styles.articleMetaRail}>
                <span className={styles.metaChip}>{toVisibilityText(t, documentDetail.voVisibility)}</span>
                <span className={styles.metaChip}>{toStatusText(t, documentDetail.voStatus)}</span>
                <span className={styles.metaChip}>{t('wiki.meta.slug', { value: documentDetail.voSlug })}</span>
              </div>
            </div>

            <div className={styles.articleMetaGrid}>
              <span className={styles.metaChip}>{t('wiki.meta.source', { value: documentDetail.voSourceType })}</span>
              <span className={styles.metaChip}>
                {t('wiki.meta.updated', { value: formatDateTimeByTimeZone(documentDetail.voModifyTime || documentDetail.voCreateTime, displayTimeZone) })}
              </span>
              <span className={styles.metaChip}>
                {t('wiki.meta.created', { value: formatDateTimeByTimeZone(documentDetail.voCreateTime, displayTimeZone) })}
              </span>
            </div>

            <div ref={articleBodyRef} className={styles.articleBody} onClick={handleMarkdownLinkClick}>
              <MarkdownRenderer content={documentDetail.voMarkdownContent} className={styles.markdownContent} />
            </div>
          </article>
        )}
      </div>
    </section>
  );
};
