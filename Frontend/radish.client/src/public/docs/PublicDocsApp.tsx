import { useEffect, useMemo, useRef, useState, type MouseEvent, type RefObject } from 'react';
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
  rewritePublicDocsHref,
  resolvePublicDocsRouteFromHref,
  type PublicDocsListRoute,
  type PublicDocsRoute,
} from '../docsRouteState';
import { getPublicWikiDocumentBySlug, getPublicWikiList, getPublicWikiTree } from './publicDocsApi';
import styles from './PublicDocsApp.module.css';

interface PublicDocsAppProps {
  route: PublicDocsRoute;
  fallbackListRoute: PublicDocsListRoute;
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

function toVisibilityText(t: (key: string) => string, visibility?: number): string {
  switch (visibility) {
    case WikiDocumentVisibility.Public:
      return t('wiki.visibility.public');
    case WikiDocumentVisibility.Restricted:
      return t('wiki.visibility.restricted');
    default:
      return t('wiki.visibility.authenticated');
  }
}

function toStatusText(t: (key: string) => string, status?: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return t('wiki.status.published');
    case WikiDocumentStatus.Archived:
      return t('wiki.status.archived');
    default:
      return t('wiki.status.draft');
  }
}

function buildListRouteKey(): string {
  return 'docs:list';
}

function getCurrentOrigin(): string {
  if (typeof window !== 'undefined' && window.location.origin) {
    return window.location.origin;
  }

  return 'https://localhost:5000';
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

export const PublicDocsApp = ({ route, fallbackListRoute, onNavigate }: PublicDocsAppProps) => {
  const { t } = useTranslation();
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));
  const pageRef = useRef<HTMLDivElement>(null);
  const previousRouteRef = useRef<PublicDocsRoute>(route);
  const listScrollSnapshotRef = useRef<{ routeKey: string; scrollTop: number } | null>(null);
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

  useEffect(() => {
    const nextTitle = route.kind === 'list'
      ? `${t('desktop.apps.document.name')} · ${t('wiki.public.pageTitle')}`
      : `${t('desktop.apps.document.name')} · ${t('wiki.public.detailTitle')}`;

    document.title = nextTitle;
  }, [route.kind, t]);

  useEffect(() => {
    const page = pageRef.current;
    const previousRoute = previousRouteRef.current;

    if (!page) {
      previousRouteRef.current = route;
      return;
    }

    if (previousRoute.kind === 'list' && route.kind === 'detail') {
      listScrollSnapshotRef.current = {
        routeKey: buildListRouteKey(),
        scrollTop: page.scrollTop
      };
      setPendingRestoreScrollTop(null);
      page.scrollTo({ top: 0, behavior: 'auto' });
    } else if (route.kind === 'list') {
      if (listScrollSnapshotRef.current?.routeKey === buildListRouteKey()) {
        setPendingRestoreScrollTop(listScrollSnapshotRef.current.scrollTop);
      } else {
        setPendingRestoreScrollTop(null);
        page.scrollTo({ top: 0, behavior: 'auto' });
      }
    } else {
      setPendingRestoreScrollTop(null);
      page.scrollTo({ top: 0, behavior: 'auto' });
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

  return (
    <div className={styles.page} ref={pageRef}>
      <header className={styles.hero}>
        <div className={styles.heroInner}>
          <button
            type="button"
            className={styles.brand}
            onClick={() => onNavigate({ kind: 'list' })}
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
            onBack={() => onNavigate(fallbackListRoute)}
            onNavigate={onNavigate}
          />
        ) : (
          <PublicDocsList
            displayTimeZone={displayTimeZone}
            collectionState={collectionState}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            onScrollRestored={() => setPendingRestoreScrollTop(null)}
            onReload={() => setCollectionReloadToken((current) => current + 1)}
            onOpenDocument={(slug) => onNavigate({ kind: 'detail', slug })}
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
  onOpenDocument: (slug: string) => void;
}

const PublicDocsList = ({
  displayTimeZone,
  collectionState,
  scrollContainerRef,
  restoreScrollTop,
  onScrollRestored,
  onReload,
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

  useEffect(() => {
    if (restoreScrollTop == null || loadingTree || loadingDocuments) {
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

      const maxScrollTop = Math.max(container.scrollHeight - container.clientHeight, 0);
      const nextScrollTop = Math.min(targetScrollTop, maxScrollTop);
      container.scrollTo({ top: nextScrollTop, behavior: 'auto' });

      const restored = Math.abs(container.scrollTop - nextScrollTop) <= 2;
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
  }, [loadingDocuments, loadingTree, onScrollRestored, restoreScrollTop, scrollContainerRef]);

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

interface PublicDocsDetailProps {
  route: PublicDocsRoute & { kind: 'detail' };
  displayTimeZone: string;
  onBack: () => void;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean }) => void;
}

const PublicDocsDetail = ({ route, displayTimeZone, onBack, onNavigate }: PublicDocsDetailProps) => {
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
          <span>{t('wiki.public.backToList')}</span>
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
              label: t('wiki.public.backToList'),
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
              label: t('wiki.public.backToList'),
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
