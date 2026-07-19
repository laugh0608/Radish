import { useCallback, useEffect, useMemo, useState } from 'react';
import type { ChatReactionEmojiType } from '@radish/http';
import { toast } from '@radish/ui/toast';
import { useTranslation } from 'react-i18next';
import { getChatMessageReactionStates, setChatMessageReaction } from '@/api/chat';
import { useChatStore, type ChatConnectionState } from '@/stores/chatStore';
import type { ChannelMessageVo, EntityIdValue } from '@/types/chat';
import { isPersistedEntityId, normalizeEntityId } from '@/types/chat';
import { log } from '@/utils/logger';
import { buildChatReactionKey, buildChatReactionOperationId } from '@/utils/chatMessageReactions';

const REACTION_READ_BATCH_SIZE = 100;

interface UseChatMessageReactionsOptions {
  activeChannelId: EntityIdValue | null;
  messages: ChannelMessageVo[];
  connectionState: ChatConnectionState;
  canReact: boolean;
}

export interface ToggleChatMessageReactionPayload {
  messageId: EntityIdValue;
  emojiType: ChatReactionEmojiType;
  emojiValue: string;
}

function splitIntoBatches<T>(items: T[], size: number): T[][] {
  const batches: T[][] = [];
  for (let index = 0; index < items.length; index += size) {
    batches.push(items.slice(index, index + size));
  }
  return batches;
}

export function useChatMessageReactions({
  activeChannelId,
  messages,
  connectionState,
  canReact,
}: UseChatMessageReactionsOptions) {
  const { t } = useTranslation();
  const reactionStateMap = useChatStore((state) => state.reactionStateMap);
  const setReactionStates = useChatStore((state) => state.setReactionStates);
  const [loading, setLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [refreshRevision, setRefreshRevision] = useState(0);

  const messageIdSignature = useMemo(
    () => messages
      .filter((message) => (
        isPersistedEntityId(message.voId)
        && !message.voIsRecalled
        && (message.voLocalStatus ?? 'sent') === 'sent'
      ))
      .map((message) => normalizeEntityId(message.voId))
      .filter((messageId): messageId is string => Boolean(messageId))
      .join(','),
    [messages]
  );

  useEffect(() => {
    const channelId = normalizeEntityId(activeChannelId);
    const messageIds = messageIdSignature ? messageIdSignature.split(',') : [];
    if (!channelId || messageIds.length === 0) {
      setLoading(false);
      setLoadError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setLoadError(null);

    const loadStates = async () => {
      try {
        const responses = await Promise.all(
          splitIntoBatches(messageIds, REACTION_READ_BATCH_SIZE)
            .map((batch) => getChatMessageReactionStates(channelId, batch))
        );
        if (!cancelled) {
          setReactionStates(responses.flat());
        }
      } catch (error) {
        log.warn('ChatMessageReaction', '加载消息回应失败', error);
        if (!cancelled) {
          setLoadError(t('chat.reaction.loadFailed'));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void loadStates();
    return () => {
      cancelled = true;
    };
  }, [activeChannelId, connectionState, messageIdSignature, refreshRevision, setReactionStates, t]);

  const retry = useCallback(() => {
    setRefreshRevision((revision) => revision + 1);
  }, []);

  const toggleReaction = useCallback(async ({
    messageId,
    emojiType,
    emojiValue,
  }: ToggleChatMessageReactionPayload): Promise<void> => {
    const channelId = normalizeEntityId(activeChannelId);
    const normalizedMessageId = normalizeEntityId(messageId);
    if (!canReact || !channelId || !normalizedMessageId || !isPersistedEntityId(normalizedMessageId)) {
      return;
    }

    const currentState = useChatStore.getState().reactionStateMap[normalizedMessageId];
    const currentItem = currentState?.voItems.find((item) => (
      buildChatReactionKey(item.voEmojiType, item.voEmojiValue)
        === buildChatReactionKey(emojiType, emojiValue)
    ));

    try {
      const mutation = await setChatMessageReaction({
        channelId,
        messageId: normalizedMessageId,
        emojiType,
        emojiValue,
        isActive: !(currentItem?.voIsReacted ?? false),
        clientOperationId: buildChatReactionOperationId(normalizedMessageId),
      });
      useChatStore.getState().setReactionStates([mutation.voState]);
    } catch (error) {
      log.warn('ChatMessageReaction', '更新消息回应失败', error);
      toast.error(error instanceof Error ? error.message : t('chat.reaction.updateFailed'));
    }
  }, [activeChannelId, canReact, t]);

  return {
    reactionStateMap,
    loading,
    loadError,
    retry,
    toggleReaction,
  };
}
