import type { PublicDiscoverRoute } from './discoverRouteState';
import type { PublicDocsBrowseRoute, PublicDocsRoute } from './docsRouteState';
import type { PublicForumBrowseRoute, PublicForumRoute } from './forumRouteState';
import type { PublicLeaderboardRoute } from './leaderboardRouteState';
import type { PublicProfileRoute } from './profileRouteState';
import type { PublicShopRoute } from './shopRouteState';
import type { CircleRoute } from '../circle/circleRouteState';

const PUBLIC_ROUTE_SOURCE_TRANSFER_STORAGE_KEY = 'radish:public-route-source-transfer';

export type PublicRouteDescriptor =
  | { app: 'discover'; route: PublicDiscoverRoute }
  | { app: 'forum'; route: PublicForumRoute }
  | { app: 'docs'; route: PublicDocsRoute }
  | { app: 'profile'; route: PublicProfileRoute }
  | { app: 'leaderboard'; route: PublicLeaderboardRoute }
  | { app: 'shop'; route: PublicShopRoute }
  | { app: 'circle'; route: CircleRoute }
  | { app: 'me'; route: { kind: 'index' } }
  | { app: 'notifications'; route: { kind: 'index' } };

export type PublicContentRouteDescriptor = Exclude<
  PublicRouteDescriptor,
  { app: 'circle' } | { app: 'me' } | { app: 'notifications' }
>;

export type PublicDetailBackMode =
  | 'discover'
  | 'source'
  | 'forum'
  | 'docs'
  | 'profile'
  | 'leaderboard'
  | 'shop'
  | 'shopProducts'
  | 'circle'
  | 'me'
  | 'notifications';

export interface PublicRouteSourceState {
  forumDetailSourceRoute?: PublicRouteDescriptor | null;
  docsDetailSourceRoute?: PublicRouteDescriptor | null;
  profileSourceRoute?: PublicRouteDescriptor | null;
  shopDetailSourceRoute?: PublicRouteDescriptor | null;
}

export interface PublicRouteSourceStateOptions {
  preserveExisting?: boolean;
}

interface PublicRouteSourceTransfer {
  targetPath: string;
  sourceState: PublicRouteSourceState;
}

function getSessionStorage(): Storage | null {
  if (typeof window === 'undefined') {
    return null;
  }

  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

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

  if (route.app === 'discover') {
    return 'discover';
  }

  if (route.app === 'shop') {
    return route.route.kind === 'products' ? 'shopProducts' : 'shop';
  }

  return route.app;
}

export function getPublicDetailBackLabelKey(mode: PublicDetailBackMode | null | undefined): string | null {
  switch (mode) {
    case 'discover':
      return 'public.shell.backToDiscover';
    case 'source':
      return 'public.shell.backToSource';
    case 'forum':
      return 'public.shell.backToForum';
    case 'docs':
      return 'public.shell.backToDocs';
    case 'profile':
      return 'public.shell.backToProfile';
    case 'leaderboard':
      return 'public.shell.backToLeaderboard';
    case 'shop':
      return 'public.shell.backToShop';
    case 'shopProducts':
      return 'public.shell.backToShopProducts';
    case 'circle':
      return 'public.shell.backToCircle';
    case 'me':
      return 'public.shell.backToMe';
    case 'notifications':
      return 'public.shell.backToNotifications';
    default:
      return null;
  }
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function normalizePublicPath(value: string | null | undefined): string | null {
  const trimmed = value?.trim();
  if (!trimmed || !trimmed.startsWith('/') || trimmed.startsWith('//') || trimmed.includes('\\')) {
    return null;
  }

  try {
    const url = new URL(trimmed, 'https://radish.local');
    return `${url.pathname}${url.search}${url.hash}`;
  } catch {
    return null;
  }
}

export function rememberPublicRouteSourceTransfer(
  targetPath: string,
  sourceState: PublicRouteSourceState,
  storage = getSessionStorage()
): boolean {
  const normalizedTargetPath = normalizePublicPath(targetPath);
  if (!normalizedTargetPath || !storage) {
    return false;
  }

  const transfer: PublicRouteSourceTransfer = {
    targetPath: normalizedTargetPath,
    sourceState
  };

  try {
    storage.setItem(PUBLIC_ROUTE_SOURCE_TRANSFER_STORAGE_KEY, JSON.stringify(transfer));
    return true;
  } catch {
    return false;
  }
}

export function consumePublicRouteSourceTransfer(
  currentPath: string,
  storage = getSessionStorage()
): PublicRouteSourceState | null {
  const normalizedCurrentPath = normalizePublicPath(currentPath);
  if (!normalizedCurrentPath || !storage) {
    return null;
  }

  try {
    const rawTransfer = storage.getItem(PUBLIC_ROUTE_SOURCE_TRANSFER_STORAGE_KEY);
    storage.removeItem(PUBLIC_ROUTE_SOURCE_TRANSFER_STORAGE_KEY);
    if (!rawTransfer) {
      return null;
    }

    const transfer = JSON.parse(rawTransfer) as unknown;
    if (!isPlainObject(transfer) || !isPlainObject(transfer.sourceState)) {
      return null;
    }

    return normalizePublicPath(typeof transfer.targetPath === 'string' ? transfer.targetPath : null) === normalizedCurrentPath
      ? transfer.sourceState as PublicRouteSourceState
      : null;
  } catch {
    return null;
  }
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

export function shouldCaptureShopDetailSource(
  currentRoute: PublicRouteDescriptor,
  nextRoute: PublicRouteDescriptor
): boolean {
  if (nextRoute.app !== 'shop' || nextRoute.route.kind !== 'detail') {
    return false;
  }

  return currentRoute.app !== 'shop'
    || currentRoute.route.kind !== 'detail'
    || currentRoute.route.productId !== nextRoute.route.productId;
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

export function createPublicRouteSourceState(
  currentState: PublicRouteSourceState,
  currentRoute: PublicRouteDescriptor,
  nextRoute: PublicRouteDescriptor,
  options: PublicRouteSourceStateOptions = {}
): PublicRouteSourceState {
  const nextState: PublicRouteSourceState = { ...currentState };
  if (options.preserveExisting) {
    return nextState;
  }

  if (shouldCaptureForumDetailSource(currentRoute, nextRoute)) {
    nextState.forumDetailSourceRoute = currentRoute;
  } else if (nextRoute.app === 'forum' && nextRoute.route.kind !== 'detail') {
    nextState.forumDetailSourceRoute = null;
  }

  if (shouldCaptureDocsDetailSource(currentRoute, nextRoute)) {
    nextState.docsDetailSourceRoute = currentRoute;
  } else if (nextRoute.app === 'docs' && nextRoute.route.kind !== 'detail') {
    nextState.docsDetailSourceRoute = null;
  }

  if (shouldCaptureProfileDetailSource(currentRoute, nextRoute)) {
    nextState.profileSourceRoute = currentRoute;
  }

  if (shouldCaptureShopDetailSource(currentRoute, nextRoute)) {
    nextState.shopDetailSourceRoute = currentRoute;
  } else if (nextRoute.app === 'shop' && nextRoute.route.kind !== 'detail') {
    nextState.shopDetailSourceRoute = null;
  }

  return nextState;
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

  if (sourceRoute.app === 'discover' || sourceRoute.app === 'me') {
    return sourceRoute.app;
  }

  return 'source';
}

export function resolveProfileBackMode(sourceRoute: PublicRouteDescriptor | null): PublicDetailBackMode | null {
  if (!sourceRoute) {
    return null;
  }

  if (
    sourceRoute.app === 'discover'
    || sourceRoute.app === 'circle'
    || sourceRoute.app === 'me'
    || sourceRoute.app === 'notifications'
  ) {
    return sourceRoute.app;
  }

  return 'source';
}

export function resolveShopDetailBackMode(sourceRoute: PublicRouteDescriptor | null): PublicDetailBackMode | null {
  if (!sourceRoute) {
    return null;
  }

  return resolveBackMode(sourceRoute);
}
