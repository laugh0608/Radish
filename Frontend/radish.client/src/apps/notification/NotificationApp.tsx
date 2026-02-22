import { useEffect, useMemo, useState, useCallback } from 'react';
import { log } from '@/utils/logger';
import { NotificationList } from '@radish/ui/notification-list';
import type { NotificationItemData } from '@radish/ui/notification';
import { notificationApi, type UserNotificationVo } from '@/api/notification';
import { useNotificationStore, type NotificationItem } from '@/stores/notificationStore';
import { toast } from '@radish/ui/toast';
import styles from './NotificationApp.module.css';

/**
 * 通知中心 App
 *
 * 作为独立应用在 WebOS 中运行，显示完整的通知列表和管理功能
 *
 * 注意：SignalR 连接由 Shell 统一管理，此组件只负责读取状态和调用方法
 */
export const NotificationApp = () => {
  const { unreadCount, recentNotifications } = useNotificationStore();

  interface NotificationListItem extends NotificationItemData {
    notificationId?: number;
  }

  const [notifications, setNotifications] = useState<NotificationListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [pageIndex, setPageIndex] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [activeFilter, setActiveFilter] = useState<'all' | 'unread' | 'mention' | 'comment' | 'like'>('all');
  const pageSize = 20;

  const mapApiNotificationToStore = (n: UserNotificationVo): NotificationItem => {
    const notification = n.voNotification;
    return {
      id: n.voId,
      notificationId: n.voNotificationId,
      type: mapNotificationTypeToStore(notification?.voType || 'System'),
      title: notification?.voTitle || '',
      content: notification?.voContent || '',
      isRead: n.voIsRead,
      createdAt: n.voCreateTime,
      businessId: notification?.voBusinessId,
      businessType: notification?.voBusinessType,
      triggerId: notification?.voTriggerId,
      triggerName: notification?.voTriggerName,
      triggerAvatar: notification?.voTriggerAvatar
    };
  };

  const mapApiNotificationToUi = (n: UserNotificationVo): NotificationListItem => {
    const notification = n.voNotification;
    return {
      id: n.voId,
      notificationId: n.voNotificationId,
      type: mapNotificationTypeToStore(notification?.voType || 'System'),
      title: notification?.voTitle || '',
      content: notification?.voContent || '',
      priority: notification?.voPriority ?? 1,
      businessType: notification?.voBusinessType,
      businessId: notification?.voBusinessId,
      triggerId: notification?.voTriggerId,
      triggerName: notification?.voTriggerName,
      triggerAvatar: notification?.voTriggerAvatar,
      isRead: n.voIsRead,
      createdAt: n.voCreateTime
    };
  };

  const mapStoreToUi = (items: NotificationItem[]): NotificationListItem[] => {
    return items.map((n) => ({
      id: n.id,
      notificationId: n.notificationId ?? n.id,
      type: n.type,
      title: n.title,
      content: n.content,
      priority: 1,
      businessType: n.businessType,
      businessId: n.businessId,
      triggerId: n.triggerId,
      triggerName: n.triggerName,
      triggerAvatar: n.triggerAvatar,
      isRead: n.isRead,
      createdAt: n.createdAt
    }));
  };

  const mergeNotifications = (
    base: NotificationListItem[],
    incoming: NotificationListItem[]
  ) => {
    if (incoming.length === 0) return base;
    const getMergeKey = (item: NotificationListItem) =>
      `${item.notificationId ?? item.id}|${item.createdAt}|${item.type}`;
    const baseMap = new Map<string, NotificationListItem>();
    for (const item of base) {
      baseMap.set(getMergeKey(item), item);
    }
    const result: NotificationListItem[] = [];
    const seen = new Set<string>();
    for (const item of incoming) {
      const mergeKey = getMergeKey(item);
      const existing = baseMap.get(mergeKey);
      result.push(existing ? { ...existing, ...item } : item);
      seen.add(mergeKey);
    }
    for (const item of base) {
      if (!seen.has(getMergeKey(item))) {
        result.push(item);
      }
    }
    return result;
  };

  const resolveBackendNotificationId = useCallback((uiId: number): number => {
    const current = notifications.find(item => item.id === uiId);
    if (current?.notificationId !== undefined) {
      return current.notificationId;
    }

    const fromStore = recentNotifications.find(item => item.id === uiId);
    if (fromStore?.notificationId !== undefined) {
      return fromStore.notificationId;
    }

    return uiId;
  }, [notifications, recentNotifications]);

  const syncUnreadCountFromServer = useCallback(async () => {
    try {
      const unreadCountFromServer = await notificationApi.getUnreadCount();
      useNotificationStore.getState().setUnreadCount(unreadCountFromServer);
    } catch (error) {
      log.warn('同步未读数量失败:', error);
    }
  }, []);

  // 将 Store 中的通知转换为 UI 组件需要的格式
  useEffect(() => {
    const converted = mapStoreToUi(recentNotifications);
    setNotifications(prev => mergeNotifications(prev, converted));
  }, [recentNotifications]);

  // 初始加载通知列表
  useEffect(() => {
    const loadNotifications = async () => {
      if (typeof window === 'undefined') return;
      const token = window.localStorage.getItem('access_token');
      if (!token) return;

      setLoading(true);
      try {
        const result = await notificationApi.getMyNotifications({
          pageIndex: 1,
          pageSize
        });

        if (result && result.data) {
          const store = useNotificationStore.getState();
          // 从嵌套的 UserNotificationVo 结构转换为 NotificationItem
          // 这是业务层的职责：将后端 VO 转换为内部数据结构
          store.setRecentNotifications(
            result.data.map(mapApiNotificationToStore)
          );
          const firstPage = result.data.map(mapApiNotificationToUi);
          setNotifications(firstPage);
          setPageIndex(result.page);
          setHasMore(result.page < result.pageCount);
          await syncUnreadCountFromServer();
        } else {
          setHasMore(false);
        }
      } catch (error) {
        log.error('加载通知列表失败:', error);
        toast.error('加载通知列表失败');
        setHasMore(false);
      } finally {
        setLoading(false);
      }
    };

    void loadNotifications();
  }, []);

  const handleLoadMore = useCallback(async () => {
    if (loadingMore || loading || !hasMore) return;
    const nextPage = pageIndex + 1;
    setLoadingMore(true);
    try {
      const result = await notificationApi.getMyNotifications({
        pageIndex: nextPage,
        pageSize
      });
      if (result && result.data) {
        const nextItems = result.data.map(mapApiNotificationToUi);
        setNotifications(prev => {
          const indexMap = new Map<number, number>();
          const merged = prev.slice();
          merged.forEach((item, idx) => indexMap.set(item.id, idx));
          for (const item of nextItems) {
            const existingIndex = indexMap.get(item.id);
            if (existingIndex !== undefined) {
              merged[existingIndex] = { ...merged[existingIndex], ...item };
            } else {
              indexMap.set(item.id, merged.length);
              merged.push(item);
            }
          }
          return merged;
        });
        setPageIndex(result.page);
        setHasMore(result.page < result.pageCount);
      } else {
        setHasMore(false);
      }
    } catch (error) {
      log.error('加载更多通知失败:', error);
      toast.error('加载更多通知失败');
    } finally {
      setLoadingMore(false);
    }
  }, [hasMore, loading, loadingMore, pageIndex, pageSize]);

  const filteredNotifications = useMemo(() => {
    switch (activeFilter) {
      case 'unread':
        return notifications.filter(n => !n.isRead);
      case 'mention':
        return notifications.filter(n => n.type === 'mention');
      case 'comment':
        return notifications.filter(n => n.type === 'reply');
      case 'like':
        return notifications.filter(n => n.type === 'like');
      default:
        return notifications;
    }
  }, [activeFilter, notifications]);

  // 标记已读
  const handleMarkAsRead = useCallback(async (id: number) => {
    try {
      const backendNotificationId = resolveBackendNotificationId(id);

      const success = await notificationApi.markAsRead(backendNotificationId);
      if (!success) {
        throw new Error(`标记通知已读失败，NotificationId: ${backendNotificationId}`);
      }

      const store = useNotificationStore.getState();
      store.markAsRead([backendNotificationId]);
      setNotifications(prev => prev.map(n => (n.id === id ? { ...n, isRead: true } : n)));
      await syncUnreadCountFromServer();

      toast.success('已标记为已读');
    } catch (error) {
      log.error('标记已读失败:', error);
      toast.error('标记已读失败');
    }
  }, [resolveBackendNotificationId, syncUnreadCountFromServer]);

  // 点击通知
  const handleNotificationClick = useCallback((notification: NotificationItemData) => {
    log.debug('点击通知:', notification);

    // 如果未读，标记为已读
    if (!notification.isRead) {
      void handleMarkAsRead(notification.id);
    }

    // TODO: 根据 businessType 跳转到对应页面
    if (notification.businessType === 'Post' && notification.businessId) {
      toast.info(`跳转到帖子 ${notification.businessId}`);
    } else if (notification.businessType === 'Comment' && notification.businessId) {
      toast.info(`跳转到评论 ${notification.businessId}`);
    }
  }, [handleMarkAsRead]);

  // 标记全部已读
  const handleMarkAllAsRead = useCallback(async () => {
    try {
      const success = await notificationApi.markAllAsRead();
      if (!success) {
        throw new Error('标记全部已读失败');
      }

      const store = useNotificationStore.getState();
      store.markAllAsRead();
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      await syncUnreadCountFromServer();

      toast.success('已标记全部为已读');
    } catch (error) {
      log.error('标记全部已读失败:', error);
      toast.error('标记全部已读失败');
    }
  }, [syncUnreadCountFromServer]);

  // 删除通知
  const handleDelete = useCallback(async (id: number) => {
    try {
      const backendNotificationId = resolveBackendNotificationId(id);
      await notificationApi.deleteNotification(backendNotificationId);

      // 更新 Store：从列表中移除该通知（兼容 id/notificationId）
      const store = useNotificationStore.getState();
      store.removeNotification(backendNotificationId);
      setNotifications(prev => prev.filter(n => (n.notificationId ?? n.id) !== backendNotificationId));

      toast.success('通知已删除');
    } catch (error) {
      log.error('删除通知失败:', error);
      toast.error('删除通知失败');
    }
  }, [resolveBackendNotificationId]);

  return (
    <div className={styles.notificationApp}>
      <div className={styles.header}>
        <div className={styles.headerTop}>
          <h1 className={styles.title}>通知中心</h1>
          <div className={styles.actions}>
            <span className={styles.count}>
              {unreadCount > 0 ? `${unreadCount} 条未读` : '全部已读'}
            </span>
            {unreadCount > 0 && (
              <button className={styles.markAllBtn} onClick={handleMarkAllAsRead}>
                全部已读
              </button>
            )}
          </div>
        </div>
        <div className={styles.filters}>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'all' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('all')}
          >
            全部
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'unread' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('unread')}
          >
            未读
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'mention' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('mention')}
          >
            @我
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'comment' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('comment')}
          >
            评论
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'like' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('like')}
          >
            点赞
          </button>
        </div>
      </div>
      <div className={styles.content}>
        <NotificationList
          notifications={filteredNotifications}
          loading={loading}
          hasMore={hasMore}
          loadingMore={loadingMore}
          onEndReached={handleLoadMore}
          onNotificationClick={handleNotificationClick}
          onMarkAsRead={handleMarkAsRead}
          onDelete={handleDelete}
        />
      </div>
    </div>
  );
};

/**
 * 将 API 返回的通知类型映射到 Store 的类型
 * 注意：这是类型枚举映射，不是字段名映射
 */
function mapNotificationTypeToStore(type: string): 'system' | 'reply' | 'mention' | 'like' | 'follow' {
  const typeMap: Record<string, 'system' | 'reply' | 'mention' | 'like' | 'follow'> = {
    'System': 'system',
    'CommentReply': 'reply',
    'CommentReplied': 'reply',
    'Mention': 'mention',
    'Mentioned': 'mention',
    'PostLiked': 'like',
    'CommentLiked': 'like',
    'Follow': 'follow',
    'Followed': 'follow'
  };
  return typeMap[type] || 'system';
}
