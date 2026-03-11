import { create } from 'zustand';
import type {
  ChannelMessageVo,
  ChannelUnreadChangedPayload,
  ChannelVo,
} from '@/types/chat';

export type ChatConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

interface TypingUser {
  userId: number;
  userName: string;
}

interface ChatStore {
  channels: ChannelVo[];
  activeChannelId: number | null;
  messageMap: Record<number, ChannelMessageVo[]>;
  typingMap: Record<number, TypingUser[]>;
  connectionState: ChatConnectionState;

  setChannels: (channels: ChannelVo[]) => void;
  setActiveChannel: (channelId: number | null) => void;
  setConnectionState: (state: ChatConnectionState) => void;
  setChannelMessages: (channelId: number, messages: ChannelMessageVo[]) => void;
  prependChannelMessages: (channelId: number, messages: ChannelMessageVo[]) => void;
  addMessage: (message: ChannelMessageVo) => void;
  removeMessage: (channelId: number, messageId: number) => void;
  recallMessage: (channelId: number, messageId: number) => void;
  updateUnread: (payload: ChannelUnreadChangedPayload) => void;
  setTypingUser: (channelId: number, userId: number, userName: string) => void;
  removeTypingUser: (channelId: number, userId: number) => void;
  reset: () => void;
}

function getNumericId(value: number | string | undefined | null): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : 0;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function normalizeClientRequestId(value: string | null | undefined): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const normalized = value.trim();
  return normalized ? normalized : null;
}

function normalizeMessage(message: ChannelMessageVo): ChannelMessageVo {
  const localStatus = message.voLocalStatus ?? (getNumericId(message.voId) > 0 ? 'sent' : 'sending');

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

  return getNumericId(message.voId);
}

function sortMessages(messages: ChannelMessageVo[]): ChannelMessageVo[] {
  return [...messages].sort((left, right) => {
    const timeDiff = getMessageTimestamp(left) - getMessageTimestamp(right);
    if (timeDiff !== 0) {
      return timeDiff;
    }

    return getNumericId(left.voId) - getNumericId(right.voId);
  });
}

function findMessageIndex(messages: ChannelMessageVo[], target: ChannelMessageVo): number {
  const targetId = getNumericId(target.voId);
  const targetClientRequestId = normalizeClientRequestId(target.voClientRequestId);

  if (targetId > 0) {
    const realIdIndex = messages.findIndex((item) => getNumericId(item.voId) === targetId);
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

  if (targetId !== 0) {
    return messages.findIndex((item) => getNumericId(item.voId) === targetId);
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
  return messages.filter((message) => {
    const messageId = getNumericId(message.voId);
    return messageId <= 0 || message.voLocalStatus === 'sending' || message.voLocalStatus === 'failed';
  });
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

  setActiveChannel: (channelId: number | null) => {
    set({ activeChannelId: channelId });
  },

  setConnectionState: (connectionState: ChatConnectionState) => {
    set({ connectionState });
  },

  setChannelMessages: (channelId: number, messages: ChannelMessageVo[]) => {
    set((state) => ({
      messageMap: {
        ...state.messageMap,
        [channelId]: mergeMessages(messages, extractLocalMessages(state.messageMap[channelId] || [])),
      },
    }));
  },

  prependChannelMessages: (channelId: number, messages: ChannelMessageVo[]) => {
    const current = get().messageMap[channelId] || [];
    set((state) => ({
      messageMap: {
        ...state.messageMap,
        [channelId]: mergeMessages(current, messages),
      },
    }));
  },

  addMessage: (message: ChannelMessageVo) => {
    const channelId = getNumericId(message.voChannelId);
    if (channelId <= 0) {
      return;
    }

    const current = get().messageMap[channelId] || [];
    const merged = mergeMessages(current, [message]);

    set((state) => ({
      messageMap: {
        ...state.messageMap,
        [channelId]: merged,
      },
    }));
  },

  removeMessage: (channelId: number, messageId: number) => {
    set((state) => {
      const current = state.messageMap[channelId] || [];
      if (current.length === 0) {
        return state;
      }

      return {
        messageMap: {
          ...state.messageMap,
          [channelId]: current.filter((item) => getNumericId(item.voId) !== messageId),
        },
      };
    });
  },

  recallMessage: (channelId: number, messageId: number) => {
    set((state) => {
      const current = state.messageMap[channelId] || [];
      if (current.length === 0) {
        return state;
      }

      return {
        messageMap: {
          ...state.messageMap,
          [channelId]: current.map((item) => {
            if (getNumericId(item.voId) !== messageId) {
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
        if (getNumericId(channel.voId) !== payload.channelId) {
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

  setTypingUser: (channelId: number, userId: number, userName: string) => {
    set((state) => {
      const current = state.typingMap[channelId] || [];
      const exists = current.some((item) => item.userId === userId);
      const next = exists
        ? current.map((item) => (item.userId === userId ? { ...item, userName } : item))
        : [...current, { userId, userName }];

      return {
        typingMap: {
          ...state.typingMap,
          [channelId]: next,
        },
      };
    });
  },

  removeTypingUser: (channelId: number, userId: number) => {
    set((state) => {
      const current = state.typingMap[channelId] || [];
      return {
        typingMap: {
          ...state.typingMap,
          [channelId]: current.filter((item) => item.userId !== userId),
        },
      };
    });
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
