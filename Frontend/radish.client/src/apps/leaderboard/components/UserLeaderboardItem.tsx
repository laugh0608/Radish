import type { UnifiedLeaderboardItemData } from '@/api/leaderboard';
import styles from '../LeaderboardApp.module.css';

interface UserLeaderboardItemProps {
  item: UnifiedLeaderboardItemData;
  getRankIcon: (rank: number) => string | null;
  getRankClass: (rank: number) => string;
}

export const UserLeaderboardItem = ({ item, getRankIcon, getRankClass }: UserLeaderboardItemProps) => {
  return (
    <div
      className={`${styles.item} ${item.voIsCurrentUser ? styles.currentUser : ''} ${getRankClass(item.voRank)}`}
    >
      <div className={styles.rank}>{getRankIcon(item.voRank) || `#${item.voRank}`}</div>

      <div className={styles.userInfo}>
        <div className={styles.userName}>{item.voUserName}</div>
        <div className={styles.level} style={{ color: item.voThemeColor || '#9E9E9E' }}>
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
