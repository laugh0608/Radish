import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { getChatReadReceiptSummaries } from '@/api/chat';
import { useChatStore, type ChatConnectionState } from '@/stores/chatStore';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';
import {
  buildReadReceiptItemMap,
  resolveDirectReadBoundaryMessageId,
  selectOwnReceiptMessageIds,
} from './chatReadReceipts';

interface UseChatReadReceiptsOptions {
  accountKey: string;
  activeChannelId: EntityIdValue | null;
  messages: ChannelMessageVo[];
  connectionState: ChatConnectionState;
  loadErrorMessage: string;
}

const RECEIPT_INVALIDATION_DEBOUNCE_MS = 180;

export function useChatReadReceipts({
  accountKey,
  activeChannelId,
  messages,
  connectionState,
  loadErrorMessage,
}: UseChatReadReceiptsOptions) {
  const channelKey = normalizeEntityId(activeChannelId);
  const summary = useChatStore((state) => (
    channelKey ? state.readReceiptSummaryMap[channelKey] : undefined
  ));
  const invalidationRevision = useChatStore((state) => (
    channelKey ? (state.readReceiptInvalidationMap[channelKey] ?? 0) : 0
  ));
  const setReadReceiptSummaries = useChatStore((state) => state.setReadReceiptSummaries);
  const clearChannelReadReceipts = useChatStore((state) => state.clearChannelReadReceipts);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [retryRevision, setRetryRevision] = useState(0);
  const requestRevisionRef = useRef(0);
  const previousAccountKeyRef = useRef(accountKey);
  const messageIds = useMemo(
    () => selectOwnReceiptMessageIds(messages, accountKey),
    [accountKey, messages]
  );
  const messageIdSignature = messageIds.join(',');

  useEffect(() => {
    if (previousAccountKeyRef.current === accountKey) {
      return;
    }

    previousAccountKeyRef.current = accountKey;
    requestRevisionRef.current += 1;
    if (channelKey) {
      clearChannelReadReceipts(channelKey);
    }
  }, [accountKey, channelKey, clearChannelReadReceipts]);

  useEffect(() => {
    return () => {
      if (channelKey) {
        clearChannelReadReceipts(channelKey);
      }
    };
  }, [channelKey, clearChannelReadReceipts]);

  useEffect(() => {
    if (!channelKey || messageIds.length === 0) {
      setLoading(false);
      setLoadError(null);
      if (channelKey) {
        clearChannelReadReceipts(channelKey);
      }
      return;
    }

    const requestRevision = ++requestRevisionRef.current;
    const timer = window.setTimeout(() => {
      setLoading(true);
      setLoadError(null);
      void getChatReadReceiptSummaries(channelKey, messageIds)
        .then((result) => {
          if (requestRevision !== requestRevisionRef.current) {
            return;
          }
          setReadReceiptSummaries(result);
        })
        .catch((error) => {
          if (requestRevision !== requestRevisionRef.current) {
            return;
          }
          clearChannelReadReceipts(channelKey);
          setLoadError(loadErrorMessage);
          log.warn('ChatReadReceipts', '读取权威回执摘要失败:', error);
        })
        .finally(() => {
          if (requestRevision === requestRevisionRef.current) {
            setLoading(false);
          }
        });
    }, invalidationRevision > 0 ? RECEIPT_INVALIDATION_DEBOUNCE_MS : 0);

    return () => {
      window.clearTimeout(timer);
      requestRevisionRef.current += 1;
    };
  }, [
    channelKey,
    clearChannelReadReceipts,
    connectionState,
    invalidationRevision,
    loadErrorMessage,
    messageIds,
    messageIdSignature,
    retryRevision,
    setReadReceiptSummaries,
  ]);

  const retry = useCallback(() => {
    setRetryRevision((current) => current + 1);
  }, []);
  const itemMap = useMemo(() => buildReadReceiptItemMap(summary), [summary]);
  const directBoundaryMessageId = useMemo(
    () => resolveDirectReadBoundaryMessageId(messages, accountKey, summary),
    [accountKey, messages, summary]
  );

  return {
    summary,
    itemMap,
    directBoundaryMessageId,
    loading,
    loadError,
    retry,
  };
}
