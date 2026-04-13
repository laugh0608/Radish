export function createForumCommentHighlightMap<T>(
  highlights?: Record<string, T> | null
): Map<string, T> {
  const next = new Map<string, T>();
  if (!highlights) {
    return next;
  }

  for (const [postId, highlight] of Object.entries(highlights)) {
    if (highlight) {
      next.set(postId, highlight);
    }
  }

  return next;
}

export function getForumCommentHighlight<T>(
  highlights: ReadonlyMap<string, T>,
  postId: string | number
): T | undefined {
  return highlights.get(String(postId));
}
