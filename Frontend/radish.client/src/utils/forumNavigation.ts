export interface ForumNavigationTarget {
  postId?: string;
  postPublicId?: string;
  commentId?: string;
}

export type ForumWorkspaceIntent = 'comment' | 'quickReply';

export interface ForumWindowParams {
  postId?: string;
  postPublicId?: string;
  commentId?: string;
  intent?: ForumWorkspaceIntent;
  navigationKey?: string;
}

export interface ForumAppParamTarget {
  postId?: string;
  postPublicId?: string;
  commentId?: string;
  intent?: string | null;
}

function decodeRouteCandidate(value: string): string {
  let current = value.trim();

  for (let attempt = 0; attempt < 2; attempt += 1) {
    if (!current.includes('%')) {
      break;
    }

    try {
      const decoded = decodeURIComponent(current);
      if (decoded === current) {
        break;
      }

      current = decoded.trim();
    } catch {
      break;
    }
  }

  return current;
}

function normalizePositiveIntegerString(value: unknown): string | undefined {
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || value <= 0) {
      return undefined;
    }

    return String(value);
  }

  if (typeof value !== 'string') {
    return undefined;
  }

  const trimmed = value.trim();
  if (!/^\d+$/.test(trimmed)) {
    return undefined;
  }

  const normalized = trimmed.replace(/^0+/, '');
  return normalized || undefined;
}

function normalizePostPublicId(value: unknown): string | undefined {
  if (typeof value !== 'string') {
    return undefined;
  }

  const normalized = value.trim().toLowerCase();
  return /^pst_[a-f0-9]{32}$/.test(normalized) ? normalized : undefined;
}

function normalizeForumWorkspaceIntent(value: unknown): ForumWorkspaceIntent | undefined {
  if (typeof value !== 'string') {
    return undefined;
  }

  const normalized = value.trim();
  return normalized === 'comment' || normalized === 'quickReply' ? normalized : undefined;
}

function hasPostNavigationTarget(target: ForumNavigationTarget): boolean {
  return Boolean(target.postPublicId || target.postId);
}

function buildForumNavigationTarget(target: {
  postId?: unknown;
  postPublicId?: unknown;
  commentId?: unknown;
}): ForumNavigationTarget {
  const postPublicId = normalizePostPublicId(target.postPublicId);
  const postId = postPublicId ? undefined : normalizePositiveIntegerString(target.postId);
  const commentId = normalizePositiveIntegerString(target.commentId);

  return {
    ...(postPublicId ? { postPublicId } : {}),
    ...(postId ? { postId } : {}),
    ...(commentId ? { commentId } : {})
  };
}

export function buildForumAppParams(target: ForumAppParamTarget): Record<string, unknown> {
  const postId = normalizePositiveIntegerString(target.postId);
  const postPublicId = normalizePostPublicId(target.postPublicId);
  if (!postId && !postPublicId) {
    return {};
  }

  const commentId = normalizePositiveIntegerString(target.commentId);
  const intent = normalizeForumWorkspaceIntent(target.intent);
  return {
    ...(postId ? { postId } : {}),
    ...(postPublicId ? { postPublicId } : {}),
    ...(commentId ? { commentId } : {}),
    ...(intent ? { intent } : {})
  };
}

function parseForumRouteCandidate(routePath: string): ForumNavigationTarget | null {
  const [pathWithQuery] = routePath.split('#');
  const [pathname, queryString = ''] = pathWithQuery.split('?');
  if (!pathname.startsWith('/forum/post/')) {
    return null;
  }

  const rawPostIdentifier = pathname.split('/').pop();
  const postId = normalizePositiveIntegerString(rawPostIdentifier);
  const postPublicId = normalizePostPublicId(rawPostIdentifier);
  if (!postId && !postPublicId) {
    return null;
  }

  const commentId = normalizePositiveIntegerString(new URLSearchParams(queryString).get('commentId'));
  return {
    ...(postId ? { postId } : {}),
    ...(postPublicId ? { postPublicId } : {}),
    ...(commentId ? { commentId } : {})
  };
}

export function parseForumWindowParams(appParams?: Record<string, unknown> | null): ForumWindowParams {
  if (!appParams) {
    return {};
  }

  const postId = normalizePositiveIntegerString(appParams.postId);
  const postPublicId = normalizePostPublicId(appParams.postPublicId);
  if (!postId && !postPublicId) {
    return {};
  }

  const commentId = normalizePositiveIntegerString(appParams.commentId);
  const intent = normalizeForumWorkspaceIntent(appParams.intent);
  const rawNavigationKey = appParams.__navigationKey;
  const navigationKey = rawNavigationKey == null ? undefined : String(rawNavigationKey);

  return {
    ...(postId ? { postId } : {}),
    ...(postPublicId ? { postPublicId } : {}),
    ...(commentId ? { commentId } : {}),
    ...(intent ? { intent } : {}),
    ...(navigationKey ? { navigationKey } : {})
  };
}

export function parseForumNotificationNavigation(extData?: string | null): ForumNavigationTarget | null {
  const normalized = extData?.trim();
  if (!normalized) {
    return null;
  }

  const routeNavigation = parseForumRoutePath(normalized);
  if (routeNavigation) {
    return routeNavigation;
  }

  try {
    const parsed = JSON.parse(normalized) as Record<string, unknown> | string;

    if (typeof parsed === 'string') {
      return parseForumRoutePath(parsed);
    }

    const routeLikeValue = [
      parsed.routePath,
      parsed.path,
      parsed.url,
      parsed.relatedUrl,
    ].find((value): value is string => typeof value === 'string' && value.trim().length > 0);

    if (parsed.app === 'forum') {
      const navigation = buildForumNavigationTarget(parsed);
      if (navigation.postPublicId) {
        return navigation;
      }

      const routeNavigation = routeLikeValue ? parseForumRoutePath(routeLikeValue) : null;
      if (routeNavigation?.postPublicId) {
        return routeNavigation;
      }

      return hasPostNavigationTarget(navigation) ? navigation : routeNavigation;
    }

    return routeLikeValue ? parseForumRoutePath(routeLikeValue) : null;
  } catch {
    return null;
  }
}

export function parseForumRoutePath(routePath?: string | null): ForumNavigationTarget | null {
  const normalized = routePath?.trim();
  if (!normalized) {
    return null;
  }

  const candidates = [decodeRouteCandidate(normalized)];

  const decoded = candidates[0];
  if (/^https?:\/\//i.test(decoded)) {
    try {
      const url = new URL(decoded);
      candidates.push(`${url.pathname}${url.search}`);

      const hashRoute = url.hash.startsWith('#') ? url.hash.slice(1) : url.hash;
      if (hashRoute) {
        candidates.push(hashRoute);
      }
    } catch {
      // ignore invalid absolute URL and continue with direct candidates
    }
  }

  const hashRoute = decoded.startsWith('#') ? decoded.slice(1) : '';
  if (hashRoute) {
    candidates.push(hashRoute);
  }

  for (const candidate of candidates) {
    const navigation = parseForumRouteCandidate(candidate);
    if (navigation) {
      return navigation;
    }
  }

  return null;
}
