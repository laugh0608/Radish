import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { canUseDocsAuthorTools } from '@/docs/docsAuthorAccess';
import { useUserStore } from '@/stores/userStore';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { copyRecoveryDiagnostics } from '@/utils/recoveryDiagnostics';
import {
  buildPublicDocsPath,
  createDefaultDocsListRoute,
  createDefaultDocsSearchRoute,
  type PublicDocsBrowseRoute,
  type PublicDocsRoute,
} from '../docsRouteState';
import {
  getPublicDetailBackLabelKey,
  type PublicDetailBackMode,
} from '../publicRouteNavigation';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { getPublicWikiList, getPublicWikiTree } from './publicDocsApi';
import { PublicDocsDetail } from './PublicDocsDetail';
import { PublicDocsList, type PublicDocsCollectionState } from './PublicDocsList';
import { PublicDocsSearch } from './PublicDocsSearch';
import {
  readPublicDocsScrollTop,
  writePublicDocsScrollTop,
} from './publicDocsViewSupport';
import styles from './PublicDocsApp.module.css';

interface PublicDocsAppProps {
  route: PublicDocsRoute;
  fallbackBrowseRoute: PublicDocsBrowseRoute;
  detailBackAction?: {
    mode: PublicDetailBackMode;
    href?: string;
    onBack: () => void;
  } | null;
  onNavigate: (route: PublicDocsRoute, options?: { replace?: boolean; preserveSourceState?: boolean }) => void;
}

function buildBrowseRouteKey(route: PublicDocsBrowseRoute): string {
  return buildPublicDocsPath(route);
}

function buildPublicDocsDiagnosticTarget(
  route: PublicDocsRoute
): Record<string, string | number | boolean | undefined> {
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

export const PublicDocsApp = ({
  route,
  fallbackBrowseRoute,
  detailBackAction,
  onNavigate
}: PublicDocsAppProps) => {
  const { t } = useTranslation();
  const userId = useUserStore((state) => state.userId);
  const [displayTimeZone] = useState(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE));
  const showAuthorTools = useMemo(() => canUseDocsAuthorTools(userId), [userId]);
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
        loginLabel={t('public.shell.loginAction')}
      />

      <main className={styles.main}>
        {route.kind === 'detail' ? (
          <PublicDocsDetail
            key={`docs-${route.slug}-${route.anchor ?? 'root'}`}
            route={route}
            displayTimeZone={displayTimeZone}
            backLabel={backLabel}
            backHref={detailBackHref}
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
