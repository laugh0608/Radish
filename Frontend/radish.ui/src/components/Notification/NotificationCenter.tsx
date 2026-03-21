import { useState, useEffect, useRef } from 'react';
import { Notification, type NotificationItemData, type NotificationTextOverrides } from './Notification';
import { NotificationBadge } from './NotificationBadge';
import styles from './NotificationCenter.module.css';

export interface NotificationCenterTextOverrides extends NotificationTextOverrides {
  triggerTitle?: string;
  panelTitle?: string;
  markAllRead?: string;
  loading?: string;
  emptyTitle?: string;
  viewMore?: string;
}

export interface NotificationCenterProps {
  /** 未读数量 */
  unreadCount: number;
  /** 通知列表 */
  notifications: NotificationItemData[];
  /** 是否正在加载 */
  loading?: boolean;
  /** 点击通知回调 */
  onNotificationClick?: (notification: NotificationItemData) => void;
  /** 标记已读回调 */
  onMarkAsRead?: (id: number) => void;
  /** 标记全部已读回调 */
  onMarkAllAsRead?: () => void;
  /** 删除通知回调 */
  onDelete?: (id: number) => void;
  /** 查看更多回调 */
  onViewMore?: () => void;
  /** 文案覆盖 */
  labels?: NotificationCenterTextOverrides;
  /** 相对时间格式化 */
  formatRelativeTime?: (createdAt: string) => string;
}

/**
 * NotificationCenter 通知中心组件
 *
 * 下拉面板形式，显示最近的通知列表
 */
export const NotificationCenter = ({
  unreadCount,
  notifications,
  loading = false,
  onNotificationClick,
  onMarkAsRead,
  onMarkAllAsRead,
  onDelete,
  onViewMore,
  labels,
  formatRelativeTime
}: NotificationCenterProps) => {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  // 点击外部关闭面板
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => {
        document.removeEventListener('mousedown', handleClickOutside);
      };
    }
  }, [isOpen]);

  const togglePanel = () => {
    setIsOpen(!isOpen);
  };

  const handleNotificationClick = (notification: NotificationItemData) => {
    onNotificationClick?.(notification);
    setIsOpen(false);
  };

  return (
    <div className={styles.container} ref={containerRef}>
      {/* 通知图标按钮 */}
      <button
        className={`${styles.trigger} ${isOpen ? styles.active : ''}`}
        onClick={togglePanel}
        title={labels?.triggerTitle ?? '通知中心'}
      >
        🔔
        <NotificationBadge count={unreadCount} size="small" />
      </button>

      {/* 下拉面板 */}
      {isOpen && (
        <div className={styles.panel}>
          {/* 面板头部 */}
          <div className={styles.header}>
            <h3 className={styles.title}>
              {labels?.panelTitle ?? '通知中心'}
              {unreadCount > 0 && (
                <span className={styles.badge}>
                  <NotificationBadge count={unreadCount} />
                </span>
              )}
            </h3>
            {unreadCount > 0 && (
              <button className={styles.markAllBtn} onClick={onMarkAllAsRead}>
                {labels?.markAllRead ?? '全部已读'}
              </button>
            )}
          </div>

          {/* 通知列表 */}
          <div className={styles.list}>
            {loading ? (
              <div className={styles.loading}>{labels?.loading ?? '加载中...'}</div>
            ) : notifications.length > 0 ? (
              notifications.map((notification) => (
                <Notification
                  key={notification.id}
                  notification={notification}
                  onClick={handleNotificationClick}
                  onMarkAsRead={onMarkAsRead}
                  onDelete={onDelete}
                  showActions={true}
                  labels={labels}
                  formatRelativeTime={formatRelativeTime}
                />
              ))
            ) : (
              <div className={styles.empty}>
                <div className={styles.emptyIcon}>🔔</div>
                <div className={styles.emptyText}>{labels?.emptyTitle ?? '暂无通知'}</div>
              </div>
            )}
          </div>

          {/* 面板底部 */}
          {notifications.length > 0 && (
            <div className={styles.footer}>
              <button className={styles.viewMoreBtn} onClick={onViewMore}>
                {labels?.viewMore ?? '查看全部通知'}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
