import { useState, useEffect } from 'react';
import { experienceApi, type LeaderboardItem } from '@radish/ui';
import { Icon } from '@radish/ui';
import styles from './LeaderboardApp.module.css';

export const LeaderboardApp = () => {
  const [leaderboard, setLeaderboard] = useState<LeaderboardItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageIndex, setPageIndex] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [myRank, setMyRank] = useState<number | null>(null);
  const pageSize = 50;

  useEffect(() => {
    void loadLeaderboard();
    void loadMyRank();
  }, [pageIndex]);

  const loadLeaderboard = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await experienceApi.getLeaderboard(pageIndex, pageSize);
      setLeaderboard(response.data);
      setTotalPages(response.pageCount);
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载排行榜失败');
      console.error('加载排行榜失败:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadMyRank = async () => {
    try {
      const rank = await experienceApi.getMyRank();
      setMyRank(rank);
    } catch (err) {
      console.error('加载我的排名失败:', err);
    }
  };

  const handlePrevPage = () => {
    if (pageIndex > 1) {
      setPageIndex(pageIndex - 1);
    }
  };

  const handleNextPage = () => {
    if (pageIndex < totalPages) {
      setPageIndex(pageIndex + 1);
    }
  };

  const getRankIcon = (rank: number) => {
    if (rank === 1) return '🥇';
    if (rank === 2) return '🥈';
    if (rank === 3) return '🥉';
    return null;
  };

  const getRankClass = (rank: number) => {
    if (rank === 1) return styles.rankGold;
    if (rank === 2) return styles.rankSilver;
    if (rank === 3) return styles.rankBronze;
    return '';
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>
          <Icon icon="mdi:trophy" size={32} />
          经验值排行榜
        </h1>
        {myRank !== null && myRank > 0 && (
          <div className={styles.myRank}>
            我的排名: <span className={styles.rankNumber}>#{myRank}</span>
          </div>
        )}
      </div>

      {loading && (
        <div className={styles.loading}>
          <Icon icon="mdi:loading" size={32} className={styles.spinner} />
          <p>加载中...</p>
        </div>
      )}

      {error && (
        <div className={styles.error}>
          <Icon icon="mdi:alert-circle" size={24} />
          <p>{error}</p>
          <button onClick={() => void loadLeaderboard()} className={styles.retryButton}>
            重试
          </button>
        </div>
      )}

      {!loading && !error && leaderboard.length === 0 && (
        <div className={styles.empty}>
          <Icon icon="mdi:trophy-outline" size={64} />
          <p>暂无排行榜数据</p>
        </div>
      )}

      {!loading && !error && leaderboard.length > 0 && (
        <>
          <div className={styles.leaderboard}>
            {leaderboard.map((item) => (
              <div
                key={item.userId}
                className={`${styles.item} ${item.isCurrentUser ? styles.currentUser : ''} ${getRankClass(item.rank)}`}
              >
                <div className={styles.rank}>
                  {getRankIcon(item.rank) || `#${item.rank}`}
                </div>

                <div className={styles.userInfo}>
                  <div className={styles.userName}>{item.userName}</div>
                  <div className={styles.level} style={{ color: item.themeColor || '#9E9E9E' }}>
                    Lv.{item.currentLevel} {item.currentLevelName}
                  </div>
                </div>

                <div className={styles.exp}>
                  <div className={styles.expValue}>{Number(item.totalExp).toLocaleString()}</div>
                  <div className={styles.expLabel}>总经验值</div>
                </div>
              </div>
            ))}
          </div>

          <div className={styles.pagination}>
            <button
              onClick={handlePrevPage}
              disabled={pageIndex === 1}
              className={styles.pageButton}
            >
              <Icon icon="mdi:chevron-left" size={20} />
              上一页
            </button>

            <span className={styles.pageInfo}>
              第 {pageIndex} / {totalPages} 页
            </span>

            <button
              onClick={handleNextPage}
              disabled={pageIndex >= totalPages}
              className={styles.pageButton}
            >
              下一页
              <Icon icon="mdi:chevron-right" size={20} />
            </button>
          </div>
        </>
      )}
    </div>
  );
};
