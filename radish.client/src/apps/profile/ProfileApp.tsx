import { useState, useEffect, useMemo } from 'react';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { UserInfoCard } from './components/UserInfoCard';
import { UserPostList } from './components/UserPostList';
import { UserCommentList } from './components/UserCommentList';
import { UserAttachmentList } from './components/UserAttachmentList';
import { CoinWallet } from './components/CoinWallet';
import styles from './ProfileApp.module.css';

interface UserStats {
  postCount: number;
  commentCount: number;
  totalLikeCount: number;
  postLikeCount: number;
  commentLikeCount: number;
}

export const ProfileApp = () => {
  const { userId, userName, isAuthenticated } = useUserStore();
  const [activeTab, setActiveTab] = useState<'posts' | 'comments' | 'attachments' | 'wallet'>('posts');
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loadingStats, setLoadingStats] = useState(true);

  // 统一通过 Gateway 访问
  const apiBaseUrl = useMemo(() => {
    if (typeof window !== 'undefined') {
      return window.location.origin;
    }
    return 'https://localhost:5000';
  }, []);

  useEffect(() => {
    if (isAuthenticated() && userId > 0) {
      loadStats();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId]);

  const loadStats = async () => {
    setLoadingStats(true);
    try {
      const response = await fetch(
        `${apiBaseUrl}/api/v1/User/GetUserStats?userId=${userId}`
      );
      const json = await response.json();
      if (json.isSuccess && json.responseData) {
        setStats(json.responseData);
      }
    } catch (error) {
      log.error('加载统计信息失败:', error);
    } finally {
      setLoadingStats(false);
    }
  };

  if (!isAuthenticated()) {
    return (
      <div className={styles.container}>
        <div className={styles.notLoggedIn}>
          <p>请先登录查看个人主页</p>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>个人主页</h1>
      </div>

      <div className={styles.content}>
        <UserInfoCard
          userId={userId}
          userName={userName}
          stats={stats || undefined}
          loading={loadingStats}
          apiBaseUrl={apiBaseUrl}
        />

        <div className={styles.tabs}>
          <button
            className={`${styles.tab} ${activeTab === 'posts' ? styles.active : ''}`}
            onClick={() => setActiveTab('posts')}
          >
            我的帖子
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'comments' ? styles.active : ''}`}
            onClick={() => setActiveTab('comments')}
          >
            我的评论
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'attachments' ? styles.active : ''}`}
            onClick={() => setActiveTab('attachments')}
          >
            我的附件
          </button>
          <button
            className={`${styles.tab} ${activeTab === 'wallet' ? styles.active : ''}`}
            onClick={() => setActiveTab('wallet')}
          >
            我的钱包
          </button>
        </div>

        <div className={styles.tabContent}>
          {activeTab === 'posts' && (
            <UserPostList userId={userId} apiBaseUrl={apiBaseUrl} />
          )}
          {activeTab === 'comments' && (
            <UserCommentList userId={userId} apiBaseUrl={apiBaseUrl} />
          )}
          {activeTab === 'attachments' && (
            <UserAttachmentList apiBaseUrl={apiBaseUrl} />
          )}
          {activeTab === 'wallet' && (
            <CoinWallet apiBaseUrl={apiBaseUrl} />
          )}
        </div>
      </div>
    </div>
  );
};
