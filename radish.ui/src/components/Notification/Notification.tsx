import { formatDistanceToNow } from 'date-fns';
import { zhCN } from 'date-fns/locale';
import styles from './Notification.module.css';

export interface NotificationItemData {
  /** 通知 ID */
  id: number;
  /** 通知类型 */
  type: string;
  /** 通知标题 */
  title: string;
  /** 通知内容 */
  content: string;
  /** 优先级 */
  priority: number;
  /** 业务类型 */
  businessType?: string;
  /** 业务 ID */
  businessId?: number;
  /** 触发者 ID */
  triggerId?: number;
  /** 触发者名称 */
  triggerName?: string;
  /** 触发者头像 */
  triggerAvatar?: string;
  /** 是否已读 */
  isRead: boolean;
  /** 创建时间 */
  createdAt: string;
}

export interface NotificationProps {
  /** 通知数据 */
  notification: NotificationItemData;
  /** 点击通知回调 */
  onClick?: (notification: NotificationItemData) => void;
  /** 标记已读回调 */
  onMarkAsRead?: (id: number) => void;
  /** 删除回调 */
  onDelete?: (id: number) => void;
  /** 是否显示操作按钮 */
  showActions?: boolean;
}

/**
 * Notification 通知项组件
 *
 * 用于显示持久化通知，支持已读/未读状态
 */
export const Notification = ({
  notification,
  onClick,
  onMarkAsRead,
  onDelete,
  showActions = true
}: NotificationProps) => {
  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'PostLiked':
      case 'CommentLiked':
        return '👍';
      case 'CommentReplied':
        return '💬';
      case 'Mentioned':
        return '@';
      case 'GodComment':
        return '⭐';
      case 'Sofa':
        return '🏆';
      case 'LevelUp':
        return '🎉';
      case 'CoinBalanceChanged':
        return '🥕';
      case 'SystemAnnouncement':
        return '📢';
      case 'AccountSecurity':
        return '🔒';
      default:
        return 'ℹ️';
    }
  };

  const getTypeColor = (type: string) => {
    if (type.endsWith('Liked')) return 'like';
    if (type.includes('Comment') || type.includes('Replied')) return 'comment';
    if (type.includes('God') || type.includes('Sofa')) return 'achievement';
    if (type.includes('System') || type.includes('Security')) return 'system';
    return 'default';
  };

  const handleClick = () => {
    onClick?.(notification);
  };

  const handleMarkAsRead = (e: React.MouseEvent) => {
    e.stopPropagation();
    onMarkAsRead?.(notification.id);
  };

  const handleDelete = (e: React.MouseEvent) => {
    e.stopPropagation();
    onDelete?.(notification.id);
  };

  const timeAgo = formatDistanceToNow(new Date(notification.createdAt), {
    addSuffix: true,
    locale: zhCN
  });

  return (
    <div
      className={`${styles.notification} ${
        notification.isRead ? styles.read : styles.unread
      } ${styles[getTypeColor(notification.type)]}`}
      onClick={handleClick}
    >
      {/* 未读指示器 */}
      {!notification.isRead && <div className={styles.unreadDot} />}

      {/* 头像或图标 */}
      <div className={styles.avatar}>
        {notification.triggerAvatar ? (
          <img src={notification.triggerAvatar} alt={notification.triggerName || ''} />
        ) : (
          <span className={styles.icon}>{getTypeIcon(notification.type)}</span>
        )}
      </div>

      {/* 内容区域 */}
      <div className={styles.content}>
        <div className={styles.header}>
          <span className={styles.title}>{notification.title}</span>
          <span className={styles.time}>{timeAgo}</span>
        </div>
        <div className={styles.message}>
          {notification.triggerName && (
            <span className={styles.triggerName}>{notification.triggerName} </span>
          )}
          {notification.content}
        </div>
      </div>

      {/* 操作按钮 */}
      {showActions && (
        <div className={styles.actions}>
          {!notification.isRead && (
            <button
              className={styles.actionBtn}
              onClick={handleMarkAsRead}
              title="标记已读"
            >
              ✓
            </button>
          )}
          <button
            className={styles.actionBtn}
            onClick={handleDelete}
            title="删除"
          >
            ✕
          </button>
        </div>
      )}
    </div>
  );
};
