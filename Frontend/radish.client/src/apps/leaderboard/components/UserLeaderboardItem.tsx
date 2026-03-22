import type { UnifiedLeaderboardItemData } from '@/api/leaderboard';
import styles from '../LeaderboardApp.module.css';

interface UserLeaderboardItemProps {
  item: UnifiedLeaderboardItemData;
  getRankIcon: (rank: number) => string | null;
  getRankClass: (rank: number) => string;
  onUserClick?: (item: UnifiedLeaderboardItemData) => void;
}

export const UserLeaderboardItem = ({ item, getRankIcon, getRankClass, onUserClick }: UserLeaderboardItemProps) => {
  const userName = item.voUserName?.trim() || (item.voUserId ? `用户 ${item.voUserId}` : '未知用户');

  return (
    <div
      className={`${styles.item} ${item.voIsCurrentUser ? styles.currentUser : ''} ${getRankClass(item.voRank)}`}
    >
      <div className={styles.rank}>{getRankIcon(item.voRank) || `#${item.voRank}`}</div>

      <div className={styles.userInfo}>
        <button
          type="button"
          className={styles.userNameButton}
          onClick={() => onUserClick?.(item)}
          disabled={!item.voUserId}
          title={item.voUserId ? `查看 ${userName} 的主页` : '用户信息不可用'}
        >
          <span className={styles.userName}>{userName}</span>
        </button>
        <div className={styles.level} style={{ color: item.voThemeColor || 'var(--theme-text-placeholder)' }}>
          Lv.{item.voCurrentLevel} {item.voCurrentLevelName}
        </div>
      </div>

      <div className={styles.exp}>
        <div className={styles.expValue}>{Number(item.voPrimaryValue).toLocaleString()}</div>
        <div className={styles.expLabel}>{item.voPrimaryLabel}</div>
        {item.voSecondaryValue !== undefined && item.voSecondaryLabel && (
          <div className={styles.secondaryValue}>
            {Number(item.voSecondaryValue).toLocaleString()} {item.voSecondaryLabel}
          </div>
        )}
      </div>
    </div>
  );
};
