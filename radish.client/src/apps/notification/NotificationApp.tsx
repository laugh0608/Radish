import { useEffect, useState, useCallback } from 'react';
import { NotificationList, notificationApi, type NotificationItemData } from '@radish/ui';
import { useNotificationStore } from '@/stores/notificationStore';
import { notificationHub } from '@/services/notificationHub';
import { toast } from '@radish/ui';
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

  const [notifications, setNotifications] = useState<NotificationItemData[]>([]);
  const [loading, setLoading] = useState(false);

  // 将 Store 中的通知转换为 UI 组件需要的格式
  useEffect(() => {
    const converted: NotificationItemData[] = recentNotifications.map(n => ({
      id: n.id,
      type: n.type,
      title: n.title,
      content: n.content,
      priority: 1,
      businessType: n.sourceType,
      businessId: n.sourceId,
      triggerId: n.actorId,
      triggerName: n.actorName,
      triggerAvatar: n.actorAvatar,
      isRead: n.isRead,
      createdAt: n.createdAt
    }));
    setNotifications(converted);
  }, [recentNotifications]);

  // 初始加载通知列表
  useEffect(() => {
    const loadNotifications = async () => {
      if (typeof window === 'undefined') return;
      const token = window.localStorage.getItem('access_token');
      if (!token) return;

      setLoading(true);
      try {
        const result = await notificationApi.getList({
          pageIndex: 1,
          pageSize: 20
        });

        // API 已经返回了正确格式的数据，直接添加到 Store
        const store = useNotificationStore.getState();
        result.notifications.forEach(n => {
          store.addNotification({
            id: n.id,
            type: mapNotificationTypeToStore(n.type),
            title: n.title,
            content: n.content,
            isRead: n.isRead,
            createdAt: n.createdAt,
            sourceId: n.businessId,
            sourceType: n.businessType,
            actorId: n.triggerId,
            actorName: n.triggerName,
            actorAvatar: n.triggerAvatar
          });
        });
      } catch (error) {
        console.error('加载通知列表失败:', error);
        toast.error('加载通知列表失败');
      } finally {
        setLoading(false);
      }
    };

    void loadNotifications();
  }, []);

  // 点击通知
  const handleNotificationClick = useCallback((notification: NotificationItemData) => {
    console.log('点击通知:', notification);

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
  }, []);

  // 标记已读
  const handleMarkAsRead = useCallback(async (id: number) => {
    try {
      await notificationHub.markAsRead(id);
      await notificationApi.markAsRead([id]);
    } catch (error) {
      console.error('标记已读失败:', error);
      toast.error('标记已读失败');
    }
  }, []);

  // 标记全部已读
  const handleMarkAllAsRead = useCallback(async () => {
    try {
      await notificationHub.markAllAsRead();
      await notificationApi.markAllAsRead();
      toast.success('已标记全部为已读');
    } catch (error) {
      console.error('标记全部已读失败:', error);
      toast.error('标记全部已读失败');
    }
  }, []);

  // 删除通知
  const handleDelete = useCallback(async (id: number) => {
    try {
      await notificationApi.deleteNotification(id);

      // 从 Store 中移除
      const store = useNotificationStore.getState();
      const notification = recentNotifications.find(n => n.id === id);
      if (notification && !notification.isRead) {
        store.setUnreadCount(unreadCount - 1);
      }

      toast.success('通知已删除');
    } catch (error) {
      console.error('删除通知失败:', error);
      toast.error('删除通知失败');
    }
  }, [recentNotifications, unreadCount]);

  return (
    <div className={styles.notificationApp}>
      <div className={styles.header}>
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
      <div className={styles.content}>
        <NotificationList
          notifications={notifications}
          loading={loading}
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
