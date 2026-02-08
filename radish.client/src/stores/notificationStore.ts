import { create } from 'zustand';

/** 通知类型 */
export type NotificationType = 'system' | 'reply' | 'mention' | 'like' | 'follow';

/**
 * 通知项（Store 内部使用，与后端 VO 字段对应）
 * 注意：这是 Store 内部的数据结构，UI 组件使用 NotificationItemData
 */
export interface NotificationItem {
  /** 通知 ID */
  id: number;
  /** 通知类型 */
  type: NotificationType;
  /** 通知标题 */
  title: string;
  /** 通知内容 */
  content: string;
  /** 是否已读 */
  isRead: boolean;
  /** 创建时间 */
  createdAt: string;
  /** 业务 ID */
  businessId?: number | null;
  /** 业务类型 */
  businessType?: string | null;
  /** 触发者 ID */
  triggerId?: number | null;
  /** 触发者名称 */
  triggerName?: string | null;
  /** 触发者头像 */
  triggerAvatar?: string | null;
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

  /** 设置最近通知（覆盖） */
  setRecentNotifications: (notifications: NotificationItem[]) => void;

  /** 添加新通知 */
  addNotification: (notification: NotificationItem) => void;

  /** 移除通知（按通知 ID） */
  removeNotification: (notificationId: number) => void;

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

  setRecentNotifications: (notifications: NotificationItem[]) => {
    const unique: NotificationItem[] = [];
    const seen = new Set<number>();

    for (const item of notifications) {
      if (seen.has(item.id)) continue;
      seen.add(item.id);
      unique.push(item);
    }

    const trimmed = unique.slice(0, 20);
    const unreadCount = trimmed.filter(item => !item.isRead).length;

    set({
      recentNotifications: trimmed,
      unreadCount
    });
  },

  addNotification: (notification: NotificationItem) => {
    set(state => ({
      recentNotifications: (() => {
        const existingIndex = state.recentNotifications.findIndex(n => n.id === notification.id);
        if (existingIndex === -1) {
          return [notification, ...state.recentNotifications].slice(0, 20);
        }

        const next = state.recentNotifications.slice();
        next[existingIndex] = { ...next[existingIndex], ...notification };
        return next;
      })(),
      unreadCount: (() => {
        const existing = state.recentNotifications.find(n => n.id === notification.id);
        if (!existing) {
          return notification.isRead ? state.unreadCount : state.unreadCount + 1;
        }

        const wasUnread = !existing.isRead;
        const nowUnread = !notification.isRead;
        const delta = (nowUnread ? 1 : 0) - (wasUnread ? 1 : 0);
        return Math.max(0, state.unreadCount + delta);
      })()
    }));
  },

  removeNotification: (notificationId: number) => {
    set(state => {
      const removedUnread = state.recentNotifications.filter(n => n.id === notificationId && !n.isRead).length;
      return {
        recentNotifications: state.recentNotifications.filter(n => n.id !== notificationId),
        unreadCount: Math.max(0, state.unreadCount - removedUnread)
      };
    });
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
