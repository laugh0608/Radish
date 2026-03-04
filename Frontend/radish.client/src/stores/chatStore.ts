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

function mergeMessages(base: ChannelMessageVo[], incoming: ChannelMessageVo[]): ChannelMessageVo[] {
  const map = new Map<number, ChannelMessageVo>();

  for (const item of base) {
    const id = getNumericId(item.voId);
    if (id > 0) {
      map.set(id, item);
    }
  }

  for (const item of incoming) {
    const id = getNumericId(item.voId);
    if (id > 0) {
      const existing = map.get(id);
      map.set(id, existing ? { ...existing, ...item } : item);
    }
  }

  return Array.from(map.values()).sort((a, b) => getNumericId(a.voId) - getNumericId(b.voId));
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
        [channelId]: mergeMessages([], messages),
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
