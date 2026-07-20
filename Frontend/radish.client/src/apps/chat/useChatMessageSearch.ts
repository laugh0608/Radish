import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  ChatMessageSearchScopes,
  type ChannelMessageSearchItemVo,
  type ChatMessageSearchScope,
  type SearchChannelMessagesDto,
} from '@radish/http';
import { searchChannelMessages } from '@/api/chat';
import { log } from '@/utils/logger';
import {
  isChatSearchCursorInvalidError,
  mergeChatSearchItems,
  toChatSearchUtcBoundary,
} from './chatMessageSearch';

export type ChatMessageSearchStatus =
  | 'idle'
  | 'loading'
  | 'success'
  | 'empty'
  | 'error'
  | 'cursor-invalid'
  | 'offline';

interface SearchSnapshot {
  scope: ChatMessageSearchScope;
  channelId?: string;
  keyword: string;
  fromUtc?: string;
  toUtc?: string;
}

interface UseChatMessageSearchOptions {
  activeChannelId: string | null;
  accountKey: string;
}

const SEARCH_PAGE_SIZE = 20;

function isBrowserOnline(): boolean {
  return typeof navigator === 'undefined' || navigator.onLine;
}

function getErrorMessage(error: unknown): string | null {
  return error instanceof Error && error.message.trim() ? error.message : null;
}

export function useChatMessageSearch({ activeChannelId, accountKey }: UseChatMessageSearchOptions) {
  const [keyword, setKeyword] = useState('');
  const [scope, setScope] = useState<ChatMessageSearchScope>(
    activeChannelId ? ChatMessageSearchScopes.CurrentChannel : ChatMessageSearchScopes.AllVisibleChannels
  );
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [items, setItems] = useState<ChannelMessageSearchItemVo[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [status, setStatus] = useState<ChatMessageSearchStatus>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const snapshotRef = useRef<SearchSnapshot | null>(null);
  const requestIdRef = useRef(0);
  const activeChannelIdRef = useRef(activeChannelId);

  useEffect(() => {
    activeChannelIdRef.current = activeChannelId;
  }, [activeChannelId]);

  const reset = useCallback(() => {
    requestIdRef.current += 1;
    setKeyword('');
    setScope(activeChannelIdRef.current ? ChatMessageSearchScopes.CurrentChannel : ChatMessageSearchScopes.AllVisibleChannels);
    setFromDate('');
    setToDate('');
    setItems([]);
    setNextCursor(null);
    setHasMore(false);
    setStatus('idle');
    setErrorMessage(null);
    setLoadingMore(false);
    snapshotRef.current = null;
  }, []);

  useEffect(() => {
    reset();
  }, [accountKey, reset]);

  useEffect(() => {
    if (!activeChannelId && scope === ChatMessageSearchScopes.CurrentChannel) {
      setScope(ChatMessageSearchScopes.AllVisibleChannels);
    }
  }, [activeChannelId, scope]);

  useEffect(() => {
    const handleOffline = () => {
      setStatus('offline');
    };
    const handleOnline = () => {
      setStatus((current) => {
        if (current !== 'offline') {
          return current;
        }
        return items.length > 0 ? 'success' : snapshotRef.current ? 'empty' : 'idle';
      });
    };

    window.addEventListener('offline', handleOffline);
    window.addEventListener('online', handleOnline);
    return () => {
      window.removeEventListener('offline', handleOffline);
      window.removeEventListener('online', handleOnline);
    };
  }, [items.length]);

  const currentSnapshot = useMemo<SearchSnapshot>(() => ({
    scope,
    ...(scope === ChatMessageSearchScopes.CurrentChannel && activeChannelId
      ? { channelId: activeChannelId }
      : {}),
    keyword: keyword.trim(),
    ...(toChatSearchUtcBoundary(fromDate, false) ? { fromUtc: toChatSearchUtcBoundary(fromDate, false) } : {}),
    ...(toChatSearchUtcBoundary(toDate, true) ? { toUtc: toChatSearchUtcBoundary(toDate, true) } : {}),
  }), [activeChannelId, fromDate, keyword, scope, toDate]);

  const executeSearch = useCallback(async (append: boolean) => {
    if (!isBrowserOnline()) {
      setStatus('offline');
      return;
    }

    const snapshot = append ? snapshotRef.current : currentSnapshot;
    if (!snapshot || snapshot.keyword.length < 2) {
      setErrorMessage(null);
      setStatus('error');
      return;
    }

    if (snapshot.scope === ChatMessageSearchScopes.CurrentChannel && !snapshot.channelId) {
      setErrorMessage(null);
      setStatus('error');
      return;
    }

    const cursor = append ? nextCursor : null;
    if (append && !cursor) {
      return;
    }

    const request: SearchChannelMessagesDto = {
      scope: snapshot.scope,
      channelId: snapshot.channelId,
      keyword: snapshot.keyword,
      fromUtc: snapshot.fromUtc,
      toUtc: snapshot.toUtc,
      cursor,
      pageSize: SEARCH_PAGE_SIZE,
    };
    const requestId = ++requestIdRef.current;
    const startedAt = performance.now();
    setErrorMessage(null);
    if (append) {
      setLoadingMore(true);
    } else {
      setStatus('loading');
      snapshotRef.current = snapshot;
    }

    try {
      const page = await searchChannelMessages(request);
      if (requestId !== requestIdRef.current) {
        return;
      }

      setItems((current) => append ? mergeChatSearchItems(current, page.voItems) : page.voItems);
      setNextCursor(page.voNextCursor ?? null);
      setHasMore(page.voHasMore);
      setStatus((append ? items.length + page.voItems.length : page.voItems.length) > 0 ? 'success' : 'empty');
      log.info('ChatMessageSearch', '消息搜索请求完成', {
        scope: snapshot.scope,
        pageSize: SEARCH_PAGE_SIZE,
        resultCount: page.voItems.length,
        durationMs: Math.round(performance.now() - startedAt),
      });
    } catch (error) {
      if (requestId !== requestIdRef.current) {
        return;
      }

      if (isChatSearchCursorInvalidError(error)) {
        setHasMore(false);
        setNextCursor(null);
        setStatus('cursor-invalid');
      } else {
        setStatus('error');
        setErrorMessage(getErrorMessage(error));
      }
      log.warn('ChatMessageSearch', '消息搜索请求失败', {
        append,
        cursorInvalid: isChatSearchCursorInvalidError(error),
      });
    } finally {
      if (requestId === requestIdRef.current) {
        setLoadingMore(false);
      }
    }
  }, [currentSnapshot, items.length, nextCursor]);

  return {
    keyword,
    setKeyword,
    scope,
    setScope,
    fromDate,
    setFromDate,
    toDate,
    setToDate,
    items,
    hasMore,
    status,
    errorMessage,
    loadingMore,
    search: () => executeSearch(false),
    loadMore: () => executeSearch(true),
    reset,
  };
}
