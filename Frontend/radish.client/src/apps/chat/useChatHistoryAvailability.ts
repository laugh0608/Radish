import { useCallback, useState } from 'react';
import { useChatStore } from '@/stores/chatStore';
import type { EntityIdValue } from '@/types/chat';
import { normalizeEntityId } from '@/types/chat';
import { isChatChannelUnavailableError } from '@/utils/chatHistoryAvailability';

export function useChatHistoryAvailability(activeChannelId: EntityIdValue | null) {
  const clearChannelServerMessages = useChatStore((state) => state.clearChannelServerMessages);
  const [unavailableChannelKeys, setUnavailableChannelKeys] = useState<ReadonlySet<string>>(() => new Set());
  const activeChannelKey = normalizeEntityId(activeChannelId);

  const markHistoryAvailable = useCallback((channelId: EntityIdValue) => {
    const channelKey = normalizeEntityId(channelId);
    if (channelKey) {
      setUnavailableChannelKeys((current) => {
        if (!current.has(channelKey)) {
          return current;
        }

        const next = new Set(current);
        next.delete(channelKey);
        return next;
      });
    }
  }, []);

  const handleHistoryError = useCallback((channelId: EntityIdValue, error: unknown): boolean => {
    const channelKey = normalizeEntityId(channelId);
    if (!channelKey || !isChatChannelUnavailableError(error)) {
      return false;
    }

    clearChannelServerMessages(channelId);
    setUnavailableChannelKeys((current) => {
      if (current.has(channelKey)) {
        return current;
      }

      const next = new Set(current);
      next.add(channelKey);
      return next;
    });
    return true;
  }, [clearChannelServerMessages]);

  return {
    historyUnavailable: Boolean(activeChannelKey && unavailableChannelKeys.has(activeChannelKey)),
    markHistoryAvailable,
    handleHistoryError,
    clearServerMessages: clearChannelServerMessages,
  };
}
