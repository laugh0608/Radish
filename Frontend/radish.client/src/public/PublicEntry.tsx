import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiBaseUrl } from '@/config/env';
import { bootstrapAuth } from '@/services/authBootstrap';
import { PublicForumApp } from './forum/PublicForumApp';
import { PublicDocsApp } from './docs/PublicDocsApp';
import { PublicProfileApp } from './profile/PublicProfileApp';
import { PublicLeaderboardApp } from './leaderboard/PublicLeaderboardApp';
import { PublicShopApp } from './shop/PublicShopApp';
import {
  buildPublicForumPath,
  createDefaultListRoute,
  createDefaultSearchRoute,
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
import {
  buildPublicLeaderboardPath,
  parsePublicLeaderboardRoute,
} from './leaderboardRouteState';
import {
  buildPublicShopPath,
  createDefaultPublicShopProductsRoute,
  parsePublicShopRoute,
} from './shopRouteState';
import type {
  PublicForumBrowseRoute,
  PublicForumRoute,
} from './forumRouteState';
import type {
  PublicDocsBrowseRoute,
  PublicDocsRoute,
} from './docsRouteState';
import type {
  PublicProfileRoute,
} from './profileRouteState';
import type {
  PublicLeaderboardRoute,
} from './leaderboardRouteState';
import type {
  PublicShopProductsRoute,
  PublicShopRoute,
} from './shopRouteState';
export type {
  PublicListSort,
} from './forumRouteState';

type PublicRoute =
  | { app: 'forum'; route: PublicForumRoute }
  | { app: 'docs'; route: PublicDocsRoute }
  | { app: 'profile'; route: PublicProfileRoute }
  | { app: 'leaderboard'; route: PublicLeaderboardRoute }
  | { app: 'shop'; route: PublicShopRoute };

function parsePublicRoute(): PublicRoute {
  const profileRoute = parsePublicProfileRoute(window.location.pathname, window.location.search);
  if (profileRoute) {
    return {
      app: 'profile',
      route: profileRoute
    };
  }

  if (window.location.pathname === '/leaderboard' || window.location.pathname.startsWith('/leaderboard/')) {
    return {
      app: 'leaderboard',
      route: parsePublicLeaderboardRoute(window.location.pathname, window.location.search)
    };
  }

  if (window.location.pathname === '/shop' || window.location.pathname.startsWith('/shop/')) {
    return {
      app: 'shop',
      route: parsePublicShopRoute(window.location.pathname, window.location.search)
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
      route: parsePublicDocsRoute(window.location.pathname, window.location.search, window.location.hash)
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

  if (nextRoute.app === 'leaderboard') {
    return buildPublicLeaderboardPath(nextRoute.route);
  }

  if (nextRoute.app === 'shop') {
    return buildPublicShopPath(nextRoute.route);
  }

  return nextRoute.app === 'docs'
    ? buildPublicDocsPath(nextRoute.route)
    : buildPublicForumPath(nextRoute.route);
}

export const PublicEntry = () => {
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const [route, setRoute] = useState<PublicRoute>(() => parsePublicRoute());
  const [lastForumBrowseRoute, setLastForumBrowseRoute] = useState<PublicForumBrowseRoute>(() => {
    const parsedRoute = parsePublicForumRoute(window.location.pathname, window.location.search);
    return parsedRoute.kind === 'detail' ? createDefaultListRoute() : parsedRoute;
  });
  const [lastDocsBrowseRoute, setLastDocsBrowseRoute] = useState<PublicDocsBrowseRoute>(() => {
    const parsedRoute = parsePublicDocsRoute(window.location.pathname, window.location.search, window.location.hash);
    return parsedRoute.kind === 'detail' ? createDefaultDocsListRoute() : parsedRoute;
  });
  const [lastShopProductsRoute, setLastShopProductsRoute] = useState<PublicShopProductsRoute>(() => {
    const parsedRoute = parsePublicShopRoute(window.location.pathname, window.location.search);
    return parsedRoute.kind === 'products' ? parsedRoute : createDefaultPublicShopProductsRoute();
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
      if (nextRoute.app === 'forum' && nextRoute.route.kind !== 'detail') {
        setLastForumBrowseRoute(nextRoute.route);
      }
      if (nextRoute.app === 'docs' && nextRoute.route.kind !== 'detail') {
        setLastDocsBrowseRoute(nextRoute.route);
      }
      if (nextRoute.app === 'shop' && nextRoute.route.kind === 'products') {
        setLastShopProductsRoute(nextRoute.route);
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

    if (nextRoute.app === 'forum' && nextRoute.route.kind !== 'detail') {
      setLastForumBrowseRoute(nextRoute.route);
    }
    if (nextRoute.app === 'docs' && nextRoute.route.kind !== 'detail') {
      setLastDocsBrowseRoute(nextRoute.route);
    }
    if (nextRoute.app === 'shop' && nextRoute.route.kind === 'products') {
      setLastShopProductsRoute(nextRoute.route);
    }
    setRoute(nextRoute);
  }, []);

  const navigateToDocsRoute = useCallback((nextRoute: PublicDocsRoute, options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'docs', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToForumRoute = useCallback((nextRoute: PublicForumRoute, options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'forum', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToProfileRoute = useCallback((nextRoute: PublicProfileRoute, options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'profile', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToLeaderboardRoute = useCallback((nextRoute: PublicLeaderboardRoute, options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'leaderboard', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToShopRoute = useCallback((nextRoute: PublicShopRoute, options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'shop', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToDefaultForumList = useCallback(() => {
    navigateToForumRoute(createDefaultListRoute());
  }, [navigateToForumRoute]);

  const navigateToPublicForumSearch = useCallback((keyword: string = '') => {
    navigateToForumRoute({
      ...createDefaultSearchRoute(),
      keyword: keyword.trim()
    });
  }, [navigateToForumRoute]);

  const navigateToForumPost = useCallback((postId: string) => {
    navigateToForumRoute({ kind: 'detail', postId });
  }, [navigateToForumRoute]);

  const navigateToProfileFromForum = useCallback((userId: string) => {
    navigateToProfileRoute({ kind: 'detail', userId, tab: 'posts', page: 1 });
  }, [navigateToProfileRoute]);

  return route.app === 'leaderboard' ? (
    <PublicLeaderboardApp
      route={route.route}
      onNavigate={navigateToLeaderboardRoute}
      onNavigateToProfile={(userId) => navigateToProfileRoute({ kind: 'detail', userId, tab: 'posts', page: 1 })}
    />
  ) : route.app === 'shop' ? (
    <PublicShopApp
      route={route.route}
      fallbackProductsRoute={lastShopProductsRoute}
      onNavigate={navigateToShopRoute}
    />
  ) : route.app === 'docs' ? (
    <PublicDocsApp
      route={route.route}
      fallbackBrowseRoute={lastDocsBrowseRoute}
      onNavigate={navigateToDocsRoute}
    />
  ) : route.app === 'profile' ? (
    <PublicProfileApp
      route={route.route}
      onNavigate={navigateToProfileRoute}
      onNavigateToForumList={navigateToDefaultForumList}
      onNavigateToForumPost={navigateToForumPost}
    />
  ) : (
    <PublicForumApp
      route={route.route}
      fallbackBrowseRoute={lastForumBrowseRoute}
      onNavigate={navigateToForumRoute}
      onNavigateToProfile={navigateToProfileFromForum}
      onNavigateToSearch={navigateToPublicForumSearch}
    />
  );
};
