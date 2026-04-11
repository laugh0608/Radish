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

  try {
    const parsed = JSON.parse(normalized) as Record<string, unknown>;
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

  const [pathWithQuery] = normalized.split('#');
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
