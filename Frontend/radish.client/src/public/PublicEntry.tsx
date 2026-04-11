import { useCallback, useEffect, useState } from 'react';
import { PublicForumApp } from './forum/PublicForumApp';
import { PublicDocsApp } from './docs/PublicDocsApp';
import {
  buildPublicForumPath,
  createDefaultListRoute,
  parsePublicForumRoute,
} from './forumRouteState';
import {
  buildPublicDocsPath,
  createDefaultDocsListRoute,
  parsePublicDocsRoute,
} from './docsRouteState';
import type {
  PublicForumListRoute,
  PublicForumRoute,
} from './forumRouteState';
import type {
  PublicDocsListRoute,
  PublicDocsRoute,
} from './docsRouteState';
export type {
  PublicListSort,
} from './forumRouteState';

type PublicRoute =
  | { app: 'forum'; route: PublicForumRoute }
  | { app: 'docs'; route: PublicDocsRoute };

function parsePublicRoute(): PublicRoute {
  if (
    window.location.pathname === '/docs'
    || window.location.pathname.startsWith('/docs/')
    || window.location.pathname === '/__documents__'
    || window.location.pathname.startsWith('/__documents__/')
  ) {
    return {
      app: 'docs',
      route: parsePublicDocsRoute(window.location.pathname, window.location.hash)
    };
  }

  return {
    app: 'forum',
    route: parsePublicForumRoute(window.location.pathname, window.location.search)
  };
}

function buildPublicPath(nextRoute: PublicRoute): string {
  return nextRoute.app === 'docs'
    ? buildPublicDocsPath(nextRoute.route)
    : buildPublicForumPath(nextRoute.route);
}

export const PublicEntry = () => {
  const [route, setRoute] = useState<PublicRoute>(() => parsePublicRoute());
  const [lastListRoute, setLastListRoute] = useState<PublicForumListRoute>(() => {
    const parsedRoute = parsePublicForumRoute(window.location.pathname, window.location.search);
    return parsedRoute.kind === 'list' ? parsedRoute : createDefaultListRoute();
  });
  const [lastDocsListRoute, setLastDocsListRoute] = useState<PublicDocsListRoute>(() => {
    const parsedRoute = parsePublicDocsRoute(window.location.pathname, window.location.hash);
    return parsedRoute.kind === 'list' ? parsedRoute : createDefaultDocsListRoute();
  });

  useEffect(() => {
    const handlePopState = () => {
      const nextRoute = parsePublicRoute();
      setRoute(nextRoute);
      if (nextRoute.app === 'forum' && nextRoute.route.kind === 'list') {
        setLastListRoute(nextRoute.route);
      }
      if (nextRoute.app === 'docs' && nextRoute.route.kind === 'list') {
        setLastDocsListRoute(nextRoute.route);
      }
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  const navigateToRoute = useCallback((nextRoute: PublicRoute, options?: { replace?: boolean }) => {
    const nextPath = buildPublicPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (options?.replace) {
      window.history.replaceState({}, '', nextPath);
    } else if (currentPath !== nextPath) {
      window.history.pushState({}, '', nextPath);
    }

    if (nextRoute.app === 'forum' && nextRoute.route.kind === 'list') {
      setLastListRoute(nextRoute.route);
    }
    if (nextRoute.app === 'docs' && nextRoute.route.kind === 'list') {
      setLastDocsListRoute(nextRoute.route);
    }
    setRoute(nextRoute);
  }, []);

  return route.app === 'docs' ? (
    <PublicDocsApp
      route={route.route}
      fallbackListRoute={lastDocsListRoute}
      onNavigate={(nextRoute, options) => navigateToRoute({ app: 'docs', route: nextRoute }, options)}
    />
  ) : (
    <PublicForumApp
      route={route.route}
      fallbackListRoute={lastListRoute}
      onNavigate={(nextRoute, options) => navigateToRoute({ app: 'forum', route: nextRoute }, options)}
    />
  );
};
