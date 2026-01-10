import { useEffect, useState, useCallback } from 'react';
import { NotificationCenter, notificationApi, type NotificationItemData } from '@radish/ui';
import { useNotificationStore } from '@/stores/notificationStore';
import { useNotificationHub } from '@/services/notificationHub';
import { toast } from '@radish/ui';

/**
 * NotificationCenter 适配器
 *
 * 连接 @radish/ui 的 NotificationCenter 组件与 radish.client 的状态管理和 API
 */
export const NotificationCenterAdapter = () => {
  const { unreadCount, recentNotifications } = useNotificationStore();
  const { start, stop, markAsRead: hubMarkAsRead, markAllAsRead: hubMarkAllAsRead } = useNotificationHub();

  const [notifications, setNotifications] = useState<NotificationItemData[]>([]);
  const [loading, setLoading] = useState(false);

  // 启动 SignalR 连接
  useEffect(() => {
    void start();
    return () => {
      void stop();
    };
  }, [start, stop]);

  // 将 Store 中的通知转换为 UI 组件需要的格式
  useEffect(() => {
    const converted: NotificationItemData[] = recentNotifications.map(n => ({
      id: n.id,
      type: mapNotificationType(n.type),
      title: n.title,
      content: n.content,
      priority: 1, // 默认优先级
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
          pageSize: 10
        });

        // 将 API 返回的通知添加到 Store
        const store = useNotificationStore.getState();
        result.notifications.forEach(n => {
          store.addNotification({
            id: n.id,
            type: mapNotificationTypeReverse(n.type),
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
      // 通过 SignalR Hub 标记已读（会同步到其他端）
      await hubMarkAsRead(id);

      // 同时调用 API 确保持久化
      await notificationApi.markAsRead([id]);
    } catch (error) {
      console.error('标记已读失败:', error);
      toast.error('标记已读失败');
    }
  }, [hubMarkAsRead]);

  // 标记全部已读
  const handleMarkAllAsRead = useCallback(async () => {
    try {
      // 通过 SignalR Hub 标记全部已读
      await hubMarkAllAsRead();

      // 同时调用 API 确保持久化
      await notificationApi.markAllAsRead();

      toast.success('已标记全部为已读');
    } catch (error) {
      console.error('标记全部已读失败:', error);
      toast.error('标记全部已读失败');
    }
  }, [hubMarkAllAsRead]);

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

  // 查看更多
  const handleViewMore = useCallback(() => {
    // TODO: 跳转到通知列表页面
    toast.info('跳转到通知列表页面（待实现）');
  }, []);

  return (
    <NotificationCenter
      unreadCount={unreadCount}
      notifications={notifications}
      loading={loading}
      onNotificationClick={handleNotificationClick}
      onMarkAsRead={handleMarkAsRead}
      onMarkAllAsRead={handleMarkAllAsRead}
      onDelete={handleDelete}
      onViewMore={handleViewMore}
    />
  );
};

/**
 * 将 Store 的通知类型映射到 UI 组件的类型
 */
function mapNotificationType(type: string): string {
  const typeMap: Record<string, string> = {
    'system': 'System',
    'reply': 'CommentReply',
    'mention': 'Mention',
    'like': 'PostLiked',
    'follow': 'Follow'
  };
  return typeMap[type] || 'System';
}

/**
 * 将 UI 组件的通知类型映射回 Store 的类型
 */
function mapNotificationTypeReverse(type: string): 'system' | 'reply' | 'mention' | 'like' | 'follow' {
  const typeMap: Record<string, 'system' | 'reply' | 'mention' | 'like' | 'follow'> = {
    'System': 'system',
    'CommentReply': 'reply',
    'Mention': 'mention',
    'PostLiked': 'like',
    'CommentLiked': 'like',
    'Follow': 'follow'
  };
  return typeMap[type] || 'system';
}
