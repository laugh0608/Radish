import { useCallback, useEffect, useState } from 'react';
import { PublicForumApp } from './forum/PublicForumApp';

export type PublicListSort = 'newest' | 'hottest';

export interface PublicForumListRoute {
  kind: 'list';
  categoryId: number | null;
  sortBy: PublicListSort;
  page: number;
}

export interface PublicForumDetailRoute {
  kind: 'detail';
  postId: number;
}

export type PublicForumRoute = PublicForumListRoute | PublicForumDetailRoute;

function createDefaultListRoute(): PublicForumListRoute {
  return {
    kind: 'list',
    categoryId: null,
    sortBy: 'newest',
    page: 1
  };
}

function normalizePositiveInteger(value: string | undefined): number | undefined {
  if (!value) {
    return undefined;
  }

  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return undefined;
  }

  return parsed;
}

function normalizeSortBy(value: string | null): PublicListSort {
  return value === 'hottest' ? 'hottest' : 'newest';
}

function parseListRoute(search: string): PublicForumListRoute {
  const params = new URLSearchParams(search);
  return {
    kind: 'list',
    categoryId: normalizePositiveInteger(params.get('category') ?? undefined) ?? null,
    sortBy: normalizeSortBy(params.get('sort')),
    page: normalizePositiveInteger(params.get('page') ?? undefined) ?? 1
  };
}

function parsePublicForumRoute(pathname: string, search: string): PublicForumRoute {
  if (pathname === '/forum' || pathname === '/forum/') {
    return parseListRoute(search);
  }

  const matched = pathname.match(/^\/forum\/post\/(\d+)\/?$/);
  const postId = normalizePositiveInteger(matched?.[1]);
  if (postId) {
    return { kind: 'detail', postId };
  }

  return parseListRoute(search);
}

function buildPublicForumPath(route: PublicForumRoute): string {
  if (route.kind === 'detail') {
    return `/forum/post/${route.postId}`;
  }

  const search = new URLSearchParams();
  if (route.categoryId) {
    search.set('category', String(route.categoryId));
  }
  if (route.sortBy !== 'newest') {
    search.set('sort', route.sortBy);
  }
  if (route.page > 1) {
    search.set('page', String(route.page));
  }

  const queryString = search.toString();
  return queryString ? `/forum?${queryString}` : '/forum';
}

export const PublicEntry = () => {
  const [route, setRoute] = useState<PublicForumRoute>(() =>
    parsePublicForumRoute(window.location.pathname, window.location.search)
  );
  const [lastListRoute, setLastListRoute] = useState<PublicForumListRoute>(() => {
    const parsedRoute = parsePublicForumRoute(window.location.pathname, window.location.search);
    return parsedRoute.kind === 'list' ? parsedRoute : createDefaultListRoute();
  });

  useEffect(() => {
    const handlePopState = () => {
      const nextRoute = parsePublicForumRoute(window.location.pathname, window.location.search);
      setRoute(nextRoute);
      if (nextRoute.kind === 'list') {
        setLastListRoute(nextRoute);
      }
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  const navigateToRoute = useCallback((nextRoute: PublicForumRoute, options?: { replace?: boolean }) => {
    const nextPath = buildPublicForumPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}`;
    if (options?.replace) {
      window.history.replaceState({}, '', nextPath);
    } else if (currentPath !== nextPath) {
      window.history.pushState({}, '', nextPath);
    }

    if (nextRoute.kind === 'list') {
      setLastListRoute(nextRoute);
    }
    setRoute(nextRoute);
  }, []);

  return (
    <PublicForumApp
      route={route}
      fallbackListRoute={lastListRoute}
      onNavigate={navigateToRoute}
    />
  );
};
