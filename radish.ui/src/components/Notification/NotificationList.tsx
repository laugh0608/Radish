import { useRef, type UIEvent } from 'react';
import { Notification, type NotificationItemData } from './Notification';
import styles from './NotificationList.module.css';

export interface NotificationListProps {
  /** 通知列表 */
  notifications: NotificationItemData[];
  /** 是否正在加载 */
  loading?: boolean;
  /** 是否还有更多 */
  hasMore?: boolean;
  /** 是否正在加载更多 */
  loadingMore?: boolean;
  /** 触底回调 */
  onEndReached?: () => void;
  /** 触底阈值（像素） */
  endOffset?: number;
  /** 点击通知回调 */
  onNotificationClick?: (notification: NotificationItemData) => void;
  /** 标记已读回调 */
  onMarkAsRead?: (id: number) => void;
  /** 删除通知回调 */
  onDelete?: (id: number) => void;
}

/**
 * NotificationList 通知列表组件
 *
 * 纯列表展示，适用于独立页面或窗口
 */
export const NotificationList = ({
  notifications,
  loading = false,
  hasMore = false,
  loadingMore = false,
  onEndReached,
  endOffset = 200,
  onNotificationClick,
  onMarkAsRead,
  onDelete
}: NotificationListProps) => {
  const lastTriggerHeightRef = useRef(0);

  const handleScroll = (event: UIEvent<HTMLDivElement>) => {
    if (!onEndReached || loadingMore || !hasMore) return;
    const target = event.currentTarget;
    if (target.scrollTop + target.clientHeight >= target.scrollHeight - endOffset) {
      if (lastTriggerHeightRef.current !== target.scrollHeight) {
        lastTriggerHeightRef.current = target.scrollHeight;
        onEndReached();
      }
    }
  };

  return (
    <div className={styles.container}>
      {loading ? (
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <div>加载中...</div>
        </div>
      ) : notifications.length > 0 ? (
        <div className={styles.list} onScroll={handleScroll}>
          {notifications.map((notification) => (
            <Notification
              key={notification.id}
              notification={notification}
              onClick={onNotificationClick}
              onMarkAsRead={onMarkAsRead}
              onDelete={onDelete}
              showActions={true}
            />
          ))}
          {(hasMore || loadingMore) && (
            <div className={styles.loadMore}>
              {loadingMore ? '加载更多中...' : '上拉加载更多'}
            </div>
          )}
          {!hasMore && !loadingMore && notifications.length > 0 && (
            <div className={styles.endText}>已全部加载</div>
          )}
        </div>
      ) : (
        <div className={styles.empty}>
          <div className={styles.emptyIcon}>🔔</div>
          <div className={styles.emptyText}>暂无通知</div>
          <div className={styles.emptyHint}>当有新通知时，会显示在这里</div>
        </div>
      )}
    </div>
  );
};
