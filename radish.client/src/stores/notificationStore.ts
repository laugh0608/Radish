import { create } from 'zustand';

/** 通知类型 */
export type NotificationType = 'system' | 'reply' | 'mention' | 'like' | 'follow';

/** 通知项 */
export interface NotificationItem {
  id: number;
  type: NotificationType;
  title: string;
  content: string;
  isRead: boolean;
  createdAt: string;
  sourceId?: number;
  sourceType?: string;
  actorId?: number;
  actorName?: string;
  actorAvatar?: string;
}

/** 连接状态 */
export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

interface NotificationStore {
  /** 未读数量 */
  unreadCount: number;

  /** 连接状态 */
  connectionState: ConnectionState;

  /** 最近通知列表（用于弹窗预览） */
  recentNotifications: NotificationItem[];

  /** 设置未读数量 */
  setUnreadCount: (count: number) => void;

  /** 增加未读数量 */
  incrementUnreadCount: (delta?: number) => void;

  /** 设置连接状态 */
  setConnectionState: (state: ConnectionState) => void;

  /** 添加新通知 */
  addNotification: (notification: NotificationItem) => void;

  /** 标记通知已读 */
  markAsRead: (notificationIds: number[]) => void;

  /** 标记全部已读 */
  markAllAsRead: () => void;

  /** 清空最近通知 */
  clearRecentNotifications: () => void;
}

export const useNotificationStore = create<NotificationStore>((set) => ({
  unreadCount: 0,
  connectionState: 'disconnected',
  recentNotifications: [],

  setUnreadCount: (count: number) => {
    set({ unreadCount: Math.max(0, count) });
  },

  incrementUnreadCount: (delta = 1) => {
    set(state => ({ unreadCount: Math.max(0, state.unreadCount + delta) }));
  },

  setConnectionState: (connectionState: ConnectionState) => {
    set({ connectionState });
  },

  addNotification: (notification: NotificationItem) => {
    set(state => ({
      recentNotifications: [notification, ...state.recentNotifications].slice(0, 20),
      unreadCount: notification.isRead ? state.unreadCount : state.unreadCount + 1
    }));
  },

  markAsRead: (notificationIds: number[]) => {
    const idSet = new Set(notificationIds);
    set(state => {
      const updated = state.recentNotifications.map(n =>
        idSet.has(n.id) ? { ...n, isRead: true } : n
      );
      const markedCount = state.recentNotifications.filter(n => idSet.has(n.id) && !n.isRead).length;
      return {
        recentNotifications: updated,
        unreadCount: Math.max(0, state.unreadCount - markedCount)
      };
    });
  },

  markAllAsRead: () => {
    set(state => ({
      recentNotifications: state.recentNotifications.map(n => ({ ...n, isRead: true })),
      unreadCount: 0
    }));
  },

  clearRecentNotifications: () => {
    set({ recentNotifications: [] });
  }
}));
