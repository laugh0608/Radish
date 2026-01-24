import { useState } from 'react';
import { useNotifications } from '../../hooks';
import { formatDateTime, formatCoinAmount } from '../../utils';
import type { NotificationItem } from '../../types';
import styles from './NotificationCenter.module.css';

/**
 * 通知中心组件
 */
export const NotificationCenter = () => {
  const [filter, setFilter] = useState<'all' | 'unread' | 'transaction' | 'security'>('all');
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');

  const {
    notifications,
    unreadCount,
    loading,
    error,
    refetch,
    markAsRead,
    markAllAsRead
  } = useNotifications();

  const useWhiteRadish = displayMode === 'white';

  const filteredNotifications = notifications.filter(notification => {
    switch (filter) {
      case 'unread':
        return !notification.isRead;
      case 'transaction':
        return notification.type === 'transaction';
      case 'security':
        return notification.type === 'security';
      default:
        return true;
    }
  });

  const handleNotificationClick = (notification: NotificationItem) => {
    if (!notification.isRead) {
      markAsRead(notification.id);
    }
  };

  const toggleDisplayMode = () => {
    setDisplayMode(prev => prev === 'carrot' ? 'white' : 'carrot');
  };

  const getNotificationIcon = (type: string): string => {
    switch (type) {
      case 'transaction': return '💰';
      case 'security': return '🔒';
      case 'system': return '📢';
      default: return '📝';
    }
  };

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载通知中...</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {/* 页面标题和操作 */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>通知中心</h2>
          <p className={styles.subtitle}>
            {unreadCount > 0 ? `您有 ${unreadCount} 条未读通知` : '所有通知已读'}
          </p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={`切换到${displayMode === 'carrot' ? '白萝卜' : '胡萝卜'}显示`}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {displayMode === 'carrot' ? '胡萝卜' : '白萝卜'}
          </button>
          {unreadCount > 0 && (
            <button className={styles.markAllButton} onClick={markAllAsRead}>
              全部已读
            </button>
          )}
          <button className={styles.refreshButton} onClick={refetch} title="刷新通知">
            🔄
          </button>
        </div>
      </div>

      {/* 筛选器 */}
      <div className={styles.filters}>
        <button
          className={`${styles.filterButton} ${filter === 'all' ? styles.active : ''}`}
          onClick={() => setFilter('all')}
        >
          全部 ({notifications.length})
        </button>
        <button
          className={`${styles.filterButton} ${filter === 'unread' ? styles.active : ''}`}
          onClick={() => setFilter('unread')}
        >
          未读 ({unreadCount})
        </button>
        <button
          className={`${styles.filterButton} ${filter === 'transaction' ? styles.active : ''}`}
          onClick={() => setFilter('transaction')}
        >
          交易通知 ({notifications.filter(n => n.type === 'transaction').length})
        </button>
        <button
          className={`${styles.filterButton} ${filter === 'security' ? styles.active : ''}`}
          onClick={() => setFilter('security')}
        >
          安全通知 ({notifications.filter(n => n.type === 'security').length})
        </button>
      </div>

      {/* 通知列表 */}
      <div className={styles.content}>
        {filteredNotifications.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>🔔</div>
            <h3>暂无通知</h3>
            <p>当前筛选条件下没有通知</p>
          </div>
        ) : (
          <div className={styles.notificationList}>
            {filteredNotifications.map((notification) => (
              <div
                key={notification.id}
                className={`${styles.notificationItem} ${
                  !notification.isRead ? styles.unread : ''
                }`}
                onClick={() => handleNotificationClick(notification)}
              >
                <div className={styles.notificationIcon}>
                  {getNotificationIcon(notification.type)}
                </div>

                <div className={styles.notificationContent}>
                  <div className={styles.notificationHeader}>
                    <div className={styles.notificationTitle}>
                      {notification.title}
                    </div>
                    <div className={styles.notificationTime}>
                      {formatDateTime(notification.createdAt)}
                    </div>
                  </div>

                  <div className={styles.notificationBody}>
                    {notification.content}
                  </div>

                  {notification.amount && (
                    <div className={styles.notificationAmount}>
                      金额: {formatCoinAmount(notification.amount, true, useWhiteRadish)}
                    </div>
                  )}
                </div>

                {!notification.isRead && (
                  <div className={styles.unreadIndicator}></div>
                )}
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};