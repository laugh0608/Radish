import { Notification, type NotificationItemData } from './Notification';
import styles from './NotificationList.module.css';

export interface NotificationListProps {
  /** 通知列表 */
  notifications: NotificationItemData[];
  /** 是否正在加载 */
  loading?: boolean;
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
  onNotificationClick,
  onMarkAsRead,
  onDelete
}: NotificationListProps) => {
  return (
    <div className={styles.container}>
      {loading ? (
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <div>加载中...</div>
        </div>
      ) : notifications.length > 0 ? (
        <div className={styles.list}>
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
