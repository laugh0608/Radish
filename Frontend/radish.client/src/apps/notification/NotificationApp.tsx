import { useEffect, useMemo, useRef, useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDistanceToNow } from 'date-fns';
import { enUS, zhCN } from 'date-fns/locale';
import { log } from '@/utils/logger';
import { NotificationList } from '@radish/ui/notification-list';
import type { NotificationItemData } from '@radish/ui/notification';
import { notificationApi, type UserNotificationVo } from '@/api/notification';
import { getPublicProfile, type LongId } from '@/api/user';
import { getApiBaseUrl } from '@/config/env';
import { useNotificationStore, type NotificationItem } from '@/stores/notificationStore';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import { tokenService } from '@/services/tokenService';
import { toast } from '@radish/ui/toast';
import { buildChatAppParams, parseChatNotificationNavigation } from '@/utils/chatNavigation';
import { buildForumAppParams, parseForumNotificationNavigation } from '@/utils/forumNavigation';
import { isSameLongId, normalizePositiveLongIdKey } from '@/utils/longId';
import styles from './NotificationApp.module.css';

/**
 * 通知中心 App
 *
 * 作为独立应用在 WebOS 中运行，显示完整的通知列表和管理功能
 *
 * 注意：SignalR 连接由 Shell 统一管理，此组件只负责读取状态和调用方法
 */
export const NotificationApp = () => {
  const { t, i18n } = useTranslation();
  const { openApp, openOrReuseApp } = useWindowStore();
  const currentUserId = useUserStore((state) => state.userId);
  const { unreadCount, recentNotifications } = useNotificationStore();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const triggerAvatarCacheRef = useRef(new Map<string, string | null>());

  interface NotificationListItem extends NotificationItemData {
    notificationId?: LongId;
  }

  const resolveNotificationAvatar = useCallback((avatarUrl?: string | null) => {
    const normalized = avatarUrl?.trim();
    if (!normalized) {
      return null;
    }

    if (/^https?:\/\//i.test(normalized)) {
      return normalized;
    }

    if (normalized.startsWith('/')) {
      return `${apiBaseUrl}${normalized}`;
    }

    return `${apiBaseUrl}/${normalized}`;
  }, [apiBaseUrl]);

  const [notifications, setNotifications] = useState<NotificationListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [pageIndex, setPageIndex] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [activeFilter, setActiveFilter] = useState<'all' | 'unread' | 'mention' | 'comment' | 'like'>('all');
  const pageSize = 20;
  const getNotificationKey = useCallback((item: Pick<NotificationListItem, 'id' | 'notificationId'>) => (
    normalizePositiveLongIdKey(item.notificationId)
    ?? normalizePositiveLongIdKey(item.id)
    ?? String(item.notificationId ?? item.id)
  ), []);
  const notificationListLabels = useMemo(() => ({
    loading: t('notification.shared.loading'),
    loadingMore: t('notification.shared.loadingMore'),
    loadMore: t('notification.shared.loadMore'),
    loadedAll: t('notification.shared.loadedAll'),
    emptyTitle: t('notification.shared.emptyTitle'),
    emptyHint: t('notification.shared.emptyHint'),
    markAsRead: t('notification.shared.markAsRead'),
    delete: t('notification.shared.delete'),
  }), [t]);
  const formatRelativeTime = useCallback((createdAt: string) => {
    const locale = i18n.language.startsWith('en') ? enUS : zhCN;
    return formatDistanceToNow(new Date(createdAt), {
      addSuffix: true,
      locale,
    });
  }, [i18n.language]);

  const mapApiNotificationToStore = useCallback((n: UserNotificationVo): NotificationItem => {
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
      triggerAvatar: resolveNotificationAvatar(notification?.voTriggerAvatar),
      extData: notification?.voExtData
    };
  }, [resolveNotificationAvatar]);

  const mapApiNotificationToUi = useCallback((n: UserNotificationVo): NotificationListItem => {
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
      triggerAvatar: resolveNotificationAvatar(notification?.voTriggerAvatar),
      extData: notification?.voExtData,
      isRead: n.voIsRead,
      createdAt: n.voCreateTime
    };
  }, [resolveNotificationAvatar]);

  const mapStoreToUi = useCallback((items: NotificationItem[]): NotificationListItem[] => {
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
      triggerAvatar: resolveNotificationAvatar(n.triggerAvatar),
      extData: n.extData,
      isRead: n.isRead,
      createdAt: n.createdAt
    }));
  }, [resolveNotificationAvatar]);

  const patchNotificationsWithResolvedAvatars = useCallback((avatarMap: Map<string, string | null>) => {
    if (avatarMap.size === 0) {
      return;
    }

    const store = useNotificationStore.getState();
    let hasUiChanges = false;

    setNotifications((prev) => {
      const next = prev.map((item) => {
        const triggerIdKey = normalizePositiveLongIdKey(item.triggerId);
        if (item.triggerAvatar || !triggerIdKey) {
          return item;
        }

        const resolvedAvatar = avatarMap.get(triggerIdKey);
        if (!resolvedAvatar) {
          return item;
        }

        hasUiChanges = true;
        return { ...item, triggerAvatar: resolvedAvatar };
      });

      return hasUiChanges ? next : prev;
    });

    const nextStoreItems = store.recentNotifications.map((item) => {
      const triggerIdKey = normalizePositiveLongIdKey(item.triggerId);
      if (item.triggerAvatar || !triggerIdKey) {
        return item;
      }

      const resolvedAvatar = avatarMap.get(triggerIdKey);
      return resolvedAvatar ? { ...item, triggerAvatar: resolvedAvatar } : item;
    });

    if (nextStoreItems.some((item, index) => item !== store.recentNotifications[index])) {
      store.setRecentNotifications(nextStoreItems);
    }
  }, []);

  const backfillTriggerAvatars = useCallback(async (items: NotificationListItem[]) => {
    const missingTriggerIds = Array.from(new Set(
      items
        .filter((item) => !item.triggerAvatar)
        .map((item) => normalizePositiveLongIdKey(item.triggerId))
        .filter((triggerId): triggerId is string => Boolean(triggerId))
    ));

    if (missingTriggerIds.length === 0) {
      return;
    }

    const unresolvedTriggerIds = missingTriggerIds.filter((triggerId) => !triggerAvatarCacheRef.current.has(triggerId));
    if (unresolvedTriggerIds.length > 0) {
      const profileResults = await Promise.allSettled(
        unresolvedTriggerIds.map(async (triggerId) => {
          const profile = await getPublicProfile(triggerId);
          return {
            triggerId,
            avatar: resolveNotificationAvatar(profile.voAvatarThumbnailUrl || profile.voAvatarUrl)
          };
        })
      );

      for (const result of profileResults) {
        if (result.status === 'fulfilled') {
          triggerAvatarCacheRef.current.set(result.value.triggerId, result.value.avatar);
        }
      }
    }

    const resolvedAvatarMap = new Map<string, string | null>();
    for (const triggerId of missingTriggerIds) {
      resolvedAvatarMap.set(triggerId, triggerAvatarCacheRef.current.get(triggerId) ?? null);
    }

    patchNotificationsWithResolvedAvatars(resolvedAvatarMap);
  }, [patchNotificationsWithResolvedAvatars, resolveNotificationAvatar]);

  const mergeNotifications = useCallback((
    base: NotificationListItem[],
    incoming: NotificationListItem[]
  ) => {
    if (incoming.length === 0) return base;
    const baseMap = new Map<string, NotificationListItem>();
    for (const item of base) {
      baseMap.set(getNotificationKey(item), item);
    }
    const result: NotificationListItem[] = [];
    const seen = new Set<string>();
    for (const item of incoming) {
      const mergeKey = getNotificationKey(item);
      const existing = baseMap.get(mergeKey);
      result.push(existing ? { ...existing, ...item } : item);
      seen.add(mergeKey);
    }
    for (const item of base) {
      if (!seen.has(getNotificationKey(item))) {
        result.push(item);
      }
    }
    return result;
  }, [getNotificationKey]);

  const resolveBackendNotificationId = useCallback((uiId: LongId): LongId | null => {
    const uiIdKey = normalizePositiveLongIdKey(uiId);
    if (!uiIdKey) {
      return null;
    }

    const current = notifications.find((item) => normalizePositiveLongIdKey(item.id) === uiIdKey);
    if (current?.notificationId !== undefined) {
      return current.notificationId;
    }

    const fromStore = recentNotifications.find((item) => normalizePositiveLongIdKey(item.id) === uiIdKey);
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
    void backfillTriggerAvatars(converted);
  }, [backfillTriggerAvatars, mapStoreToUi, mergeNotifications, recentNotifications]);

  // 初始加载通知列表
  useEffect(() => {
    const loadNotifications = async () => {
      if (typeof window === 'undefined') return;
      const token = tokenService.getAccessToken();
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
          void backfillTriggerAvatars(firstPage);
          setPageIndex(result.page);
          setHasMore(result.page < result.pageCount);
          await syncUnreadCountFromServer();
        } else {
          setHasMore(false);
        }
      } catch (error) {
        log.error('加载通知列表失败:', error);
        toast.error(t('notification.loadFailed'));
        setHasMore(false);
      } finally {
        setLoading(false);
      }
    };

    void loadNotifications();
  }, [backfillTriggerAvatars, mapApiNotificationToStore, mapApiNotificationToUi, syncUnreadCountFromServer, t]);

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
        void backfillTriggerAvatars(nextItems);
        setNotifications(prev => {
          const indexMap = new Map<string, number>();
          const merged = prev.slice();
          merged.forEach((item, idx) => indexMap.set(getNotificationKey(item), idx));
          for (const item of nextItems) {
            const itemKey = getNotificationKey(item);
            const existingIndex = indexMap.get(itemKey);
            if (existingIndex !== undefined) {
              merged[existingIndex] = { ...merged[existingIndex], ...item };
            } else {
              indexMap.set(itemKey, merged.length);
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
      toast.error(t('notification.loadMoreFailed'));
    } finally {
      setLoadingMore(false);
    }
  }, [backfillTriggerAvatars, getNotificationKey, hasMore, loading, loadingMore, mapApiNotificationToUi, pageIndex, pageSize, t]);

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
  const handleMarkAsRead = useCallback(async (
    id: LongId,
    options?: { silent?: boolean }
  ) => {
    try {
      const backendNotificationId = resolveBackendNotificationId(id);
      if (!backendNotificationId) {
        throw new Error(`无法解析通知 ID，UiId: ${String(id)}`);
      }
      const targetIdKey = normalizePositiveLongIdKey(id);

      const success = await notificationApi.markAsRead(backendNotificationId);
      if (!success) {
        throw new Error(`标记通知已读失败，NotificationId: ${String(backendNotificationId)}`);
      }

      const store = useNotificationStore.getState();
      store.markAsRead([backendNotificationId]);
      setNotifications(prev => prev.map((item) => (
        targetIdKey !== null && normalizePositiveLongIdKey(item.id) === targetIdKey
          ? { ...item, isRead: true }
          : item
      )));
      await syncUnreadCountFromServer();

      if (!options?.silent) {
        toast.success(t('notification.markReadSuccess'));
      }
    } catch (error) {
      log.error('标记已读失败:', error);
      if (!options?.silent) {
        toast.error(t('notification.markReadFailed'));
      }
    }
  }, [resolveBackendNotificationId, syncUnreadCountFromServer, t]);

  // 点击通知
  const handleNotificationClick = useCallback((notification: NotificationItemData) => {
    log.debug('点击通知:', notification);

    // 如果未读，标记为已读
    if (!notification.isRead) {
      void handleMarkAsRead(notification.id, { silent: true });
    }

    const businessType = notification.businessType?.trim();
    const chatNavigation = parseChatNotificationNavigation(notification.extData);
    const forumNavigation = parseForumNotificationNavigation(notification.extData);

    if (chatNavigation) {
      openOrReuseApp('chat', buildChatAppParams(chatNavigation));
      return;
    }

    if (forumNavigation) {
      openOrReuseApp('forum', buildForumAppParams(forumNavigation));
      return;
    }

    if (businessType === 'Post' && notification.businessId != null) {
      const forumParams = buildForumAppParams({ postId: notification.businessId });
      if ('postId' in forumParams) {
        openOrReuseApp('forum', forumParams);
        return;
      }
    }

    if (businessType === 'Comment') {
      openOrReuseApp('forum');
      return;
    }

    if (businessType === 'User' || notification.type === 'follow') {
      const targetUserId = normalizePositiveLongIdKey(
        notification.type === 'follow'
          ? (notification.triggerId ?? notification.businessId)
          : (notification.businessId ?? notification.triggerId)
      );

      if (targetUserId) {
        if (isSameLongId(targetUserId, currentUserId)) {
          openApp('profile');
        } else {
          openApp('profile', {
            userId: targetUserId,
            userName: notification.triggerName?.trim() || t('common.userFallback', { id: targetUserId }),
            avatarUrl: notification.triggerAvatar ?? null,
          });
        }
        return;
      }
    }

    if (businessType === 'Order') {
      const targetOrderId = normalizePositiveLongIdKey(notification.businessId);
      if (targetOrderId) {
        openOrReuseApp('shop', { orderId: targetOrderId });
      } else {
        openApp('shop');
      }
      return;
    }

    if (notification.type === 'reply' || notification.type === 'mention' || notification.type === 'like') {
      openOrReuseApp('forum');
      return;
    }

    toast.info(t('notification.unsupportedNavigation'));
  }, [currentUserId, handleMarkAsRead, openApp, openOrReuseApp, t]);

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

      toast.success(t('notification.markAllReadSuccess'));
    } catch (error) {
      log.error('标记全部已读失败:', error);
      toast.error(t('notification.markAllReadFailed'));
    }
  }, [syncUnreadCountFromServer, t]);

  // 删除通知
  const handleDelete = useCallback(async (id: LongId) => {
    try {
      const backendNotificationId = resolveBackendNotificationId(id);
      if (!backendNotificationId) {
        throw new Error(`无法解析通知 ID，UiId: ${String(id)}`);
      }
      const backendNotificationIdKey = normalizePositiveLongIdKey(backendNotificationId);
      if (!backendNotificationIdKey) {
        throw new Error(`通知 ID 非法，NotificationId: ${String(backendNotificationId)}`);
      }
      await notificationApi.deleteNotification(backendNotificationId);

      // 更新 Store：从列表中移除该通知（兼容 id/notificationId）
      const store = useNotificationStore.getState();
      store.removeNotification(backendNotificationId);
      setNotifications((prev) => prev.filter((item) => (
        normalizePositiveLongIdKey(item.notificationId ?? item.id) !== backendNotificationIdKey
      )));
      await syncUnreadCountFromServer();

      toast.success(t('notification.deleteSuccess'));
    } catch (error) {
      log.error('删除通知失败:', error);
      toast.error(t('notification.deleteFailed'));
    }
  }, [resolveBackendNotificationId, syncUnreadCountFromServer, t]);

  return (
    <div className={styles.notificationApp}>
      <div className={styles.header}>
        <div className={styles.headerTop}>
          <h1 className={styles.title}>{t('notification.title')}</h1>
          <div className={styles.actions}>
            <span className={styles.count}>
              {unreadCount > 0 ? t('notification.unreadCount', { count: unreadCount }) : t('notification.allRead')}
            </span>
            {unreadCount > 0 && (
              <button className={styles.markAllBtn} onClick={handleMarkAllAsRead}>
                {t('notification.markAllRead')}
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
            {t('notification.filter.all')}
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'unread' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('unread')}
          >
            {t('notification.filter.unread')}
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'mention' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('mention')}
          >
            {t('notification.filter.mention')}
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'comment' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('comment')}
          >
            {t('notification.filter.comment')}
          </button>
          <button
            type="button"
            className={`${styles.filterTab} ${activeFilter === 'like' ? styles.filterActive : ''}`}
            onClick={() => setActiveFilter('like')}
          >
            {t('notification.filter.like')}
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
          labels={notificationListLabels}
          formatRelativeTime={formatRelativeTime}
        />
      </div>
    </div>
  );
};

/**
 * 将 API 返回的通知类型映射到 Store 的类型
 * 注意：这是类型枚举映射，不是字段名映射
 */
function mapNotificationTypeToStore(type: string): 'system' | 'reply' | 'mention' | 'like' | 'follow' | 'lottery' {
  const typeMap: Record<string, 'system' | 'reply' | 'mention' | 'like' | 'follow' | 'lottery'> = {
    'System': 'system',
    'CommentReply': 'reply',
    'CommentReplied': 'reply',
    'PostQuickReplied': 'reply',
    'Mention': 'mention',
    'Mentioned': 'mention',
    'PostLiked': 'like',
    'CommentLiked': 'like',
    'Follow': 'follow',
    'Followed': 'follow',
    'LotteryWon': 'lottery'
  };
  return typeMap[type] || 'system';
}
