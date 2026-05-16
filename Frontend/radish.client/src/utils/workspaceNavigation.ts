import type { LongId, UserBrowseHistoryItem } from '@/api/user';
import { buildForumAppParams, parseForumRoutePath } from './forumNavigation.ts';

export type WorkspaceNavigationAppId = 'forum' | 'shop' | 'document';

export interface WorkspaceNavigationTarget {
  appId: WorkspaceNavigationAppId;
  appParams?: Record<string, unknown>;
}

export type WorkspaceAppOpener = (appId: WorkspaceNavigationAppId, appParams?: Record<string, unknown>) => void;

type BrowseHistoryNavigationInput = Pick<
  UserBrowseHistoryItem,
  'voRoutePath' | 'voTargetType' | 'voTargetId' | 'voTargetSlug'
>;

function normalizePositiveIdString(value: LongId | null | undefined): string | null {
  if (typeof value === 'number') {
    return Number.isSafeInteger(value) && value > 0 ? String(value) : null;
  }

  if (typeof value !== 'string') {
    return null;
  }

  const trimmed = value.trim();
  if (!/^[1-9]\d*$/.test(trimmed)) {
    return null;
  }

  return trimmed;
}

function getRouteTailSegment(routePath: string): string {
  const normalizedPath = routePath.split('?')[0]?.split('#')[0] || '';
  return normalizedPath.split('/').pop() || '';
}

function decodeRouteSegment(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export function resolveBrowseHistoryWorkspaceTarget(
  item: BrowseHistoryNavigationInput
): WorkspaceNavigationTarget | null {
  const routePath = item.voRoutePath?.trim() || '';
  const targetId = normalizePositiveIdString(item.voTargetId);

  const forumNavigation = parseForumRoutePath(routePath);
  if (forumNavigation) {
    return {
      appId: 'forum',
      appParams: buildForumAppParams(forumNavigation)
    };
  }

  if (routePath.startsWith('/shop/product/')) {
    const productId = normalizePositiveIdString(getRouteTailSegment(routePath));
    if (productId) {
      return {
        appId: 'shop',
        appParams: { productId }
      };
    }
  }

  if (routePath.startsWith('/wiki/doc/') || routePath.startsWith('/docs/')) {
    const routeTarget = decodeRouteSegment(getRouteTailSegment(routePath));
    const documentId = normalizePositiveIdString(routeTarget);
    if (documentId) {
      return {
        appId: 'document',
        appParams: { documentId }
      };
    }

    if (routeTarget) {
      return {
        appId: 'document',
        appParams: { slug: routeTarget }
      };
    }
  }

  if (item.voTargetType === 'Post') {
    const forumParams = buildForumAppParams({ postId: item.voTargetId });
    if ('postId' in forumParams) {
      return {
        appId: 'forum',
        appParams: forumParams
      };
    }
  }

  if (item.voTargetType === 'Product' && targetId) {
    return {
      appId: 'shop',
      appParams: { productId: targetId }
    };
  }

  if (item.voTargetType === 'Wiki') {
    const slug = item.voTargetSlug?.trim();
    if (slug) {
      return {
        appId: 'document',
        appParams: { slug }
      };
    }

    if (targetId) {
      return {
        appId: 'document',
        appParams: { documentId: targetId }
      };
    }
  }

  return null;
}

export function resolveBrowseHistoryDisplayRouteText(
  item: BrowseHistoryNavigationInput,
  fallback: string
): string {
  const routePath = item.voRoutePath?.trim();
  if (!routePath) {
    return fallback;
  }

  if (/^\/forum\/post\/\d+(?:[?#].*)?$/i.test(routePath)) {
    return fallback;
  }

  if (/^\/wiki\/doc\/\d+(?:[?#].*)?$/i.test(routePath)) {
    return fallback;
  }

  return routePath;
}

export function resolveForumPostWorkspaceTarget(
  postId: LongId,
  postPublicId?: string | null
): WorkspaceNavigationTarget | null {
  const forumParams = buildForumAppParams({ postId, postPublicId: postPublicId ?? undefined });
  if (!('postId' in forumParams) && !('postPublicId' in forumParams)) {
    return null;
  }

  return {
    appId: 'forum',
    appParams: forumParams
  };
}

export function openWorkspaceNavigationTarget(
  openApp: WorkspaceAppOpener,
  target: WorkspaceNavigationTarget | null
): boolean {
  if (!target) {
    return false;
  }

  openApp(target.appId, target.appParams);
  return true;
}
