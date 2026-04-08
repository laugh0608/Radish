export interface ForumNavigationTarget {
  postId: number;
  commentId?: number;
}

export interface ForumWindowParams {
  postId?: number;
  commentId?: number;
  navigationKey?: string;
}

function normalizePositiveNumber(value: unknown): number | undefined {
  const numeric = typeof value === 'number'
    ? value
    : typeof value === 'string'
      ? Number(value)
      : NaN;

  if (!Number.isFinite(numeric) || numeric <= 0) {
    return undefined;
  }

  return numeric;
}

export function buildForumAppParams(target: ForumNavigationTarget): Record<string, unknown> {
  return target.commentId
    ? { postId: target.postId, commentId: target.commentId }
    : { postId: target.postId };
}

export function parseForumWindowParams(appParams?: Record<string, unknown> | null): ForumWindowParams {
  if (!appParams) {
    return {};
  }

  const postId = normalizePositiveNumber(appParams.postId);
  if (!postId) {
    return {};
  }

  const commentId = normalizePositiveNumber(appParams.commentId);
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

  try {
    const parsed = JSON.parse(normalized) as Record<string, unknown>;
    if (parsed.app !== 'forum') {
      return null;
    }

    const postId = normalizePositiveNumber(parsed.postId);
    if (!postId) {
      return null;
    }

    const commentId = normalizePositiveNumber(parsed.commentId);
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

  const [pathWithQuery] = normalized.split('#');
  const [pathname, queryString = ''] = pathWithQuery.split('?');
  if (!pathname.startsWith('/forum/post/')) {
    return null;
  }

  const postId = normalizePositiveNumber(pathname.split('/').pop());
  if (!postId) {
    return null;
  }

  const commentId = normalizePositiveNumber(new URLSearchParams(queryString).get('commentId'));
  return commentId
    ? { postId, commentId }
    : { postId };
}
