import { lazy, Suspense, useState, useEffect, useMemo } from 'react';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { useWindowStore } from '@/stores/windowStore';
import { UserInfoCard } from './components/UserInfoCard';
import { getApiBaseUrl } from '@/config/env';
import styles from './ProfileApp.module.css';

const UserPostList = lazy(() =>
  import('./components/UserPostList').then((module) => ({ default: module.UserPostList }))
);
const UserCommentList = lazy(() =>
  import('./components/UserCommentList').then((module) => ({ default: module.UserCommentList }))
);
const UserAttachmentList = lazy(() =>
  import('./components/UserAttachmentList').then((module) => ({ default: module.UserAttachmentList }))
);

interface UserStats {
  voPostCount: number;
  voCommentCount: number;
  voTotalLikeCount: number;
  voPostLikeCount: number;
  voCommentLikeCount: number;
}

export const ProfileApp = () => {
  const { userId, userName, isAuthenticated } = useUserStore();
  const { openApp } = useWindowStore();
  const [activeTab, setActiveTab] = useState<'posts' | 'comments' | 'attachments'>('posts');
  const [stats, setStats] = useState<UserStats | null>(null);
  const [loadingStats, setLoadingStats] = useState(true);

  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);

  const handlePostClick = (postId: number) => {
    openApp('forum');
    log.debug('ProfileApp', `打开帖子: ${postId}`);
  };

  const handleCommentClick = (postId: number, commentId: number) => {
    openApp('forum');
    log.debug('ProfileApp', `打开帖子 ${postId} 的评论 ${commentId}`);
  };

  useEffect(() => {
    if (isAuthenticated() && userId > 0) {
      void loadStats();
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
        </div>

        <div className={styles.tabContent}>
          <Suspense fallback={<div className={styles.notLoggedIn}>加载中...</div>}>
            {activeTab === 'posts' && (
              <UserPostList userId={userId} apiBaseUrl={apiBaseUrl} onPostClick={handlePostClick} />
            )}
            {activeTab === 'comments' && (
              <UserCommentList userId={userId} apiBaseUrl={apiBaseUrl} onCommentClick={handleCommentClick} />
            )}
            {activeTab === 'attachments' && (
              <UserAttachmentList apiBaseUrl={apiBaseUrl} />
            )}
          </Suspense>
        </div>
      </div>
    </div>
  );
};
