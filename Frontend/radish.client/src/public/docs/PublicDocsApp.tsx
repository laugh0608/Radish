import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { Icon } from '@radish/ui/icon';
import { DEFAULT_TIME_ZONE, formatDateTimeByTimeZone, getBrowserTimeZoneId } from '@/utils/dateTime';
import type {
  WikiDocumentDetailVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
} from '@/apps/wiki/types/wiki';
import { canUseDocsAuthorTools, isBuiltInWikiDocument } from '@/docs/docsAuthorAccess';
import { buildDocsAuthorPath } from '@/docs/docsAuthorRouteState';
import { useUserStore } from '@/stores/userStore';
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
import {
  getPublicDetailBackLabelKey,
  type PublicDetailBackMode,
} from '../publicRouteNavigation';
import {
  applyPublicStructuredData,
  buildDocsArticleStructuredData,
  removePublicStructuredData,
} from '../publicStructuredData';
import { buildPublicShareUrl } from '../publicHead';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { usePublicShareLink } from '../hooks/usePublicShareLink';
import { WebStateSlot, type WebStateSlotAction } from '@/components/web-shell';
import { copyRecoveryDiagnostics } from '@/utils/recoveryDiagnostics';
import { getPublicWikiDocumentBySlug, getPublicWikiList, getPublicWikiTree } from './publicDocsApi';
import { PublicDocsDetailRail, PublicDocsListRail, PublicDocsSearchRail } from './PublicDocsRails';
import { toSourceText, toStatusText, toVisibilityText } from './publicDocsFormat';
import styles from './PublicDocsApp.module.css';

const PUBLIC_DOCS_SEARCH_PAGE_SIZE = 10;

interface PublicDocsAppProps {
  route: PublicDocsRoute;
  fallbackBrowseRoute: PublicDocsBrowseRoute;
  detailBackAction?: {
    mode: PublicDetailBackMode;
    href?: string;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean; preserveSourceState?: boolean }) => void;
  onNavigateToDiscover?: () => void;
}

interface PublicDocsTreeRow {
  id: string;
  slug: string;
  title: string;
  depth: number;
  childCount: number;
}

const PUBLIC_DOCS_DIRECTORY_PREVIEW_LIMIT = 36;

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

interface PublicStatusCardAction {
  label: string;
  href?: string;
  onClick: () => void;
}

type PublicDocsDiagnosticCopyHandler = (stage: string, error: string | null | undefined) => Promise<void>;

interface PublicStatusCardProps {
  tone: 'loading' | 'empty' | 'error' | 'notFound';
  title: string;
  description: string;
  compact?: boolean;
  primaryAction?: PublicStatusCardAction;
  secondaryAction?: PublicStatusCardAction;
  diagnosticAction?: PublicStatusCardAction;
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

function buildPublicDocsDiagnosticTarget(route: PublicDocsRoute): Record<string, string | number | boolean | undefined> {
  if (route.kind === 'detail') {
    return {
      routeKind: route.kind,
      slug: route.slug,
      hasAnchor: Boolean(route.anchor),
    };
  }

  if (route.kind === 'search') {
    return {
      routeKind: route.kind,
      page: route.page,
      hasKeyword: Boolean(route.keyword.trim()),
    };
  }

  return {
    routeKind: route.kind,
  };
}

function normalizeMarkdownHeadingText(value: string): string {
  return value.replace(/[ \t]+#+[ \t]*$/, '').trim();
}

function stripDuplicateLeadingMarkdownTitle(markdown: string, title: string): string {
  const normalizedTitle = normalizeMarkdownHeadingText(title);
  if (!markdown || !normalizedTitle) {
    return markdown;
  }

  const leadingTitleMatch = markdown.match(/^(\uFEFF?(?:[ \t]*(?:\r?\n))*[ \t]{0,3})#(?!#)[ \t]+([^\r\n]*?)(?:\r?\n|$)/);
  if (!leadingTitleMatch) {
    return markdown;
  }

  const leadingTitle = normalizeMarkdownHeadingText(leadingTitleMatch[2] ?? '');
  if (leadingTitle !== normalizedTitle) {
    return markdown;
  }

  return markdown.slice(leadingTitleMatch[0].length).replace(/^[ \t]*(?:\r?\n)/, '');
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

function shouldHandlePublicDocsLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handlePublicDocsLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandlePublicDocsLink(event)) {
    return;
  }

  event.preventDefault();
  action();
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
  secondaryAction,
  diagnosticAction
}: PublicStatusCardProps) {
  const resolvedIcon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:file-document-outline'
      : tone === 'notFound'
        ? 'mdi:file-search-outline'
        : 'mdi:alert-circle-outline';
  const actions: WebStateSlotAction[] = [];

  if (primaryAction) {
    actions.push({
      label: primaryAction.label,
      href: primaryAction.href,
      kind: 'primary',
      onClick: primaryAction.href
        ? (event) => handlePublicDocsLinkClick(event as MouseEvent<HTMLAnchorElement>, primaryAction.onClick)
        : () => primaryAction.onClick(),
    });
  }

  if (secondaryAction) {
    actions.push({
      label: secondaryAction.label,
      href: secondaryAction.href,
      kind: 'secondary',
      onClick: secondaryAction.href
        ? (event) => handlePublicDocsLinkClick(event as MouseEvent<HTMLAnchorElement>, secondaryAction.onClick)
        : () => secondaryAction.onClick(),
    });
  }

  if (diagnosticAction) {
    actions.push({
      label: diagnosticAction.label,
      href: diagnosticAction.href,
      kind: 'secondary',
      onClick: diagnosticAction.href
        ? (event) => handlePublicDocsLinkClick(event as MouseEvent<HTMLAnchorElement>, diagnosticAction.onClick)
        : () => diagnosticAction.onClick(),
    });
  }

  return (
    <WebStateSlot
      tone={tone}
      title={title}
      description={description}
      icon={resolvedIcon}
      compact={compact}
      actions={actions}
    />
  );
}

export const PublicDocsApp = ({
  route,
  fallbackBrowseRoute,
  detailBackAction,
  onNavigate,
  onNavigateToDiscover
}: PublicDocsAppProps) => {
  const { t } = useTranslation();
  const roles = useUserStore((state) => state.roles || []);
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));
  const showAuthorTools = useMemo(() => canUseDocsAuthorTools(roles), [roles]);
  const pageRef = useRef<HTMLDivElement>(null);
  const previousRouteRef = useRef<PublicDocsRoute>(route);
  const browseScrollSnapshotRef = useRef<{ routeKey: string; scrollTop: number } | null>(null);
  const [pendingRestoreScrollTop, setPendingRestoreScrollTop] = useState<number | null>(null);
  const [diagnosticCopyState, setDiagnosticCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');
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

  const getDiagnosticActionLabel = () => t(diagnosticCopyState === 'copied'
    ? 'common.diagnosticsCopied'
    : diagnosticCopyState === 'failed'
      ? 'common.diagnosticsCopyFailed'
      : 'common.copyDiagnostics');

  const handleCopyDiagnostics = useCallback(async (stage: string, error: string | null | undefined) => {
    try {
      await copyRecoveryDiagnostics({
        module: 'public.docs',
        stage,
        error: error || 'unknown',
        target: buildPublicDocsDiagnosticTarget(route),
      });
      setDiagnosticCopyState('copied');
    } catch {
      setDiagnosticCopyState('failed');
    }
  }, [route]);

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
    setDiagnosticCopyState('idle');
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

  const detailBackLabelKey = getPublicDetailBackLabelKey(detailBackAction?.mode);
  const backLabel = detailBackLabelKey
    ? t(detailBackLabelKey)
    : fallbackBrowseRoute.kind === 'search'
      ? t('wiki.public.backToSearch')
      : t('wiki.public.backToList');
  const handleDocsDetailBack = detailBackAction?.onBack ?? (() => onNavigate(fallbackBrowseRoute));
  const detailBackHref = detailBackAction?.href ?? buildPublicDocsPath(fallbackBrowseRoute);

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="文"
        brandName={t('desktop.apps.document.name')}
        brandSubline={t('wiki.public.shellLabel')}
        onBrandClick={() => onNavigate(createDefaultDocsListRoute())}
        onNavigateToDiscover={onNavigateToDiscover}
        discoverLabel={t('public.shell.discoverAction')}
        loginLabel={t('public.shell.loginAction')}
        myStatusLabel={t('public.shell.myStatusAction')}
        circleLabel={t('public.shell.circleAction')}
        desktopLabel={t('public.shell.desktopAction')}
      />

      <main className={styles.main}>
        {route.kind === 'detail' ? (
          <PublicDocsDetail
            key={`docs-${route.slug}-${route.anchor ?? 'root'}`}
            route={route}
            displayTimeZone={displayTimeZone}
            backLabel={backLabel}
            backHref={detailBackHref}
            canUseDocsAuthorTools={showAuthorTools}
            relatedDocuments={collectionState.documents}
            getDiagnosticActionLabel={getDiagnosticActionLabel}
            onCopyDiagnostics={handleCopyDiagnostics}
            onBack={handleDocsDetailBack}
            onNavigate={onNavigate}
            onOpenDocument={(slug) => onNavigate({ kind: 'detail', slug })}
          />
        ) : route.kind === 'search' ? (
          <PublicDocsSearch
            route={route}
            displayTimeZone={displayTimeZone}
            scrollContainerRef={pageRef}
            restoreScrollTop={pendingRestoreScrollTop}
            getDiagnosticActionLabel={getDiagnosticActionLabel}
            onCopyDiagnostics={handleCopyDiagnostics}
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
            canUseDocsAuthorTools={showAuthorTools}
            getDiagnosticActionLabel={getDiagnosticActionLabel}
            onCopyDiagnostics={handleCopyDiagnostics}
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
  canUseDocsAuthorTools: boolean;
  getDiagnosticActionLabel: () => string;
  onCopyDiagnostics: PublicDocsDiagnosticCopyHandler;
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
  canUseDocsAuthorTools,
  getDiagnosticActionLabel,
  onCopyDiagnostics,
  onScrollRestored,
  onReload,
  onOpenSearch,
  onOpenDocument
}: PublicDocsListProps) => {
  const { t } = useTranslation();
  const [directoryExpanded, setDirectoryExpanded] = useState(false);
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
  const visibleTreeRows = directoryExpanded ? treeRows : treeRows.slice(0, PUBLIC_DOCS_DIRECTORY_PREVIEW_LIMIT);
  const isDirectoryTruncated = treeRows.length > visibleTreeRows.length;
  const listCards = useMemo(() => documents.slice(0, 12), [documents]);
  const searchHref = buildPublicDocsPath(createDefaultDocsSearchRoute());
  const authorHref = buildDocsAuthorPath({ kind: 'mine' });
  const hasAnyContent = treeRows.length > 0 || listCards.length > 0;
  const isLoading = (loadingTree || loadingDocuments) && !hasAnyContent;
  const isError = !hasAnyContent && Boolean(treeError || listError);
  const isEmpty = !loadingTree && !loadingDocuments && !hasAnyContent && !treeError && !listError;

  return (
    <section className={styles.sectionCard}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>{t('wiki.public.listGuideKicker')}</p>
          <h1 className={styles.pageTitle}>{t('wiki.public.pageTitle')}</h1>
          <p className={styles.pageIntro}>{t('wiki.public.pageIntro')}</p>
        </div>
        <div className={styles.sectionActions}>
          {canUseDocsAuthorTools ? (
            <a className={styles.primaryButton} href={authorHref}>
              <Icon icon="mdi:pencil-box-outline" size={18} />
              <span>文档作者台</span>
            </a>
          ) : null}
          <a
            className={styles.secondaryButton}
            href={searchHref}
            onClick={(event) => handlePublicDocsLinkClick(event, onOpenSearch)}
          >
            <Icon icon="mdi:magnify" size={18} />
            <span>{t('wiki.public.searchAction')}</span>
          </a>
        </div>
      </div>

      <div className={styles.contentWrap}>
        {isLoading ? (
          <PublicStatusCard
            tone="loading"
            title={t('wiki.public.loadingTitle')}
            description={t('wiki.public.loadingDescription')}
          />
        ) : isError ? (
          <PublicStatusCard
            tone="error"
            title={t('wiki.public.listErrorTitle')}
            description={treeError || listError || t('wiki.public.loadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: onReload
            }}
            diagnosticAction={{
              label: getDiagnosticActionLabel(),
              onClick: () => {
                void onCopyDiagnostics('list-load', treeError || listError);
              }
            }}
          />
        ) : isEmpty ? (
          <PublicStatusCard
            tone="empty"
            title={t('wiki.public.emptyTitle')}
            description={t('wiki.public.emptyDescription')}
          />
        ) : (
          <>
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

            <div className={styles.docsIndexGrid}>
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
                    {listCards.map((document) => {
                      const href = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });

                      return (
                        <a
                          key={document.voId}
                          className={styles.docCard}
                          href={href}
                          onClick={(event) => handlePublicDocsLinkClick(event, () => onOpenDocument(document.voSlug))}
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
                        </a>
                      );
                    })}
                  </div>
                )}
              </section>

              <section className={`${styles.panel} ${styles.directoryPanel}`}>
                <div className={styles.panelHeader}>
                  <div>
                    <h2 className={styles.panelTitle}>{t('wiki.public.directoryTitle')}</h2>
                    <p className={styles.panelHint}>{t('wiki.public.directoryHint')}</p>
                  </div>
                  <span className={styles.panelStat}>
                    {directoryExpanded
                      ? t('wiki.public.directoryCount', { count: treeRows.length })
                      : t('wiki.public.directoryPreviewCount', { visible: visibleTreeRows.length, total: treeRows.length })}
                  </span>
                </div>

                {treeRows.length === 0 ? (
                  <PublicStatusCard
                    tone="empty"
                    compact={true}
                    title={t('wiki.public.directoryEmptyTitle')}
                    description={t('wiki.public.directoryEmptyDescription')}
                  />
                ) : (
                  <>
                    <div className={styles.directoryList}>
                      {visibleTreeRows.map((row) => {
                        const href = buildPublicDocsPath({ kind: 'detail', slug: row.slug });

                        return (
                          <a
                            key={row.id}
                            className={styles.directoryItem}
                            href={href}
                            onClick={(event) => handlePublicDocsLinkClick(event, () => onOpenDocument(row.slug))}
                          >
                            <span className={styles.directoryPrefix} style={{ marginLeft: `${row.depth * 14}px` }}>
                              {row.depth > 0 ? '└' : '•'}
                            </span>
                            <span className={styles.directoryTitle}>{row.title}</span>
                            {row.childCount > 0 && (
                              <span className={styles.directoryMeta}>{t('wiki.public.childCount', { count: row.childCount })}</span>
                            )}
                          </a>
                        );
                      })}
                    </div>
                    {(isDirectoryTruncated || directoryExpanded) ? (
                      <div className={styles.directoryFooter}>
                        <button
                          type="button"
                          className={styles.inlineTextButton}
                          onClick={() => setDirectoryExpanded((current) => !current)}
                        >
                          {directoryExpanded
                            ? t('wiki.public.directoryCollapse')
                            : t('wiki.public.directoryExpand', { hidden: treeRows.length - visibleTreeRows.length })}
                        </button>
                        <span className={styles.directoryFooterHint}>{t('wiki.public.directorySearchHint')}</span>
                      </div>
                    ) : null}
                  </>
                )}
              </section>

              <PublicDocsListRail
                directoryCount={treeRows.length}
                totalDocuments={totalDocuments}
                canUseDocsAuthorTools={canUseDocsAuthorTools}
                authorHref={authorHref}
                searchHref={searchHref}
                onOpenSearch={onOpenSearch}
              />
            </div>
          </>
        )}
      </div>
    </section>
  );
};

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

const PublicDocsSearch = ({
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
    <section className={styles.sectionCard}>
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <p className={styles.kicker}>{t('wiki.public.searchGuideKicker')}</p>
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
              <PublicStatusCard
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
            browseDirectoryHref={browseDirectoryHref}
            hasKeyword={hasKeyword}
            resultCount={searchState.totalDocuments}
            currentPage={route.page}
            totalPages={searchState.totalPages}
            onBrowseDirectory={onBrowseDirectory}
          />
        </div>
      </div>
    </section>
  );
};

interface PublicDocsDetailProps {
  route: PublicDocsRoute & { kind: 'detail' };
  displayTimeZone: string;
  backLabel: string;
  backHref: string;
  canUseDocsAuthorTools: boolean;
  relatedDocuments: WikiDocumentVo[];
  getDiagnosticActionLabel: () => string;
  onCopyDiagnostics: PublicDocsDiagnosticCopyHandler;
  onBack: () => void;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean; preserveSourceState?: boolean }) => void;
  onOpenDocument: (slug: string) => void;
}

const PublicDocsDetail = ({
  route,
  displayTimeZone,
  backLabel,
  backHref,
  canUseDocsAuthorTools,
  relatedDocuments,
  getDiagnosticActionLabel,
  onCopyDiagnostics,
  onBack,
  onNavigate,
  onOpenDocument
}: PublicDocsDetailProps) => {
  const { t } = useTranslation();
  const [documentDetail, setDocumentDetail] = useState<WikiDocumentDetailVo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const articleBodyRef = useRef<HTMLDivElement>(null);
  const currentDocsPath = buildPublicDocsPath({
    kind: 'detail',
    slug: documentDetail?.voSlug || route.slug,
    anchor: route.anchor
  });
  const articleMarkdownContent = useMemo(
    () => stripDuplicateLeadingMarkdownTitle(
      documentDetail?.voMarkdownContent || '',
      documentDetail?.voTitle || ''
    ),
    [documentDetail?.voMarkdownContent, documentDetail?.voTitle]
  );
  const resolveArticleLinkHref = useCallback(
    (href: string) => rewritePublicDocsHref(href, getCurrentOrigin(), currentDocsPath),
    [currentDocsPath]
  );

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
    if (!documentDetail) {
      removePublicStructuredData();
      return;
    }

    applyPublicStructuredData(buildDocsArticleStructuredData({
      document: documentDetail,
      canonicalPath: buildPublicDocsPath({
        kind: 'detail',
        slug: documentDetail.voSlug,
        anchor: route.anchor,
      }),
    }));

    return removePublicStructuredData;
  }, [documentDetail, route.anchor]);

  useEffect(() => {
    if (!documentDetail?.voSlug) {
      return;
    }

    const canonicalRoute: PublicDocsRoute = {
      kind: 'detail',
      slug: documentDetail.voSlug,
      anchor: route.anchor
    };

    if (buildPublicDocsPath(route) === buildPublicDocsPath(canonicalRoute)) {
      return;
    }

    onNavigate(canonicalRoute, { replace: true, preserveSourceState: true });
  }, [documentDetail?.voSlug, onNavigate, route]);

  useEffect(() => {
    if (!route.anchor || !documentDetail || typeof globalThis.document === 'undefined') {
      return;
    }

    const anchorTarget = globalThis.document.getElementById(route.anchor)
      ?? globalThis.document.getElementsByName(route.anchor)[0];
    if (!anchorTarget || !articleBodyRef.current?.contains(anchorTarget)) {
      return;
    }

    anchorTarget.scrollIntoView({ block: 'start' });
  }, [documentDetail, route.anchor]);

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

    const nextRoute = resolvePublicDocsRouteFromHref(href, getCurrentOrigin(), currentDocsPath);
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

  const buildDocsShareUrl = useCallback(() => {
    const shareRoute: PublicDocsRoute = {
      kind: 'detail',
      slug: documentDetail?.voSlug || route.slug,
      anchor: route.anchor
    };
    return buildPublicShareUrl(buildPublicDocsPath(shareRoute));
  }, [documentDetail?.voSlug, route.anchor, route.slug]);
  const { copyShareLink, shareBusy, shareState } = usePublicShareLink({
    buildShareUrl: buildDocsShareUrl,
  });
  const canEditDocument = documentDetail !== null
    && canUseDocsAuthorTools
    && !documentDetail.voIsDeleted
    && !isBuiltInWikiDocument(documentDetail);
  const editHref = documentDetail
    ? buildDocsAuthorPath({ kind: 'edit', documentId: documentDetail.voId })
    : buildDocsAuthorPath({ kind: 'mine' });

  return (
    <section className={styles.sectionCard}>
      <div className={styles.detailTopbar}>
        <div className={styles.detailTopbarActions}>
          <a
            className={styles.secondaryButton}
            href={backHref}
            onClick={(event) => handlePublicDocsLinkClick(event, onBack)}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>{backLabel}</span>
          </a>
          <button type="button" className={styles.secondaryButton} onClick={() => void copyShareLink()} disabled={shareBusy}>
            <Icon icon={shareBusy ? 'mdi:progress-clock' : 'mdi:link-variant'} size={18} />
            <span>{shareBusy ? t('wiki.public.shareSubmitting') : t('wiki.public.shareAction')}</span>
          </button>
          {canEditDocument ? (
            <a className={styles.primaryButton} href={editHref}>
              <Icon icon="mdi:pencil-outline" size={18} />
              <span>编辑文档</span>
            </a>
          ) : null}
        </div>
        {shareState !== 'idle' && (
          <p className={styles.shareFeedback} data-state={shareState}>
            {shareState === 'success' ? t('wiki.public.shareSuccess') : t('wiki.public.shareFailed')}
          </p>
        )}
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
              href: backHref,
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
              href: backHref,
              onClick: onBack
            }}
            diagnosticAction={{
              label: getDiagnosticActionLabel(),
              onClick: () => {
                void onCopyDiagnostics('detail-load', error);
              }
            }}
          />
        )}

        {detailState === 'ready' && documentDetail && (
          <div className={styles.docsArticleGrid}>
            <div className={styles.articleMainColumn}>
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
                  </div>
                </div>

                <div className={styles.articleMetaGrid}>
                  <section className={styles.articleMetaGroup}>
                    <span className={styles.articleMetaLabel}>{t('wiki.public.detailAccessLabel')}</span>
                    <div className={styles.articleMetaValues}>
                      <span className={styles.metaChip}>{toVisibilityText(t, documentDetail.voVisibility)}</span>
                      <span className={styles.metaChip}>{toStatusText(t, documentDetail.voStatus)}</span>
                    </div>
                  </section>
                  <section className={styles.articleMetaGroup}>
                    <span className={styles.articleMetaLabel}>{t('wiki.public.detailDocumentLabel')}</span>
                    <div className={styles.articleMetaValues}>
                      <span className={styles.metaChip}>{t('wiki.meta.slug', { value: documentDetail.voSlug })}</span>
                      <span className={styles.metaChip}>{t('wiki.meta.source', { value: toSourceText(t, documentDetail.voSourceType) })}</span>
                    </div>
                  </section>
                  <section className={styles.articleMetaGroup}>
                    <span className={styles.articleMetaLabel}>{t('wiki.public.detailTimelineLabel')}</span>
                    <div className={styles.articleMetaValues}>
                      <span className={styles.metaChip}>
                        {t('wiki.meta.updated', { value: formatDateTimeByTimeZone(documentDetail.voModifyTime || documentDetail.voCreateTime, displayTimeZone) })}
                      </span>
                      <span className={styles.metaChip}>
                        {t('wiki.meta.created', { value: formatDateTimeByTimeZone(documentDetail.voCreateTime, displayTimeZone) })}
                      </span>
                    </div>
                  </section>
                  <p className={styles.articleBoundaryNote}>{t('wiki.public.detailBoundaryNote')}</p>
                </div>

                <div ref={articleBodyRef} className={styles.articleBody} onClick={handleMarkdownLinkClick}>
                  <MarkdownRenderer
                    content={articleMarkdownContent}
                    className={styles.markdownContent}
                    resolveLinkHref={resolveArticleLinkHref}
                  />
                </div>
              </article>
            </div>

            <PublicDocsDetailRail
              document={documentDetail}
              relatedDocuments={relatedDocuments}
              displayTimeZone={displayTimeZone}
              backLabel={backLabel}
              backHref={backHref}
              canEditDocument={canEditDocument}
              editHref={editHref}
              onBack={onBack}
              onOpenDocument={onOpenDocument}
            />
          </div>
        )}
      </div>
    </section>
  );
};
