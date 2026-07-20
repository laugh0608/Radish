import type {
  ChatMessageReactionStateVo,
  ChatReactionEmojiType,
  ChatReactionSummaryVo,
} from '@radish/http';

export type ChatReactionStateSource = 'authoritative' | 'broadcast';

function normalizeRevision(value: string): string {
  const normalized = value.trim().replace(/^0+(?=\d)/, '');
  return /^\d+$/.test(normalized) ? normalized : '0';
}

export function compareChatReactionRevisions(left: string, right: string): number {
  const normalizedLeft = normalizeRevision(left);
  const normalizedRight = normalizeRevision(right);
  if (normalizedLeft.length !== normalizedRight.length) {
    return normalizedLeft.length - normalizedRight.length;
  }

  return normalizedLeft.localeCompare(normalizedRight);
}

export function buildChatReactionKey(
  emojiType: ChatReactionEmojiType,
  emojiValue: string
): string {
  return `${emojiType}:${emojiValue}`;
}

function withoutBroadcastActorSelection(state: ChatMessageReactionStateVo): ChatMessageReactionStateVo {
  return {
    ...state,
    voItems: state.voItems.map((item) => ({ ...item, voIsReacted: false })),
  };
}

function preserveCurrentUserSelection(
  current: ChatMessageReactionStateVo,
  incoming: ChatMessageReactionStateVo
): ChatMessageReactionStateVo {
  const reactedKeys = new Set(
    current.voItems
      .filter((item) => item.voIsReacted)
      .map((item) => buildChatReactionKey(item.voEmojiType, item.voEmojiValue))
  );

  return {
    ...incoming,
    voItems: incoming.voItems.map((item): ChatReactionSummaryVo => ({
      ...item,
      voIsReacted: reactedKeys.has(buildChatReactionKey(item.voEmojiType, item.voEmojiValue)),
    })),
  };
}

export function mergeChatReactionState(
  current: ChatMessageReactionStateVo | undefined,
  incoming: ChatMessageReactionStateVo,
  source: ChatReactionStateSource
): ChatMessageReactionStateVo {
  if (!current) {
    return source === 'broadcast' ? withoutBroadcastActorSelection(incoming) : incoming;
  }

  const revisionComparison = compareChatReactionRevisions(incoming.voRevision, current.voRevision);
  if (revisionComparison < 0 || (revisionComparison === 0 && source === 'broadcast')) {
    return current;
  }

  return source === 'broadcast'
    ? preserveCurrentUserSelection(current, incoming)
    : incoming;
}

export function buildChatReactionOperationId(messageId: string): string {
  const randomPart = typeof globalThis.crypto?.randomUUID === 'function'
    ? globalThis.crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
  return `chat-reaction-${messageId}-${randomPart}`.slice(0, 100);
}
