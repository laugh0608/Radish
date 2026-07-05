import { useCallback, useEffect, useMemo, useState } from 'react';
import { getApiBaseUrl } from '@/config/env';
import { bootstrapAuth } from '@/services/authBootstrap';
import { buildCirclePath } from '@/circle/circleRouteState';
import { buildMessagesPath } from '@/messages/messagesRouteState';
import { PublicDiscoverApp } from './discover/PublicDiscoverApp';
import { PublicForumApp } from './forum/PublicForumApp';
import { PublicDocsApp } from './docs/PublicDocsApp';
import { PublicProfileApp } from './profile/PublicProfileApp';
import { PublicLeaderboardApp } from './leaderboard/PublicLeaderboardApp';
import { PublicCommitmentsApp } from './legal/PublicCommitmentsApp';
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
  buildPublicLegalPath,
  parsePublicLegalRoute,
} from './legalRouteState';
import {
  buildPublicShopPath,
  createDefaultPublicShopProductsRoute,
  parsePublicShopRoute,
} from './shopRouteState';
import type {
  PublicDiscoverRoute,
} from './discoverRouteState';
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
  consumePublicRouteSourceTransfer,
  createPublicRouteSourceState,
  resolveDocsDetailBackMode,
  resolveForumDetailBackMode,
  resolveProfileBackMode,
  resolveShopDetailBackMode,
  shouldCommitPublicRouteUpdate,
  type PublicContentRouteDescriptor,
  type PublicDetailBackMode,
  type PublicRouteDescriptor,
  type PublicRouteSourceState,
} from './publicRouteNavigation';
import { applyPublicHead, buildPublicRouteHead } from './publicHead';
import {
  applyPublicStructuredData,
  buildPublicRouteStructuredData,
  removePublicStructuredData,
} from './publicStructuredData';
export type {
  PublicListSort,
} from './forumRouteState';

const PUBLIC_ROUTE_SOURCE_STATE_KEY = 'radishPublicRouteSource';

interface PublicBackAction {
  mode: PublicDetailBackMode;
  href?: string;
  onBack: () => void;
}

interface PublicNavigateOptions {
  replace?: boolean;
  preserveSourceState?: boolean;
}

function readPublicRouteSourceState(): PublicRouteSourceState {
  const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  const transferState = consumePublicRouteSourceTransfer(currentPath);
  if (transferState) {
    return transferState;
  }

  const historyState = window.history.state;
  if (!historyState || typeof historyState !== 'object') {
    return {};
  }

  const sourceState = (historyState as Record<string, unknown>)[PUBLIC_ROUTE_SOURCE_STATE_KEY];
  return sourceState && typeof sourceState === 'object'
    ? sourceState as PublicRouteSourceState
    : {};
}

function buildPublicHistoryState(sourceState: PublicRouteSourceState): Record<string, unknown> {
  const historyState = window.history.state;
  const nextHistoryState = historyState && typeof historyState === 'object'
    ? { ...(historyState as Record<string, unknown>) }
    : {};

  nextHistoryState[PUBLIC_ROUTE_SOURCE_STATE_KEY] = sourceState;
  return nextHistoryState;
}

function parsePublicRoute(): PublicContentRouteDescriptor {
  const discoverRoute = parsePublicDiscoverRoute(window.location.pathname, window.location.search);
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

  const legalRoute = parsePublicLegalRoute(window.location.pathname);
  if (legalRoute) {
    return {
      app: 'legal',
      route: legalRoute
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
  if (nextRoute.app === 'circle') {
    return buildCirclePath(nextRoute.route);
  }

  if (nextRoute.app === 'notifications') {
    return '/notifications';
  }

  if (nextRoute.app === 'me') {
    return '/me';
  }

  if (nextRoute.app === 'messages') {
    return buildMessagesPath(nextRoute.route);
  }

  if (nextRoute.app === 'discover') {
    return buildPublicDiscoverPath(nextRoute.route);
  }

  if (nextRoute.app === 'profile') {
    return buildPublicProfilePath(nextRoute.route);
  }

  if (nextRoute.app === 'leaderboard') {
    return buildPublicLeaderboardPath(nextRoute.route);
  }

  if (nextRoute.app === 'legal') {
    return buildPublicLegalPath(nextRoute.route);
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
  const [route, setRoute] = useState<PublicContentRouteDescriptor>(() => parsePublicRoute());
  const [lastDiscoverRoute, setLastDiscoverRoute] = useState<PublicDiscoverRoute>(() => {
    const parsedRoute = parsePublicDiscoverRoute(window.location.pathname, window.location.search);
    return parsedRoute ?? createDefaultPublicDiscoverRoute();
  });
  const [lastForumBrowseRoute, setLastForumBrowseRoute] = useState<PublicForumBrowseRoute>(() => {
    const parsedRoute = parsePublicForumRoute(window.location.pathname, window.location.search);
    return parsedRoute.kind === 'detail' || parsedRoute.kind === 'compose' ? createDefaultListRoute() : parsedRoute;
  });
  const [lastDocsBrowseRoute, setLastDocsBrowseRoute] = useState<PublicDocsBrowseRoute>(() => {
    const parsedRoute = parsePublicDocsRoute(window.location.pathname, window.location.search, window.location.hash);
    return parsedRoute.kind === 'detail' ? createDefaultDocsListRoute() : parsedRoute;
  });
  const [lastShopProductsRoute, setLastShopProductsRoute] = useState<PublicShopProductsRoute>(() => {
    const parsedRoute = parsePublicShopRoute(window.location.pathname, window.location.search);
    return parsedRoute.kind === 'products' ? parsedRoute : createDefaultPublicShopProductsRoute();
  });
  const [routeSourceState, setRouteSourceState] = useState<PublicRouteSourceState>(() => readPublicRouteSourceState());

  useEffect(() => {
    window.history.replaceState(buildPublicHistoryState(routeSourceState), '');
  }, [routeSourceState]);

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    return () => {
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    const canonicalPath = buildPublicPath(route);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath === canonicalPath) {
      return;
    }

    window.history.replaceState(window.history.state, '', canonicalPath);
  }, [route]);

  useEffect(() => {
    applyPublicHead(buildPublicRouteHead(route));
  }, [route]);

  useEffect(() => {
    applyPublicStructuredData(buildPublicRouteStructuredData(route));
    return removePublicStructuredData;
  }, [route]);

  useEffect(() => {
    const handlePopState = () => {
      const nextRoute = parsePublicRoute();
      setRoute(nextRoute);
      setRouteSourceState(readPublicRouteSourceState());
      if (nextRoute.app === 'discover') {
        setLastDiscoverRoute(nextRoute.route);
      }
      if (nextRoute.app === 'forum' && nextRoute.route.kind !== 'detail' && nextRoute.route.kind !== 'compose') {
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

  const navigateToRoute = useCallback((nextRoute: PublicRouteDescriptor, options?: PublicNavigateOptions) => {
    const currentRoute = parsePublicRoute();
    const nextPath = buildPublicPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;

    if (
      nextRoute.app === 'circle'
      || nextRoute.app === 'me'
      || nextRoute.app === 'messages'
      || nextRoute.app === 'notifications'
    ) {
      window.location.href = nextPath;
      return;
    }

    if (!shouldCommitPublicRouteUpdate(currentRoute, nextRoute, currentPath, nextPath)) {
      return;
    }

    const nextRouteSourceState = createPublicRouteSourceState(
      readPublicRouteSourceState(),
      currentRoute,
      nextRoute,
      { preserveExisting: options?.preserveSourceState }
    );
    const nextHistoryState = buildPublicHistoryState(nextRouteSourceState);

    if (options?.replace) {
      window.history.replaceState(nextHistoryState, '', nextPath);
    } else if (currentPath !== nextPath) {
      window.history.pushState(nextHistoryState, '', nextPath);
    }

    if (nextRoute.app === 'discover') {
      setLastDiscoverRoute(nextRoute.route);
    }
    if (nextRoute.app === 'forum' && nextRoute.route.kind !== 'detail' && nextRoute.route.kind !== 'compose') {
      setLastForumBrowseRoute(nextRoute.route);
    }
    if (nextRoute.app === 'docs' && nextRoute.route.kind !== 'detail') {
      setLastDocsBrowseRoute(nextRoute.route);
    }
    if (nextRoute.app === 'shop' && nextRoute.route.kind === 'products') {
      setLastShopProductsRoute(nextRoute.route);
    }
    setRouteSourceState(nextRouteSourceState);
    setRoute(nextRoute);
  }, []);

  const navigateToDocsRoute = useCallback((nextRoute: PublicDocsRoute, options?: PublicNavigateOptions) => {
    navigateToRoute({ app: 'docs', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToDiscoverRoute = useCallback((nextRoute?: PublicDiscoverRoute, options?: PublicNavigateOptions) => {
    navigateToRoute({ app: 'discover', route: nextRoute ?? lastDiscoverRoute }, options);
  }, [lastDiscoverRoute, navigateToRoute]);

  const navigateToForumRoute = useCallback((nextRoute: PublicForumRoute, options?: PublicNavigateOptions) => {
    navigateToRoute({ app: 'forum', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToProfileRoute = useCallback((nextRoute: PublicProfileRoute, options?: PublicNavigateOptions) => {
    navigateToRoute({ app: 'profile', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToLeaderboardRoute = useCallback((nextRoute: PublicLeaderboardRoute, options?: PublicNavigateOptions) => {
    navigateToRoute({ app: 'leaderboard', route: nextRoute }, options);
  }, [navigateToRoute]);

  const navigateToShopRoute = useCallback((nextRoute: PublicShopRoute, options?: PublicNavigateOptions) => {
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
    const mode = resolveForumDetailBackMode(routeSourceState.forumDetailSourceRoute ?? null);
    if (!mode || !routeSourceState.forumDetailSourceRoute) {
      return null;
    }

    return {
      mode,
      href: buildPublicPath(routeSourceState.forumDetailSourceRoute),
      onBack: () => navigateToRoute(routeSourceState.forumDetailSourceRoute!, { preserveSourceState: true })
    };
  }, [navigateToRoute, routeSourceState.forumDetailSourceRoute]);

  const docsDetailBackAction = useMemo<PublicBackAction | null>(() => {
    const mode = resolveDocsDetailBackMode(routeSourceState.docsDetailSourceRoute ?? null);
    if (!mode || !routeSourceState.docsDetailSourceRoute) {
      return null;
    }

    return {
      mode,
      href: buildPublicPath(routeSourceState.docsDetailSourceRoute),
      onBack: () => navigateToRoute(routeSourceState.docsDetailSourceRoute!, { preserveSourceState: true })
    };
  }, [navigateToRoute, routeSourceState.docsDetailSourceRoute]);

  const profileBackAction = useMemo<PublicBackAction | null>(() => {
    const mode = resolveProfileBackMode(routeSourceState.profileSourceRoute ?? null);
    if (!mode || !routeSourceState.profileSourceRoute) {
      return null;
    }

    return {
      mode,
      href: buildPublicPath(routeSourceState.profileSourceRoute),
      onBack: () => navigateToRoute(routeSourceState.profileSourceRoute!, { preserveSourceState: true })
    };
  }, [navigateToRoute, routeSourceState.profileSourceRoute]);

  const shopDetailBackAction = useMemo<PublicBackAction | null>(() => {
    const mode = resolveShopDetailBackMode(routeSourceState.shopDetailSourceRoute ?? null);
    if (!mode || !routeSourceState.shopDetailSourceRoute) {
      return null;
    }

    return {
      mode,
      href: buildPublicPath(routeSourceState.shopDetailSourceRoute),
      onBack: () => navigateToRoute(routeSourceState.shopDetailSourceRoute!, { preserveSourceState: true })
    };
  }, [navigateToRoute, routeSourceState.shopDetailSourceRoute]);

  return route.app === 'discover' ? (
    <PublicDiscoverApp
      route={route.route}
      onNavigate={navigateToDiscoverRoute}
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
      onNavigateToShopProduct={(productId) => navigateToShopRoute({ kind: 'detail', productId })}
    />
  ) : route.app === 'legal' ? (
    <PublicCommitmentsApp
      onNavigateToDiscover={() => navigateToDiscoverRoute()}
    />
  ) : route.app === 'shop' ? (
    <PublicShopApp
      route={route.route}
      fallbackProductsRoute={lastShopProductsRoute}
      detailBackAction={shopDetailBackAction}
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
      routeSourceState={routeSourceState}
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
