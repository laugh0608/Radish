import { Icon } from '@radish/ui';
import styles from './UserInfoCard.module.css';

interface UserStats {
  postCount: number;
  commentCount: number;
  totalLikeCount: number;
  postLikeCount: number;
  commentLikeCount: number;
}

interface UserInfoCardProps {
  userId: number;
  userName: string;
  stats?: UserStats;
  loading?: boolean;
}

export const UserInfoCard = ({ userId, userName, stats, loading = false }: UserInfoCardProps) => {
  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <div className={styles.avatar}>
          <Icon icon="mdi:account-circle" size={64} />
        </div>
        <div className={styles.info}>
          <h2 className={styles.userName}>{userName}</h2>
          <p className={styles.userId}>ID: {userId}</p>
        </div>
      </div>

      {loading && (
        <div className={styles.loading}>加载统计信息中...</div>
      )}

      {!loading && stats && (
        <div className={styles.stats}>
          <div className={styles.statItem}>
            <Icon icon="mdi:file-document" size={24} />
            <div className={styles.statValue}>{stats.postCount}</div>
            <div className={styles.statLabel}>帖子</div>
          </div>
          <div className={styles.statItem}>
            <Icon icon="mdi:comment" size={24} />
            <div className={styles.statValue}>{stats.commentCount}</div>
            <div className={styles.statLabel}>评论</div>
          </div>
          <div className={styles.statItem}>
            <Icon icon="mdi:heart" size={24} />
            <div className={styles.statValue}>{stats.totalLikeCount}</div>
            <div className={styles.statLabel}>获赞</div>
          </div>
        </div>
      )}
    </div>
  );
};
