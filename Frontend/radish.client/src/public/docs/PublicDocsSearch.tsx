import { useEffect, useState, type FormEvent, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import {
  buildPublicDocsPath,
  createDefaultDocsListRoute,
  createDefaultDocsSearchRoute,
  type PublicDocsRoute,
  type PublicDocsSearchRoute,
} from '../docsRouteState';
import { getPublicWikiList } from './publicDocsApi';
import { PublicDocsSearchRail } from './PublicDocsRails';
import { PublicDocsStatusCard } from './PublicDocsStatusCard';
import { toSourceText, toStatusText, toVisibilityText } from './publicDocsFormat';
import {
  handlePublicDocsLinkClick,
  usePublicDocsScrollRestore,
  type PublicDocsDiagnosticCopyHandler,
} from './publicDocsViewSupport';
import styles from './PublicDocsApp.module.css';

const PUBLIC_DOCS_SEARCH_PAGE_SIZE = 10;

interface PublicDocsSearchState {
  documents: WikiDocumentVo[];
  totalDocuments: number;
  totalPages: number;
  loading: boolean;
  error: string | null;
}

interface PublicDocsSearchProps {
  route: PublicDocsSearchRoute;
  displayTimeZone: string;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  getDiagnosticActionLabel: () => string;
  onCopyDiagnostics: PublicDocsDiagnosticCopyHandler;
  onScrollRestored: () => void;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean; preserveSourceState?: boolean }) => void;
  onBrowseDirectory: () => void;
  onOpenDocument: (slug: string) => void;
}

export const PublicDocsSearch = ({
  route,
  displayTimeZone,
  scrollContainerRef,
  restoreScrollTop,
  getDiagnosticActionLabel,
  onCopyDiagnostics,
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

        const nextTotalPages = Math.max(result.pageCount || 1, 1);
        if (route.page > nextTotalPages) {
          onNavigate({
            kind: 'search',
            keyword: appliedKeyword,
            page: nextTotalPages
          }, { replace: true });
          return;
        }

        setSearchState({
          documents: result.data || [],
          totalDocuments: result.dataCount || 0,
          totalPages: nextTotalPages,
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
  }, [onNavigate, reloadToken, route.keyword, route.page]);

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
  const browseDirectoryHref = buildPublicDocsPath(createDefaultDocsListRoute());

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
    <section className={styles.sectionCard} data-public-docs-view="search">
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <h1 className={styles.pageTitle}>
            {hasKeyword ? t('wiki.public.searchResultTitle', { keyword: appliedKeyword }) : t('wiki.public.searchTitle')}
          </h1>
          <p className={styles.pageIntro}>
            {hasKeyword ? t('wiki.public.searchResultIntro') : t('wiki.public.searchIntro')}
          </p>
        </div>
        <div className={styles.sectionActions}>
          <a
            className={styles.secondaryButton}
            href={browseDirectoryHref}
            onClick={(event) => handlePublicDocsLinkClick(event, onBrowseDirectory)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{t('wiki.public.backToList')}</span>
          </a>
        </div>
      </div>

      <div className={styles.contentWrap}>
        <div className={styles.docsSearchGrid}>
          <div className={styles.searchMainColumn}>
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
              <PublicDocsStatusCard
                tone="empty"
                title={t('wiki.public.searchIdleTitle')}
                description={t('wiki.public.searchIdleDescription')}
                secondaryAction={{
                  label: t('wiki.public.backToList'),
                  href: browseDirectoryHref,
                  onClick: onBrowseDirectory
                }}
              />
            ) : isLoading ? (
              <PublicDocsStatusCard
                tone="loading"
                title={t('wiki.public.searchLoadingTitle')}
                description={t('wiki.public.searchLoadingDescription')}
              />
            ) : isError ? (
              <PublicDocsStatusCard
                tone="error"
                title={t('wiki.public.searchErrorTitle')}
                description={searchState.error || t('wiki.public.searchLoadingDescription')}
                primaryAction={{
                  label: t('common.retry'),
                  onClick: () => setReloadToken((current) => current + 1)
                }}
                secondaryAction={{
                  label: t('wiki.public.backToList'),
                  href: browseDirectoryHref,
                  onClick: onBrowseDirectory
                }}
                diagnosticAction={{
                  label: getDiagnosticActionLabel(),
                  onClick: () => {
                    void onCopyDiagnostics('search-load', searchState.error);
                  }
                }}
              />
            ) : isEmpty ? (
              <PublicDocsStatusCard
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
                  {searchState.documents.map((document) => {
                    const href = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });

                    return (
                      <a
                        key={document.voId}
                        className={`${styles.docCard} ${styles.searchResultCard}`}
                        href={href}
                        onClick={(event) => handlePublicDocsLinkClick(event, () => onOpenDocument(document.voSlug))}
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
                          <span>{t('wiki.meta.source', { value: toSourceText(t, document.voSourceType) })}</span>
                          <span>{formatDateTimeByTimeZone(document.voModifyTime || document.voCreateTime, displayTimeZone)}</span>
                        </div>
                        <div className={styles.docCardFooter}>
                          <span>{t('wiki.public.searchOpenHint')}</span>
                          <span className={styles.docCardAction}>{t('wiki.public.openDocument')}</span>
                        </div>
                      </a>
                    );
                  })}
                </div>

                {searchState.totalPages > 1 && (
                  <div className={styles.paginationBar}>
                    {route.page <= 1 ? (
                      <button type="button" className={styles.secondaryButton} disabled>
                        {t('common.previousPage')}
                      </button>
                    ) : (
                      <a
                        className={styles.secondaryButton}
                        href={buildPublicDocsPath({ ...route, page: route.page - 1 })}
                        onClick={(event) => handlePublicDocsLinkClick(event, () => handleChangePage(route.page - 1))}
                      >
                        {t('common.previousPage')}
                      </a>
                    )}
                    <span className={styles.paginationInfo}>
                      {t('common.pageInfo', { current: route.page, total: searchState.totalPages })}
                    </span>
                    {route.page >= searchState.totalPages ? (
                      <button type="button" className={styles.secondaryButton} disabled>
                        {t('common.nextPage')}
                      </button>
                    ) : (
                      <a
                        className={styles.secondaryButton}
                        href={buildPublicDocsPath({ ...route, page: route.page + 1 })}
                        onClick={(event) => handlePublicDocsLinkClick(event, () => handleChangePage(route.page + 1))}
                      >
                        {t('common.nextPage')}
                      </a>
                    )}
                  </div>
                )}
              </section>
            )}
          </div>

          <PublicDocsSearchRail
            hasKeyword={hasKeyword}
            resultCount={searchState.totalDocuments}
            currentPage={route.page}
            totalPages={searchState.totalPages}
          />
        </div>
      </div>
    </section>
  );
};
