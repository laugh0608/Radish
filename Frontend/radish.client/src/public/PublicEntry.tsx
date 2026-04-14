import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiBaseUrl } from '@/config/env';
import { bootstrapAuth } from '@/services/authBootstrap';
import { PublicDiscoverApp } from './discover/PublicDiscoverApp';
import { PublicForumApp } from './forum/PublicForumApp';
import { PublicDocsApp } from './docs/PublicDocsApp';
import { PublicProfileApp } from './profile/PublicProfileApp';
import { PublicLeaderboardApp } from './leaderboard/PublicLeaderboardApp';
import { PublicShopApp } from './shop/PublicShopApp';
import {
  buildPublicDiscoverPath,
  createDefaultPublicDiscoverRoute,
  parsePublicDiscoverRoute,
} from './discoverRouteState';
import {
  buildPublicForumPath,
  createDefaultListRoute,
  createDefaultSearchRoute,
  createDefaultTypeRoute,
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
import {
  resolveDocsDetailBackMode,
  resolveForumDetailBackMode,
  resolveProfileBackMode,
  shouldCommitPublicRouteUpdate,
  shouldCaptureDocsDetailSource,
  shouldCaptureForumDetailSource,
  shouldCaptureProfileDetailSource,
  type PublicDetailBackMode,
  type PublicRouteDescriptor,
} from './publicRouteNavigation';
export type {
  PublicListSort,
} from './forumRouteState';

interface PublicBackAction {
  mode: PublicDetailBackMode;
  onBack: () => void;
}

function parsePublicRoute(): PublicRouteDescriptor {
  const discoverRoute = parsePublicDiscoverRoute(window.location.pathname);
  if (discoverRoute) {
    return {
      app: 'discover',
      route: discoverRoute
    };
  }

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

function buildPublicPath(nextRoute: PublicRouteDescriptor): string {
  if (nextRoute.app === 'discover') {
    return buildPublicDiscoverPath(nextRoute.route);
  }

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
  const [route, setRoute] = useState<PublicRouteDescriptor>(() => parsePublicRoute());
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
  const [forumDetailSourceRoute, setForumDetailSourceRoute] = useState<PublicRouteDescriptor | null>(null);
  const [docsDetailSourceRoute, setDocsDetailSourceRoute] = useState<PublicRouteDescriptor | null>(null);
  const [profileSourceRoute, setProfileSourceRoute] = useState<PublicRouteDescriptor | null>(null);

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

  const navigateToRoute = useCallback((nextRoute: PublicRouteDescriptor, options?: { replace?: boolean }) => {
    const nextPath = buildPublicPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;

    if (!shouldCommitPublicRouteUpdate(route, nextRoute, currentPath, nextPath)) {
      return;
    }

    if (shouldCaptureForumDetailSource(route, nextRoute)) {
      setForumDetailSourceRoute(route);
    }
    if (shouldCaptureDocsDetailSource(route, nextRoute)) {
      setDocsDetailSourceRoute(route);
    }
    if (shouldCaptureProfileDetailSource(route, nextRoute)) {
      setProfileSourceRoute(route);
    }

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
  }, [route]);

  const navigateToDocsRoute = useCallback((nextRoute: PublicDocsRoute, options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'docs', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToDiscoverRoute = useCallback((options?: { replace?: boolean }) => {
    navigateToRoute({ app: 'discover', route: createDefaultPublicDiscoverRoute() }, options);
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

  const navigateToForumPost = useCallback((postId: string, commentId?: string) => {
    navigateToForumRoute(commentId
      ? { kind: 'detail', postId, commentId }
      : { kind: 'detail', postId });
  }, [navigateToForumRoute]);

  const navigateToForumTag = useCallback((tagSlug: string) => {
    navigateToForumRoute({ kind: 'tag', tagSlug, sortBy: 'newest', page: 1 });
  }, [navigateToForumRoute]);

  const navigateToForumQuestion = useCallback(() => {
    navigateToForumRoute(createDefaultTypeRoute('question'));
  }, [navigateToForumRoute]);

  const navigateToForumPoll = useCallback(() => {
    navigateToForumRoute(createDefaultTypeRoute('poll'));
  }, [navigateToForumRoute]);

  const navigateToForumLottery = useCallback(() => {
    navigateToForumRoute(createDefaultTypeRoute('lottery'));
  }, [navigateToForumRoute]);

  const navigateToProfileFromForum = useCallback((userId: string) => {
    navigateToProfileRoute({ kind: 'detail', userId, tab: 'posts', page: 1 });
  }, [navigateToProfileRoute]);

  const forumDetailBackAction = useMemo<PublicBackAction | null>(() => {
    const mode = resolveForumDetailBackMode(forumDetailSourceRoute);
    if (!mode || !forumDetailSourceRoute) {
      return null;
    }

    return {
      mode,
      onBack: () => navigateToRoute(forumDetailSourceRoute)
    };
  }, [forumDetailSourceRoute, navigateToRoute]);

  const docsDetailBackAction = useMemo<PublicBackAction | null>(() => {
    const mode = resolveDocsDetailBackMode(docsDetailSourceRoute);
    if (!mode || !docsDetailSourceRoute) {
      return null;
    }

    return {
      mode,
      onBack: () => navigateToRoute(docsDetailSourceRoute)
    };
  }, [docsDetailSourceRoute, navigateToRoute]);

  const profileBackAction = useMemo<PublicBackAction | null>(() => {
    const mode = resolveProfileBackMode(profileSourceRoute);
    if (!mode || !profileSourceRoute) {
      return null;
    }

    return {
      mode,
      onBack: () => navigateToRoute(profileSourceRoute)
    };
  }, [navigateToRoute, profileSourceRoute]);

  return route.app === 'discover' ? (
    <PublicDiscoverApp
      onNavigateToForum={navigateToForumRoute}
      onNavigateToDocs={navigateToDocsRoute}
      onNavigateToLeaderboard={navigateToLeaderboardRoute}
      onNavigateToShop={navigateToShopRoute}
    />
  ) : route.app === 'leaderboard' ? (
    <PublicLeaderboardApp
      route={route.route}
      onNavigate={navigateToLeaderboardRoute}
      onNavigateToDiscover={navigateToDiscoverRoute}
      onNavigateToProfile={(userId) => navigateToProfileRoute({ kind: 'detail', userId, tab: 'posts', page: 1 })}
    />
  ) : route.app === 'shop' ? (
    <PublicShopApp
      route={route.route}
      fallbackProductsRoute={lastShopProductsRoute}
      onNavigate={navigateToShopRoute}
      onNavigateToDiscover={navigateToDiscoverRoute}
    />
  ) : route.app === 'docs' ? (
    <PublicDocsApp
      route={route.route}
      fallbackBrowseRoute={lastDocsBrowseRoute}
      detailBackAction={docsDetailBackAction}
      onNavigate={navigateToDocsRoute}
      onNavigateToDiscover={navigateToDiscoverRoute}
    />
  ) : route.app === 'profile' ? (
    <PublicProfileApp
      route={route.route}
      backAction={profileBackAction}
      onNavigate={navigateToProfileRoute}
      onNavigateToDiscover={navigateToDiscoverRoute}
      onNavigateToForumList={navigateToDefaultForumList}
      onNavigateToForumPost={navigateToForumPost}
    />
  ) : (
    <PublicForumApp
      route={route.route}
      fallbackBrowseRoute={lastForumBrowseRoute}
      detailBackAction={forumDetailBackAction}
      onNavigate={navigateToForumRoute}
      onNavigateToDiscover={navigateToDiscoverRoute}
      onNavigateToProfile={navigateToProfileFromForum}
      onNavigateToSearch={navigateToPublicForumSearch}
      onNavigateToTag={navigateToForumTag}
      onNavigateToQuestion={navigateToForumQuestion}
      onNavigateToPoll={navigateToForumPoll}
      onNavigateToLottery={navigateToForumLottery}
    />
  );
};
