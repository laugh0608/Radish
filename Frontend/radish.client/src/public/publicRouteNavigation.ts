import type { PublicDiscoverRoute } from './discoverRouteState';
import type { PublicDocsBrowseRoute, PublicDocsRoute } from './docsRouteState';
import type { PublicForumBrowseRoute, PublicForumRoute } from './forumRouteState';
import type { PublicLeaderboardRoute } from './leaderboardRouteState';
import type { PublicProfileRoute } from './profileRouteState';
import type { PublicShopRoute } from './shopRouteState';

export type PublicRouteDescriptor =
  | { app: 'discover'; route: PublicDiscoverRoute }
  | { app: 'forum'; route: PublicForumRoute }
  | { app: 'docs'; route: PublicDocsRoute }
  | { app: 'profile'; route: PublicProfileRoute }
  | { app: 'leaderboard'; route: PublicLeaderboardRoute }
  | { app: 'shop'; route: PublicShopRoute };

export type PublicDetailBackMode = 'discover' | 'source';

function isForumBrowseDescriptor(
  route: PublicRouteDescriptor | null
): route is { app: 'forum'; route: PublicForumBrowseRoute } {
  return !!route && route.app === 'forum' && route.route.kind !== 'detail';
}

function isDocsBrowseDescriptor(
  route: PublicRouteDescriptor | null
): route is { app: 'docs'; route: PublicDocsBrowseRoute } {
  return !!route && route.app === 'docs' && route.route.kind !== 'detail';
}

function resolveBackMode(route: PublicRouteDescriptor | null): PublicDetailBackMode | null {
  if (!route) {
    return null;
  }

  return route.app === 'discover' ? 'discover' : 'source';
}

export function shouldCaptureForumDetailSource(
  currentRoute: PublicRouteDescriptor,
  nextRoute: PublicRouteDescriptor
): boolean {
  if (nextRoute.app !== 'forum' || nextRoute.route.kind !== 'detail') {
    return false;
  }

  return currentRoute.app !== 'forum'
    || currentRoute.route.kind !== 'detail'
    || currentRoute.route.postId !== nextRoute.route.postId;
}

export function shouldCaptureDocsDetailSource(
  currentRoute: PublicRouteDescriptor,
  nextRoute: PublicRouteDescriptor
): boolean {
  if (nextRoute.app !== 'docs' || nextRoute.route.kind !== 'detail') {
    return false;
  }

  return currentRoute.app !== 'docs'
    || currentRoute.route.kind !== 'detail'
    || currentRoute.route.slug !== nextRoute.route.slug
    || currentRoute.route.anchor !== nextRoute.route.anchor;
}

export function shouldCaptureProfileDetailSource(
  currentRoute: PublicRouteDescriptor,
  nextRoute: PublicRouteDescriptor
): boolean {
  if (nextRoute.app !== 'profile') {
    return false;
  }

  return currentRoute.app !== 'profile'
    || currentRoute.route.userId !== nextRoute.route.userId;
}

export function shouldCommitPublicRouteUpdate(
  currentRoute: PublicRouteDescriptor,
  nextRoute: PublicRouteDescriptor,
  currentPath: string,
  nextPath: string
): boolean {
  if (currentPath !== nextPath) {
    return true;
  }

  return currentRoute.app !== nextRoute.app;
}

export function resolveForumDetailBackMode(sourceRoute: PublicRouteDescriptor | null): PublicDetailBackMode | null {
  if (!sourceRoute || isForumBrowseDescriptor(sourceRoute)) {
    return null;
  }

  return resolveBackMode(sourceRoute);
}

export function resolveDocsDetailBackMode(sourceRoute: PublicRouteDescriptor | null): PublicDetailBackMode | null {
  if (!sourceRoute || isDocsBrowseDescriptor(sourceRoute)) {
    return null;
  }

  return resolveBackMode(sourceRoute);
}

export function resolveProfileBackMode(sourceRoute: PublicRouteDescriptor | null): PublicDetailBackMode | null {
  return resolveBackMode(sourceRoute);
}
