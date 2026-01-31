import type { UnifiedLeaderboardItemData } from '@/api/leaderboard';
import { Icon } from '@radish/ui';
import styles from '../LeaderboardApp.module.css';

interface ProductLeaderboardItemProps {
  item: UnifiedLeaderboardItemData;
  getRankIcon: (rank: number) => string | null;
  getRankClass: (rank: number) => string;
}

export const ProductLeaderboardItem = ({
  item,
  getRankIcon,
  getRankClass,
}: ProductLeaderboardItemProps) => {
  return (
    <div className={`${styles.item} ${getRankClass(item.voRank)}`}>
      <div className={styles.rank}>{getRankIcon(item.voRank) || `#${item.voRank}`}</div>

      <div className={styles.productIcon}>
        {item.voProductIcon ? (
          <Icon icon={item.voProductIcon} size={40} />
        ) : (
          <Icon icon="mdi:package-variant" size={40} />
        )}
      </div>

      <div className={styles.userInfo}>
        <div className={styles.userName}>{item.voProductName}</div>
        <div className={styles.productPrice}>
          <Icon icon="mdi:carrot" size={16} />
          {Number(item.voProductPrice || 0).toLocaleString()}
        </div>
      </div>

      <div className={styles.exp}>
        <div className={styles.expValue}>{Number(item.voPrimaryValue).toLocaleString()}</div>
        <div className={styles.expLabel}>{item.voPrimaryLabel}</div>
      </div>
    </div>
  );
};
