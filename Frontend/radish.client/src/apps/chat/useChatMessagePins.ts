import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { toast } from '@radish/ui/toast';
import { getChatMessagePinState, setChatMessagePin } from '@/api/chat';
import { useChatStore, type ChatConnectionState } from '@/stores/chatStore';
import type { EntityIdValue } from '@/types/chat';
import { isPersistedEntityId, normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';

interface UseChatMessagePinsOptions {
  activeChannelId: EntityIdValue | null;
  connectionState: ChatConnectionState;
  canPinMessages: boolean;
  refreshAfterTargetUnavailable: boolean;
}

export function useChatMessagePins({
  activeChannelId,
  connectionState,
  canPinMessages,
  refreshAfterTargetUnavailable,
}: UseChatMessagePinsOptions) {
  const { t } = useTranslation();
  const pinStateMap = useChatStore((state) => state.pinStateMap);
  const setPinState = useChatStore((state) => state.setPinState);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [pendingMessageId, setPendingMessageId] = useState<string | null>(null);
  const [refreshRevision, setRefreshRevision] = useState(0);
  const channelId = normalizeEntityId(activeChannelId);
  const state = channelId ? pinStateMap[channelId] : undefined;
  const pinnedMessageIds = useMemo(
    () => new Set(state?.voItems.map((item) => item.voMessageId) ?? []),
    [state]
  );

  useEffect(() => {
    if (refreshAfterTargetUnavailable) {
      setRefreshRevision((revision) => revision + 1);
    }
  }, [refreshAfterTargetUnavailable]);

  useEffect(() => {
    if (!channelId) {
      setLoading(false);
      setLoadError(null);
      setPendingMessageId(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setLoadError(null);

    const loadState = async () => {
      try {
        const response = await getChatMessagePinState(channelId);
        if (!cancelled) {
          setPinState(response);
        }
      } catch (error) {
        log.warn('ChatMessagePin', '加载消息置顶失败', error);
        if (!cancelled) {
          setLoadError(t('chat.pin.loadFailed'));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadState();
    return () => {
      cancelled = true;
    };
  }, [channelId, connectionState, refreshRevision, setPinState, t]);

  const retry = useCallback(() => {
    setRefreshRevision((revision) => revision + 1);
  }, []);

  const setPinned = useCallback(async (
    messageId: EntityIdValue,
    isPinned: boolean
  ): Promise<void> => {
    const normalizedMessageId = normalizeEntityId(messageId);
    if (!canPinMessages || !channelId || !normalizedMessageId || !isPersistedEntityId(normalizedMessageId)) {
      return;
    }

    setPendingMessageId(normalizedMessageId);
    try {
      const mutation = await setChatMessagePin({
        channelId,
        messageId: normalizedMessageId,
        isPinned,
      });
      useChatStore.getState().setPinState(mutation.voState);
      if (mutation.voChanged) {
        toast.success(t(isPinned ? 'chat.pin.pinned' : 'chat.pin.unpinned'));
      }
    } catch (error) {
      log.warn('ChatMessagePin', '更新消息置顶失败', error);
      toast.error(error instanceof Error ? error.message : t('chat.pin.updateFailed'));
    } finally {
      setPendingMessageId((current) => current === normalizedMessageId ? null : current);
    }
  }, [canPinMessages, channelId, t]);

  return {
    state,
    pinnedMessageIds,
    loading,
    loadError,
    pendingMessageId,
    retry,
    setPinned,
  };
}
