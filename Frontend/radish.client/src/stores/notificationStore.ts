import { create } from 'zustand';
import type { LongId } from '@/api/user';
import { normalizePositiveLongIdKey } from '@/utils/longId';

/** 通知类型 */
export type NotificationType = 'system' | 'reply' | 'mention' | 'like' | 'follow' | 'lottery';

/**
 * 通知项（Store 内部使用，与后端 VO 字段对应）
 * 注意：这是 Store 内部的数据结构，UI 组件使用 NotificationItemData
 */
export interface NotificationItem {
  /** 列表项唯一 ID（优先使用用户通知关系 ID） */
  id: LongId;
  /** 后端通知 ID（用于已读/删除等接口） */
  notificationId?: LongId;
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
  businessId?: LongId | null;
  /** 业务类型 */
  businessType?: string | null;
  /** 触发者 ID */
  triggerId?: LongId | null;
  /** 触发者名称 */
  triggerName?: string | null;
  /** 触发者头像 */
  triggerAvatar?: string | null;
  /** 扩展数据（JSON 字符串） */
  extData?: string | null;
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
  removeNotification: (notificationId: LongId) => void;

  /** 标记通知已读 */
  markAsRead: (notificationIds: LongId[]) => void;

  /** 标记全部已读 */
  markAllAsRead: () => void;

  /** 清空最近通知 */
  clearRecentNotifications: () => void;
}

function isSameNotificationList(a: NotificationItem[], b: NotificationItem[]): boolean {
  return a.length === b.length && a.every((item, index) => item === b[index]);
}

function getNotificationPrimaryKey(item: Pick<NotificationItem, 'id' | 'notificationId' | 'createdAt' | 'type'>): string {
  return normalizePositiveLongIdKey(item.notificationId)
    ?? normalizePositiveLongIdKey(item.id)
    ?? `${String(item.notificationId ?? item.id)}|${item.createdAt}|${item.type}`;
}

function matchesNotificationId(
  item: Pick<NotificationItem, 'id' | 'notificationId'>,
  notificationIdKey: string
): boolean {
  return normalizePositiveLongIdKey(item.id) === notificationIdKey
    || normalizePositiveLongIdKey(item.notificationId) === notificationIdKey;
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
    const seen = new Set<string>();

    for (const item of notifications) {
      const key = getNotificationPrimaryKey(item);
      if (seen.has(key)) continue;
      seen.add(key);
      unique.push(item);
    }

    const trimmed = unique.slice(0, 20);

    // 仅维护最近通知列表，不用本地切片覆盖全局未读数。
    // 全局未读数由服务端事件/接口同步，避免列表页与 Dock 角标口径不一致。
    set((state) => {
      if (isSameNotificationList(state.recentNotifications, trimmed)) {
        return state;
      }

      return {
        recentNotifications: trimmed
      };
    });
  },

  addNotification: (notification: NotificationItem) => {
    set(state => ({
      recentNotifications: (() => {
        const notificationKey = getNotificationPrimaryKey(notification);
        const existingIndex = state.recentNotifications.findIndex(
          (item) => getNotificationPrimaryKey(item) === notificationKey
        );
        if (existingIndex === -1) {
          return [notification, ...state.recentNotifications].slice(0, 20);
        }

        const next = state.recentNotifications.slice();
        next[existingIndex] = { ...next[existingIndex], ...notification };
        return next;
      })()
    }));
  },

  removeNotification: (notificationId: LongId) => {
    const notificationIdKey = normalizePositiveLongIdKey(notificationId);
    if (!notificationIdKey) {
      return;
    }

    set(state => {
      const removedUnread = state.recentNotifications.filter(
        (item) => matchesNotificationId(item, notificationIdKey) && !item.isRead
      ).length;
      return {
        recentNotifications: state.recentNotifications.filter(
          (item) => !matchesNotificationId(item, notificationIdKey)
        ),
        unreadCount: Math.max(0, state.unreadCount - removedUnread)
      };
    });
  },

  markAsRead: (notificationIds: LongId[]) => {
    const idSet = new Set(
      notificationIds
        .map((id) => normalizePositiveLongIdKey(id))
        .filter((id): id is string => id !== null)
    );

    if (idSet.size === 0) {
      return;
    }

    set(state => {
      const shouldMarkItem = (item: Pick<NotificationItem, 'id' | 'notificationId'>) => (
        Array.from(idSet).some((id) => matchesNotificationId(item, id))
      );
      const updated = state.recentNotifications.map(n =>
        shouldMarkItem(n)
          ? { ...n, isRead: true }
          : n
      );
      const markedUnreadCount = state.recentNotifications.filter(
        (item) => !item.isRead && shouldMarkItem(item)
      ).length;
      return {
        recentNotifications: updated,
        unreadCount: Math.max(0, state.unreadCount - markedUnreadCount)
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
