import type {
  ChatReadReceiptSummariesVo,
  ChatReadReceiptSummaryItemVo,
} from '@radish/http';
import type { ChannelMessageVo, EntityIdValue } from '../../types/chat.ts';
import {
  compareEntityIds,
  isPersistedEntityId,
  normalizeEntityId,
} from '../../types/chat.ts';

export const CHAT_READ_RECEIPT_SUMMARY_LIMIT = 20;
export const CHAT_READ_BOTTOM_THRESHOLD_PX = 120;

export interface ActiveReadSurfaceState {
  documentVisible: boolean;
  documentFocused: boolean;
  windowForeground: boolean;
  atConversationTail: boolean;
  hasMoreNewerHistory: boolean;
  loadingHistory: boolean;
  navigatingHistory: boolean;
}

export function canAdvanceChatReadState(state: ActiveReadSurfaceState): boolean {
  return state.documentVisible
    && state.documentFocused
    && state.windowForeground
    && state.atConversationTail
    && !state.hasMoreNewerHistory
    && !state.loadingHistory
    && !state.navigatingHistory;
}

export function selectOwnReceiptMessageIds(
  messages: ChannelMessageVo[],
  currentUserId: EntityIdValue,
  limit: number = CHAT_READ_RECEIPT_SUMMARY_LIMIT
): string[] {
  const currentUserKey = normalizeEntityId(currentUserId);
  if (!currentUserKey || limit <= 0) {
    return [];
  }

  return messages
    .filter((message) => (
      normalizeEntityId(message.voUserId) === currentUserKey
      && isPersistedEntityId(message.voId)
      && !message.voIsRecalled
      && (message.voLocalStatus ?? 'sent') === 'sent'
    ))
    .slice(-limit)
    .map((message) => normalizeEntityId(message.voId)!)
    .filter(Boolean);
}

export function buildReadReceiptItemMap(
  summaries: ChatReadReceiptSummariesVo | undefined
): Record<string, ChatReadReceiptSummaryItemVo> {
  if (!summaries) {
    return {};
  }

  return Object.fromEntries(summaries.voItems.map((item) => [item.voMessageId, item]));
}

export function resolveDirectReadBoundaryMessageId(
  messages: ChannelMessageVo[],
  currentUserId: EntityIdValue,
  summaries: ChatReadReceiptSummariesVo | undefined
): string | null {
  if (summaries?.voMode !== 'direct') {
    return null;
  }

  const readMessageIds = new Set(
    summaries.voItems
      .filter((item) => item.voPeerHasRead === true)
      .map((item) => item.voMessageId)
  );
  const ownMessageIds = selectOwnReceiptMessageIds(messages, currentUserId);
  return ownMessageIds.filter((messageId) => readMessageIds.has(messageId)).at(-1) ?? null;
}

export function selectHighestVisiblePersistedMessageId(
  messages: ChannelMessageVo[],
  visibleMessageIds: ReadonlySet<string>
): string | null {
  const visibleMessages = messages.filter((message) => {
    const messageId = normalizeEntityId(message.voId);
    return isPersistedEntityId(message.voId) && !!messageId && visibleMessageIds.has(messageId);
  });
  return normalizeEntityId(visibleMessages.at(-1)?.voId);
}

export function selectHigherReadTarget(
  current: string | null,
  candidate: string | null
): string | null {
  if (!candidate) {
    return current;
  }

  if (!current || compareEntityIds(candidate, current) > 0) {
    return candidate;
  }

  return current;
}
