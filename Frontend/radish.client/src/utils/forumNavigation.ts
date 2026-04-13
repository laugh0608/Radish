export interface ForumNavigationTarget {
  postId: string;
  commentId?: string;
}

export interface ForumWindowParams {
  postId?: string;
  commentId?: string;
  navigationKey?: string;
}

export interface ForumAppParamTarget {
  postId: string | number;
  commentId?: string | number;
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

export function buildForumAppParams(target: ForumAppParamTarget): Record<string, unknown> {
  const postId = normalizePositiveIntegerString(target.postId);
  if (!postId) {
    return {};
  }

  const commentId = normalizePositiveIntegerString(target.commentId);
  return commentId
    ? { postId, commentId }
    : { postId };
}

function parseForumRouteCandidate(routePath: string): ForumNavigationTarget | null {
  const [pathWithQuery] = routePath.split('#');
  const [pathname, queryString = ''] = pathWithQuery.split('?');
  if (!pathname.startsWith('/forum/post/')) {
    return null;
  }

  const postId = normalizePositiveIntegerString(pathname.split('/').pop());
  if (!postId) {
    return null;
  }

  const commentId = normalizePositiveIntegerString(new URLSearchParams(queryString).get('commentId'));
  return commentId
    ? { postId, commentId }
    : { postId };
}

export function parseForumWindowParams(appParams?: Record<string, unknown> | null): ForumWindowParams {
  if (!appParams) {
    return {};
  }

  const postId = normalizePositiveIntegerString(appParams.postId);
  if (!postId) {
    return {};
  }

  const commentId = normalizePositiveIntegerString(appParams.commentId);
  const rawNavigationKey = appParams.__navigationKey;
  const navigationKey = rawNavigationKey == null ? undefined : String(rawNavigationKey);

  return {
    postId,
    commentId,
    navigationKey
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

    if (routeLikeValue) {
      const routeNavigationFromPayload = parseForumRoutePath(routeLikeValue);
      if (routeNavigationFromPayload) {
        return routeNavigationFromPayload;
      }
    }

    if (parsed.app !== 'forum') {
      return null;
    }

    const postId = normalizePositiveIntegerString(parsed.postId);
    if (!postId) {
      return null;
    }

    const commentId = normalizePositiveIntegerString(parsed.commentId);
    return commentId
      ? { postId, commentId }
      : { postId };
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
