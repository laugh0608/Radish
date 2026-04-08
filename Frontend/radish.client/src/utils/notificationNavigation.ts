export interface ForumNotificationNavigationPayload {
  app: 'forum';
  postId: number;
  commentId?: number;
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

export function parseForumNotificationNavigation(extData?: string | null): ForumNotificationNavigationPayload | null {
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
      ? { app: 'forum', postId, commentId }
      : { app: 'forum', postId };
  } catch {
    return null;
  }
}
