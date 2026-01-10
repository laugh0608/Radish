import styles from './NotificationBadge.module.css';

export interface NotificationBadgeProps {
  /** 未读数量 */
  count: number;
  /** 最大显示数量（超过显示 99+） */
  max?: number;
  /** 徽章大小 */
  size?: 'small' | 'default';
  /** 是否显示为红点（count > 0 时） */
  dot?: boolean;
}

/**
 * NotificationBadge 未读数徽章组件
 *
 * 用于显示未读通知数量
 */
export const NotificationBadge = ({
  count,
  max = 99,
  size = 'default',
  dot = false
}: NotificationBadgeProps) => {
  if (count <= 0) {
    return null;
  }

  const displayCount = count > max ? `${max}+` : count.toString();

  if (dot) {
    return <span className={`${styles.badge} ${styles.dot} ${styles[size]}`} />;
  }

  return (
    <span className={`${styles.badge} ${styles[size]}`}>
      {displayCount}
    </span>
  );
};
