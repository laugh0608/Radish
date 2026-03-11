import { create } from 'zustand';
import type {
  ChannelMessageVo,
  ChannelUnreadChangedPayload,
  ChannelVo,
  EntityIdValue,
} from '@/types/chat';
import {
  areEntityIdsEqual,
  compareEntityIds,
  isPersistedEntityId,
  isTemporaryEntityId,
  normalizeEntityId,
} from '@/types/chat';

export type ChatConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

interface TypingUser {
  userId: EntityIdValue;
  userName: string;
}

interface ChatStore {
  channels: ChannelVo[];
  activeChannelId: EntityIdValue | null;
  messageMap: Record<string, ChannelMessageVo[]>;
  typingMap: Record<string, TypingUser[]>;
  connectionState: ChatConnectionState;

  setChannels: (channels: ChannelVo[]) => void;
  setActiveChannel: (channelId: EntityIdValue | null) => void;
  setConnectionState: (state: ChatConnectionState) => void;
  setChannelMessages: (channelId: EntityIdValue, messages: ChannelMessageVo[]) => void;
  prependChannelMessages: (channelId: EntityIdValue, messages: ChannelMessageVo[]) => void;
  addMessage: (message: ChannelMessageVo) => void;
  removeMessage: (channelId: EntityIdValue, messageId: EntityIdValue) => void;
  recallMessage: (channelId: EntityIdValue, messageId: EntityIdValue) => void;
  updateUnread: (payload: ChannelUnreadChangedPayload) => void;
  setTypingUser: (channelId: EntityIdValue, userId: EntityIdValue, userName: string) => void;
  removeTypingUser: (channelId: EntityIdValue, userId: EntityIdValue) => void;
  reset: () => void;
}

function normalizeClientRequestId(value: string | null | undefined): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim();
  return normalized ? normalized : null;
}

function getChannelKey(channelId: EntityIdValue | null | undefined): string | null {
  return normalizeEntityId(channelId);
}

function normalizeMessage(message: ChannelMessageVo): ChannelMessageVo {
  const localStatus = message.voLocalStatus ?? (isTemporaryEntityId(message.voId) ? 'sending' : 'sent');

  return {
    ...message,
    voClientRequestId: normalizeClientRequestId(message.voClientRequestId),
    voLocalStatus: localStatus,
    voLocalError: localStatus === 'sent' ? null : (message.voLocalError ?? null),
  };
}

function getMessageTimestamp(message: ChannelMessageVo): number {
  const timestamp = Date.parse(message.voCreateTime);
  if (!Number.isNaN(timestamp)) {
    return timestamp;
  }

  if (typeof message.voId === 'number' && Number.isFinite(message.voId)) {
    return message.voId;
  }

  return 0;
}

function sortMessages(messages: ChannelMessageVo[]): ChannelMessageVo[] {
  return [...messages].sort((left, right) => {
    const timeDiff = getMessageTimestamp(left) - getMessageTimestamp(right);
    if (timeDiff !== 0) {
      return timeDiff;
    }

    return compareEntityIds(left.voId, right.voId);
  });
}

function findMessageIndex(messages: ChannelMessageVo[], target: ChannelMessageVo): number {
  const targetClientRequestId = normalizeClientRequestId(target.voClientRequestId);

  if (isPersistedEntityId(target.voId)) {
    const realIdIndex = messages.findIndex((item) => areEntityIdsEqual(item.voId, target.voId));
    if (realIdIndex >= 0) {
      return realIdIndex;
    }
  }

  if (targetClientRequestId) {
    const requestIdIndex = messages.findIndex((item) => normalizeClientRequestId(item.voClientRequestId) === targetClientRequestId);
    if (requestIdIndex >= 0) {
      return requestIdIndex;
    }
  }

  if (target.voId !== undefined && target.voId !== null) {
    return messages.findIndex((item) => areEntityIdsEqual(item.voId, target.voId));
  }

  return -1;
}

function mergeMessageRecord(current: ChannelMessageVo, incoming: ChannelMessageVo): ChannelMessageVo {
  const merged = normalizeMessage({
    ...current,
    ...incoming,
  });

  if (merged.voLocalStatus === 'sent') {
    merged.voLocalError = null;
  }

  return merged;
}

function extractLocalMessages(messages: ChannelMessageVo[]): ChannelMessageVo[] {
  return messages.filter((message) => (
    isTemporaryEntityId(message.voId) || message.voLocalStatus === 'sending' || message.voLocalStatus === 'failed'
  ));
}

function mergeMessages(base: ChannelMessageVo[], incoming: ChannelMessageVo[]): ChannelMessageVo[] {
  const result = base.map(normalizeMessage);

  for (const item of incoming.map(normalizeMessage)) {
    const index = findMessageIndex(result, item);
    if (index >= 0) {
      result[index] = mergeMessageRecord(result[index], item);
      continue;
    }

    result.push(item);
  }

  return sortMessages(result);
}

export const useChatStore = create<ChatStore>((set, get) => ({
  channels: [],
  activeChannelId: null,
  messageMap: {},
  typingMap: {},
  connectionState: 'disconnected',

  setChannels: (channels: ChannelVo[]) => {
    set({ channels });
  },

  setActiveChannel: (channelId: EntityIdValue | null) => {
    set({ activeChannelId: channelId });
  },

  setConnectionState: (connectionState: ChatConnectionState) => {
    set({ connectionState });
  },

  setChannelMessages: (channelId: EntityIdValue, messages: ChannelMessageVo[]) => {
    const channelKey = getChannelKey(channelId);
    if (!channelKey) {
      return;
    }

    set((state) => ({
      messageMap: {
        ...state.messageMap,
        [channelKey]: mergeMessages(messages, extractLocalMessages(state.messageMap[channelKey] || [])),
      },
    }));
  },

  prependChannelMessages: (channelId: EntityIdValue, messages: ChannelMessageVo[]) => {
    const channelKey = getChannelKey(channelId);
    if (!channelKey) {
      return;
    }

    const current = get().messageMap[channelKey] || [];
    set((state) => ({
      messageMap: {
        ...state.messageMap,
        [channelKey]: mergeMessages(current, messages),
      },
    }));
  },

  addMessage: (message: ChannelMessageVo) => {
    const channelKey = getChannelKey(message.voChannelId);
    if (!channelKey) {
      return;
    }

    const current = get().messageMap[channelKey] || [];
    const merged = mergeMessages(current, [message]);

    set((state) => ({
      messageMap: {
        ...state.messageMap,
        [channelKey]: merged,
      },
    }));
  },

  removeMessage: (channelId: EntityIdValue, messageId: EntityIdValue) => {
    const channelKey = getChannelKey(channelId);
    if (!channelKey) {
      return;
    }

    set((state) => {
      const current = state.messageMap[channelKey] || [];
      if (current.length === 0) {
        return state;
      }

      return {
        messageMap: {
          ...state.messageMap,
          [channelKey]: current.filter((item) => !areEntityIdsEqual(item.voId, messageId)),
        },
      };
    });
  },

  recallMessage: (channelId: EntityIdValue, messageId: EntityIdValue) => {
    const channelKey = getChannelKey(channelId);
    if (!channelKey) {
      return;
    }

    set((state) => {
      const current = state.messageMap[channelKey] || [];
      if (current.length === 0) {
        return state;
      }

      return {
        messageMap: {
          ...state.messageMap,
          [channelKey]: current.map((item) => {
            if (!areEntityIdsEqual(item.voId, messageId)) {
              return item;
            }

            return {
              ...item,
              voIsRecalled: true,
              voContent: null,
              voImageUrl: null,
              voImageThumbnailUrl: null,
            };
          }),
        },
      };
    });
  },

  updateUnread: (payload: ChannelUnreadChangedPayload) => {
    set((state) => ({
      channels: state.channels.map((channel) => {
        if (!areEntityIdsEqual(channel.voId, payload.channelId)) {
          return channel;
        }

        return {
          ...channel,
          voUnreadCount: Math.max(0, payload.unreadCount),
          voHasMention: payload.hasMention,
        };
      }),
    }));
  },

  setTypingUser: (channelId: EntityIdValue, userId: EntityIdValue, userName: string) => {
    const channelKey = getChannelKey(channelId);
    if (!channelKey) {
      return;
    }

    set((state) => {
      const current = state.typingMap[channelKey] || [];
      const exists = current.some((item) => areEntityIdsEqual(item.userId, userId));
      const next = exists
        ? current.map((item) => (areEntityIdsEqual(item.userId, userId) ? { ...item, userName } : item))
        : [...current, { userId, userName }];

      return {
        typingMap: {
          ...state.typingMap,
          [channelKey]: next,
        },
      };
    });
  },

  removeTypingUser: (channelId: EntityIdValue, userId: EntityIdValue) => {
    const channelKey = getChannelKey(channelId);
    if (!channelKey) {
      return;
    }

    set((state) => ({
      typingMap: {
        ...state.typingMap,
        [channelKey]: (state.typingMap[channelKey] || []).filter((item) => !areEntityIdsEqual(item.userId, userId)),
      },
    }));
  },

  reset: () => {
    set({
      channels: [],
      activeChannelId: null,
      messageMap: {},
      typingMap: {},
      connectionState: 'disconnected',
    });
  },
}));
