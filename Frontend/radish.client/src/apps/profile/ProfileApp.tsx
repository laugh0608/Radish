import { lazy, Suspense, useState, useEffect, useMemo } from 'react';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { useWindowStore } from '@/stores/windowStore';
import { UserInfoCard } from './components/UserInfoCard';
import { getMyTimePreference, getTimeSettings, updateMyTimePreference } from '@/api/time';
import { getApiBaseUrl } from '@/config/env';
import {
  DEFAULT_TIME_FORMAT,
  DEFAULT_TIME_ZONE,
  getBrowserTimeZoneId,
  resolveTimeZoneId,
} from '@/utils/dateTime';
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
  const [systemTimeZone, setSystemTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeZone, setDisplayTimeZone] = useState(DEFAULT_TIME_ZONE);
  const [displayTimeFormat, setDisplayTimeFormat] = useState(DEFAULT_TIME_FORMAT);
  const [savingTimeZone, setSavingTimeZone] = useState(false);

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
      void loadTimeSettings();
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

  const loadTimeSettings = async () => {
    let fallbackSystemTimeZone = DEFAULT_TIME_ZONE;
    let fallbackDisplayTimeFormat = DEFAULT_TIME_FORMAT;

    try {
      const settings = await getTimeSettings();
      fallbackSystemTimeZone = resolveTimeZoneId(settings.voDefaultTimeZoneId, DEFAULT_TIME_ZONE);
      fallbackDisplayTimeFormat = settings.voDisplayFormat?.trim() || DEFAULT_TIME_FORMAT;
    } catch (error) {
      log.warn('ProfileApp', '加载系统时间配置失败，已回退默认配置');
      log.error('ProfileApp', '加载系统时间配置失败：', error);
    }

    try {
      const preference = await getMyTimePreference();
      const resolvedSystemTimeZone = resolveTimeZoneId(
        preference.voSystemDefaultTimeZoneId,
        fallbackSystemTimeZone
      );
      const resolvedDisplayTimeFormat = preference.voDisplayFormat?.trim() || fallbackDisplayTimeFormat;
      const browserTimeZone = getBrowserTimeZoneId(resolvedSystemTimeZone);
      const initialDisplayTimeZone = preference.voIsCustomized
        ? resolveTimeZoneId(preference.voTimeZoneId, browserTimeZone)
        : browserTimeZone;

      setSystemTimeZone(resolvedSystemTimeZone);
      setDisplayTimeFormat(resolvedDisplayTimeFormat);
      setDisplayTimeZone(initialDisplayTimeZone);

      if (!preference.voIsCustomized) {
        try {
          const updatedPreference = await updateMyTimePreference(initialDisplayTimeZone);
          const updatedDisplayTimeZone = resolveTimeZoneId(
            updatedPreference.voTimeZoneId,
            initialDisplayTimeZone
          );
          setDisplayTimeZone(updatedDisplayTimeZone);
        } catch (error) {
          log.warn('ProfileApp', '初始化用户时区偏好失败，已使用浏览器时区展示');
          log.error('ProfileApp', '初始化用户时区偏好失败：', error);
        }
      }
    } catch (error) {
      const browserTimeZone = getBrowserTimeZoneId(fallbackSystemTimeZone);

      setSystemTimeZone(fallbackSystemTimeZone);
      setDisplayTimeFormat(fallbackDisplayTimeFormat);
      setDisplayTimeZone(browserTimeZone);

      log.warn('ProfileApp', '加载用户时区偏好失败，已回退浏览器时区');
      log.error('ProfileApp', '加载用户时区偏好失败：', error);
    }
  };

  const handleTimeZoneChange = async (timeZoneId: string) => {
    const resolvedTimeZone = resolveTimeZoneId(timeZoneId, systemTimeZone);
    if (savingTimeZone) {
      return;
    }

    setSavingTimeZone(true);
    try {
      const updatedPreference = await updateMyTimePreference(resolvedTimeZone);
      const updatedDisplayTimeZone = resolveTimeZoneId(updatedPreference.voTimeZoneId, resolvedTimeZone);
      setDisplayTimeZone(updatedDisplayTimeZone);
      log.info('ProfileApp', `用户时区已切换为 ${updatedDisplayTimeZone}`);
    } catch (error) {
      log.error('ProfileApp', '保存用户时区偏好失败：', error);
      throw error;
    } finally {
      setSavingTimeZone(false);
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
          displayTimeZone={displayTimeZone}
          systemTimeZone={systemTimeZone}
          displayTimeFormat={displayTimeFormat}
          savingTimeZone={savingTimeZone}
          onTimeZoneChange={handleTimeZoneChange}
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
              <UserPostList
                userId={userId}
                apiBaseUrl={apiBaseUrl}
                onPostClick={handlePostClick}
                displayTimeZone={displayTimeZone}
              />
            )}
            {activeTab === 'comments' && (
              <UserCommentList
                userId={userId}
                apiBaseUrl={apiBaseUrl}
                onCommentClick={handleCommentClick}
                displayTimeZone={displayTimeZone}
              />
            )}
            {activeTab === 'attachments' && (
              <UserAttachmentList apiBaseUrl={apiBaseUrl} displayTimeZone={displayTimeZone} />
            )}
          </Suspense>
        </div>
      </div>
    </div>
  );
};
