import { useCallback, useEffect, useRef, useState, type MouseEvent } from 'react';
import { useBrowserNavigationLock } from '@/bootstrap/browserNavigationLock';
import {
  buildDocsAuthorPath,
  createDefaultDocsAuthorRoute,
  parseDocsAuthorRoute,
  type DocsAuthorRoute,
} from './docsAuthorRouteState';

const DOCS_AUTHOR_HISTORY_POSITION_KEY = '__radishDocsAuthorPosition';

function readDocsAuthorHistoryPosition(state: unknown): number | null {
  if (!state || typeof state !== 'object') {
    return null;
  }

  const position = (state as Record<string, unknown>)[DOCS_AUTHOR_HISTORY_POSITION_KEY];
  return typeof position === 'number' && Number.isInteger(position) ? position : null;
}

function buildDocsAuthorHistoryState(
  position: number,
  historyState: unknown = window.history.state,
): Record<string, unknown> {
  const currentState = historyState;
  const state = currentState && typeof currentState === 'object'
    ? currentState as Record<string, unknown>
    : {};

  return {
    ...state,
    [DOCS_AUTHOR_HISTORY_POSITION_KEY]: position,
  };
}

function resolveInitialDocsAuthorRoute(): DocsAuthorRoute {
  if (typeof window === 'undefined') {
    return createDefaultDocsAuthorRoute();
  }

  return parseDocsAuthorRoute(window.location.pathname) ?? createDefaultDocsAuthorRoute();
}

export function shouldHandleAuthorLinkClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

interface DocsAuthorNavigation {
  route: DocsAuthorRoute;
  navigateToRoute: (nextRoute: DocsAuthorRoute, options?: { replace?: boolean }) => void;
}

export function useDocsAuthorNavigation(navigationLocked: boolean): DocsAuthorNavigation {
  const [route, setRoute] = useState<DocsAuthorRoute>(() => resolveInitialDocsAuthorRoute());
  const historyPositionRef = useRef(
    typeof window === 'undefined' ? 0 : readDocsAuthorHistoryPosition(window.history.state) ?? 0,
  );
  const restoringHistoryRef = useRef(false);
  const lockedPathRef = useRef<string | null>(null);
  const lockedHistoryStateRef = useRef<Record<string, unknown> | null>(null);

  useBrowserNavigationLock(navigationLocked);

  const navigateToRoute = useCallback((nextRoute: DocsAuthorRoute, options?: { replace?: boolean }) => {
    if (navigationLocked) {
      return;
    }

    const nextPath = buildDocsAuthorPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (options?.replace) {
      window.history.replaceState(buildDocsAuthorHistoryState(historyPositionRef.current), '', nextPath);
    } else if (currentPath !== nextPath) {
      const nextPosition = historyPositionRef.current + 1;
      window.history.pushState(buildDocsAuthorHistoryState(nextPosition), '', nextPath);
      historyPositionRef.current = nextPosition;
    }

    setRoute(nextRoute);
  }, [navigationLocked]);

  useEffect(() => {
    const currentPosition = readDocsAuthorHistoryPosition(window.history.state);
    if (currentPosition !== null) {
      historyPositionRef.current = currentPosition;
      return;
    }

    window.history.replaceState(
      buildDocsAuthorHistoryState(historyPositionRef.current),
      '',
      `${window.location.pathname}${window.location.search}${window.location.hash}`,
    );
  }, []);

  useEffect(() => {
    const handlePopState = () => {
      if (restoringHistoryRef.current) {
        restoringHistoryRef.current = false;
        return;
      }

      const nextPosition = readDocsAuthorHistoryPosition(window.history.state);
      if (navigationLocked) {
        if (nextPosition !== null) {
          const restoreDelta = historyPositionRef.current - nextPosition;
          if (restoreDelta !== 0) {
            restoringHistoryRef.current = true;
            window.history.go(restoreDelta);
            return;
          }
        }

        const lockedPath = lockedPathRef.current ?? buildDocsAuthorPath(route);
        window.history.pushState(
          buildDocsAuthorHistoryState(
            historyPositionRef.current,
            lockedHistoryStateRef.current,
          ),
          '',
          lockedPath,
        );
        return;
      }

      if (nextPosition !== null) {
        historyPositionRef.current = nextPosition;
      }
      setRoute(resolveInitialDocsAuthorRoute());
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, [navigationLocked, route]);

  useEffect(() => {
    if (!navigationLocked) {
      lockedPathRef.current = null;
      lockedHistoryStateRef.current = null;
      return;
    }

    lockedPathRef.current = buildDocsAuthorPath(route);
    lockedHistoryStateRef.current = buildDocsAuthorHistoryState(historyPositionRef.current);
  }, [navigationLocked, route]);

  useEffect(() => {
    if (!navigationLocked) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
    };
  }, [navigationLocked]);

  useEffect(() => {
    const canonicalPath = buildDocsAuthorPath(route);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, [route]);

  return { route, navigateToRoute };
}
