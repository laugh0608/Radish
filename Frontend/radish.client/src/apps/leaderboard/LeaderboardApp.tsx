import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { useWindowStore } from '@/stores/windowStore';
import { useUserStore } from '@/stores/userStore';
import {
  leaderboardApi,
  LeaderboardType,
  LeaderboardCategory,
  type LeaderboardTypeData,
  type UnifiedLeaderboardItemData,
} from '@/api/leaderboard';
import { Icon } from '@radish/ui/icon';
import { UserLeaderboardItem } from './components/UserLeaderboardItem';
import { ProductLeaderboardItem } from './components/ProductLeaderboardItem';
import styles from './LeaderboardApp.module.css';

export const LeaderboardApp = () => {
  const { openApp } = useWindowStore();
  const currentUserId = useUserStore((state) => state.userId);
  const [leaderboard, setLeaderboard] = useState<UnifiedLeaderboardItemData[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageIndex, setPageIndex] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [myRank, setMyRank] = useState<number | null>(null);
  const [leaderboardTypes, setLeaderboardTypes] = useState<LeaderboardTypeData[]>([]);
  const [activeType, setActiveType] = useState<LeaderboardType>(LeaderboardType.Experience);
  const pageSize = 50;

  // 加载排行榜类型
  useEffect(() => {
    void loadLeaderboardTypes();
  }, []);

  // 当类型或页码变化时加载数据
  useEffect(() => {
    void loadLeaderboard();
    void loadMyRank();
  }, [activeType, pageIndex]);

  const loadLeaderboardTypes = async () => {
    try {
      const types = await leaderboardApi.getTypes();
      if (types) {
        setLeaderboardTypes(types);
      }
    } catch (err) {
      log.error('加载排行榜类型失败:', err);
    }
  };

  const loadLeaderboard = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await leaderboardApi.getLeaderboard(activeType, pageIndex, pageSize);
      if (response) {
        setLeaderboard(response.data);
        setTotalPages(response.pageCount);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载排行榜失败');
      log.error('加载排行榜失败:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadMyRank = async () => {
    try {
      const rank = await leaderboardApi.getMyRank(activeType);
      setMyRank(rank);
    } catch (err) {
      log.error('加载我的排名失败:', err);
    }
  };

  const handleTypeChange = (type: LeaderboardType) => {
    setActiveType(type);
    setPageIndex(1);
    setMyRank(null);
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

  const handleOpenUserProfile = (item: UnifiedLeaderboardItemData) => {
    if (!item.voUserId) {
      return;
    }

    if (String(item.voUserId) === String(currentUserId ?? 0)) {
      openApp('profile');
      return;
    }

    openApp('profile', {
      userId: item.voUserId,
      userName: item.voUserName?.trim() || `用户 ${item.voUserId}`,
      avatarUrl: item.voAvatarUrl?.trim() || null,
    });
  };

  const activeTypeConfig = leaderboardTypes.find((t) => t.voType === activeType);
  const isUserLeaderboard = activeTypeConfig?.voCategory === LeaderboardCategory.User;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>
          <Icon icon={activeTypeConfig?.voIcon || 'mdi:trophy'} size={32} />
          {activeTypeConfig?.voName || '排行榜'}
        </h1>
        {isUserLeaderboard && myRank !== null && myRank > 0 && (
          <div className={styles.myRank}>
            我的排名: <span className={styles.rankNumber}>#{myRank}</span>
          </div>
        )}
      </div>

      {/* Tab 切换 */}
      <div className={styles.tabs}>
        {leaderboardTypes.map((type) => (
          <button
            key={type.voType}
            className={`${styles.tab} ${activeType === type.voType ? styles.tabActive : ''}`}
            onClick={() => handleTypeChange(type.voType)}
          >
            <Icon icon={type.voIcon} size={18} />
            <span>{type.voName}</span>
          </button>
        ))}
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
            {leaderboard.map((item, index) =>
              item.voCategory === LeaderboardCategory.User ? (
                <UserLeaderboardItem
                  key={`${item.voUserId}-${item.voRank}-${index}`}
                  item={item}
                  getRankIcon={getRankIcon}
                  getRankClass={getRankClass}
                  onUserClick={handleOpenUserProfile}
                />
              ) : (
                <ProductLeaderboardItem
                  key={`${item.voProductId}-${item.voRank}-${index}`}
                  item={item}
                  getRankIcon={getRankIcon}
                  getRankClass={getRankClass}
                />
              )
            )}
          </div>

          <div className={styles.pagination}>
            <button onClick={handlePrevPage} disabled={pageIndex === 1} className={styles.pageButton}>
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
