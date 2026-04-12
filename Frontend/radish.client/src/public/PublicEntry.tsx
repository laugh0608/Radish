import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiBaseUrl } from '@/config/env';
import { bootstrapAuth } from '@/services/authBootstrap';
import { PublicForumApp } from './forum/PublicForumApp';
import { PublicDocsApp } from './docs/PublicDocsApp';
import { PublicProfileApp } from './profile/PublicProfileApp';
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
import {
  buildPublicProfilePath,
  parsePublicProfileRoute,
} from './profileRouteState';
import type {
  PublicForumListRoute,
  PublicForumRoute,
} from './forumRouteState';
import type {
  PublicDocsListRoute,
  PublicDocsRoute,
} from './docsRouteState';
import type {
  PublicProfileRoute,
} from './profileRouteState';
export type {
  PublicListSort,
} from './forumRouteState';

type PublicRoute =
  | { app: 'forum'; route: PublicForumRoute }
  | { app: 'docs'; route: PublicDocsRoute }
  | { app: 'profile'; route: PublicProfileRoute };

function parsePublicRoute(): PublicRoute {
  const profileRoute = parsePublicProfileRoute(window.location.pathname, window.location.search);
  if (profileRoute) {
    return {
      app: 'profile',
      route: profileRoute
    };
  }

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
  if (nextRoute.app === 'profile') {
    return buildPublicProfilePath(nextRoute.route);
  }

  return nextRoute.app === 'docs'
    ? buildPublicDocsPath(nextRoute.route)
    : buildPublicForumPath(nextRoute.route);
}

export const PublicEntry = () => {
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
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
    const cleanup = bootstrapAuth({ apiBaseUrl });
    return () => {
      cleanup();
    };
  }, [apiBaseUrl]);

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
  ) : route.app === 'profile' ? (
    <PublicProfileApp
      route={route.route}
      onNavigate={(nextRoute, options) => navigateToRoute({ app: 'profile', route: nextRoute }, options)}
      onNavigateToForumList={() => navigateToRoute({ app: 'forum', route: createDefaultListRoute() })}
      onNavigateToForumPost={(postId) => navigateToRoute({ app: 'forum', route: { kind: 'detail', postId } })}
    />
  ) : (
    <PublicForumApp
      route={route.route}
      fallbackListRoute={lastListRoute}
      onNavigate={(nextRoute, options) => navigateToRoute({ app: 'forum', route: nextRoute }, options)}
      onNavigateToProfile={(userId) => navigateToRoute({ app: 'profile', route: { kind: 'detail', userId, tab: 'posts', page: 1 } })}
    />
  );
};
