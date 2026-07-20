import type { ChannelMessageSearchItemVo } from '@radish/http';

export function toChatSearchUtcBoundary(date: string, exclusiveEnd: boolean): string | undefined {
  if (!/^\d{4}-\d{2}-\d{2}$/u.test(date)) {
    return undefined;
  }

  const [year, month, day] = date.split('-').map(Number);
  const localDayStart = new Date(year, month - 1, day);
  if (localDayStart.getFullYear() !== year
    || localDayStart.getMonth() !== month - 1
    || localDayStart.getDate() !== day) {
    return undefined;
  }

  const localBoundary = exclusiveEnd
    ? new Date(year, month - 1, day + 1)
    : localDayStart;
  return localBoundary.toISOString();
}

export function isChatSearchCursorInvalidError(error: unknown): boolean {
  if (!error || typeof error !== 'object') {
    return false;
  }

  const candidate = error as { code?: unknown; messageKey?: unknown };
  const code = typeof candidate.code === 'string' ? candidate.code.trim().toLowerCase() : '';
  const messageKey = typeof candidate.messageKey === 'string'
    ? candidate.messageKey.trim().toLowerCase()
    : '';
  return code === 'chat.searchcursorinvalid'
    || messageKey === 'error.chat.search_cursor_invalid';
}

export function mergeChatSearchItems(
  current: ChannelMessageSearchItemVo[],
  incoming: ChannelMessageSearchItemVo[]
): ChannelMessageSearchItemVo[] {
  const seen = new Set(current.map((item) => item.voMessageId));
  const merged = current.slice();
  for (const item of incoming) {
    if (!seen.has(item.voMessageId)) {
      seen.add(item.voMessageId);
      merged.push(item);
    }
  }

  return merged;
}
